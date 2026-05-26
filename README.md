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

## Install From Source

```bash
dotnet pack src/Elsa.Templates/Elsa.Templates.csproj
dotnet new install artifacts/package/release/Elsa.Templates.*.nupkg
```

## Development

See [Roadmap](docs/roadmap.md) and [Tasks](docs/tasks.md).
