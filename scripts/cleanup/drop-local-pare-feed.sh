#!/usr/bin/env sh
# Removes packages/ (the Pare 0.6.0 nupkgs committed as a local feed). Pare 0.6.1 restores from nuget.org now.
# Dry run by default; -e / --execute acts. The removal is a normal git change: commit it afterwards.
set -eu
cd "$(dirname "$0")/../.."
execute=0
case "${1:-}" in -e|--execute) execute=1 ;; esac
if [ -d packages ]; then
  if [ "$execute" = 1 ]; then git rm -r -q packages; else git ls-files packages | sed 's/^/would remove /'; fi
fi
