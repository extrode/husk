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
| [HUSK002](#husk002) | error (MSBuild) | Husk needs Extrode.Pare.Build |
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

The project references Extrode.Husk.Build but not Extrode.Pare.Build. NuGet imports a package's
build targets only into projects that reference it directly, and Husk's build runs inside Pare's,
so the project references both:

```xml
<PackageReference Include="Extrode.Pare.Build" Version="0.6.0" PrivateAssets="all" />
```

## HUSK100

Svelte rejected the source; its code and message follow (`expected_token`, `js_parse_error`). In
Development it is also logged when the compiler itself failed, with the exception. A `.svelte.ts`
that Pare cannot strip reports Pare's code instead (PARE100 and up, in Pare's error codes).

## HUSK101

The build gave a source outputs Husk does not write: a missing `.svelte.css` or `.js.map` item, or
an output path that is the source itself (a `.svelte.js` outside `wwwroot` with no `PareOutDir`;
give the item `PareOutDir`, or move it below `wwwroot`). In Development it means the app was built
by an older Husk than the one compiling; rebuild.

## HUSK102

A source, output, page, `husk.css` or `importmap.json` could not be read or written: the file is
locked, the disk is full, or the folder is read-only. The message carries the operating system's
reason. Outputs that failed are written again by the next build.

## HUSK200

A Svelte compiler warning, such as `a11y_missing_attribute` for an `<img>` without `alt`. It does
not fail the build; `<TreatWarningsAsErrors>` or `<WarningsAsErrors>HUSK200</WarningsAsErrors>`
makes it.
