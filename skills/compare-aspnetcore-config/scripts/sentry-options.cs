// Lists the option names of SentryAspNetCoreOptions (incl. base classes) that can be bound from the "Sentry" config section.
// Usage: dotnet run sentry-options.cs > ../references/sentry-options.txt
// Change the package version below when the Sentry SDK is updated.
#:sdk Microsoft.NET.Sdk.Web
#:package Sentry.AspNetCore@7.0.1
using System.Reflection;

var type = typeof(Sentry.AspNetCore.SentryAspNetCoreOptions);
var version = type.Assembly.GetName().Version;
Console.WriteLine($"# Sentry.AspNetCore {version} – SentryAspNetCoreOptions (inkl. Basisklassen), per Reflection ermittelt");
Console.WriteLine("# Neu erzeugen mit: dotnet run scripts/sentry-options.cs > references/sentry-options.txt");
foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.SetMethod is { IsPublic: true } || p.PropertyType.IsGenericType)
             .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() is null)
             .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(p.Name);
}
