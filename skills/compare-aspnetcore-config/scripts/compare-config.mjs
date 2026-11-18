#!/usr/bin/env node
// Compares the Sentry and Serilog configuration of all ASP.NET Core projects below the given folders.
// Usage: node compare-config.mjs [options] <folder> [<folder> ...]
//   --standard <file>          expected values (default: ../assets/standard.json, "none" to skip)
//   --sentry-options <file>    valid Sentry option names (default: ../references/sentry-options.txt)
//   --include-override <regex> MinimumLevel:Override categories to compare (default: ^(Microsoft|System)(\.|$))
//   --exclude-override <regex> app specific categories to skip (default: ^System\.Net\.Http\.HttpClient\.)
//   --no-checks                only print the tables
// Rules: see ../references/rules.md
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const opts = {
  standard: path.join(here, '..', 'assets', 'standard.json'),
  sentryOptions: path.join(here, '..', 'references', 'sentry-options.txt'),
  include: /^(Microsoft|System)(\.|$)/i,
  exclude: /^System\.Net\.Http\.HttpClient\./i,
  checks: true,
  roots: [],
};
const argv = process.argv.slice(2);
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === '--standard') opts.standard = argv[++i];
  else if (a === '--sentry-options') opts.sentryOptions = argv[++i];
  else if (a === '--include-override') opts.include = new RegExp(argv[++i], 'i');
  else if (a === '--exclude-override') opts.exclude = new RegExp(argv[++i], 'i');
  else if (a === '--no-checks') opts.checks = false;
  else opts.roots.push(a);
}
if (!opts.roots.length) opts.roots.push(process.cwd());

const FILES = ['appsettings.json', 'appsettings.Production.json', 'appsettings.Development.json'];
const CONFIG_DEFAULT = 'config.default.json';
const SKIP_DIRS = new Set(['node_modules', 'bin', 'obj', '.git', '.vs', '.idea', 'dist', 'artifacts', '.claude', 'submodule', 'submodules', '.angular']);

// ---------- JSON with comments and trailing commas (like Microsoft.Extensions.Configuration.Json) ----------
function parseJsonc(text) {
  text = text.replace(/^﻿/, '');
  let out = '';
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '"') {
      let j = i + 1;
      while (j < text.length && text[j] !== '"') j += text[j] === '\\' ? 2 : 1;
      out += text.slice(i, j + 1);
      i = j;
    } else if (c === '/' && text[i + 1] === '/') {
      while (i < text.length && text[i] !== '\n') i++;
      out += '\n';
    } else if (c === '/' && text[i + 1] === '*') {
      i = text.indexOf('*/', i + 2) + 1;
    } else out += c;
  }
  // drop trailing commas (outside strings)
  let res = '';
  for (let i = 0; i < out.length; i++) {
    const c = out[i];
    if (c === '"') {
      let j = i + 1;
      while (j < out.length && out[j] !== '"') j += out[j] === '\\' ? 2 : 1;
      res += out.slice(i, j + 1);
      i = j;
    } else if (c === ',' && /^\s*[}\]]/.test(out.slice(i + 1))) {
      continue;
    } else res += c;
  }
  return JSON.parse(res);
}
const readJson = (f) => (fs.existsSync(f) ? parseJsonc(fs.readFileSync(f, 'utf8')) : null);
const isObj = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);
const toObj = (v) => (Array.isArray(v) ? Object.fromEntries(v.map((x, i) => [String(i), x])) : v);

// configuration merge: keys are case-insensitive, arrays merge by index
function merge(a, b) {
  if (b == null) return a;
  if (a == null) return structuredClone(b);
  a = toObj(a);
  for (const [k, v] of Object.entries(toObj(b))) {
    const ek = Object.keys(a).find((x) => x.toLowerCase() === k.toLowerCase()) ?? k;
    if ((isObj(v) || Array.isArray(v)) && (isObj(a[ek]) || Array.isArray(a[ek]))) a[ek] = merge(a[ek], v);
    else a[ek] = structuredClone(v);
  }
  return a;
}

