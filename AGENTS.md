# AGENTS.md — release and maintenance runbook

This file is the operating manual for any AI agent (or maintainer) working on
**SimpleXisoDrive**. Follow it exactly when preparing, building, tagging and deploying a release.
It is the source of truth for the release routine; `docs/Building.md` links here.

---

## 1. Repository at a glance

| Item | Value |
| --- | --- |
| Repository | `purelogiccode/SimpleXisoDrive` |
| Default branch | `master` |
| Windows app | `SimpleXisoDrive/SimpleXisoDrive.csproj` (`net10.0-windows`, Dokan) |
| Unix app | `SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj` (`net10.0`, FUSE 3 / macFUSE) |
| Shared core | `SimpleXisoDrive.Core/SimpleXisoDrive.Core.csproj` |
| FUSE library | External NuGet package **`LibFuseSharp`** (`net8.0;net9.0;net10.0`, MIT); source repo `C:\Users\HomePC\Dropbox\source\repos\CSharp_LibFuseSharp` |
| Tests | `SimpleXisoDrive.Tests` (Windows/core) and `SimpleXisoDrive.Unix.Tests` (Unix front end); library tests live in `CSharp_LibFuseSharp` |
| Docs source | `docs/` (published to GitHub Pages **and** the GitHub wiki) |
| Local bundles | `SimpleXisoDrive/bin/Release/release_<version>_<rid>.zip` |
| Solution | `CSharp_SimpleXisoDrive.sln` |
| .NET SDK | 10.0.x (`global.json` pins 10.0.0 with `rollForward: latestMajor`) |

Runtime identifiers: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

---

## 2. Golden rules

1. **Never delete anything in `SimpleXisoDrive/bin/Release/`.** It contains historical release
   bundles. Add new zips; never clean the folder.
2. **Bundles are framework-dependent.** Always publish with `--self-contained false
   -p:PublishSingleFile=true`. Do not embed the .NET runtime.
3. **Never change the release tag or asset naming scheme.** The update checker parses
   `release_<major>.<minor>.<patch>` and the workflow publishes
   `release_<version>_<rid>.zip`.
4. **Tag only after the version commit is pushed to `master`.**
5. **Do not edit wiki pages directly.** `docs/` is the source of truth; `wiki.yml` overwrites the
   wiki on every docs change.
6. **Keep both side menus in sync** when adding/renaming a docs page:
   `docs/_Sidebar.md` (wiki) and `docs/_data/navigation.yml` (Pages).
7. **Run InspectCode and the full test suite before producing bundles.** No warnings or errors.
8. **Bump every app project together**: Windows, Unix, Core and both test projects.
9. **The FUSE library is a separate package.** Source and releases live in
   `C:\Users\HomePC\Dropbox\source\repos\CSharp_LibFuseSharp`; publish `LibFuseSharp` there, then
   update the pinned `PackageReference` in this repo. Never re-add a vendored `FuseSharp/` folder.
10. **Never reuse a NuGet version.** `LibFuseSharp 1.0.0` and `1.0.1` are already published; bump
    before pushing again.
11. **Do not force-push or delete `master`.** The branch ruleset blocks both.

---

## 3. Release pipeline (what CI does)

```
version bump + docs commit ──push──> master
        │
        ├─ ci.yml      build + test (Windows), Unix publish, Jekyll docs build
        ├─ pages.yml   deploy docs/ to GitHub Pages        (docs/** changes)
        └─ wiki.yml    mirror docs/ to the GitHub wiki     (docs/** changes)

git tag release_X.Y.Z ──push──> release.yml
        ├─ verify       tag matches <AssemblyVersion>; build + tests
        ├─ package      6 zips: win/linux/osx × x64/arm64
        └─ github-release (tags only)
                         creates GitHub release with the 6 zips
```

Key points:

- `release.yml` runs on tags matching `release_*` and on manual `workflow_dispatch`
  (version input).
