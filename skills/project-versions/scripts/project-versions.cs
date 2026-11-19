// Lists the .NET, Angular and Flutter versions of all projects (git repositories) below the given folders,
// the update path (latest minor/patch of the current major first, then every following major, nothing skipped)
// and the support status of the versions in use. Output is Markdown (default) or JSON.
//
// Sources (fetched live):
//  - .NET:            https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json
//  - .NET Framework:  https://endoflife.date/api/v1/products/dotnetfx
//  - Angular:         https://registry.npmjs.org/@angular/core (versions) + https://endoflife.date/api/v1/products/angular (support)
//  - Flutter:         https://storage.googleapis.com/flutter_infra_release/releases/releases_windows.json
//
// Usage: dotnet run project-versions.cs -- <absolute folder> [<absolute folder> ...] [--format markdown|json] [--soon-days 90]
//   (dotnet run does not keep the caller's working directory, so pass absolute paths)
//   Warnings go to stderr, so the JSON on stdout stays valid.
#:property PublishAot=false
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;

var soonDays = 90;
var format = "markdown";
var roots = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--soon-days" && i + 1 < args.Length) { soonDays = int.Parse(args[++i], CultureInfo.InvariantCulture); }
    else if (args[i] == "--format" && i + 1 < args.Length) { format = args[++i].ToLowerInvariant(); }
    else { roots.Add(args[i]); }
}
if (format is not ("markdown" or "json")) { Console.Error.WriteLine($"Unbekanntes Format: {format} (markdown|json)"); return 2; }
if (roots.Count == 0) { roots.Add(Directory.GetCurrentDirectory()); }

var today = DateOnly.FromDateTime(DateTime.Today);
string[] skipDirs = ["node_modules", "bin", "obj", ".git", ".vs", ".idea", "dist", "build", ".dart_tool", ".angular", ".next", ".staging",
    "submodule", "submodules", "Pods", "artifacts", ".claude", "packages", "TestResults", "ephemeral", ".plugin_symlinks", ".symlinks", "example"];

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("project-versions-skill/1.0");

// ---------- latest versions and support data ----------
var dotnetChannels = (await GetJson("https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json"))["releases-index"]!.AsArray()
    .Select(c => new DotnetChannel(
        Version.Parse((string)c!["channel-version"]!),
        (string)c["latest-release"]!,
        (string)c["support-phase"]!,
        (string?)c["release-type"] ?? "",
        ParseDate((string?)c["eol-date"])))
    .Where(c => c.Phase is not ("preview" or "go-live"))
    .OrderBy(c => c.Channel)
    .ToList();
var latestDotnet = dotnetChannels[^1];
var latestLts = dotnetChannels.Where(c => c.Type == "lts" && c.Phase == "active").MaxBy(c => c.Channel);

var fxReleases = (await GetJson("https://endoflife.date/api/v1/products/dotnetfx"))["result"]!["releases"]!.AsArray()
    .Select(r => new EolRelease((string)r!["name"]!, (bool?)r["isEol"] ?? false, ParseDate((string?)r["eolFrom"]), false))
    .ToList();
var latestFx = fxReleases.Select(r => Version.TryParse(r.Name.Split('-')[0], out var v) ? v : null).Where(v => v is not null).Max()!;

var angularSupport = (await GetJson("https://endoflife.date/api/v1/products/angular"))["result"]!["releases"]!.AsArray()
    .Select(r => new EolRelease((string)r!["name"]!, (bool?)r["isEol"] ?? false, ParseDate((string?)r["eolFrom"]), (bool?)r["isEoas"] ?? false))
    .ToList();
var angularVersions = (await GetJson("https://registry.npmjs.org/@angular/core", "application/vnd.npm.install-v1+json"))["versions"]!.AsObject()
    .Select(kv => kv.Key)
    .Where(v => !v.Contains('-'))
    .Select(v => Version.TryParse(v, out var p) ? p : null)
    .Where(v => v is not null)
    .Cast<Version>()
    .ToList();
var latestAngularPerMajor = angularVersions.GroupBy(v => v.Major).ToDictionary(g => g.Key, g => g.Max()!);
var latestAngular = angularVersions.Max()!;

