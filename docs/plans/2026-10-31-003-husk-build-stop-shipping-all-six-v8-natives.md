# Husk.Build: stop shipping all six V8 natives

## Context
`Extrode.Husk.Build` is 85.2 MB (preview.1) and 260 MB on disk per Husk version in the NuGet
folder, because `tasks/net10.0/runtimes/` carries six ClearScript V8 natives (76.8 MB packed) plus
ICU (4.6 MB). A build machine needs one. Design source: slice F of audit 2 (`tmp/audit2/f.out`).
This plan was checked by an Opus audit worker against the code; its corrections are folded in.

## Package IDs for approval
**No new Extrode package IDs.** The six IDs below are already on the approved list in `AGENTS.md:90-93`
(spec 001: "ClearScript V8 7.5.1.1 with its six native packages"), version 7.5.1.1, Microsoft's:

- `Microsoft.ClearScript.V8.Native.win-x64`
- `Microsoft.ClearScript.V8.Native.win-arm64`
- `Microsoft.ClearScript.V8.Native.linux-x64`
- `Microsoft.ClearScript.V8.Native.linux-arm64`
- `Microsoft.ClearScript.V8.Native.osx-x64`
- `Microsoft.ClearScript.V8.Native.osx-arm64`

**Scope question for you:** that approval covered them as a private dependency bundled inside our
package. Under this plan they become nuspec dependencies, so they are restored into every user's
project graph and can conflict with an app's own ClearScript pins (see Risks). The IDs are the
same; the exposure is new. Option D (six `Extrode.Husk.Build.<rid>` IDs) would be new IDs and is
rejected: it puts platform conditions in every user's csproj.

## Approach: two stages
**Stage 1 (this plan):** Husk.Build contains no natives and depends on Microsoft's six with
`exclude="All"`. Husk.Build drops from 85 MB to about 6 MB per version; the 77 MB of natives is
downloaded once per ClearScript version and shared across Husk upgrades. No install change.
First install still downloads all six (same first-download size as today).

**Stage 2 (not in this plan, separate approval):** Husk.Build as an MSBuild project SDK so a first
restore downloads only the host native (option A2). Changes README, first-app, sample csproj and
test consumers, rests on unverified resolver behaviour.

Rejected: A1 (PackageDownload in package props; first build on a clean checkout fails), A3
(restore inside build), C (`runtime.json`; wrong native for `publish -r` elsewhere), E.

## Verified by the audit (experiment, osx-arm64)
ClearScript.V8 7.5.1.1 loads its native through `NativeLibrary.Load` over a list of folders. A
`NativeLibrary.SetDllImportResolver` that returns the cache-folder native is enough; the resolver
runs before the load context and before ClearScript's own fallback. With nothing found the failure
is `TypeLoadException("Cannot load ClearScript V8 library")`, not `DllNotFoundException`.
Not verified on Linux, Windows or the Visual Studio task host.

## Steps (stage 1)
1. **NativeProbe** (`src/Extrode.Husk.Compiler/NativeProbe.cs:44-48`): after the existing lookup next
   to `ClearScript.V8.dll`, look in the sibling NuGet package folder derived from that assembly's own
   path: `<cache>/<id>/<ver>/tasks/net10.0/` goes up four levels to `<cache>`, then
   `microsoft.clearscript.v8.native.<rid>/<ClearScript version>/runtimes/<rid>/native/`. No
   AppContext and no new parameter: it works the same in the build task and the Development app, so
   an older AspNetCore with a newer Build still finds the native (an AppContext hand-off would
   not, and is process-wide in a reused MSBuild node). Rid comes from the process
   (`RuntimeInformation`), so an x64 app under Rosetta picks osx-x64, which is present because
   stage 1 restores all six. Fix the stale comment at `NativeProbe.cs:8-9`.