- The `verify` job reads `<AssemblyVersion>` from
  `SimpleXisoDrive/SimpleXisoDrive.csproj`; the tag must match exactly.
- Each zip contains the single-file executable **plus** `ReadMe.md`, `WhatsNew.md` and
  `LICENSE.txt`.
- On Linux/macOS zips the executable bit is set by the workflow.
- The GitHub release body is built from the matching `# What's New in X.Y.Z` section in
  `WhatsNew.md`.

---

## 4. Pre-flight

```powershell
git status --porcelain
git fetch origin
git rev-list --left-right --count HEAD...origin/master   # expect: 0  0
dotnet --version
```

The working tree must be clean (or contain only intentional changes) and `master` must be up to
date. Run every command from the repository root.

---

## 5. Step 1 — review and test

```powershell
dotnet build CSharp_SimpleXisoDrive.sln --configuration Release
dotnet test  CSharp_SimpleXisoDrive.sln --configuration Release
jb inspectcode "C:\Users\HomePC\Dropbox\source\repos\CSharp_SimpleXisoDrive\CSharp_SimpleXisoDrive.sln" -o="C:\Users\HomePC\Dropbox\source\repos\CSharp_SimpleXisoDrive\InspectCode.log"
```

Requirements:

- Build: **0 warnings, 0 errors**.
- Tests: all pass. Current app suite: **429** (395 core + 34 Unix). If the library changed, its
  **55** tests must pass too. Record the counts; `docs/Testing.md`, `WhatsNew.md` and
  `docs/Release-History.md` must match.
- InspectCode: **no `warning` or `error` results**. Notes/suggestions may remain only if they are
  deliberate (false positives or intentional public API).
- Review `git log <previous-tag>..HEAD` and fix anything the review exposes before releasing.

To inspect the SARIF report:

```powershell
$sarif = Get-Content InspectCode.log -Raw | ConvertFrom-Json
$sarif.runs[0].results | Where-Object level -in 'warning','error' |
    Select-Object ruleId, level, @{n='file';e={$_.locations[0].physicalLocation.artifactLocation.uri}},
                  @{n='line';e={$_.locations[0].physicalLocation.region.startLine}}, @{n='msg';e={$_.message.text}}
```

---

## 6. Step 2 — choose the version

Semantic versioning (`major.minor.patch`):

- **Patch** (`1.5.1`): bug fixes, dependency updates, docs/infra only.
- **Minor** (`1.6.0`): new user-visible functionality.
- **Major**: breaking changes.

The tag is `release_<version>`; it must be three numeric parts because the update checker uses
`\d+\.\d+\.\d+` on the GitHub `tag_name`.

> The FUSE library is versioned separately in `CSharp_LibFuseSharp` and consumed as the
> `LibFuseSharp` NuGet package. The app pin is in
> `SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj`; the current pin is `1.0.1`.
> See section 11b for the library release routine.

---

## 7. Step 3 — bump the app version

Update `<AssemblyVersion>` and `<FileVersion>` in **all five** files to the same value:

1. `SimpleXisoDrive/SimpleXisoDrive.csproj`
2. `SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj`
3. `SimpleXisoDrive.Core/SimpleXisoDrive.Core.csproj`
4. `SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj`
5. `SimpleXisoDrive.Unix.Tests/SimpleXisoDrive.Unix.Tests.csproj`

Quick check:

```powershell
Select-String -Path */*.csproj -Pattern '<(AssemblyVersion|FileVersion)>'
```

The Windows `AssemblyVersion` is the one enforced by `release.yml`.

---

## 8. Step 4 — update the documentation

### 8.1 `WhatsNew.md`

Add a new section at the **top**, exactly in this form (the release workflow searches for it):

```markdown
# What's New in 1.5.1 (since 1.5.0)

Prepared for the `release_1.5.1` tag. Framework-dependent ... require the .NET 10.0 Runtime ...

## <group>

- **Change.** Description.
```

The heading text must match `# What's New in <version>` exactly (the version has no `release_`
prefix).

