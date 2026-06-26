# Internal Release Guide

How to cut a release of **Terminator** and **Terminator.Templates** to nuget.org.

> **Audience:** maintainers. This is not needed to *use* the library.

## How it works (the short version)

- **Versions come from git tags** — [MinVer](https://github.com/adamralph/minver) computes the package
  version from the nearest tag. There is no `<Version>` in the `.csproj` files; never hand-edit a version.
- **Releasing = pushing a tag.** A GitHub Actions workflow packs both packages and publishes them.
- **Publishing is keyless** — the workflow authenticates to nuget.org with a short-lived OIDC token
  ([Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)). No API key
  is stored anywhere.

## Releasing (the ritual)

```bash
# 1. Be on a green main
git checkout main && git pull

# 2. Choose the version. This is a semver decision (patch / minor / major) —
#    the tag IS the version, so pick deliberately.
git tag 1.0.23
git push origin 1.0.23      # or: git push --follow-tags

# 3. Watch the run: https://github.com/arendvw/Terminator/actions
#    Both packages appear on nuget.org within a few minutes (after indexing).
```

That's it. Nothing to edit, no keys to handle.

### Choosing the version number

`MAJOR.MINOR.PATCH`:
- **PATCH** (`1.0.22 → 1.0.23`) — bug fixes, no API change.
- **MINOR** (`1.0.x → 1.1.0`) — backwards-compatible additions.
- **MAJOR** (`1.x → 2.0.0`) — breaking changes.

## How versioning behaves (MinVer)

| Commit state | Version produced | Published? |
|--------------|------------------|------------|
| On an exact tag (e.g. `1.0.23`) | `1.0.23` | yes, by the workflow |
| One or more commits **after** a tag | pre-release, e.g. `1.0.24-alpha.0.3` | no — local/CI builds only |
| Working tree changes | same as above | no |

The library and templates **always share the same version**, because both read the same tag.

## One-time setup

### 1. Trusted Publishing policy on nuget.org

nuget.org → your avatar → **Trusted Publishing** → add a policy:

| Field | Value |
|-------|-------|
| Owner | `avanwaart` (the account that owns the packages) |
| Repository Owner | `arendvw` |
| Repository | `Terminator` |
| Workflow File | `publish.yml` (filename only — no path) |
| Environment | *(leave blank — see optional guardrails)* |

> If you don't see "Trusted Publishing", it isn't rolled out to your account yet — fall back to a
> scoped API key stored as the `NUGET_API_KEY` secret until it appears.

### 2. The publish workflow

`.github/workflows/publish.yml`:

```yaml
name: Publish to NuGet.org
on:
  push:
    tags: ['[0-9]+.[0-9]+.[0-9]+']     # fires on 1.0.23-style tags

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      id-token: write     # required for OIDC — this is the whole trick
      contents: read
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0   # MinVer needs full history + tags

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Pack
        run: |
          dotnet pack src/Terminator.csproj -c Release -o artifacts
          dotnet pack template/Terminator.Templates.csproj -c Release -o artifacts

      - name: NuGet login (OIDC -> short-lived key)
        uses: NuGet/login@v1
        id: login
        with:
          user: avanwaart           # nuget.org profile name, not an email

      - name: Push
        run: >
          dotnet nuget push "artifacts/*.nupkg"
          --api-key ${{ steps.login.outputs.NUGET_API_KEY }}
          --source https://api.nuget.org/v3/index.json
          --skip-duplicate
```

Key points:
- `fetch-depth: 0` — without full history MinVer can't see tags and versions come out wrong.
- `--skip-duplicate` — re-running a release that's already published is a harmless no-op.
- The OIDC key lives only for ~1 hour and is requested right before the push.

## Optional guardrails

nuget.org is **immutable** — a published version can be *unlisted* but never deleted. The defaults above
(specific tag trigger + `--skip-duplicate` + MinVer making the tag *be* the version) prevent most mistakes.
Add any of these for more protection:

### A. Manual approval gate (recommended if you want a final "are you sure?")

Pauses the publish until you click **Approve** in the Actions UI, even after a tag fires.

1. In `publish.yml`, add to the job:
   ```yaml
   jobs:
     publish:
       environment: release
   ```
2. Repo → **Settings → Environments → `release` → Required reviewers** → add yourself.
3. *(optional, tighter)* set **Environment: `release`** on the nuget.org policy too, so a key can only be
   minted from this gated environment.

On public repos this is free.

### B. Tag protection (stop accidental tags)

Repo → **Settings → Rules → Rulesets** → restrict who can create tags matching
`[0-9]+.[0-9]+.[0-9]+`. Since the tag is the trigger, protecting tag creation protects releases.

### C. Version / tag mismatch

Not needed — with MinVer the tag *is* the version, so they can't disagree. (This is the class of bug the
old hand-edited `<Version>` approach was prone to.)

## Recovering from a bad release

You can't delete a version from nuget.org. If a release is broken:
1. **Unlist** it on nuget.org (hides it from search/restore; existing pins still resolve).
2. Fix the issue, then tag and publish a **higher** version (e.g. `1.0.24`).

## Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| Workflow publishes a `-alpha`/pre-release version | HEAD isn't the tagged commit, or `fetch-depth: 0` is missing so MinVer can't see the tag. |
| `403` on push | Policy fields don't match (owner / repo / workflow filename), or the policy is scoped to an environment the job didn't run in. |
| Version not installable right after the run | Normal — nuget.org validation/indexing takes a few minutes. |
| "Trusted Publishing" missing in nuget.org | Gradual rollout; use a scoped `NUGET_API_KEY` secret temporarily. |

## Notes

- **GitHub Packages** is no longer the publish target — nuget.org is the canonical public feed. (The legacy
  `build/` release tool that pushed to GitHub Packages is superseded by this flow.)
- MinVer is referenced with `PrivateAssets="all"`, so it's build-time only and not a dependency of the
  shipped package.
