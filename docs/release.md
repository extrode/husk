# Releasing Husk

Husk publishes to nuget.org by trusted publishing (decision 008): no API key exists. A tag runs `release.yml`, which asks GitHub for a short-lived token.

## One-time setup (done once per repository)

1. nuget.org, Trusted Publishing: policy for repository `extrode/husk`, workflow `release.yml`, owner `sbeparey`, pattern `Extrode.Husk.*`. A new policy must be used within 7 days to stay active.
2. GitHub: variable `NUGET_USER` (`sbeparey`) and environment `packages`. Add a required reviewer once the repo is public.
3. GitHub Pages: source "GitHub Actions"; custom domain `husk.extrode.com`; DNS CNAME `husk` to `extrode.github.io`.

## Each release

1. Pare: the `Extrode.Pare*` version in `Directory.Packages.props` must already be on nuget.org. The release job checks and refuses otherwise.
2. Set `<Version>` in `Directory.Build.props`. While Husk is `0.x` it carries a prerelease suffix (`0.1.0-preview.1`); a test enforces it.
3. Move `CHANGELOG.md` `[Unreleased]` under the new version.
4. Regenerate the site and commit it: `scripts/build-docs.sh` (needs the sibling `../docs` checkout).
5. Merge to `main`, tag, push:

   ```sh
   git tag v0.1.0-preview.1
   git push origin v0.1.0-preview.1
   ```

6. Approve the `packages` environment deployment. The workflow tests, packs, pushes with `--skip-duplicate` and creates a GitHub release (prerelease for 0.x and suffixed tags).

To prove the trusted-publishing exchange without publishing, run `release.yml` by hand (workflow_dispatch).