### 8.2 `ReadMe.md`

- Keep the badge block directly under the H1 at the top of the file.
- Add/update the release highlights link and the docs list if pages were added.
- Keep the **Security** and **Releasing** sections current.

### 8.3 `docs/`

Update at minimum:

| File | What to update |
| --- | --- |
| `docs/Release-History.md` | Add the new version row and section; move the previous version out of "Unreleased". |
| `docs/Home.md` | "Current version" row. |
| `docs/Building.md` | Current version, tag examples, bundle contents, analyzer versions, target frameworks. |
| `docs/Testing.md` | Test count and runner version. |
| `docs/Contributing.md` | "currently X.Y.Z" in the bug-report list. |
| `docs/Privacy-and-Networking.md` | Example `version` payloads. |
| `docs/FuseSharp.md` | Library package version/targets/API when the library changes. |
| `docs/_Sidebar.md` | Wiki side menu: add/rename pages here. |
| `docs/_data/navigation.yml` | Pages side menu: keep identical structure to `_Sidebar.md`. |

Both menus are required: the wiki renders `_Sidebar.md`; the Pages layout renders
`_data/navigation.yml`. Never let them diverge.

### 8.4 Badges

Every `README.md` / `ReadMe.md` must start with the project title followed by a badge block:

- `ReadMe.md` — CI, Release, latest release, downloads, stars, issues, last commit, license,
  .NET, Windows, Linux, macOS.
- `CSharp_LibFuseSharp/LibFuseSharp/README.md` — NuGet, MIT, .NET 8/9/10 (separate repo).

Maintainers may add badges, but do not remove existing ones.

---

## 9. Step 5 — produce local bundles (do not delete old ones)

Publish all six runtimes to a staging folder, then zip into
`SimpleXisoDrive/bin/Release/`. The script below mirrors `release.yml`, including the Unix
executable bit. It reads the version from the Windows project so it cannot drift.

