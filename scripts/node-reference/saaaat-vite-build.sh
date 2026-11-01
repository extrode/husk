#!/bin/sh
# The Vite build of the saaaat web app, for the parity references and the size/build-time table
# (spec 003, R4 and R14). The one place Node runs for saaaat; never in a Husk build or a test.
# Usage: scripts/node-reference/saaaat-vite-build.sh [saaaat-checkout]   (default ../../ikhbat.org/saaaat.com)
# Prints the checkout's commit, the install and build wall time in seconds, the build size in bytes,
# and the output folder (last line), which SaaaatParityTests reads from HUSK_SAAAAT_VITE_BUILD.
set -eu
dir=${1:-../../ikhbat.org/saaaat.com}
[ -d "$dir/apps/web" ] || { echo "no saaaat checkout at $dir" >&2; exit 1; }
dir=$(cd "$dir" && pwd)
echo "commit: $(git -C "$dir" rev-parse HEAD)"
start=$(date +%s)
(cd "$dir" && pnpm install --frozen-lockfile >&2)
mid=$(date +%s)
(cd "$dir/apps/web" && pnpm build >&2)
end=$(date +%s)
out=$dir/apps/web/build
[ -d "$out" ] || out=$dir/apps/web/.svelte-kit/output
echo "install_seconds: $((mid - start))"
echo "build_seconds: $((end - mid))"
echo "size_bytes: $(find "$out" -type f -exec wc -c {} + | tail -1 | awk '{print $1}')"
echo "$out"