// ---------- project discovery ----------
function findProjects(root) {
  const found = [];
  const walk = (dir) => {
    let entries;
    try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
    for (const e of entries) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { if (!SKIP_DIRS.has(e.name.toLowerCase())) walk(p); }
      else if (e.name.endsWith('.csproj')) {
        const xml = fs.readFileSync(p, 'utf8');
        if (/Sdk="Microsoft\.NET\.Sdk\.Web"/i.test(xml) && fs.existsSync(path.join(dir, 'appsettings.json'))) {
          found.push({ name: path.basename(e.name, '.csproj'), dir, angular: /SpaProxy/i.test(xml) });
        }
      }
    }
  };
  walk(root);
  return found;
}

// ---------- normalisation ----------
const MASKED = /(^Sentry:Dsn$|:ApplicationName$|:apiKey$|:serverUrl$)/i;
const sinkKey = (v, k) => (isObj(v) && v.Name ? v.Name : typeof v === 'string' ? v : k);

function normaliseSinks(section) {
  const out = {};
  for (const [k, v] of Object.entries(toObj(section))) {
    const name = sinkKey(v, k);
    if (isObj(v)) {
      const copy = structuredClone(v);
      delete copy.Name;
      if (copy.Args && copy.Args.configure) copy.Args.configure = normaliseSinks(copy.Args.configure);
      out[name] = Object.keys(copy).length ? copy : 'on';
    } else out[name] = 'on';
  }
  return out;
}

function flatten(json, { ignoreUsing = false } = {}) {
  const out = {};
  if (!json) return out;
  const put = (k, v) => { out[k] = v; };
  const walk = (o, prefix) => {
    for (const [k, v] of Object.entries(o)) {
      const key = prefix ? `${prefix}:${k}` : k;
      if (isObj(v) || Array.isArray(v)) walk(toObj(v), key);
      else put(key, v === null ? 'null' : String(v));
    }
  };
  const top = (name) => Object.keys(json).find((k) => k.toLowerCase() === name.toLowerCase());
  if (top('$schema')) put('$schema', String(json[top('$schema')]));
  if (top('Sentry')) walk(json[top('Sentry')], 'Sentry');
  if (top('Serilog')) {
    const s = structuredClone(json[top('Serilog')]);
    for (const sec of ['WriteTo', 'Enrich', 'AuditTo']) {
      const k = Object.keys(s).find((x) => x.toLowerCase() === sec.toLowerCase());
      if (k) s[k] = normaliseSinks(s[k]);
    }
    const using = Object.keys(s).find((x) => x.toLowerCase() === 'using');
    if (using) {
      const list = s[using];
      delete s[using];
      if (!ignoreUsing && Array.isArray(list)) for (const u of list) put(`Serilog:Using:${u}`, 'on');
    }
    walk(s, 'Serilog');
  }
  for (const k of Object.keys(out)) {
    const m = k.match(/^Serilog:MinimumLevel:Override:(.+)$/i);
    if (m && (!opts.include.test(m[1]) || opts.exclude.test(m[1]))) delete out[k];
    else if (MASKED.test(k)) out[k] = out[k] === '' ? '""' : out[k] === 'null' ? 'null' : '(set)';
  }
  return out;
}

// case-insensitive key map: lower -> {key, value}
const keyed = (flat) => Object.fromEntries(Object.entries(flat).map(([k, v]) => [k.toLowerCase(), { k, v }]));

// ---------- collect ----------
const hasLoggingConfig = (json) => json && Object.keys(json).some((k) => /^(sentry|serilog)$/i.test(k));
const projects = opts.roots.flatMap(findProjects)
  .map((p) => ({ ...p, raw: Object.fromEntries([...FILES, CONFIG_DEFAULT].map((f) => [f, readJson(path.join(p.dir, f))])) }))
  .filter((p) => Object.values(p.raw).some(hasLoggingConfig)) // projects without Sentry/Serilog config are not compared
  .sort((a, b) => a.name.localeCompare(b.name));
