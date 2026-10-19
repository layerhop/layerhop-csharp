# Contributing

Thanks for your interest in LayerHop!

## Where the code lives

This repository is **exported automatically** from the LayerHop monorepo
(`sdks/csharp`), which is the single source of truth. Every sync commit is titled
`Sync from layerhop monorepo <sha>` and references the monorepo commit it was built from.

**Pull requests are welcome here.** A maintainer reviews your PR, applies the change upstream in the
monorepo, and the next sync brings it back into this repository with you credited in the PR. Because
of that, your PR may be closed rather than merged directly; that is expected and does not mean it was
rejected.

Issues are welcome too. For security problems, follow [SECURITY.md](SECURITY.md) instead.

## Development

See [README.md](README.md) for setup. CI (`.github/workflows/ci.yml`) runs the same checks you
should run locally before opening a PR.

## Releases

Bump `Version` in `src/LayerHop/LayerHop.csproj` and `version` in `src/LayerHop/package.json` (Unity), then push a `vX.Y.Z` tag. `.github/workflows/release.yml` runs the tests, packs and pushes to NuGet (requires the `NUGET_API_KEY` repository secret); versions already on NuGet are skipped. Local dev: `dotnet test` (.NET 8 SDK).
