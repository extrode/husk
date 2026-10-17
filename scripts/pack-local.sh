#!/bin/sh
# Packs Extrode.Husk.Build and Extrode.Husk.AspNetCore into tmp/feed as 0.1.0-dev.<unix time>
# and re-restores samples/svelte-app when it exists, whose `0.1.0-*` references then pick the new
# build. A unique version keeps NuGet from serving a cached package with the same number.
set -eu
root=$(cd "$(dirname "$0")/.." && pwd)
version="${HUSK_VERSION:-0.1.0-dev.$(date +%s)}"
feed="${HUSK_FEED:-$root/tmp/feed}"
for project in Extrode.Husk.Build Extrode.Husk.AspNetCore; do
  dotnet pack "$root/src/$project" -c Release -o "$feed" -p:Version="$version" -v q -nologo
done
if [ -d "$root/samples/svelte-app" ]; then
  dotnet restore "$root/samples/svelte-app" --force-evaluate -v q
fi
echo "packed Extrode.Husk.Build and Extrode.Husk.AspNetCore $version into $feed"
