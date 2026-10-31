# Releasing Husk

Husk publishes to nuget.org by trusted publishing (decision 008): no API key exists. A tag runs `release.yml`, which asks GitHub for a short-lived token.

## One-time setup (done once per repository)

1. nuget.org, Trusted Publishing: policy for repository `extrode/husk`, workflow `release.yml`, owner `sbeparey`, pattern `Extrode.Husk.*`. A new policy must be used within 7 days to stay active.
2. GitHub: variable `NUGET_USER` (`sbeparey`) and environment `packages`. Add a required reviewer once the repo is public.
3. GitHub Pages: source "GitHub Actions"; custom domain `husk.extrode.com`; DNS CNAME `husk` to `extrode.github.io`.

## Each release

1. Pare: the `Extrode.Pare*` version in `Directory.Packages.props` must already be on nuget.org. The release job checks and refuses otherwise.
2. Set `<Version>` in `Directory.Build.props`. While Husk is `0.x` it carries a prerelease suffix (`0.1.0-preview.2`); a test enforces it.
3. Move `CHANGELOG.md` `[Unreleased]` under the new version.
4. Merge to `main` and push. `pages.yml` deploys the site when a push to `main` changes `dist/docs-site/**`, so docs that reach the site go live at once. Install snippets that name the new version should wait until step 7.
5. Tag and push:

   ```sh
   git tag v0.1.0-preview.2
   git push origin v0.1.0-preview.2
   ```

6. The workflow's `build` job tests and packs with a read-only token. The `release` job then waits for approval from the `packages` environment's reviewer, and pushes to nuget.org and to GitHub Packages (`nuget.pkg.github.com/extrode`), each with `--skip-duplicate`, and creates a GitHub release (prerelease for 0.x and suffixed tags). Re-running the job after the release exists fails at `gh release create` (the nuget.org push before it is skipped as a duplicate); finish the GitHub Packages push with `backfill_tag`, below. Approve it under the run's *Review deployments*; the environment admits only `v*` tags and `main`.
7. Once the version is on nuget.org, bump the install snippets and Status (`README.md`, `docs/00-overview/README.md`, `first-app.md`), run `scripts/build-docs.sh` (needs the sibling `../docs` checkout), commit and merge to `main`.

To push an existing tag to GitHub Packages only (a version released before that push existed), run `release.yml` by hand from `main` with `backfill_tag` set to the tag, e.g. `v0.1.0-preview.1`. It does not touch nuget.org.

To prove the trusted-publishing exchange without publishing, run `release.yml` by hand (workflow_dispatch).
