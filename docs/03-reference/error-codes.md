# Error codes

Husk reports through MSBuild in a build and through the app's log in Development. Both print the
same shape: `file(line,col): error HUSK100: code: message`, with Svelte's line and a 1-based
column. A failed source gets no new output: the build fails and keeps the previous files, and the
Development app keeps serving the last good ones.

Svelte's own codes (`a11y_missing_attribute`, `expected_token`) come after the Husk code; they are
listed in [Svelte's docs](https://svelte.dev/docs/svelte/compiler-errors).

| Code | Kind | Message |
|---|---|---|
| [HUSK001](#husk001) | error (MSBuild) | Husk cannot build on this platform |
| [HUSK002](#husk002) | error (MSBuild) | Pare's build targets were not imported |
| [HUSK003](#husk003) | error (MSBuild) | V8's native library for this machine was not restored |
| [HUSK004](#husk004) | error (MSBuild) | this build process already loaded a different V8 library |
| [HUSK100](#husk100) | error | Svelte could not compile the source |
| [HUSK101](#husk101) | error | the outputs the build gave do not match the source |
| [HUSK102](#husk102) | error | a file cannot be read or written |
| [HUSK200](#husk200) | warning | a Svelte compiler warning |

## HUSK001

The build machine is not one of the six V8 ships for: win-x64, win-arm64, osx-x64, osx-arm64,
linux-x64 and linux-arm64 (glibc). A musl Linux (Alpine), FreeBSD and the like fail here, before
anything compiles. Build on a supported machine; the output is plain JavaScript and CSS and runs
anywhere.

## HUSK002

Husk's build runs inside Pare's. Extrode.Husk.Build depends on Extrode.Pare.Build, whose
`buildTransitive/` targets NuGet imports into the project. HUSK002 means they were not imported:
a direct Extrode.Pare.Build reference excludes its build assets (`ExcludeAssets="build;buildtransitive"`,
`"all"`, or an `IncludeAssets` without them). A direct reference decides for the package, so
Husk's dependency does not bring them back. Remove that `ExcludeAssets`, or the reference itself.

## HUSK003

Extrode.Husk.Build carries no V8 library itself. It depends on Microsoft's
`Microsoft.ClearScript.V8.Native.<rid>` packages at exactly 7.5.1.1, and the build looks for the one
for this machine in the NuGet cache. The file is missing when the project was not restored, when a
package source mapping does not allow `Microsoft.ClearScript.*`, or when the app pins another
version of a `Microsoft.ClearScript.V8.Native.*` package (a lower pin is NU1605; a higher one means
7.5.1.1 is never downloaded). Restore the project, allow the packages, or remove the pin.

## HUSK004

`dotnet build` keeps its build processes running for a few minutes and reuses them, for any
project you build, in any folder. Each Husk loads V8's native library from the NuGet cache. Husk
versions that use the same ClearScript version find the same file and share it. Two different files
in one process break V8 (the build spins, then dies), so Husk stops instead. It happens when the
process already compiled with a Husk on another ClearScript version, or with a Husk outside the
NuGet cache (`HuskTaskAssembly`). Run `dotnet build-server shutdown` and build again; in a
solution, give every project the same Extrode.Husk.Build version.

## HUSK100

Svelte rejected the source; its code and message follow (`expected_token`, `js_parse_error`). In
Development it is also logged when the compiler itself failed, with the exception. A `.svelte.ts`
that Pare cannot strip is logged as HUSK100 with Pare's code as the message (`PARE100` and up for
syntax it cannot erase, `PARE001` for a syntax error; see Pare's error codes). Search for the
`HUSK100` code, not `PARE1xx`.

## HUSK101

The build gave a source outputs Husk does not write: a missing `.svelte.css` or `.js.map` item, or
an output path that is the source itself (a `.svelte.js` outside `wwwroot` with no `PareOutDir`
property; set the `PareOutDir` project property, or move it below `wwwroot`). In Development it means the app was built
by an older Husk than the one compiling; rebuild.

## HUSK102

A source, output, page, `husk.css` or `importmap.json` could not be read or written: the file is
locked, the disk is full, or the folder is read-only. The message carries the operating system's
reason. Outputs that failed are written again by the next build.

## HUSK200

A Svelte compiler warning, such as `a11y_missing_attribute` for an `<img>` without `alt`. It does
not fail the build; `<TreatWarningsAsErrors>` or `<WarningsAsErrors>HUSK200</WarningsAsErrors>`
makes it. Then, as with HUSK100, that source's outputs are left as they were, so the next build
fails on it again instead of finding it up to date.
