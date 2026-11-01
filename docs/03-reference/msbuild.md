# MSBuild reference

## Items

| Item | Default | Metadata |
|---|---|---|
| `HuskComponent` | `wwwroot/**/*.svelte` | `PareServeAt` per item. `PareOutDir` and `PareEmbed` are Pare project properties, not item metadata; setting either means Husk serves no `husk.css`, import map, runtime or pages |
| `HuskModule` | `wwwroot/**/*.svelte.ts`, `wwwroot/**/*.svelte.js` | same |
| `HuskPage` | `wwwroot/**/*.html` | `PareServeAt` is optional below `wwwroot` |
| `HuskRoutes` | none | The routes folder (`src/routes`). Its presence turns on the [router](../02-guides/router.md); nothing changes without it |
| `HuskShell` | none | The app's HTML shell (`src/app.html`), written to `index.html` with the import map, stylesheet and entry script filled in |
| `HuskAlias` | none | Include is the alias (`$lib`), `Path` the folder, optional `Entry` the file the bare alias means (`index.ts`) |
| `HuskImport` | none | Include is a bare specifier, `Url` where the browser finds it (`/vendor/x.js`) |
| `HuskEnv` | none | Include is an `import.meta.env` name, `Value` its value in this build |

## Properties

| Property | Default | Meaning |
|---|---|---|
| `EnableHuskDefaultItems` | `true` | `false` removes the default globs for components, modules and pages; list the items yourself; a source outside `wwwroot` needs `PareServeAt` |
| `HuskHooks` | none | The client hooks file (`src/hooks.client.ts`); its `init` runs before the root layout's module is imported |
| `HuskDev` | `true` in Debug | Svelte's `dev` option |
| `HuskHashNames` | `false` in Debug, `true` otherwise | serve every module and runtime file as `name.<hash>.js`, point the import map at them and add `modulepreload` links to pages. In Development leave it `false`: the browser loads hashed copies that only a build writes, so a save would have no visible effect (the app logs a warning) |

## Packages

| Package | Role |
|---|---|
| `Extrode.Husk.Build` | build-time: compiler, vendored Svelte; depends on Microsoft's `Microsoft.ClearScript.V8.Native.*` at 7.5.1.1 (source mapping must allow `Microsoft.ClearScript.*`). Reference with `PrivateAssets="all"` |
| `Extrode.Husk.AspNetCore` | `AddHusk()`: Development recompiles and `dotnet watch` reloads. Does nothing outside Development |
| `Extrode.Pare.Build` | Pare's build, which Husk plugs into. A dependency of `Extrode.Husk.Build`; a direct reference is optional (0.7.2 or later) |

## Errors

Every code is listed in [Error codes](error-codes.md).

## License

ISL-P 1.2 with the ISL-OE 1.2 output exception: you may ship what Husk generates as part of your own app. See `LICENSE.md` and `NOTICE.md` in the repository.
