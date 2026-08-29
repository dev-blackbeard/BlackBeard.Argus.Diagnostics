using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Pipeline;
using Argus.State;
using Xunit;

namespace Argus.Package.Tests;

/// <summary>
/// Tests that run against the packed artifacts rather than the projects.
/// </summary>
/// <remarks>
/// <para>
/// Every other test project in this repository references the projects, which proves the code
/// compiles. This one references the <c>.nupkg</c> files from <c>./artifacts/local-feed</c>,
/// which proves something else and more useful: that the packages are actually usable. A type
/// accidentally left internal, a dependency that leaked into the nuspec, a target framework
/// that does not resolve, a missing file in the package — none of those are visible from
/// inside the repository, and all of them are visible from here.
/// </para>
/// <para>
/// Run <c>scripts/pack-local</c> before this project builds.
/// </para>
/// </remarks>
public sealed class PackageSurfaceTests
{
    /// <summary>
    /// Architecture rule 2, checked against the artifact rather than the project file.
    /// </summary>
    /// <remarks>
    /// The MSBuild guard in <c>Directory.Build.targets</c> checks the project's declared
    /// references. This checks what actually came out, which is the claim that matters to a
    /// consumer: installing Argus.Core adds one assembly to their application and nothing else.
    /// </remarks>
    [Fact]
    public void ArgusCorePackageDeclaresNoDependencies()
    {
        string package = FindPackage("BlackBeard.Argus.Core");
        IReadOnlyList<string> dependencies = ReadDependencies(package, excludeFrameworkPlumbing: true);

        Assert.True(
            dependencies.Count == 0,
            "BlackBeard.Argus.Core must have zero package dependencies, but " + Path.GetFileName(package)
                + " declares: " + string.Join(", ", dependencies));
    }

    /// <summary>Architecture rule 3, checked the same way.</summary>
    [Fact]
    public void ArgusGraphicsDependsOnlyOnCoreAndMauiGraphics()
    {
        string package = FindPackage("BlackBeard.Argus.Graphics");
        IReadOnlyList<string> dependencies = ReadDependencies(package, excludeFrameworkPlumbing: true);

        foreach (string dependency in dependencies)
        {
            Assert.True(
                string.Equals(dependency, "BlackBeard.Argus.Core", StringComparison.Ordinal)
                    || string.Equals(dependency, "Microsoft.Maui.Graphics", StringComparison.Ordinal),
                "BlackBeard.Argus.Graphics must depend only on BlackBeard.Argus.Core and Microsoft.Maui.Graphics, but declares " + dependency);
        }

        Assert.Contains("Microsoft.Maui.Graphics", dependencies);
    }

    [Fact]
    public void PackagesMultiTargetAsSpecified()
    {
        Assert.Equal(
            new[] { "netstandard2.0", "net8.0" },
            SortedFrameworks(FindPackage("BlackBeard.Argus.Core")));

        Assert.Equal(
            new[] { "netstandard2.0", "net8.0" },
            SortedFrameworks(FindPackage("BlackBeard.Argus.Testing")));

        Assert.Equal(
            new[] { "netstandard2.0" },
            SortedFrameworks(FindPackage("BlackBeard.Argus.Graphics")));
    }

    [Fact]
    public void SymbolPackagesAreProduced()
    {
        Assert.True(File.Exists(FindPackage("BlackBeard.Argus.Core", ".snupkg")));
        Assert.True(File.Exists(FindPackage("BlackBeard.Argus.Graphics", ".snupkg")));
        Assert.True(File.Exists(FindPackage("BlackBeard.Argus.Testing", ".snupkg")));
    }

    /// <summary>
    /// The whole public workflow, exercised through the packages exactly as a stranger would.
    /// </summary>
    [Fact]
    public void TheDocumentedWorkflowRunsAgainstThePackagedAssemblies()
    {
        var options = new MonitorOptions();
        options.Thresholds.MaxTeleportDistanceMeters = 1000.0;
        options.Thresholds.MaxSpeedMetersPerSecond = 250.0;
        options.Thresholds.GroupOutlierRadiusMeters = 5000.0;

        var monitor = new EntityHealthMonitor(options);
        var now = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var samples = new List<EntitySample>();
        for (int i = 0; i < 5; i++)
        {
            samples.Add(new EntitySample("entity-" + i, now)
            {
                Latitude = 0.001 * (i + 1),
                Longitude = 0.001,
                Altitude = 100.0,
            });
        }

        GroupTickContext tick = monitor.CreateTickContext(samples, now);
        EntityHealthReport report = monitor.Observe(samples[0], tick);

        Assert.NotNull(report);
        Assert.NotNull(report.Findings);
        Assert.False(string.IsNullOrWhiteSpace(HealthFlagInfo.GetDefinition(HealthFlags.FieldShift)));
    }

