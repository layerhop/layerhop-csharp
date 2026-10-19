using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LayerHop.Tests
{
    /// <summary>Keeps the NuGet and Unity package definitions consistent.</summary>
    public class PackageTests
    {
        private static string PackageDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LayerHop.sln"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "LayerHop");
        }

        [Fact]
        public void VersionsMatch()
        {
            var pkg = PackageDir();
            var csproj = XDocument.Load(Path.Combine(pkg, "LayerHop.csproj"));
            var version = csproj.Descendants("Version").Single().Value;
            var upm = JObject.Parse(File.ReadAllText(Path.Combine(pkg, "package.json")));
            Assert.Equal(LayerHopClient.Version, version);
            Assert.Equal(LayerHopClient.Version, (string?)upm["version"]);
        }

        [Fact]
        public void EveryUnityAssetHasAMetaFile()
        {
            var pkg = PackageDir();
            var missing = Directory.EnumerateFileSystemEntries(pkg, "*", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".meta", StringComparison.Ordinal))
                .Where(p => !Path.GetRelativePath(pkg, p).Split(Path.DirectorySeparatorChar).Any(s => s == "bin" || s == "obj" || s.StartsWith(".")))
                .Where(p => !File.Exists(p + ".meta"))
                .Select(p => Path.GetRelativePath(pkg, p))
                .ToList();
            Assert.True(missing.Count == 0, "run `node tools/gen-unity-meta.mjs`; missing .meta for: " + string.Join(", ", missing));
        }
    }
}
