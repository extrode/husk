# MSBuild reference

## Items

| Item | Default | Metadata |
|---|---|---|
| `HuskComponent` | `wwwroot/**/*.svelte` | `PareServeAt` per item. `PareOutDir` and `PareEmbed` are Pare project properties, not item metadata; setting either means Husk serves no `husk.css`, import map, runtime or pages |
| `HuskModule` | `wwwroot/**/*.svelte.ts`, `wwwroot/**/*.svelte.js` | same |
| `HuskPage` | `wwwroot/**/*.html` | none |

## Properties

| Property | Default | Meaning |
|---|---|---|
| `EnableHuskDefaultItems` | `true` | `false` removes the default globs for components, modules and pages; list the items yourself, each with `PareServeAt` |
| `HuskDev` | `true` in Debug | Svelte's `dev` option |
| `HuskHashNames` | `false` in Debug, `true` otherwise | serve every module and runtime file as `name.<hash>.js`, point the import map at them and add `modulepreload` links to pages. In Development leave it `false`: the browser loads hashed copies that only a build writes, so a save would have no visible effect (the app logs a warning) |

## Packages

| Package | Role |
|---|---|
| `Extrode.Husk.Build` | build-time: compiler, V8 natives, vendored Svelte. Reference with `PrivateAssets="all"` |
| `Extrode.Husk.AspNetCore` | `AddHusk()`: Development recompiles and `dotnet watch` reloads. Does nothing outside Development |
| `Extrode.Pare.Build` | Pare's build, which Husk plugs into. Reference directly |

## Errors

Every code is listed in [Error codes](error-codes.md).

## License

ISL-P 1.2 with the ISL-OE 1.2 output exception: you may ship what Husk generates as part of your own app. See `LICENSE.md` and `NOTICE.md` in the repository.
