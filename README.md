# Elsa Templates

`elsa-templates` provides .NET solution templates for getting started with Elsa quickly.

## Version Policy

The `main` branch targets the latest stable Elsa release. At repository creation time this is Elsa `3.7.0`.

Version-specific branches may target exact stable or preview versions, for example:

- `3.7.0`
- `3.8.0-preview`
- `3.8.0-preview.1234`

## Planned Templates

- `elsa-server`: Elsa Server with selectable feature model.
- `elsa-studio`: Elsa Studio with selectable Blazor hosting model.
- `elsa-combined`: Elsa Server and Elsa Studio in one solution with selectable server feature model and Studio hosting model.

## Install From NuGet

Stable template packages are published to NuGet.org:

```bash
dotnet new install Elsa.Templates
```

## Install From Preview Feed

Preview template packages are published to the Elsa preview feed. Install a preview version by passing the feed URL as an additional source and specifying the preview version:

```bash
dotnet new install Elsa.Templates@<preview-version> --add-source https://f.feedz.io/elsa-workflows/elsa-3/nuget/index.json
```

For example: `Elsa.Templates@3.8.0-preview.1234`.

To make the preview feed available for package restore as well, add it to your `NuGet.config` next to the main NuGet feed:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="NuGet.org" value="https://api.nuget.org/v3/index.json" />
    <add key="Elsa 3 Preview" value="https://f.feedz.io/elsa-workflows/elsa-3/nuget/index.json" />
  </packageSources>
</configuration>
```

## Install From Source

```bash
dotnet pack src/Elsa.Templates/Elsa.Templates.csproj
dotnet new install artifacts/package/release/Elsa.Templates.*.nupkg
```

## Usage

Create an Elsa Server using static feature registration:

```bash
dotnet new elsa-server -n MyElsaServer --feature-model static
```

Create an Elsa Server using CShells:

```bash
dotnet new elsa-server -n MyElsaServer --feature-model shell
```

Create an Elsa Studio solution:

```bash
dotnet new elsa-studio -n MyElsaStudio --hosting server
dotnet new elsa-studio -n MyElsaStudio --hosting wasm
dotnet new elsa-studio -n MyElsaStudio --hosting hybrid
```

Create a combined Elsa Server + Studio solution:

```bash
dotnet new elsa-combined -n MyElsaApp --feature-model static --studio-hosting server
dotnet new elsa-combined -n MyElsaApp --feature-model shell --studio-hosting wasm
dotnet new elsa-combined -n MyElsaApp --feature-model shell --studio-hosting hybrid
```

Hybrid Studio output includes a host project and a WASM client project. The generated host reads `Studio:HostingModel` from configuration so the runtime can start Studio as `Server` or `Wasm`.

## Verification

The smoke tests pack the template package, install it into an isolated template hive, generate every supported option combination, and build the generated outputs.

```bash
dotnet test test/Elsa.Templates.Tests/Elsa.Templates.Tests.csproj
```

## Development

See [Roadmap](docs/roadmap.md) and [Tasks](docs/tasks.md).
