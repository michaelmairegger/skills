#!/usr/bin/env node
// Vergleicht zwei Biome-`configuration_schema.json` und listet neue, verschobene und entfernte
// Lint-Regeln / Assist-Aktionen sowie neue und entfernte Konfigurationsoptionen (Formatter, Parser, ...).
//
// Verwendung (im Verzeichnis mit `node_modules/@biomejs/biome` ausführen, wenn `--explain` gesetzt ist):
//   node biome-schema-diff.mjs [--explain] <alt/configuration_schema.json> <neu/configuration_schema.json>
//   node biome-schema-diff.mjs [--explain] --list <configuration_schema.json>
//
// --list     listet alle Regeln, Assist-Aktionen und Formatter-Optionen einer Version
// --explain  ergänzt je Regel über `biome explain` die Spalten Empfohlen, Severity, Fix und Seit

import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

const RULE_CONTAINERS = ["linter.rules", "assist.actions"];
const SKIPPED_PATHS = new Set(["$schema", "extends", "overrides", "plugins"]);
const GROUP_SETTINGS = new Set(["all", "recommended", "preset"]);
const MAX_DEPTH = 6;

function load(file) {
	const schema = JSON.parse(readFileSync(file, "utf8"));
	const defs = schema.$defs ?? schema.definitions ?? {};
	return { schema, defs };
}

function deref(node, defs) {
	if (!node?.$ref) return { node, ref: undefined };
	const ref = node.$ref.split("/").pop();
	return { node: defs[ref], ref };
}

// Liefert alle Objekt-Schemas (mit `properties`), die ein Knoten über $ref/anyOf/oneOf/allOf erreicht.
function objectsOf(node, defs, stack = []) {
	if (!node) return [];
	const { node: target, ref } = deref(node, defs);
	if (ref && stack.includes(ref)) return [];
	const nextStack = ref ? [...stack, ref] : stack;
	const result = [];
	if (target?.properties) result.push(target);
	for (const key of ["anyOf", "oneOf", "allOf"]) {
		for (const child of target?.[key] ?? []) result.push(...objectsOf(child, defs, nextStack));
	}
	return result;
}

function summarize(description = "") {
	const lines = description.split("\n").map((l) => l.trim());
	const link = lines.find((l) => l.startsWith("See https://"))?.slice(4) ?? "";
	const text = lines.filter((l) => l && !l.startsWith("See https://")).join(" ");
	const firstSentence = text.match(/^.*?[.!?](\s|$)/)?.[0].trim() ?? text;
	return { text: firstSentence.replaceAll("|", "\\|"), link };
}

function collect(file) {
	const { schema, defs } = load(file);
	const rules = new Map(); // name -> { group, container, description, link }
	const options = new Map(); // path -> description

	function walkRules(node, container) {
		for (const groupObj of objectsOf(node, defs)) {
			for (const [group, groupNode] of Object.entries(groupObj.properties)) {
				if (GROUP_SETTINGS.has(group)) continue;
				for (const ruleObj of objectsOf(groupNode, defs)) {
					for (const [name, ruleNode] of Object.entries(ruleObj.properties)) {
						if (GROUP_SETTINGS.has(name)) continue;
						rules.set(`${container}:${name}`, { name, group, container, ...summarize(ruleNode.description) });
					}
				}
			}
		}
	}

	function walk(node, path, depth) {
		for (const obj of objectsOf(node, defs)) {
			for (const [key, child] of Object.entries(obj.properties)) {
				if (SKIPPED_PATHS.has(key) && depth === 0) continue;
				const childPath = path ? `${path}.${key}` : key;
				if (RULE_CONTAINERS.includes(childPath)) {
					walkRules(child, childPath);
					continue;
				}
				if (!options.has(childPath)) options.set(childPath, summarize(child.description).text);
				if (depth < MAX_DEPTH) walk(child, childPath, depth + 1);
			}
		}
	}

	walk(schema, "", 0);
	return { rules, options };
}

const BIOME_BIN = join(process.cwd(), "node_modules", "@biomejs", "biome", "bin", "biome");

function explain(name) {
	try {
		const out = execFileSync(process.execPath, [BIOME_BIN, "explain", name], { encoding: "utf8" });
		const value = (re) => out.match(re)?.[1].trim() ?? "";
		return [
			/This rule is recommended/.test(out) ? "ja" : "nein",
			value(/Default severity:\s*(.+)/),
			value(/Fix:\s*(.+)/) || "keiner",
			value(/Available from version:\s*(.+)/),
		];
	} catch {
		return ["?", "?", "?", "?"];
	}
}

