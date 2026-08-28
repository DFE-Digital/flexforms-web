# Versioning (GitVersion)

Application version is calculated from **Git tags** and **commits** — do not edit `<Version>` in `GovUK.Dfe.FlexForms.Web.csproj`.

## Tags and releases

- Tags use the format **`v2.2.3`** (see `tag-prefix` in `GitVersion.yml`).
- On every push to **`main`**, the **Release** workflow:
  1. Calculates the semver with GitVersion
  2. Creates tag `v{x.y.z}` if it does not already exist
  3. Creates a GitHub release titled `{x.y.z} - {commit subject}`

## Bumping the version

On `main`, each merge without a matching tag increments the **patch** by default.

Use commit message hints to bump minor or major:

| Intent | Commit message contains |
|--------|-------------------------|
| Patch (default) | normal merge / fix commits |
| Minor | `+semver: minor` or `+semver: feature` |
| Major | `+semver: major` or `+semver: breaking` |

Example:

```text
Mask tenant settings by default +semver: patch

(%release-note:All tenant setting values are masked until Show value is clicked.%)
```

## Release notes

Add notes to the merge commit body:

```text
(%release-note:Describe what changed for operators and users.%)
```

## Local builds

- Local `dotnet build` uses **`0.0.0-local`** unless you pass `-p:Version=x.y.z`.
- Docker builds use `APP_VERSION` (CI sets this from the GitVersion workflow). Local docker defaults to `0.0.0-local`.

## First-time setup

Versioning is driven by existing **`v*`** git tags. Tag the first release manually if needed, then merges to `main` increment patch automatically.