    /// <summary>Rule 1, from outside: nothing presentational is reachable from Core's public surface.</summary>
    [Fact]
    public void CorePackageExposesNoPresentationTypes()
    {
        Assembly core = typeof(EntityHealthMonitor).Assembly;

        foreach (AssemblyName reference in core.GetReferencedAssemblies())
        {
            string name = reference.Name ?? string.Empty;
            Assert.False(name.StartsWith("Microsoft.Maui", StringComparison.Ordinal), "Argus.Core references " + name);
        }

        foreach (Type type in core.GetExportedTypes())
        {
            Assert.DoesNotContain("Color", type.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PackagedAssembliesCarryXmlDocumentation()
    {
        string package = FindPackage("BlackBeard.Argus.Core");
        using (ZipArchive archive = ZipFile.OpenRead(package))
        {
            bool found = false;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("Argus.Core.xml", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, "the package must ship XML documentation: findings are only self-describing if the contracts are");
        }
    }

    private static string LocalFeed()
    {
        foreach (AssemblyMetadataAttribute metadata in typeof(PackageSurfaceTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(metadata.Key, "ArgusLocalFeed", StringComparison.Ordinal) && metadata.Value != null)
            {
                return metadata.Value;
            }
        }

        throw new InvalidOperationException(
            "The ArgusLocalFeed assembly metadata is missing. It is set in Argus.Package.Tests.csproj.");
    }

    private static string FindPackage(string id, string extension = ".nupkg")
    {
        string feed = LocalFeed();
        if (!Directory.Exists(feed))
        {
            throw new DirectoryNotFoundException(
                "The local feed does not exist at " + feed + ". Run scripts/pack-local first.");
        }

        string[] matches = Directory.GetFiles(feed, id + ".*" + extension);
        if (matches.Length == 0)
        {
            throw new FileNotFoundException(
                "No " + id + extension + " in " + feed + ". Run scripts/pack-local first.", id);
        }

        Array.Sort(matches, StringComparer.Ordinal);
        return matches[matches.Length - 1];
    }

    private static XDocument ReadNuspec(string packagePath)
    {
        using (ZipArchive archive = ZipFile.OpenRead(packagePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                    && !entry.FullName.Contains("/"))
                {
                    using (Stream stream = entry.Open())
                    {
                        return XDocument.Load(stream);
                    }
                }
            }
        }

        throw new InvalidOperationException("No nuspec found in " + packagePath);
    }

    /// <summary>
    /// The package ids the SDK adds on a consumer's behalf rather than because the project asked
    /// for them.
    /// </summary>
    /// <remarks>
    /// <c>NETStandard.Library</c> is the reference assembly set for netstandard2.0. The SDK adds
    /// it implicitly to every netstandard2.0 project and it carries no third-party code, so it
    /// is framework plumbing rather than a dependency in the sense architecture rule 2 means.
    /// The MSBuild guard in <c>Directory.Build.targets</c> draws the same line, in the same
    /// place, for the same reason.
    /// </remarks>
    private static readonly string[] FrameworkPlumbing = { "NETStandard.Library" };

    private static IReadOnlyList<string> ReadDependencies(string packagePath, bool excludeFrameworkPlumbing = false)
    {
        XDocument nuspec = ReadNuspec(packagePath);
        var dependencies = new List<string>();

        foreach (XElement element in nuspec.Descendants())
        {
            if (!string.Equals(element.Name.LocalName, "dependency", StringComparison.Ordinal))
            {
                continue;
            }

            XAttribute? id = element.Attribute("id");
            if (id == null || dependencies.Contains(id.Value))
            {
                continue;
            }

            if (excludeFrameworkPlumbing && Array.IndexOf(FrameworkPlumbing, id.Value) >= 0)
            {
                continue;
            }

            dependencies.Add(id.Value);
        }

        return dependencies;
    }

    private static string[] SortedFrameworks(string packagePath)
    {
        var frameworks = new List<string>();

        using (ZipArchive archive = ZipFile.OpenRead(packagePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] parts = entry.FullName.Split('/');
                if (parts.Length >= 2 && !frameworks.Contains(parts[1]))
                {
                    frameworks.Add(parts[1]);
                }
            }
        }

        // Declaration order from the csproj, so a change to the TargetFrameworks list is visible
        // here rather than being absorbed by a sort.
        var expectedOrder = new List<string> { "netstandard2.0", "net8.0" };
        frameworks.Sort((left, right) => expectedOrder.IndexOf(left).CompareTo(expectedOrder.IndexOf(right)));
        return frameworks.ToArray();
    }
}
