#!/usr/bin/env bash
# Builds the docs site using the shared Docs tool (sibling repo: ../docs).
#
# Only the numbered sections and their assets are public; the rest of docs/ (specs, plans,
# decisions, lessons) is staged out so it never reaches the site.
#
# Output: dist/docs-site, which is COMMITTED, not gitignored: pages.yml uploads it as it is.
# Regenerate and commit before merging to main, or the published site disagrees with the markdown.
set -euo pipefail
cd "$(dirname "$0")/.."
tool="../docs/src/Docs"
if [ ! -d "$tool" ]; then
  echo "build-docs.sh: the shared Docs tool is not part of this repository; expected a sibling checkout at $tool" >&2
  exit 1
fi
stage=tmp/docs-src
rm -rf "$stage" dist/docs-site
mkdir -p "$stage"
cp -R docs/0[0-6]-* docs/assets "$stage"/
dotnet run --project "$tool" -- "$stage" dist/docs-site --site-name husk
echo "husk.extrode.com" > dist/docs-site/CNAME
