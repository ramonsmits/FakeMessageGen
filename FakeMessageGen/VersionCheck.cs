using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

static class VersionCheck
{
    const string PackageId = "nbraceit.fakemessagegen";
    static readonly SemanticVersion Version = SemanticVersion.Parse(Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyInformationalVersionAttribute>().Single().InformationalVersion);
    public static async Task<string> Report()
    {
        try
        {
            using var cache = new SourceCacheContext();
            var nuget = NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resourceFeedz = await nuget.GetResourceAsync<FindPackageByIdResource>();
            using var cts = new CancellationTokenSource(2000);
            var versions = await resourceFeedz.GetAllVersionsAsync(PackageId, cache, NullLogger.Instance, cts.Token);
            var latest = versions.Where(v => v > Version && !v.IsPrerelease).Max();
            if (Version < latest)
                return $"{Ansi.Underline}{Ansi.GetAnsiColor(ConsoleColor.Yellow)}New version available: {latest}{Ansi.Reset}, current {Version}\n";
        }
        catch
        {
        }
        return $"{Version}";
    }
}