# Roadmap

## Goal

Provide official .NET solution templates that make it fast to start new Elsa applications without copying sample hosts from the source repositories.

## Branching Model

- `main` always targets the latest stable Elsa release.
- Stable version branches can preserve older stable template baselines.
- Preview version branches can target Elsa preview packages such as `3.8.0-preview.xxx`.

## Template Set

### Elsa Server

Generate a server host with a template option for the feature model:

- `static`: legacy static feature registration, aligned with `Elsa.Server.Web`.
- `shell`: CShells-based modular feature registration, aligned with `Elsa.ModularServer.Web`.

### Elsa Studio

Generate a Studio solution with a template option for the Blazor hosting model:

- `server`: Blazor Server.
- `wasm`: Blazor WebAssembly.
- `hybrid`: host project plus wasm client project, with runtime hosting model configurable by application startup where supported.

### Elsa Combined

Generate a combined Server + Studio solution with template options for:

- Elsa Server feature model: `static` or `shell`.
- Elsa Studio hosting model: `server`, `wasm`, or `hybrid`.

## Validation Strategy

Each template option combination should be covered by a smoke test that:

1. Packs the template package.
2. Installs the local package into an isolated template hive.
3. Generates a project or solution for the option combination.
4. Restores and builds the generated output.

## Release Strategy

Package the templates as `Elsa.Templates` and publish through the normal Elsa package release flow once the template set is stable.