if (!projects.length) { console.log('Keine ASP.NET-Core-Projekte mit Sentry-/Serilog-Konfiguration gefunden.'); process.exit(0); }

for (const p of projects) {
  const all = [...FILES, CONFIG_DEFAULT].map((f) => JSON.stringify(p.raw[f] ?? {})).join();
  p.seq = /"Seq"|serilog\.sinks\.seq/i.test(all);
  p.files = {};
  for (const f of FILES) {
    if (!p.raw[f]) { p.files[f] = null; continue; }
    let flat = flatten(p.raw[f]);
    p.files[f] = { flat: keyed(flat), fromDefault: new Set() };
    if (f === 'appsettings.Production.json' && p.raw[CONFIG_DEFAULT]) {
      const merged = merge(structuredClone(p.raw[f]), Object.fromEntries(Object.entries(p.raw[CONFIG_DEFAULT]).filter(([k]) => k !== '$schema')));
      flat = flatten(merged, { ignoreUsing: true });
      const fromDefault = keyed(flatten(p.raw[CONFIG_DEFAULT], { ignoreUsing: true }));
      p.files[f] = { flat: keyed(flat), fromDefault: new Set(Object.keys(fromDefault).filter((k) => k !== '$schema')) };
    }
  }
  // effective configuration per environment (appsettings.json + environment file [+ config.default.json for Production])
  const strip = (j) => (j ? Object.fromEntries(Object.entries(j).filter(([k]) => k !== '$schema')) : null);
  p.effective = {
    Production: keyed(flatten(merge(merge(structuredClone(p.raw['appsettings.json']), strip(p.raw['appsettings.Production.json'])), strip(p.raw[CONFIG_DEFAULT])), { ignoreUsing: true })),
    Development: keyed(flatten(merge(structuredClone(p.raw['appsettings.json']), strip(p.raw['appsettings.Development.json'])))),
  };
}

// ---------- tables ----------
const fmt = (v) => (v === '(set)' || v === '""' || v === 'null' ? v : `\`${v}\``);
console.log('✅ = gemeinsamer Wert · ➖ = nicht gesetzt · 🚫 = nicht anwendbar · ⚙️ = aus config.default.json · Wert = abweichend\n');
for (const f of FILES) {
  const label = f === 'appsettings.Production.json' ? `${f} (+ ${CONFIG_DEFAULT})` : f;
  const keys = new Map();
  for (const p of projects) for (const [lk, { k }] of Object.entries(p.files[f]?.flat ?? {})) if (!keys.has(lk)) keys.set(lk, k);
  const rows = [];
  for (const [lk, k] of keys) {
    const cells = projects.map((p) => {
      const file = p.files[f];
      if (!file) return { na: true };
      if (/SpaProxy/i.test(k) && !p.angular) return { na: true };
      if (/seq/i.test(k) && !p.seq) return { na: true };
      return { v: file.flat[lk]?.v, def: file.fromDefault.has(lk) };
    });
    const counts = {};
    for (const c of cells) if (!c.na && c.v !== undefined) counts[c.v] = (counts[c.v] ?? 0) + 1;
    const applicable = cells.filter((c) => !c.na).length;
    const best = Object.entries(counts).sort((a, b) => b[1] - a[1])[0];
    const common = best && (best[1] > 1 || applicable === 1) ? best[0] : undefined;
    const shown = cells.map((c) => (c.na ? '🚫' : c.v === undefined ? '➖' : (c.v === common ? '✅' : fmt(c.v)) + (c.def ? '⚙️' : '')));
    const app = shown.filter((s) => s !== '🚫');
    const group = app.every((s) => s.startsWith('✅')) ? (shown.includes('🚫') ? 1 : 0) : 2;
    rows.push({ k, group, line: `| ${k} | ${common === undefined ? '' : fmt(common)} | ${shown.join(' | ')} |` });
  }
  rows.sort((a, b) => a.group - b.group || a.k.localeCompare(b.k, 'en', { sensitivity: 'base' }));
  console.log(`## ${label}\n\n| Key | Common | ${projects.map((p) => p.name).join(' | ')} |\n|${'---|'.repeat(projects.length + 2)}`);
  for (const r of rows) console.log(r.line);
  console.log();
}

