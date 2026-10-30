# MSBuild reference

## Items

| Item | Default | Metadata |
|---|---|---|
| `HuskComponent` | `wwwroot/**/*.svelte` | the Pare placement metadata (`PareServeAt`, `PareOutDir`, `PareEmbed`) |
| `HuskModule` | `wwwroot/**/*.svelte.ts`, `wwwroot/**/*.svelte.js` | same |
| `HuskPage` | `wwwroot/**/*.html` | none |

## Properties

| Property | Default | Meaning |
|---|---|---|
| `EnableHuskDefaultItems` | `true` | `false` removes the default globs; list the items yourself |
| `HuskDev` | `true` in Debug | Svelte's `dev` option |

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
