// Validates the Sentry and Serilog configuration of all ASP.NET Core projects below the given folders with the real .NET runtime:
//  - loads appsettings.json + appsettings.{Environment}.json (+ config.default.json for Production) with Microsoft.Extensions.Configuration,
//  - builds a Serilog logger via ReadFrom.Configuration and collects SelfLog messages (unknown sinks, wrong arguments, level switches),
//  - binds the "Sentry" section to SentryAspNetCoreOptions with ErrorOnUnknownConfiguration (unknown keys, wrong value types).
// Usage: dotnet run validate-config.cs -- <absolute folder> [<absolute folder> ...]
//   (dotnet run does not keep the caller's working directory, so pass absolute paths)
// Keep the package versions in line with the projects; sinks used by a project must be referenced here, otherwise they are reported as unknown.
#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:package Sentry.AspNetCore@7.0.1
#:package Sentry.Serilog@7.0.1
#:package Serilog.AspNetCore@10.0.0
#:package Serilog.Sinks.Seq@9.1.0
#:package Serilog.Sinks.Async@2.1.0
#:package Serilog.Enrichers.Environment@3.0.1
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Debugging;

Console.OutputEncoding = System.Text.Encoding.UTF8; // emojis and umlauts also in Windows consoles
var roots = args.Length > 0 ? args : [Directory.GetCurrentDirectory()];
foreach (var root in roots.Where(r => !Directory.Exists(r)))
{
    Console.WriteLine($"⚠️ Ordner nicht gefunden: {root} (absoluten Pfad angeben)");
}
string[] skipDirs = ["node_modules", "bin", "obj", ".git", ".vs", ".idea", "dist", "artifacts", ".claude", "submodule", "submodules", ".angular"];

var projects = roots.SelectMany(FindProjects).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
if (projects.Count == 0)
{
    Console.WriteLine("Keine ASP.NET-Core-Projekte mit Sentry-/Serilog-Konfiguration gefunden.");
    return;
}

Console.WriteLine("| Projekt | Umgebung | Serilog | Sentry |");
Console.WriteLine("|---|---|---|---|");
var failures = 0;
foreach (var project in projects)
{
    foreach (var environment in new[] { "Production", "Development" })
    {
        var builder = new ConfigurationBuilder().SetBasePath(project.Dir)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (environment == "Production")
        {
            builder.AddJsonFile("config.default.json", optional: true); // deployed as config.json
        }

        string serilog, sentry;
        IConfigurationRoot config;
        try
        {
            config = builder.Build();
        }
        catch (Exception e)
        {
            Console.WriteLine($"| {project.Name} | {environment} | ❌ JSON: {Clean(e.Message)} | – |");
            failures++;
            continue;
        }

        // Serilog: build the logger and collect SelfLog output
        var selfLog = new List<string>();
        SelfLog.Enable(m => selfLog.Add(m.Trim()));
        try
        {
            // creating the logger instantiates every sink, enricher and level switch; nothing needs to be logged
            using var logger = new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();
            serilog = selfLog.Count == 0 ? "✅" : "⚠️ " + string.Join("<br>", selfLog.Distinct().Select(Clean));
        }
        catch (Exception e)
        {
            serilog = $"❌ {Clean(e.GetBaseException().Message)}";
        }
        finally
        {
            SelfLog.Disable();
        }

        // Sentry: bind like UseSentry does, but fail on keys the options class does not know
        var section = config.GetSection("Sentry");
        if (!section.Exists())
        {
            sentry = "➖ kein Abschnitt";
        }
        else
        {
            try
            {
                section.Get<Sentry.AspNetCore.SentryAspNetCoreOptions>(o => o.ErrorOnUnknownConfiguration = true);
                sentry = "✅";
            }
            catch (Exception e)
            {
                sentry = $"❌ {Clean(e.GetBaseException().Message)}";
            }
        }

        if (!serilog.StartsWith("✅") || sentry.StartsWith("❌")) failures++;
        Console.WriteLine($"| {project.Name} | {environment} | {serilog} | {sentry} |");
    }
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "Alle Konfigurationen sind gültig." : $"{failures} Konfiguration(en) mit Befunden.");
return;

IEnumerable<(string Name, string Dir)> FindProjects(string root)
{
    var pending = new Stack<string>([root]);
    while (pending.Count > 0)
    {
        var dir = pending.Pop();
        string[] files, dirs;
        try { files = Directory.GetFiles(dir, "*.csproj"); dirs = Directory.GetDirectories(dir); }
        catch { continue; }
        foreach (var d in dirs.Where(d => !skipDirs.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))) pending.Push(d);
        foreach (var csproj in files)
        {
            if (!Regex.IsMatch(File.ReadAllText(csproj), "Sdk=\"Microsoft\\.NET\\.Sdk\\.Web\"", RegexOptions.IgnoreCase)) continue;
            var configFiles = new[] { "appsettings.json", "appsettings.Production.json", "appsettings.Development.json", "config.default.json" }
                .Select(f => Path.Combine(dir, f)).Where(File.Exists).ToList();
            if (!configFiles.Any(f => f.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(f) == "appsettings.json")) continue;
            // only projects that configure Sentry or Serilog
            if (!configFiles.Any(f => Regex.IsMatch(File.ReadAllText(f), "\"(Sentry|Serilog)\"\\s*:\\s*\\{", RegexOptions.IgnoreCase))) continue;
            yield return (Path.GetFileNameWithoutExtension(csproj), dir);
        }
    }
}

static string Clean(string s) => Regex.Replace(s.Replace("|", "\\|"), @"\s+", " ").Trim();
