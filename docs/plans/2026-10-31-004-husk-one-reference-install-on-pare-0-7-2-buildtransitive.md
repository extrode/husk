# Husk: one-reference install on Pare 0.7.2 (buildTransitive)

## Context
Pare 0.7.2 ships `Extrode.Pare.Build` with `buildTransitive/`. Husk.Build can depend on it, so an
app needs only `Extrode.Husk.Build` (+ `Extrode.Husk.AspNetCore`), not a third direct Pare.Build
reference. Steps come from `docs/handoffs/2026-10-31-pare-build-transitive.md`. Blocked until
`https://api.nuget.org/v3-flatcontainer/extrode.pare.build/index.json` lists 0.7.2 (Pare's
release run is in progress).

## Decision taken (default, say if you want otherwise)
**HUSK002 stays, reworded.** With the dependency it fires only when an app excludes Pare.Build's
build assets (`ExcludeAssets`/`IncludeAssets` without `buildtransitive`); dropping it would make
that case compile nothing, silently. New text: Pare's build targets were not imported; remove the
`ExcludeAssets` on Extrode.Pare.Build or Extrode.Husk.Build. An older direct Pare.Build than
0.7.2 becomes NU1605 (an error by default), which closes todo line 50 (Pare.Build 0.4.0 not rejected).

## Steps (branch `feat/one-reference-install`, one commit each)
1. `Directory.Packages.props`: `PareVersion` 0.7.2. AGENTS.md approved list: "0.7.2 from 2026-10-31".
2. `src/Extrode.Husk.Build/Extrode.Husk.Build.csproj`: add
   `<PackageReference Include="Extrode.Pare.Build" PrivateAssets="contentfiles;analyzers;build" />`;
   description drops "which the project references too". Build Husk.Build itself (Pare targets now
   import into it; must stay a no-op).
3. `build/Extrode.Husk.Build.targets:101-106`: HUSK002 text and comment as above.
4. Install surface: `samples/svelte-app/svelte-app.csproj` drops its Pare.Build line; README,
   `docs/01-getting-started/{README,first-app}.md`, `docs/02-guides/husk-in-plain-words.md`,
   `docs/03-reference/{msbuild,error-codes}.md`; regenerate `dist/docs-site`
   (`DOTNET_ROLL_FORWARD=Major bash scripts/build-docs.sh`). CHANGELOG under Unreleased. Todo
   lines 37 and 50 removed.
5. Tests (`tests/Extrode.Husk.Integration`):
   - `PackageTests` nuspec test: exactly one `Extrode.Pare.Build` dependency `[0.7.2, )` with
     `exclude="Build,Analyzers"`.
   - Fresh-restore test and the HUSK003 test: consumers with only Husk.Build + AspNetCore build.
   - New: a consumer that also keeps a direct `Extrode.Pare.Build` 0.7.2 still builds (one import,
     no MSB4011); one with a direct 0.7.0 fails with NU1605.
   - `Without_Pare_the_build_fails_with_HUSK002` becomes: Husk.Build with
     `ExcludeAssets="buildtransitive"` (Husk's own targets are under `build/`, so they still
     import; confirm in the test) fails with HUSK002.
   - Other literal `0.7.0` Pare refs in `PackageTests.cs`/`SamplePublishTests.cs` removed (or
     read from `PareVersion`). `TempProject`'s direct imports of `PareBuildDir` stay.
   - Break-check each new/changed test.
6. Stranger check: `scripts/pack-local.sh`, a new app in `tmp/` with only Extrode.Husk.Build from the
   local feed and a fresh `globalPackagesFolder`; `dotnet build` and `dotnet publish` produce the
   compiled `.svelte.js` and stripped `.ts`.

## Verification
Full suite (`dotnet test --solution Husk.slnx -c Release`), `laws-check.js --run`,
`structure-audit.js`, `/code-review low --fix` on the merge. Merge `--no-ff` into dev, unpushed.
A version bump/release is a separate step you call.
