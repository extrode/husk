# 008 - How Husk is published, documented and hosted

Date: 2026-10-30. Status: accepted (the user approved the domain; the rest follows jauntyq).

## Decision

- **NuGet:** trusted publishing, no API key. A `release.yml` on `v*` tags, job in environment
  `packages`, `NuGet/login` with `vars.NUGET_USER`, then `dotnet nuget push --skip-duplicate`.
  Copied in shape from `../jauntyq/.github/workflows/release.yml`, which has published
  `Extrode.JauntyQ.*` to nuget.org this way. The user adds, once per repo, a nuget.org
  trusted-publishing policy (repository, workflow `release.yml`, environment `packages`), the
  `NUGET_USER` variable and the `packages` environment with a required reviewer.
- **Site:** `husk.extrode.com`, GitHub Pages, as `jauntyq.extrode.com`. Markdown under `docs/` is built by the
  shared generator in `../docs` (`extrode/docs`, .NET, self-contained, no CDN, no Node) into a committed
  `dist/docs-site` with a `CNAME` file; `pages.yml` uploads that folder from `main`.
- **DNS (the user):** a `CNAME` record `husk` pointing at `extrode.github.io`, DNS only (not
  proxied) until GitHub has issued the certificate. Nothing is deployed on Cloudflare for the site.
- **Pare:** Husk cannot be restored by anyone else until Pare is on nuget.org (404 on
  2026-10-30) and public. Asked for in `pare/docs/handoffs/2026-10-30-husk-needs-a-published-pare.md`.

## Rejected

- A stored NuGet API key: a secret to rotate and leak; jauntyq already avoids it.
- Cloudflare Pages for the site: a second host for what GitHub Pages does, and the user has no
  reason to run both.
- A new site generator: `../docs` exists, is shared, and has the same no-Node rule as Husk.