if (!opts.checks) process.exit(0);

// ---------- checks ----------
const findings = [];
const add = (p, file, msg) => findings.push(`| ${p.name} | ${file} | ${msg} |`);
const validSentry = fs.existsSync(opts.sentryOptions)
  ? new Set(fs.readFileSync(opts.sentryOptions, 'utf8').split(/\r?\n/).filter((l) => l && !l.startsWith('#')).map((l) => l.trim().toLowerCase()))
  : null;

for (const p of projects) {
  for (const f of [...FILES, CONFIG_DEFAULT]) {
    const json = p.raw[f];
    if (!json) continue;
    if (!Object.keys(json).some((k) => k === '$schema')) add(p, f, '`$schema` fehlt');
    const sentry = json[Object.keys(json).find((k) => k.toLowerCase() === 'sentry')];
    if (validSentry && isObj(sentry)) for (const k of Object.keys(sentry)) if (!validSentry.has(k.toLowerCase())) add(p, f, `unbekannte Sentry-Option \`Sentry:${k}\` (wird ignoriert)`);
    const serilog = json[Object.keys(json).find((k) => k.toLowerCase() === 'serilog')];
    if (isObj(serilog)) for (const sec of ['WriteTo', 'Enrich', 'AuditTo']) {
      const s = serilog[Object.keys(serilog).find((k) => k.toLowerCase() === sec.toLowerCase())];
      if (s) for (const [k, v] of Object.entries(toObj(s))) if (isObj(v) && !v.Name) add(p, f, `\`Serilog:${sec}:${k}\` ohne \`Name\` (laut JSON-Schema ungültig)`);
    }
  }
  // values in environment files that repeat appsettings.json (whole file, all sections)
  const leaves = (o, prefix = '', out = {}) => {
    for (const [k, v] of Object.entries(toObj(o ?? {}))) {
      const key = prefix ? `${prefix}:${k}` : k;
      if (v !== null && typeof v === 'object') leaves(v, key, out); else out[key.toLowerCase()] = { k: key, v };
    }
    return out;
  };
  const base = leaves(p.raw['appsettings.json']);
  for (const f of ['appsettings.Production.json', 'appsettings.Development.json']) {
    for (const { k, v } of Object.values(leaves(p.raw[f]))) {
      if (k === '$schema') continue;
      if (/^Serilog:(WriteTo|Enrich|AuditTo):[^:]+:Name$/i.test(k)) continue; // required by the JSON schema
      if (base[k.toLowerCase()] && base[k.toLowerCase()].v === v) add(p, f, `\`${k}\` wiederholt den Wert aus appsettings.json`);
    }
  }
}

// deviations from the agreed standard
if (opts.standard !== 'none' && fs.existsSync(opts.standard)) {
  const standard = parseJsonc(fs.readFileSync(opts.standard, 'utf8'));
  // sections are either a file name (value must be in that file) or an environment (effective, merged value)
  for (const [f, expected] of Object.entries(standard)) {
    const isEnv = f === 'Production' || f === 'Development';
    if (!isEnv && !FILES.includes(f)) continue;
    for (const p of projects) {
      const flat = isEnv ? p.effective[f] : p.files[f]?.flat;
      if (!flat) continue;
      for (const [k, want] of Object.entries(expected)) {
        if (/SpaProxy/i.test(k) && !p.angular) continue;
        if (/seq/i.test(k) && !p.seq) continue;
        const got = flat[k.toLowerCase()]?.v;
        if (got !== String(want)) add(p, f, `Standard: \`${k}\` = ${fmt(String(want))}, ist ${got === undefined ? 'nicht gesetzt' : fmt(got)}`);
      }
    }
  }
}

console.log('## Prüfungen\n');
if (!findings.length) console.log('Keine Auffälligkeiten.');
else { console.log('| Projekt | Datei | Befund |\n|---|---|---|'); findings.forEach((l) => console.log(l)); }
