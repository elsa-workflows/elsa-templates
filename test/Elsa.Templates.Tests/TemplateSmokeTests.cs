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
        await using var workspace = TempWorkspace.Create();
        var projectName = $"Sample.{featureModel}.Server";
        var outputPath = Path.Combine(workspace.Path, "output");
        var hivePath = Path.Combine(workspace.Path, "hive");

        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(hivePath);

        await DotNet.RunAsync("new", "install", _fixture.PackagePath, "--debug:custom-hive", hivePath);
        await DotNet.RunAsync("new", "elsa-server", "-n", projectName, "-o", outputPath, "--feature-model", featureModel, "--debug:custom-hive", hivePath);
        await DotNet.RunAsync("build", Path.Combine(outputPath, $"{projectName}.csproj"));
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
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"elsa-template-tests-{Guid.NewGuid():N}");
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
