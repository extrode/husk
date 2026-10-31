# Handoff: Pare.Build flows through Extrode.Husk.Build (next Pare release)

Date: 2026-10-31. From: Pare (`../pare`, branch `dev`, merge `4848315`). Answers Husk's
`work/todo.md` item "Pare: ship `build/` as `buildTransitive/` too" and N4 of Pare's
`docs/handoffs/2026-10-30-husk-needs-a-published-pare.md`.

## State

- **Pare 0.7.1 is on nuget.org** (`Extrode.Pare`, `.Build`, `.AspNetCore`, `.Cli`). Husk is on
  0.7.0 (`Directory.Packages.props`, `PareVersion`); 0.7.1 can be taken now, nothing in it needs
  a Husk change.
- **`buildTransitive` is on Pare's `dev`, not released.** The version is the user's call; it is
  not 0.7.1. Do the steps below only once it is on nuget.org.

## What Pare ships

`Extrode.Pare.Build` packs `buildTransitive/Extrode.Pare.Build.{props,targets}`, which import
`build/`. NuGet imports `buildTransitive/` into every project that gets the package, directly or
through another package's dependency. Proven in Pare's
`BuildIntegrationTests.A_project_that_gets_the_package_through_another_strips`: a wrapper package
references Pare.Build as below, an app references only the wrapper, and `a.ts` is stripped. With a
direct Pare.Build reference as well, the targets import once (no MSB4011).

## Husk to do, once that release is out

1. `Directory.Packages.props`: `PareVersion` to the new version.
2. `src/Extrode.Husk.Build/Extrode.Husk.Build.csproj`: add
   ```xml
   <PackageReference Include="Extrode.Pare.Build" PrivateAssets="contentfiles;analyzers;build" />
   ```
   `Extrode.Pare.Build` is a `developmentDependency`, so a plain reference defaults to
   `PrivateAssets="all"` and the nuspec gets no dependency. The value above writes the dependency
   and excludes only `build`; `buildTransitive` still flows. Check the packed nuspec: one
   `Extrode.Pare.Build` dependency with `exclude="Build,Analyzers"` and no `buildTransitive`.
3. Husk.Build's own build now imports Pare's targets too. It has no `.ts`, and since 2026-10-31
   (same release) Pare loads no task when `TypeScriptSource` is empty, so this should be a no-op.
   Confirm with a build of `Extrode.Husk.Build`.
4. The install becomes one reference (`Extrode.Husk.Build`), plus `Extrode.Husk.AspNetCore`.
   - `HUSK002`: with the dependency it fires only when an app excludes Pare.Build's assets. Drop
     it, or keep it with a message that names that case; the "add Extrode.Pare.Build" text goes.
   - A direct `Extrode.Pare.Build` reference must keep working (existing apps have one). An older
     direct version than Husk's dependency is NU1605 (downgrade), which also covers the todo item
     "HUSK002 does not reject Pare.Build 0.4.0".
   - Update `samples/svelte-app/svelte-app.csproj` (drop its Pare.Build reference), the package
     description ("which the project references too"), first-app.md and the install snippets.
   - `tests/Extrode.Husk.Integration`'s `ExcludeAssets="all"` reference is for locating the
     package and stays.
5. Prove it the way a stranger would: clean cache (`dotnet nuget locals all --clear`), a new app
   with only `Extrode.Husk.Build`, `dotnet build` and `dotnet publish` strip and compile.

## Also in that release (no Husk change needed)

- A `TypeScriptSource` declared twice takes the last declaration's metadata, so a project's
  `PareServeAt` beats a package's default glob; on Windows both separators name one source.
- The development watcher retries writing an output another process holds (a lost save before).
- `pare` CLI reads Windows device paths (`\\?\C:\src\*.ts`).

## Testing it before the release

Only if needed, not committed: `dotnet pack ../pare/src/Extrode.Pare.Build -c Release
-p:Version=0.0.0-local -o <husk>/tmp/pare-feed`, and point a scratch `nuget.config` at it.