```powershell
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\HomePC\Dropbox\source\repos\CSharp_SimpleXisoDrive'
Set-Location $root

[xml]$project = Get-Content -LiteralPath 'SimpleXisoDrive/SimpleXisoDrive.csproj'
$version = ($project.Project.PropertyGroup.AssemblyVersion | Select-Object -First 1).Trim()

$targets = @(
    @{ Rid = 'win-x64';     Project = 'SimpleXisoDrive/SimpleXisoDrive.csproj';           Binary = 'SimpleXisoDrive.exe' },
    @{ Rid = 'win-arm64';   Project = 'SimpleXisoDrive/SimpleXisoDrive.csproj';           Binary = 'SimpleXisoDrive.exe' },
    @{ Rid = 'linux-x64';   Project = 'SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj'; Binary = 'SimpleXisoDrive' },
    @{ Rid = 'linux-arm64'; Project = 'SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj'; Binary = 'SimpleXisoDrive' },
    @{ Rid = 'osx-x64';     Project = 'SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj'; Binary = 'SimpleXisoDrive' },
    @{ Rid = 'osx-arm64';   Project = 'SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj'; Binary = 'SimpleXisoDrive' }
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($target in $targets) {
    $publish = "artifacts/release/$version/$($target.Rid)"
    $bundle  = "artifacts/bundle/$version/$($target.Rid)"
    $zip     = "SimpleXisoDrive/bin/Release/release_${version}_$($target.Rid).zip"

    dotnet publish $target.Project --configuration Release --runtime $target.Rid `
        --self-contained false -p:PublishSingleFile=true --output $publish

    Remove-Item -LiteralPath $bundle -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $bundle -Force | Out-Null
    Copy-Item -LiteralPath "$publish/$($target.Binary)" -Destination $bundle
    Copy-Item -LiteralPath ReadMe.md, WhatsNew.md, LICENSE.txt -Destination $bundle

    # Only overwrite this version's zip; never clear the release folder.
    Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
    $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $bundle -File) {
            $entry = $archive.CreateEntry($file.Name, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $file.LastWriteTime
            if ($target.Rid -like 'linux-*' -or $target.Rid -like 'osx-*') {
                # 0o100755 for the executable, 0o100644 for the documentation.
                $entry.ExternalAttributes =
                    if ($file.Name -eq $target.Binary) { 0x81ED0000 } else { 0x81A40000 }
            }
            $source = [System.IO.File]::OpenRead($file.FullName)
            try {
                $targetStream = $entry.Open()
                try { $source.CopyTo($targetStream) } finally { $targetStream.Dispose() }
            } finally { $source.Dispose() }
        }
    } finally { $archive.Dispose() }

    Write-Host "Created $zip"
}
```

Verify every bundle:

```powershell
$version = '1.5.1'
foreach ($rid in 'win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64') {
    $zip = "SimpleXisoDrive/bin/Release/release_${version}_$rid.zip"
    $names = tar -tf $zip
    if ($names.Count -ne 4) { throw "$zip has $($names.Count) entries, expected 4" }
    foreach ($required in 'ReadMe.md','WhatsNew.md','LICENSE.txt') {
        if ($names -notcontains $required) { throw "$zip is missing $required" }
    }
    $binary = if ($rid -like 'win-*') { 'SimpleXisoDrive.exe' } else { 'SimpleXisoDrive' }
    if ($names -notcontains $binary) { throw "$zip is missing $binary" }
    Write-Host "OK $zip"
}
```

Expected per zip: exactly **one executable + `ReadMe.md` + `WhatsNew.md` + `LICENSE.txt`**.
The executable must be a single file and must not contain the .NET runtime
(`--self-contained false`).

---

## 10. Step 6 — commit and push

```powershell
git add -A
git commit -m "release: prepare 1.5.1"
git push origin master
```

Wait for `ci.yml` on that commit to pass before tagging:

```powershell
$sha = git rev-parse HEAD
gh run list --workflow ci.yml --commit $sha --limit 1
gh run watch (gh run list --workflow ci.yml --commit $sha --json databaseId --jq '.[0].databaseId') --exit-status
```

If CI fails, fix and push again; do not tag a red commit.

---

## 11. Step 7 — tag and deploy (CI)

```powershell
$version = '1.5.1'
git tag "release_$version"
git push origin "release_$version"
```

Then monitor the release workflow:

```powershell
$run = gh run list --workflow release.yml --limit 1 --json databaseId --jq '.[0].databaseId'
gh run watch $run --exit-status
```

The workflow:

1. **verify** — validates `release_<version>` against `<AssemblyVersion>`, builds and tests.
2. **package** — publishes and zips all six RIDs on their native OSes.
3. **github-release** — creates the release with the six zips and the `WhatsNew.md` body.

Manual packaging without publishing (for inspection only):

```powershell
gh workflow run release.yml -f version=1.5.1
```

A manual run uploads the six zips as workflow artifacts and **does not** create a GitHub release.

---

## 11b. Library release (`LibFuseSharp`) — separate repository

The FUSE library is **not** in this repository. It lives in
`C:\Users\HomePC\Dropbox\source\repos\CSharp_LibFuseSharp` and is published as the NuGet package
**`LibFuseSharp`** (namespace `FuseSharp`, MIT). Do this before an app release that needs a library
change:

```powershell
Set-Location C:\Users\HomePC\Dropbox\source\repos\CSharp_LibFuseSharp
```

1. Bump `<Version>`, `<AssemblyVersion>` and `<FileVersion>` in
   `LibFuseSharp/LibFuseSharp.csproj` and `LibFuseSharp.Tests/LibFuseSharp.Tests.csproj`.
   Update `LibFuseSharp/README.md` for behavior or API changes.
2. Build, test and inspect:

   ```powershell
   dotnet build LibFuseSharp.slnx -c Release
   dotnet test LibFuseSharp.Tests/LibFuseSharp.Tests.csproj -c Release
   jb inspectcode "C:\Users\HomePC\Dropbox\source\repos\CSharp_LibFuseSharp\LibFuseSharp\LibFuseSharp.csproj" -o="C:\Users\HomePC\Dropbox\source\repos\CSharp_LibFuseSharp\InspectCode-lib.log"
   ```

   This InspectCode build does not accept `.slnx`; inspect the project files individually.
   Requirements: 0 warnings, 0 errors, all tests pass.

3. Pack and inspect the package:

   ```powershell
   dotnet pack LibFuseSharp/LibFuseSharp.csproj -c Release -o artifacts/nuget
   ```

   Confirm `lib/net8.0`, `lib/net9.0`, `lib/net10.0`, `README.md`, package id `LibFuseSharp`,
   MIT license, and the new version in the `.nuspec`.

4. **Compatibility check (required when the public API changed).** In a clean copy of
   SimpleXisoDrive, replace the `LibFuseSharp` `PackageReference` version with the new one, add a
   temporary `NuGet.Config` whose first source is `artifacts/nuget`, then run:

   ```powershell
   dotnet restore
   dotnet build CSharp_SimpleXisoDrive.sln -c Release
   dotnet test CSharp_SimpleXisoDrive.sln -c Release
   ```

   Only publish after the app builds and all tests pass against the packed package.

5. Publish (never print or commit the key):

   ```powershell
   dotnet nuget push "artifacts/nuget/LibFuseSharp.X.Y.Z.nupkg" `
       --api-key $env:NUGET_API_KEY `
       --source https://api.nuget.org/v3/index.json
   ```

6. Wait for indexing and verify:

   ```powershell
   (Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/libfusesharp/index.json').versions
   ```

7. Commit, tag and push the library repo:

   ```powershell
   git add -A
   git commit -m "release: LibFuseSharp X.Y.Z"
   git tag vX.Y.Z
   git push origin master
   git push origin vX.Y.Z
   ```

8. Update the app pin in both files, then restore/build/test and commit:

   - `SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj`
   - `SimpleXisoDrive.Unix.Tests/SimpleXisoDrive.Unix.Tests.csproj`

```powershell
dotnet restore CSharp_SimpleXisoDrive.sln
dotnet build CSharp_SimpleXisoDrive.sln -c Release
dotnet test CSharp_SimpleXisoDrive.sln -c Release
```

Never reuse a published version. Current `LibFuseSharp` release: **1.0.1**. The namespace stays
`FuseSharp`; only the package/assembly name is `LibFuseSharp`.

---

## 12. Step 8 — post-release verification

```powershell
gh release view "release_1.5.1" --json tagName,name,assets `
    --jq '{tag:.tagName, name:.name, assets:[.assets[].name] | sort}'
```

Required result:

- tag is `release_1.5.1`;
- exactly six assets:
  `release_1.5.1_linux-arm64.zip`, `release_1.5.1_linux-x64.zip`,
  `release_1.5.1_osx-arm64.zip`, `release_1.5.1_osx-x64.zip`,
  `release_1.5.1_win-arm64.zip`, `release_1.5.1_win-x64.zip`.

Also confirm:

- `ci.yml`, `pages.yml` and `wiki.yml` succeeded for the release commit.
- <https://purelogiccode.github.io/SimpleXisoDrive/> shows the new version and both side menus.
- The wiki Home and `_Sidebar.md` were refreshed (do not edit them manually).
- `SECURITY.md` is detected (Security tab → policy).
- No new Dependabot, CodeQL or secret-scanning alerts.
- The update checker will parse `release_1.5.1` as `1.5.1`. To test locally, run an older 1.5.0
  build on an interactive console: it should offer `1.5.1`.

---

## 13. Failure recovery

### Version mismatch in `verify`

`release.yml` failed with "Version mismatch". The tag and `<AssemblyVersion>` differ.

```powershell
git tag -d release_1.5.1
git push origin :refs/tags/release_1.5.1
# fix the csproj versions, commit, push, then re-tag
```

### Package job failed

Fix the cause, then re-run only the failed jobs:

```powershell
gh run rerun <run-id> --failed
```

The release job is skipped unless the run is for a tag, so a failed package job cannot leave a
partial release.

### Release created with wrong assets

Do not silently replace assets. Confirm with the maintainer, then:

```powershell
gh release delete release_1.5.1 --yes
git push origin :refs/tags/release_1.5.1
git tag release_1.5.1
git push origin release_1.5.1
```

### Wiki or Pages out of date

- **Pages:** re-run `pages.yml` (Actions → Pages → Run workflow) or push any `docs/` change.
- **Wiki:** re-run `wiki.yml`. If the wiki repository was never initialized, open the Wiki tab
  and create the first page once.
- Never edit wiki pages in the browser; the next sync overwrites them.

---

## 14. Security settings (already configured)

Keep these enabled in **Settings → Code security and analysis**:

- Dependabot alerts and Dependabot security updates.
- CodeQL default setup (`csharp` + `actions`, weekly).
- Secret scanning and push protection.
- Private vulnerability reporting.
- Branch ruleset **Protect default branch** (blocks deletion and force-pushes; normal pushes
  remain allowed).

`secret_scanning_non_provider_patterns` and `secret_scanning_validity_checks` require a paid
GitHub Secret Protection license on this organization; they are intentionally disabled on the free
plan.

---

## 15. File map for release work

| Path | Purpose |
| --- | --- |
| `.github/workflows/ci.yml` | Build, tests, Unix publish, Jekyll docs build. |
| `.github/workflows/release.yml` | Tag-driven packaging and GitHub release. |
| `.github/workflows/pages.yml` | Deploy `docs/` to GitHub Pages. |
| `.github/workflows/wiki.yml` | Mirror `docs/` into the wiki. |
| `.github/sync-wiki.ps1` | Wiki regeneration script used by `wiki.yml`. |
| `WhatsNew.md` | Release notes source; the release body is extracted from it. |
| `ReadMe.md` | Main badges, quick start, docs index, security and release links. |
| `SECURITY.md` | Vulnerability disclosure policy. |
| `docs/_Sidebar.md` | Wiki side menu. |
| `docs/_data/navigation.yml` | Pages side menu. |
| `docs/_layouts/default.html` | Pages layout and inline link rewriter. |
| `docs/_config.yml` | Jekyll configuration and defaults. |
| `SimpleXisoDrive/bin/Release/` | Local release bundles (never delete). |
| `C:\Users\HomePC\Dropbox\source\repos\CSharp_LibFuseSharp` | Source, tests and NuGet package for `LibFuseSharp` (MIT); not vendored here. |

---

## 16. Definition of done

- [ ] `git status` clean; `master` pushed.
- [ ] Build: 0 warnings, 0 errors.
- [ ] All app tests pass (currently **429**: 395 core + 34 Unix); if the library changed, its
      **55** tests pass too.
- [ ] InspectCode: no warnings or errors (app and, when changed, the library).
- [ ] Version bumped in all five app projects.
- [ ] The `LibFuseSharp` package version is pinned in both Unix projects and matches the published
      NuGet version.
- [ ] `WhatsNew.md` has the matching `# What's New in X.Y.Z` section.
- [ ] `ReadMe.md` badges intact; docs lists current.
- [ ] `docs/Release-History.md`, `Home.md`, `Building.md`, `Testing.md`, `Contributing.md`,
      `Privacy-and-Networking.md` updated.
- [ ] `_Sidebar.md` and `_data/navigation.yml` in sync.
- [ ] Six local zips exist in `SimpleXisoDrive/bin/Release/`, each with 4 entries.
- [ ] `ci.yml` green on the release commit.
- [ ] Tag `release_X.Y.Z` pushed; `release.yml` green.
- [ ] GitHub release has exactly six assets.
- [ ] Pages and wiki reflect the new docs; update checker sees `X.Y.Z`.