var flutterJson = await GetJson("https://storage.googleapis.com/flutter_infra_release/releases/releases_windows.json");
var stableHash = (string)flutterJson["current_release"]!["stable"]!;
var latestFlutter = Version.Parse((string)flutterJson["releases"]!.AsArray().First(r => (string)r!["hash"]! == stableHash)!["version"]!);

// ---------- scan projects ----------
var projects = new List<ProjectReport>();
var withoutTech = new List<string>();
foreach (var root in roots)
{
    if (!Directory.Exists(root)) { Console.Error.WriteLine($"⚠️ Ordner nicht gefunden: {root} (absoluten Pfad angeben)"); continue; }
    foreach (var repo in FindRepos(root))
    {
        var rel = Path.GetRelativePath(root, repo);
        var name = rel is "." ? Path.GetFileName(Path.TrimEndingDirectorySeparator(repo)) : rel.Replace('\\', '/').Replace("/", " / ");
        var result = AnalyzeProject(repo, name);
        if (result.Dotnet.Count + result.Angular.Count + result.Flutter.Count == 0) { withoutTech.Add(name); }
        else { projects.Add(result); }
    }
}
projects = projects.OrderByDescending(p => Rank(p.Support)).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
withoutTech.Sort(StringComparer.OrdinalIgnoreCase);

var report = new Report(
    today,
    soonDays,
    new LatestVersions(latestDotnet.Latest, latestLts?.Latest, latestFx.ToString(), latestAngular.ToString(), latestFlutter.ToString()),
    projects,
    withoutTech);

if (format == "json")
{
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }));
}
else
{
    WriteMarkdown(report);
}
return 0;

// ================= analysis =================

