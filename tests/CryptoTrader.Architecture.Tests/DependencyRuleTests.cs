using System.Reflection;
using System.Xml.Linq;
using Xunit;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Strategies.Abstractions;
using NetArchTest.Rules;
using Shouldly;

namespace CryptoTrader.Architecture.Tests;

public class DependencyRuleTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedReferences =
        new Dictionary<string, string[]>
        {
            ["CryptoTrader.Domain"] = [],
            ["CryptoTrader.Contracts"] = [],
            ["CryptoTrader.Strategies.Abstractions"] = ["CryptoTrader.Domain"],
            ["CryptoTrader.Exchanges.Abstractions"] = ["CryptoTrader.Domain"],
            ["CryptoTrader.Application"] =
            [
                "CryptoTrader.Domain",
                "CryptoTrader.Strategies.Abstractions",
                "CryptoTrader.Exchanges.Abstractions",
            ],
            ["CryptoTrader.Infrastructure"] = ["CryptoTrader.Domain", "CryptoTrader.Application"],
            ["CryptoTrader.Exchanges.Bybit"] = ["CryptoTrader.Exchanges.Abstractions"],
            ["CryptoTrader.Exchanges.Okx"] = ["CryptoTrader.Exchanges.Abstractions"],
            ["CryptoTrader.Strategies.TrendFollowing"] = ["CryptoTrader.Domain", "CryptoTrader.Strategies.Abstractions"],
            ["CryptoTrader.Backtesting"] =
            [
                "CryptoTrader.Domain",
                "CryptoTrader.Application",
                "CryptoTrader.Strategies.Abstractions",
            ],
            ["CryptoTrader.Host"] =
            [
                "CryptoTrader.Application",
                "CryptoTrader.Infrastructure",
                "CryptoTrader.Contracts",
                "CryptoTrader.Exchanges.Bybit",
                "CryptoTrader.Exchanges.Okx",
                "CryptoTrader.Strategies.TrendFollowing",
                "CryptoTrader.Backtesting",
            ],
        };

    [Fact]
    public void Domain_csproj_contains_no_project_reference()
    {
        var references = ReadProjectReferences(Path.Combine(RepositoryRoot(), "src", "CryptoTrader.Domain", "CryptoTrader.Domain.csproj"));
        references.ShouldBeEmpty();
    }

    [Fact]
    public void Project_references_follow_the_approved_graph()
    {
        var root = RepositoryRoot();
        foreach (var (project, expected) in ExpectedReferences)
        {
            var csproj = Directory.EnumerateFiles(root, project + ".csproj", SearchOption.AllDirectories).Single();
            var actual = ReadProjectReferences(csproj).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var wanted = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            actual.ShouldBe(wanted, $"Project {project} references [{string.Join(", ", actual)}].");
        }
    }

    [Fact]
    public void Package_references_do_not_embed_versions()
    {
        var root = RepositoryRoot();
        var offenders = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path)
                .Descendants("PackageReference")
                .Where(element => element.Attribute("Version") is not null)
                .Select(element => path))
            .ToArray();
        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Central_package_versions_match_the_contract()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Packages.props"));
        var versions = document.Descendants("PackageVersion")
            .ToDictionary(
                element => element.Attribute("Include")!.Value,
                element => element.Attribute("Version")!.Value,
                StringComparer.Ordinal);

        versions["Microsoft.EntityFrameworkCore"].ShouldBe("10.0.15");
        versions["Npgsql.EntityFrameworkCore.PostgreSQL"].ShouldBe("10.0.4");
        versions["Microsoft.Extensions.Options"].ShouldBe("10.0.15");
        versions["Serilog.AspNetCore"].ShouldBe("9.0.0");
        versions["Serilog.Sinks.Console"].ShouldBe("6.1.1");
        versions["Serilog.Settings.Configuration"].ShouldBe("9.0.0");
        versions["OpenTelemetry.Extensions.Hosting"].ShouldBe("1.21.0");
        versions["OpenTelemetry.Instrumentation.AspNetCore"].ShouldBe("1.21.0");
        versions["OpenTelemetry.Instrumentation.Http"].ShouldBe("1.21.0");
        versions["OpenTelemetry.Exporter.OpenTelemetryProtocol"].ShouldBe("1.21.0");
        versions["OpenTelemetry.Exporter.Prometheus.AspNetCore"].ShouldBe("1.21.0-beta.1");
        versions["xunit"].ShouldBe("2.9.3");
        versions["xunit.runner.visualstudio"].ShouldBe("3.1.5");
        versions["Microsoft.NET.Test.Sdk"].ShouldBe("17.14.1");
        versions["Shouldly"].ShouldBe("4.3.0");
        versions["NSubstitute"].ShouldBe("5.3.0");
        versions["Testcontainers.PostgreSql"].ShouldBe("4.17.0");
        versions["NetArchTest.Rules"].ShouldBe("1.3.2");
        versions["FsCheck.Xunit"].ShouldBe("3.3.1");
    }

    [Fact]
    public void Domain_assembly_does_not_reference_infrastructure_frameworks()
    {
        var names = ReferencedNames(typeof(Order).Assembly);
        foreach (var name in names)
        {
            name.ShouldNotContain("EntityFrameworkCore");
            name.ShouldNotContain("AspNetCore");
            name.ShouldNotContain("Npgsql");
            name.ShouldNotContain("Bybit");
        }

        AssertNoTypeDependency(typeof(Order).Assembly, "Microsoft.EntityFrameworkCore");
        AssertNoTypeDependency(typeof(Order).Assembly, "Microsoft.AspNetCore");
        AssertNoTypeDependency(typeof(Order).Assembly, "Npgsql");
        AssertNoTypeDependency(typeof(Order).Assembly, "Bybit");
    }

    [Fact]
    public void Abstraction_assemblies_do_not_reference_infrastructure_or_host()
    {
        AssertIsolated(typeof(ITradingStrategy).Assembly);
        AssertIsolated(typeof(IExchangeAdapter).Assembly);
    }

    private static void AssertIsolated(Assembly assembly)
    {
        var names = ReferencedNames(assembly);
        names.ShouldNotContain("CryptoTrader.Infrastructure");
        names.ShouldNotContain("CryptoTrader.Host");
        AssertNoTypeDependency(assembly, "CryptoTrader.Infrastructure");
        AssertNoTypeDependency(assembly, "CryptoTrader.Host");
    }

    private static void AssertNoTypeDependency(Assembly assembly, string dependency)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(dependency)
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    private static string[] ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToArray();

    private static IReadOnlyList<string> ReadProjectReferences(string csprojPath)
    {
        return XDocument.Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)))
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CryptoTrader.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
