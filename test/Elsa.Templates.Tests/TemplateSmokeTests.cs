using System.Diagnostics;
using Xunit;

namespace Elsa.Templates.Tests;

public class TemplateSmokeTests : IClassFixture<TemplatePackageFixture>
{
    private readonly TemplatePackageFixture _fixture;

    public TemplateSmokeTests(TemplatePackageFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("static")]
    [InlineData("shell")]
    public async Task ElsaServerTemplateBuilds(string featureModel)
    {
        var projectName = $"Sample.{featureModel}.Server";
        await GenerateAndBuildAsync("elsa-server", projectName, "--feature-model", featureModel);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("sqlserver")]
    [InlineData("postgresql")]
    [InlineData("oracle")]
    public async Task ElsaServerTemplateBuildsWithStaticPersistence(string persistence)
    {
        var projectName = $"Sample.{ToPascalCase(persistence)}.Server";
        await GenerateAndBuildAsync("elsa-server", projectName, "--feature-model", "static", "--persistence", persistence);
    }

    [Fact]
    public async Task ElsaServerTemplateBuildsWithShellPersistence()
    {
        await GenerateAndBuildAsync("elsa-server", "Sample.Shell.PostgreSql.Server", "--feature-model", "shell", "--persistence", "postgresql");
    }

    [Theory]
    [InlineData("server")]
    [InlineData("wasm")]
    [InlineData("hybrid")]
    public async Task ElsaStudioTemplateBuilds(string hosting)
    {
        var solutionName = $"Sample.{hosting}.Studio";
        await GenerateAndBuildAsync("elsa-studio", solutionName, "--hosting", hosting);
    }

    [Theory]
    [InlineData("server", "open-id-connect")]
    [InlineData("wasm", "elsa-login")]
    [InlineData("hybrid", "open-id-connect")]
    public async Task ElsaStudioTemplateBuildsWithAuthAndLabelsModule(string hosting, string authProvider)
    {
        var solutionName = $"Sample.{ToPascalCase(hosting)}.{ToPascalCase(authProvider)}.Studio";
        var options = new[] { "--hosting", hosting, "--auth-provider", authProvider, "--with-labels" };

        await GenerateAndBuildAsync("elsa-studio", solutionName, options);
    }

    [Theory]
    [InlineData("static", "server")]
    [InlineData("static", "wasm")]
    [InlineData("static", "hybrid")]
    [InlineData("shell", "server")]
    [InlineData("shell", "wasm")]
    [InlineData("shell", "hybrid")]
    public async Task ElsaCombinedTemplateBuilds(string featureModel, string studioHosting)
    {
        var solutionName = $"Sample.{ToPascalCase(featureModel)}.{ToPascalCase(studioHosting)}.Combined";
        await GenerateAndBuildAsync("elsa-combined", solutionName, "--feature-model", featureModel, "--studio-hosting", studioHosting);
    }

    [Fact]
    public async Task ElsaCombinedTemplateBuildsWithPersistenceAuthAndLabelsModule()
    {
        var solutionName = "Sample.Static.Hybrid.PostgreSql.OpenIdConnect.Combined";
        var options = new[]
        {
            "--feature-model", "static",
            "--studio-hosting", "hybrid",
            "--persistence", "postgresql",
            "--auth-provider", "open-id-connect",
            "--with-labels"
        };

        await GenerateAndBuildAsync("elsa-combined", solutionName, options);
    }

    private static string ToPascalCase(string value)
    {
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(part => string.Concat(part[..1].ToUpperInvariant(), part[1..])));
    }

    private async Task GenerateAndBuildAsync(string templateName, string solutionName, params string[] templateOptions)
    {
        await using var workspace = TempWorkspace.Create();
        var outputPath = Path.Combine(workspace.Path, "output");
        var hivePath = Path.Combine(workspace.Path, "hive");

        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(hivePath);

        await DotNet.RunAsync("new", "install", _fixture.PackagePath, "--debug:custom-hive", hivePath);

        var newArguments = new List<string>
        {
            "new",
            templateName,
            "-n",
            solutionName,
            "-o",
            outputPath
        };
        newArguments.AddRange(templateOptions);
        newArguments.AddRange(["--debug:custom-hive", hivePath]);

        await DotNet.RunAsync(newArguments.ToArray());
        AssertHealthChecksUseDedicatedEndpoint(outputPath);
        await DotNet.RunAsync("build", Path.Combine(outputPath, $"{solutionName}.slnx"));
    }

    private static void AssertHealthChecksUseDedicatedEndpoint(string outputPath)
    {
        var programFiles = Directory.GetFiles(outputPath, "Program.cs", SearchOption.AllDirectories);
        var rootHealthCheckMappings = programFiles
            .Where(file => File.ReadAllText(file).Contains("MapHealthChecks(\"/\")", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(outputPath, file))
            .ToArray();

        Assert.Empty(rootHealthCheckMappings);
    }
}

public sealed class TemplatePackageFixture : IAsyncLifetime
{
    public string PackagePath { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var repositoryRoot = RepositoryPaths.Root;
        var projectPath = Path.Combine(repositoryRoot, "src", "Elsa.Templates", "Elsa.Templates.csproj");

        await DotNet.RunAsync("pack", projectPath, "-c", "Release");

        var packageDirectory = Path.Combine(repositoryRoot, "artifacts", "package", "release");
        PackagePath = Directory
            .GetFiles(packageDirectory, "Elsa.Templates.*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}

public static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Elsa.Templates.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

public static class DotNet
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public static async Task RunAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveDotNetPath(),
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var waitTask = process.WaitForExitAsync();
        var completedTask = await Task.WhenAny(waitTask, Task.Delay(Timeout));

        if (completedTask != waitTask)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {string.Join(' ', arguments)} timed out after {Timeout}.");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    private static string ResolveDotNetPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("DOTNET_EXE");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        const string macOsPath = "/usr/local/share/dotnet/dotnet";
        return File.Exists(macOsPath) ? macOsPath : "dotnet";
    }
}

public sealed class TempWorkspace : IAsyncDisposable
{
    private TempWorkspace(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempWorkspace Create()
    {
        var parentDirectory = Directory.GetParent(RepositoryPaths.Root)?.FullName ?? RepositoryPaths.Root;
        var path = System.IO.Path.Combine(parentDirectory, ".elsa-template-tests", $"elsa-template-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return new(path);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);

        return ValueTask.CompletedTask;
    }
}
