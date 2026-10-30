# What the build produces

Husk reads three kinds of item and writes under `obj/`. The outputs are served as static web assets at the source's path, so `wwwroot/App.svelte` is imported as `/App.svelte.js`, or as `/App.svelte` through the import map.

| Item | Default glob | Output |
|---|---|---|
| `HuskComponent` | `wwwroot/**/*.svelte` | `<name>.svelte.js`, its `.map`, and its CSS joined into `/_husk/husk.css` |
| `HuskModule` | `wwwroot/**/*.svelte.ts`, `wwwroot/**/*.svelte.js` | `<name>.js` and its `.map`; types stripped by Pare first |
| `HuskPage` | `wwwroot/**/*.html` | the page, with the import map inlined at the marker |

The `svelte` runtime is served unbundled under `/_husk/`.

## Import map

A page opts in with an empty marker, which the build fills:

```html
<link rel="stylesheet" href="/_husk/husk.css">
<script type="importmap" data-husk></script>
<script type="module" src="/main.js"></script>
```

Output names carry a content hash and the import map points at them, so a changed file gets a new URL and an unchanged one stays cached.

## Incremental builds

Each source is compiled again only when it or the compiler changed. An unchanged tree compiles nothing (law L002), and a source that fails to compile keeps its last good output (law L003).

## Choosing the files yourself

Set `EnableHuskDefaultItems` to `false` and list the items:

```xml
<PropertyGroup>
  <EnableHuskDefaultItems>false</EnableHuskDefaultItems>
</PropertyGroup>
<ItemGroup>
  <HuskComponent Include="Components/**/*.svelte" />
</ItemGroup>
```

`HuskDev` is Svelte's `dev` option; it defaults to true in Debug.