2. **Targets** (`build/Extrode.Husk.Build.targets`): `_HuskNativeRid=$(NETCoreSdkPortableRuntimeIdentifier)`
   (confirmed defined in SDK 10.0.403; not `NETCoreSdkRuntimeIdentifier`, which can be a distro
   name). New error after HUSK001 when
   `$(NuGetPackageRoot)microsoft.clearscript.v8.native.<rid>/<version>/runtimes/<rid>/native/<file>`
   is missing, skipped when `HuskTaskAssembly` points at a local bin (the repo's own tests). The
   error text names the cause: the native package was not restored, or the app pins a different
   `Microsoft.ClearScript.V8.Native.*` version.
3. **HuskSession / CompilerBridge** (`HuskSession.cs:27,39`, `CompilerBridge.cs:21`): V8 starts on
   the first compile, not in `Load`, so the native check goes in `Load`/`HuskSession`, before the
   first save: when no native exists for the process RID, log one warning and turn compile-on-save
   off (as the ClearScript version-mismatch path does). Catch `TypeLoadException` as well as
   `DllNotFoundException`. Limit, stated in the docs: an AspNetCore older than this change cannot
   degrade; with a newer Build it logs HUSK100 on each save (`HuskCompiler.cs:33` catches all).
4. **Pack** (`Extrode.Husk.Build.csproj:13,54-58`): leave `runtimes/**` out of `_HuskCompilerFile`;
   rewrite the packed `Extrode.Husk.Compiler.deps.json` without the native entries; replace the
   osx-arm64 existence check (`:57-58`) with "no native packed"; replace
   `SuppressDependenciesWhenPacking` (`:13`) with the six dependencies as exact `[7.5.1.1]`
   ranges, `exclude="All"` (the other refs are `PrivateAssets="all"`, so nothing else leaks in).
   Pick the mechanism first, with a spike: `PackageReference ... IncludeAssets="none"` vs a nuspec
   fragment; confirm which one emits `exclude="All"`. Add a pack-time check that the ClearScript
   version agrees in the props literal, nuspec and CPM.
5. **Docs:** `README.md`, `docs/release.md`, `msbuild.md`, constitution: size, the dependency note,
   source mapping must allow `Microsoft.ClearScript.*`, the version-pin conflict. CHANGELOG entry.

## Tests
- `PackageTests.The_build_package_carries_the_compiler_natives...` (`PackageTests.cs:138-153`):
  assert no `tasks/net10.0/runtimes/` entries, no native in deps.json, the exact nuspec XML
  (six `[7.5.1.1]` dependencies with `exclude="All"`).
- `A_fresh_project_restores_both_from_the_feed...` (`PackageTests.cs:192`): the private `packages/`
  folder must *contain* `microsoft.clearscript.v8.native.<host rid>/7.5.1.1/runtimes/<rid>/native/`
  (it holds all six in stage 1; do not assert "only"), and bin holds no ClearScript.
- `AssetsBuildTests.A_supported_build_machine_passes_the_gate` (`:109-117`): it loads the native
  through the fallback (test bin has `runtimes/`); add a variant with the task copied beside a
  fake cache layout so the derived-folder path is exercised. HUSK001 stays the first error.
- New: an app pinning a different native version fails with the new error text (above) or NU1605.
- `NativeProbeTests`: next-to-assembly, derived folder, neither found.
- New: cross-platform publish (`-r linux-x64` on macOS, `-r osx-arm64` on Linux): builds, no
  ClearScript in output.
- `HuskDevelopmentTests`: no native for the process, so one warning and no HUSK100 per save.
- Full suite, `laws-check.js --run` (L001 byte-identity must hold), `structure-audit.js`;
  break-check each new test.

## Risks
- **Version conflicts:** an app pinning a lower native version gets NU1605 (an error in C#
  projects); a higher one makes the 7.5.1.1 folder never download and the new missing-native
  error fires. Today both build. Documented, tested, not solved.
- Dependency groups are `net10.0` only: a Build-only consumer below net10.0 gets no native.
- Source mapping in users' `nuget.config` must allow `Microsoft.ClearScript.*`.
- Windows, Linux and the Visual Studio task host are untested here (already in todo).
- A Husk.Build installed into a non-standard layout (`HuskTaskAssembly` copied elsewhere) falls
  back to next-to-assembly and the targets error; the derived-folder lookup is a convenience.

## Flow
Standard tier. Branch `feat/husk-build-no-bundled-natives` off `dev`, one commit per step, merged
`--no-ff`, unpushed. No tag, no release, no push.
