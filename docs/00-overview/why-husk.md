# Why Husk

Svelte is a compiler. Normally you run it with Node, through Vite or a similar tool. That means a second toolchain on every laptop, every CI runner and every build server, next to your .NET one.

Husk runs the Svelte compiler inside the .NET build instead. It embeds Google's V8 JavaScript engine (through ClearScript), loads Svelte's own compiler into it, and compiles your components while MSBuild runs. The output is the same bytes Node would give you; the tests check that.

## What you get

- **One command.** `dotnet build`, `dotnet watch`, `dotnet publish`. That is the whole workflow.
- **No bundler.** Components become ES modules and the browser loads them directly. An import map tells it where `svelte` and your files live, with content hashes in the names so caches stay right.
- **TypeScript included.** `<script lang="ts">` works, and so do `.svelte.ts` rune modules. Pare strips the types.
- **Fast saves.** In Development the engines stay warm, so a saved component is recompiled in the running app and the page reloads.
- **Readable errors.** A bad component fails the build like a C# error: `App.svelte(4,3): error HUSK100: ...`.

## What it costs

- **An 82.6 MB package, once per machine.** It holds V8 for six platforms and Svelte. Nothing from it ships with your app.
- **No bundling.** Your app serves many small files. That is fine on HTTP/2 and HTTP/3, and Husk adds `modulepreload` links, but if you need one big file, Husk is not it.
- **No SvelteKit.** Husk compiles components. Routing, server rendering and `$app/*` modules are not part of it yet.
- **A build machine that V8 supports:** Windows, macOS or glibc Linux, x64 or arm64. Alpine and FreeBSD cannot build. The app you publish runs anywhere.

## Husk and Pare

Husk is a separate product built on [Pare](https://github.com/extrode/pare). Pare handles where files go, how they are served and published, and the TypeScript stripping. Husk adds the Svelte compile step. You reference both. [Husk in plain words](../02-guides/husk-in-plain-words.md) covers what you need to know.
