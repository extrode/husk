# The router

Husk can run a SvelteKit-style single-page app: it reads `src/routes/`, builds a client router and
provides `$app/navigation`, `$app/state`, `$app/environment` and `@sveltejs/kit` (`redirect`,
`error`). No Node or Deno. It is off unless the project has a `HuskRoutes` item.

```xml
<ItemGroup>
  <HuskRoutes Include="src/routes" />
  <HuskShell Include="src/app.html" />
  <HuskAlias Include="$lib" Path="src/lib" />
  <HuskImport Include="@capacitor/core" Url="/vendor/capacitor-core.js" />
  <HuskEnv Include="PUBLIC_BACKEND_URL" Value="https://api.example.com" />
</ItemGroup>
<PropertyGroup>
  <HuskHooks>src/hooks.client.ts</HuskHooks>
</PropertyGroup>
```

## What is supported

- Pages, nested layouts, `[param]` folders and `(group)` folders; `+page.ts` and `+layout.ts` `load`
  functions with `parent()`; `redirect` and `error`; `+error.svelte`.
- `goto`, `beforeNavigate`, `afterNavigate`, `preloadCode`; `page` (`url`, `params`, `status`, `error`);
  `browser`, `dev`, `building`, `version`.
- Link clicks, back and forward, fragment links, and a refresh on any address.
- `import.meta.env.NAME` is replaced by the `HuskEnv` values at build time, also in Development for
  files Husk compiles (not for `.ts` files Pare compiles in Development).

Anything else from SvelteKit fails the build with HUSK301 to HUSK303 and the file and line; see
[Error codes](../03-reference/error-codes.md). `svelte.config.js` is not run; a `kit` option other
than `adapter`, `alias`, `paths` and `csp` is HUSK303.

## Serving it

Every address must return the app's `index.html`, except addresses under `/_husk/` and `/data/`,
which are a 404 when no file matches (a cached HTML answer for a module URL once broke an app for
a year).

- Development: `app.UseHuskSpa();` after `UseStaticFiles()`.
- Cloudflare: `husk-deploy ... --spa` sets `not_found_handling` to `single-page-application`.

## Not covered

- **Content-Security-Policy.** SvelteKit writes `kit.csp` as a `<meta>` tag; Husk does not read it, so
  an app ships without one until the headers follow-up. Set one in `_headers` meanwhile.
- **Service worker and web manifest.** A separate spec. Until then `/manifest.webmanifest` and the
  worker script are served as the app's HTML unless you supply them as files.
- **Side-effect CSS imports and JSON modules** from JS are not yet built (work/todo.md).
- Adding or removing a route file in Development needs a rebuild.