function table(header, rows) {
	if (rows.length === 0) return "_keine_\n";
	const head = `| ${header.join(" | ")} |\n|${header.map(() => "---").join("|")}|\n`;
	return `${head}${rows.map((r) => `| ${r.join(" | ")} |`).join("\n")}\n`;
}

const args = process.argv.slice(2);
const withExplain = args.includes("--explain");
const listMode = args.includes("--list");
const files = args.filter((a) => !a.startsWith("--"));

if (withExplain && !existsSync(BIOME_BIN)) {
	console.error(`--explain: ${BIOME_BIN} nicht gefunden. Im Verzeichnis mit installiertem Biome ausführen.`);
	process.exit(2);
}
if (files.length !== (listMode ? 1 : 2)) {
	console.error(
		"Verwendung: node biome-schema-diff.mjs [--explain] <alt.json> <neu.json> | [--explain] --list <schema.json>",
	);
	process.exit(2);
}

const EXPLAIN_HEADER = withExplain ? ["Empfohlen", "Severity", "Fix", "Seit"] : [];
const ruleHeader = (...extra) => ["Regel", ...extra, ...EXPLAIN_HEADER, "Beschreibung", "Doku"];
const ruleRow = (r, ...extra) => [
	`\`${r.group}/${r.name}\``,
	...extra,
	...(withExplain ? explain(r.name) : []),
	r.text,
	r.link,
];
const byGroup = (a, b) => `${a.group}/${a.name}`.localeCompare(`${b.group}/${b.name}`);

if (listMode) {
	const { rules, options } = collect(files[0]);
	for (const container of RULE_CONTAINERS) {
		const list = [...rules.values()].filter((r) => r.container === container).sort(byGroup);
		console.log(`## ${container} (${list.length})\n`);
		console.log(
			table(
				ruleHeader(),
				list.map((r) => ruleRow(r)),
			),
		);
	}
	const formatterOptions = [...options.entries()].filter(([p]) => /(^|\.)formatter\./.test(p)).sort();
	console.log(`## Formatter-Optionen (${formatterOptions.length})\n`);
	console.log(
		table(
			["Option", "Beschreibung"],
			formatterOptions.map(([p, d]) => [`\`${p}\``, d]),
		),
	);
	process.exit(0);
}

const before = collect(files[0]);
const after = collect(files[1]);

for (const container of RULE_CONTAINERS) {
	const inContainer = (m) => [...m.entries()].filter(([, r]) => r.container === container);
	const added = inContainer(after.rules)
		.filter(([k]) => !before.rules.has(k))
		.map(([, r]) => r);
	const removed = inContainer(before.rules)
		.filter(([k]) => !after.rules.has(k))
		.map(([, r]) => r);
	const moved = inContainer(after.rules)
		.filter(([k, r]) => before.rules.has(k) && before.rules.get(k).group !== r.group)
		.map(([k, r]) => ({ ...r, from: before.rules.get(k).group }));

	console.log(`## ${container}\n`);
	console.log(`### Neu (${added.length})\n`);
	console.log(
		table(
			ruleHeader(),
			added.sort(byGroup).map((r) => ruleRow(r)),
		),
	);
	console.log(`### Gruppe gewechselt (${moved.length})\n`);
	console.log(
		table(
			ruleHeader("vorher"),
			moved.sort(byGroup).map((r) => ruleRow(r, `\`${r.from}\``)),
		),
	);
	console.log(`### Entfernt oder umbenannt (${removed.length})\n`);
	console.log(
		table(
			["Regel", "Beschreibung"],
			removed.sort(byGroup).map((r) => [`\`${r.group}/${r.name}\``, r.text]),
		),
	);
}

const addedOptions = [...after.options.entries()].filter(([p]) => !before.options.has(p)).sort();
const removedOptions = [...before.options.keys()].filter((p) => !after.options.has(p)).sort();
console.log(`## Konfigurationsoptionen\n`);
console.log(`### Neu (${addedOptions.length})\n`);
console.log(
	table(
		["Option", "Beschreibung"],
		addedOptions.map(([p, d]) => [`\`${p}\``, d]),
	),
);
console.log(`### Entfernt oder umbenannt (${removedOptions.length})\n`);
console.log(
	table(
		["Option"],
		removedOptions.map((p) => [`\`${p}\``]),
	),
);