ProjectReport AnalyzeProject(string dir, string name)
{
    var files = EnumerateFiles(dir);
    var dotnet = new List<DotnetEntry>();
    var angular = new List<AngularEntry>();
    var flutter = new List<FlutterEntry>();

    // ---- .NET ----
    var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var versionJson = Path.Combine(dir, "version.json");
    if (File.Exists(versionJson))
    {
        var m = Regex.Match(File.ReadAllText(versionJson), "\"version\"\\s*:\\s*\"([^\"]+)\"");
        if (m.Success) { props["_NBGV_Version"] = m.Groups[1].Value; }
    }
    foreach (var f in files.Where(f => f.EndsWith(".props", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar)).ThenBy(f => f))
    {
        foreach (Match m in PropRx().Matches(File.ReadAllText(f)))
        {
            props.TryAdd(m.Groups[1].Value, m.Groups[2].Value.Trim());
        }
    }
    // patch versions per major.minor, with the property they come from (version.json when resolved via NBGV)
    var knownPatch = props.Keys
        .Where(k => k.EndsWith("NetVersion", StringComparison.OrdinalIgnoreCase))
        .Select(k => (Key: k, Value: Expand(props[k], props), Source: props[k].Contains("_NBGV_Version") ? "version.json" : k))
        .Where(x => Regex.IsMatch(x.Value, @"^\d+\.\d+\.\d+$"))
        .GroupBy(x => MajorMinor(x.Value))
        .ToDictionary(g => g.Key, g => g.First());

    var groups = new Dictionary<(string Kind, Version Ver), (string Tfm, List<string> Projects)>();
    foreach (var csproj in files.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
    {
        var text = File.ReadAllText(csproj);
        var raw = TfmRx().Matches(text).Select(m => m.Groups[1].Value).ToList();
        raw.AddRange(TfvRx().Matches(text).Select(m => m.Groups[1].Value));
        if (raw.Count == 0 && props.TryGetValue("TargetFramework", out var dbp)) { raw.Add(dbp); }
        foreach (var tfm in raw.SelectMany(r => Expand(r, props).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Where(t => !t.Contains("$(")).Distinct())
        {
            var parsed = ParseTfm(tfm);
            if (parsed is null) { continue; }
            if (!groups.TryGetValue(parsed.Value, out var group)) { groups[parsed.Value] = group = (NormalizeTfm(tfm), []); }
            var projName = Path.GetFileNameWithoutExtension(csproj);
            if (!group.Projects.Contains(projName)) { group.Projects.Add(projName); }
        }
    }
    foreach (var ((kind, ver), (tfm, names)) in groups.OrderByDescending(g => g.Key.Kind).ThenByDescending(g => g.Key.Ver))
    {
        names.Sort(StringComparer.OrdinalIgnoreCase);
        dotnet.Add(kind == "core"
            ? CoreEntry(ver, tfm, names, knownPatch.TryGetValue($"{ver.Major}.{ver.Minor}", out var p) ? (p.Value, p.Source) : null)
            : FxEntry(ver, tfm, names));
    }

    // ---- Angular ----
    foreach (var pkg in files.Where(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase)))
    {
        var m = Regex.Match(File.ReadAllText(pkg), "\"@angular/core\"\\s*:\\s*\"([^\"]+)\"");
        if (!m.Success) { continue; }
        var range = m.Groups[1].Value;
        var lockFile = Path.Combine(Path.GetDirectoryName(pkg)!, "package-lock.json");
        string? locked = null;
        if (File.Exists(lockFile))
        {
            try { locked = (string?)JsonNode.Parse(File.ReadAllText(lockFile))?["packages"]?["node_modules/@angular/core"]?["version"]; } catch { }
        }
        var vm = Regex.Match(locked ?? range, @"(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        if (!vm.Success) { continue; }
        var v = new Version(int.Parse(vm.Groups[1].Value), vm.Groups[2].Success ? int.Parse(vm.Groups[2].Value) : 0, vm.Groups[3].Success ? int.Parse(vm.Groups[3].Value) : 0);
        angular.Add(AngularEntryFor(v, range, locked is not null ? "package-lock.json" : "package.json-range", RelDir(dir, Path.GetDirectoryName(pkg)!)));
    }

    // ---- Flutter ----
    var pipelineStable = files.Where(f => (f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            && !Path.GetFileName(f).StartsWith("pubspec", StringComparison.OrdinalIgnoreCase))
        .Any(f => { var t = File.ReadAllText(f); return t.Contains("flutter", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(t, @"channel:\s*['""]?stable"); });
    foreach (var pubspec in files.Where(f => Path.GetFileName(f).Equals("pubspec.yaml", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(File.ReadAllText(f), @"sdk:\s*flutter")))
    {
        var pdir = Path.GetDirectoryName(pubspec)!;
        string? current = null, source = null;
        var fvmrc = Path.Combine(pdir, ".fvmrc");
        var fvmConfig = Path.Combine(pdir, ".fvm", "fvm_config.json");
        if (File.Exists(fvmrc)) { current = (string?)JsonNode.Parse(File.ReadAllText(fvmrc))?["flutter"]; source = ".fvmrc"; }
        else if (File.Exists(fvmConfig)) { current = (string?)JsonNode.Parse(File.ReadAllText(fvmConfig))?["flutterSdkVersion"]; source = ".fvm/fvm_config.json"; }
        if (current is null)
        {
            var lockPath = Path.Combine(pdir, "pubspec.lock");
            // [ \t]+ instead of \s+: \s also matches line breaks and makes the lazy repetition backtrack catastrophically
            var lm = File.Exists(lockPath) ? FlutterLockRx().Match(File.ReadAllText(lockPath)) : Match.Empty;
            if (lm.Success) { current = lm.Groups[1].Value; source = "pubspec.lock-minimum"; }
            else if (FlutterEnvRx().Match(File.ReadAllText(pubspec)) is { Success: true } em) { current = em.Groups[1].Value; source = "pubspec.yaml-minimum"; }
        }
        flutter.Add(FlutterEntryFor(current, source, pipelineStable, RelDir(dir, pdir)));
    }

    return new ProjectReport(name, dir, dotnet, angular, flutter);
}

DotnetEntry CoreEntry(Version tfm, string tfmText, List<string> names, (string Value, string Source)? patch)
{
    var inMajor = dotnetChannels.Where(c => c.Channel.Major == tfm.Major).MaxBy(c => c.Channel);
    var majorPath = dotnetChannels.Where(c => c.Channel.Major > tfm.Major).GroupBy(c => c.Channel.Major).OrderBy(g => g.Key).Select(g => g.MaxBy(c => c.Channel)!.Latest).ToList();
    var behind = inMajor is not null && (patch is null || Version.Parse(patch.Value.Value) < Version.Parse(inMajor.Latest));
    var channel = dotnetChannels.FirstOrDefault(c => c.Channel == new Version(tfm.Major, tfm.Minor)) ?? inMajor;
    var support = channel is null ? new SupportInfo("unknown", null, false, null) : Classify(channel.Phase == "eol", channel.Eol, channel.Type == "lts", null);
    return new DotnetEntry("core", tfmText, names, patch?.Value ?? $"{tfm.Major}.{tfm.Minor}", patch?.Source ?? "targetFramework",
        UpdateKind(behind, majorPath.Count > 0), inMajor?.Latest, majorPath, null, support);
}

DotnetEntry FxEntry(Version v, string tfmText, List<string> names)
{
    var rel = fxReleases.FirstOrDefault(r => Version.TryParse(r.Name.Split('-')[0], out var rv) && rv == v); // endoflife.date names 3.5 as "3.5-sp1"
    var support = rel is null ? new SupportInfo("unknown", null, false, null) : Classify(rel.IsEol, rel.EolFrom, false, null);
    return new DotnetEntry("framework", tfmText, names, v.ToString(), "targetFramework",
        UpdateKind(v < latestFx, true), latestFx.ToString(), [], latestDotnet.Latest, support);
}

AngularEntry AngularEntryFor(Version v, string range, string source, string location)
{
    var inMajor = latestAngularPerMajor.GetValueOrDefault(v.Major);
    var majorPath = latestAngularPerMajor.Where(kv => kv.Key > v.Major).OrderBy(kv => kv.Key).Select(kv => kv.Value.ToString()).ToList();
    var rel = angularSupport.FirstOrDefault(r => r.Name == v.Major.ToString(CultureInfo.InvariantCulture));
    var support = rel is null ? new SupportInfo("unknown", null, false, null) : Classify(rel.IsEol, rel.EolFrom, rel.IsEoas && !rel.IsEol, null);
    return new AngularEntry(location, v.ToString(), source, range, UpdateKind(inMajor is not null && inMajor > v, majorPath.Count > 0), inMajor?.ToString(), majorPath, support);
}

FlutterEntry FlutterEntryFor(string? current, string? source, bool pipelineStable, string location)
{
    if (current is null || !Version.TryParse(current, out var v))
    {
        return new FlutterEntry(location, current, source, "unknown", latestFlutter.ToString(), [], pipelineStable, new SupportInfo("unknown", null, false, null));
    }
    var behind = v < latestFlutter;
    var pinned = source is ".fvmrc" or ".fvm/fvm_config.json";
    // Flutter has no LTS: only the current stable release gets fixes
    var support = pinned
        ? (behind ? new SupportInfo("eol", null, false, $"nur {latestFlutter} wird gepflegt") : new SupportInfo("supported", null, false, null))
        : new SupportInfo("supported", null, false, pipelineStable ? null : "nur wenn mit aktuellem Stable gebaut");
    return new FlutterEntry(location, current, source, UpdateKind(behind, false), latestFlutter.ToString(), [], pipelineStable, support);
}

SupportInfo Classify(bool isEol, DateOnly? until, bool lts, string? note)
{
    if (isEol || (until is not null && until <= today)) { return new SupportInfo("eol", until, lts, note); }
    if (until is not null && until.Value.DayNumber - today.DayNumber <= soonDays) { return new SupportInfo("soon", until, lts, note); }
    return new SupportInfo("supported", until, lts, note);
}

static string UpdateKind(bool behindInMajor, bool newerMajor) => behindInMajor ? "minor" : newerMajor ? "major" : "none";
static int Rank(string status) => status switch { "eol" => 3, "soon" => 2, "supported" => 1, _ => 0 };

// ================= markdown =================

void WriteMarkdown(Report r)
{
    var l = r.Latest;
    Console.WriteLine($"Stand {Fmt(r.GeneratedAt)} · neueste Versionen: .NET {l.Dotnet}{(l.DotnetLts is not null && l.DotnetLts != l.Dotnet ? $" (LTS {l.DotnetLts})" : "")} · .NET Framework {l.DotnetFramework} · Angular {l.Angular} · Flutter {l.Flutter}");
    Console.WriteLine();
    Console.WriteLine($"Zeile: 🟥 mind. eine Technologie ohne Support · 🟨 Support endet in < {r.SoonDays} Tagen · 🟩 alles unterstützt  ");
    Console.WriteLine("Version: 🔴 Update im aktuellen Major · 🟡 nur neuer Major · 🟢 aktuell · `→` neuester Minor/Patch · `⇒` nächster Major  ");
    Console.WriteLine("Support: ✅ unterstützt · ⏳ endet bald · ❌ ohne Support");
    Console.WriteLine();
    Console.WriteLine("| Projekt | .NET | Angular | Flutter | Support |");
    Console.WriteLine("|---|---|---|---|---|");
    foreach (var p in r.Projects)
    {
        var rowIcon = p.Support switch { "eol" => "🟥", "soon" => "🟨", _ => "🟩" };
        Console.WriteLine($"| {rowIcon} {p.Name} | {DotnetCell(p.Dotnet)} | {Cell(p.Angular.Select(a => (p.Angular.Count > 1 ? a.Location + ": " : "", a.Current, a.Update, a.LatestInMajor, a.MajorPath, (string?)null)))} | {Cell(p.Flutter.Select(f => (p.Flutter.Count > 1 ? f.Location + ": " : "", f.Current is null ? "unbekannt" : f.CurrentSource!.EndsWith("-minimum") ? "≥" + f.Current : f.Current, f.Update, f.LatestInMajor, f.MajorPath, (string?)null)))} | {SupportCell(p)} |");
    }
    Console.WriteLine();
    if (r.Projects.Any(p => p.Flutter.Any(f => f.Support.Note is not null && !f.PipelineStable)))
    {
        Console.WriteLine($"\\* Flutter: nur das aktuelle Stable ({r.Latest.Flutter}) erhält Fixes. Gilt nur, wenn tatsächlich damit gebaut wird (keine Pipeline mit Channel `stable` gefunden).");
    }
    if (r.Projects.Any(p => p.Dotnet.Any(d => d.Kind == "core" && d.CurrentSource == "targetFramework")))
    {
        Console.WriteLine("- .NET ohne Patch-Angabe: nur das Target Framework ist bekannt; ob ein Patch-Update fällig ist, ist offen (daher 🔴).");
    }
    if (r.WithoutTechnology.Count > 0)
    {
        Console.WriteLine($"- Ohne .NET/Angular/Flutter: {string.Join(", ", r.WithoutTechnology)}");
    }
}

string DotnetCell(List<DotnetEntry> entries)
{
    // label groups with their projects only when the groups cover different projects (multi-targeting alone needs no label)
    var label = entries.Count > 1 && entries.Select(e => string.Join("|", e.Projects)).Distinct().Count() > 1;
    return Cell(entries.Select(e => (
        label ? (e.Projects.Count <= 3 ? string.Join(", ", e.Projects) : $"{e.Projects.Count} Projekte") + ": " : "",
        e.Kind == "framework" ? $"Framework {e.Current}" : e.CurrentSource == "targetFramework" ? e.TargetFramework : e.Current,
        e.Update, e.LatestInMajor, e.MajorPath, e.PortTo)));
}

static string Cell(IEnumerable<(string Label, string Current, string Update, string? LatestInMajor, List<string> MajorPath, string? PortTo)> entries)
{
    var parts = entries.Select(e =>
    {
        var icon = e.Update switch { "minor" => "🔴", "major" => "🟡", "none" => "🟢", _ => "?" };
        var sb = new StringBuilder($"{icon} {e.Label}{e.Current}");
        if (e.Update is "minor" or "unknown" && e.LatestInMajor is not null) { sb.Append($" → {e.LatestInMajor}"); }
        foreach (var m in e.MajorPath) { sb.Append($" ⇒ {m}"); }
        if (e.PortTo is not null) { sb.Append($" ⇒ Portierung {e.PortTo}"); }
        return sb.ToString();
    }).ToList();
    return parts.Count == 0 ? "–" : string.Join(" · ", parts);
}

static string SupportText(SupportInfo s, bool showLts, bool flutterFootnote) => s.Status switch
{
    "eol" => s.Until is null ? (s.Note is null ? "❌" : $"❌ ({s.Note})") : $"❌ (seit {Fmt(s.Until.Value)})",
    "soon" => $"⏳ (bis {Fmt(s.Until!.Value)})",
    "supported" when showLts && s.Lts && s.Until is not null => $"✅ (LTS bis {Fmt(s.Until.Value)})",
    "supported" when flutterFootnote => "✅*",
    "supported" => "✅",
    _ => "?",
};

static string SupportCell(ProjectReport p)
{
    var parts = new List<string>();
    void Add(string tech, IEnumerable<(string Key, string Text)> entries)
    {
        var distinct = entries.GroupBy(e => e.Key).Select(g => g.First()).ToList();
        if (distinct.Count == 0) { return; }
        parts.Add(distinct.Select(e => e.Text).Distinct().Count() == 1
            ? $"{tech} {distinct[0].Text}"
            : $"{tech} " + string.Join(" / ", distinct.Select(e => $"{e.Key} {e.Text}")));
    }
    Add(".NET", p.Dotnet.Select(d => (
        d.Kind == "framework" ? $"Framework {d.Current}" : Version.Parse(d.TargetFramework.Replace("netcoreapp", "").Replace("net", "")) is var v && v.Major < 5 ? $"Core {v.Major}.{v.Minor}" : $"{v.Major}",
        SupportText(d.Support, false, false))));
    Add("Angular", p.Angular.Select(a => (a.Current.Split('.')[0], SupportText(a.Support, true, false))));
    Add("Flutter", p.Flutter.Select(f => (f.Location, f.PipelineStable && f.Support.Status == "supported" ? "✅ (Pipeline: stable)" : SupportText(f.Support, false, f.Support.Note is not null && f.Support.Status == "supported"))));
    return string.Join(" · ", parts);
}

// ================= helpers =================

static string Fmt(DateOnly d) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
static DateOnly? ParseDate(string? s) => DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
static string MajorMinor(string v) => string.Join('.', v.Split('.').Take(2));
static string RelDir(string root, string dir) => Path.GetRelativePath(root, dir).Replace('\\', '/');

static string Expand(string value, Dictionary<string, string> props)
{
    for (var i = 0; i < 10 && value.Contains("$("); i++)
    {
        var before = value;
        value = SubstringRx().Replace(value, m => props.TryGetValue(m.Groups[1].Value, out var v) && !Expand(v, props).Contains("$(") ? MajorMinor(Expand(v, props)) : m.Value);
        value = VarRx().Replace(value, m => props.TryGetValue(m.Groups[1].Value, out var v) && v != m.Value ? v : m.Value);
        if (value == before) { break; }
    }
    return value;
}

// net9.0-windows7.0 → net9.0, v4.8 stays v4.8
static string NormalizeTfm(string tfm) => tfm.Trim().ToLowerInvariant().Split('-')[0];

static (string Kind, Version Ver)? ParseTfm(string tfm)
{
    var b = NormalizeTfm(tfm);
    if (b.StartsWith("netstandard")) { return null; }
    if (b.StartsWith("netcoreapp") && Version.TryParse(b[10..], out var c)) { return ("core", c); }
    if (b.StartsWith('v') && Version.TryParse(b[1..], out var fv)) { return ("fx", fv); }
    var m = Regex.Match(b, @"^net(\d+)\.(\d+)$");
    if (m.Success) { return ("core", new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))); }
    m = Regex.Match(b, @"^net(\d+)$");
    if (!m.Success) { return null; }
    var d = m.Groups[1].Value;
    if (d[0] == '4' || d is "20" or "35") { return ("fx", Version.Parse(string.Join('.', d.ToCharArray()))); } // net461 → 4.6.1
    return ("core", new Version(int.Parse(d), 0));
}

List<string> FindRepos(string root)
{
    var result = new List<string>();
    void Walk(string dir, int depth)
    {
        if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git"))) { result.Add(dir); return; }
        if (depth >= 4) { return; }
        foreach (var sub in SafeDirs(dir).Where(s => !skipDirs.Contains(Path.GetFileName(s), StringComparer.OrdinalIgnoreCase))) { Walk(sub, depth + 1); }
    }
    Walk(root, 0);
    return result;
}

List<string> EnumerateFiles(string dir)
{
    var list = new List<string>();
    var stack = new Stack<string>([dir]);
    while (stack.Count > 0)
    {
        var d = stack.Pop();
        try { list.AddRange(Directory.GetFiles(d)); } catch { }
        foreach (var s in SafeDirs(d).Where(s => !skipDirs.Contains(Path.GetFileName(s), StringComparer.OrdinalIgnoreCase))) { stack.Push(s); }
    }
    return list;
}

static string[] SafeDirs(string dir)
{
    try { return Directory.GetDirectories(dir); } catch { return []; }
}

async Task<JsonNode> GetJson(string url, string? accept = null)
{
    using var req = new HttpRequestMessage(HttpMethod.Get, url);
    if (accept is not null) { req.Headers.Accept.ParseAdd(accept); }
    try
    {
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"⚠️ Abruf fehlgeschlagen: {url} ({ex.Message})");
        Environment.Exit(1);
        throw;
    }
}

// ================= model =================

record DotnetChannel(Version Channel, string Latest, string Phase, string Type, DateOnly? Eol);
record EolRelease(string Name, bool IsEol, DateOnly? EolFrom, bool IsEoas);

record Report(DateOnly GeneratedAt, int SoonDays, LatestVersions Latest, List<ProjectReport> Projects, List<string> WithoutTechnology);
record LatestVersions(string Dotnet, string? DotnetLts, string DotnetFramework, string Angular, string Flutter);

/// <param name="Lts">.NET: release type LTS · Angular: version is in its LTS phase · otherwise false</param>
record SupportInfo(string Status, DateOnly? Until, bool Lts, string? Note);

record DotnetEntry(string Kind, string TargetFramework, List<string> Projects, string Current, string CurrentSource,
    string Update, string? LatestInMajor, List<string> MajorPath, string? PortTo, SupportInfo Support);
record AngularEntry(string Location, string Current, string CurrentSource, string Range,
    string Update, string? LatestInMajor, List<string> MajorPath, SupportInfo Support);
record FlutterEntry(string Location, string? Current, string? CurrentSource,
    string Update, string? LatestInMajor, List<string> MajorPath, bool PipelineStable, SupportInfo Support);

record ProjectReport(string Name, string Path, List<DotnetEntry> Dotnet, List<AngularEntry> Angular, List<FlutterEntry> Flutter)
{
    public string Support => Dotnet.Select(d => d.Support.Status).Concat(Angular.Select(a => a.Support.Status)).Concat(Flutter.Select(f => f.Support.Status))
        .DefaultIfEmpty("supported").MaxBy(Rank)!;

    static int Rank(string status) => status switch { "eol" => 3, "soon" => 2, "supported" => 1, _ => 0 };
}

partial class Program
{
    [GeneratedRegex(@"<([A-Za-z_][\w.]*)(?:\s[^>]*)?>([^<]*)</\1>")] internal static partial Regex PropRx();
    [GeneratedRegex(@"<TargetFrameworks?>([^<]*)</TargetFrameworks?>")] internal static partial Regex TfmRx();
    [GeneratedRegex(@"<TargetFrameworkVersion>([^<]*)</TargetFrameworkVersion>")] internal static partial Regex TfvRx();
    [GeneratedRegex(@"\$\((\w+)\.Substring\(0,\s*\$\(\1\.LastIndexOf\('\.'\)\)\)\)")] internal static partial Regex SubstringRx();
    [GeneratedRegex(@"\$\((\w+)\)")] internal static partial Regex VarRx();
    [GeneratedRegex(@"^sdks:(?:\r?\n[ \t]+.*)*?\r?\n[ \t]+flutter:[ \t]*""?>=[ \t]*([\d.]+)", RegexOptions.Multiline)] internal static partial Regex FlutterLockRx();
    [GeneratedRegex(@"^environment:(?:\r?\n[ \t]+.*)*?\r?\n[ \t]+flutter:[ \t]*['""]?>=[ \t]*([\d.]+)", RegexOptions.Multiline)] internal static partial Regex FlutterEnvRx();
}
