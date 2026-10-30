# husk

Svelte components for .NET projects without Node or Deno: the Svelte compiler runs in an
embedded V8 inside MSBuild, components compile to ES modules, and the `svelte` runtime is served
unbundled behind an import map. Built on [Pare](https://github.com/extrode/pare).

**Status: pre-release.** The packages build and pass their tests but are not on NuGet yet;
`scripts/pack-local.sh` packs them into `tmp/feed`.

## Install

Three packages, all three referenced directly by the app (NuGet imports build targets only for a
direct reference; without Pare the build stops with HUSK002):

```xml
<ItemGroup>
  <PackageReference Include="Extrode.Pare.Build" Version="0.6.0" PrivateAssets="all" />
  <PackageReference Include="Extrode.Husk.Build" Version="0.1.0" PrivateAssets="all" />
  <PackageReference Include="Extrode.Husk.AspNetCore" Version="0.1.0" />
</ItemGroup>
```

`Extrode.Husk.Build` holds the compiler, V8 for six platforms and the vendored Svelte 5.57.2: an
82.6 MB package, restored once per machine. It is a build-time package; nothing of it is
published. The build machine must be win, osx or glibc linux, x64 or arm64 (HUSK001 otherwise);
the published app runs anywhere.

## What it builds

| Item | Default | Output |
|---|---|---|
| `HuskComponent` | `wwwroot/**/*.svelte` | `<name>.svelte.js`, its `.map`, and its CSS joined into `/_husk/husk.css` |
| `HuskModule` | `wwwroot/**/*.svelte.ts`, `wwwroot/**/*.svelte.js` | `<name>.js` and its `.map`; types stripped by Pare first |
| `HuskPage` | `wwwroot/**/*.html` | the page, with the import map inlined at the marker |

Outputs are written under `obj/` and served as static web assets at the source's path, so
`wwwroot/App.svelte` imports as `/App.svelte.js`, or as `/App.svelte` through the import map. The
`svelte` runtime is served unbundled under `/_husk/`. Set `EnableHuskDefaultItems` to `false` to
list the items yourself; `HuskDev` (default: true in Debug) is Svelte's `dev` option.

A page opts into the import map with an empty marker, which the build fills:

```html
<link rel="stylesheet" href="/_husk/husk.css">
<script type="importmap" data-husk></script>
<script type="module" src="/main.js"></script>
```

## Development

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddHusk();
var app = builder.Build();
app.MapStaticAssets();
app.Run();
```

`AddHusk` does nothing outside Development. In Development it compiles a saved component in the
running app, with the engines kept warm, and `dotnet watch` reloads the page. A failed save is
logged and the last good output is kept.

Errors and warnings are listed in [docs/error-codes.md](docs/error-codes.md). Third-party
licenses ship in the build package, listed in
[src/Extrode.Husk.Build/NOTICE.md](src/Extrode.Husk.Build/NOTICE.md).

## Sample

```sh
scripts/pack-local.sh
dotnet watch --project samples/svelte-app
```

## License

Husk is licensed under the Islamic Software License - Permissive (ISL-P) 1.2, with the Output
Exception (ISL-OE) 1.2, the same terms as Pare: [LICENSE.md](LICENSE.md), [NOTICE.md](NOTICE.md).
The exception lets you ship what Husk generates (the compiled modules) as part of your own app.
These are source-available, ethically restricted licenses, not OSI-approved open source. Svelte,
ClearScript and V8 keep their MIT and BSD licenses.

## Layout

| Path | What |
|---|---|
| `src/` | `Extrode.Husk.Compiler` (V8 + Svelte), `Extrode.Husk.Build` (MSBuild), `Extrode.Husk.AspNetCore` (Development) |
| `tests/` | Unit tests, integration tests (real builds, browsers, `dotnet watch`), fixtures |
| `samples/` | `svelte-app`, restored from the local feed |
| `third-party/` | The vendored Svelte, pinned |
| `prototypes/` | Spikes, numbered; throwaway code with its own README |
| `work/` | Current work: tasklist, status window, backlog, milestones |
| `docs/` | Specs, plans, architecture, decisions, conventions, lessons |
| `scripts/` | Operational scripts. `scripts/cleanup/` is written but never run |

Agent instructions are in `AGENTS.md`. `CLAUDE.md` is a one-line shim that imports it.

## Status

See `work/status/current.md` for what is being worked on right now, and `CHANGELOG.md` for
what has shipped.

---

**Local READMEs.** Any directory whose purpose is not obvious from its name gets its own
`README.md`, kept to a few lines: what belongs here, what does not, and the naming rule if
there is one. The test is simple — if someone would have to open three files to work out what
a directory is for, it needs one.
