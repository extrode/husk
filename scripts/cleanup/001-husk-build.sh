#!/usr/bin/env bash
# Cleanup after spec 001 (Extrode.Husk.Build), written 2026-10-17.
# Removes the two spikes' build output and npm install, and the build's scratch: vendored runtime
# trials, the PARE200 repro, partial and trial feeds, the corpus diff probe and review scratch.
# Keeps tmp/feed (the sample restores from it) and tmp/corpus (CorpusTests reads it).
#
#   scripts/cleanup/001-husk-build.sh                             # dry run, changes nothing
#   scripts/cleanup/001-husk-build.sh --execute --delete-scratch  # delete
set -euo pipefail

execute=0
delete_scratch=0
for arg in "$@"; do
    case "$arg" in
        --execute|-e) execute=1 ;;
        --delete-scratch) delete_scratch=1 ;;
        *) printf 'unknown argument: %s\n' "$arg" >&2; exit 2 ;;
    esac
done

repo="$(cd "$(dirname "$0")/../.." && pwd)"
if [ ! -f "$repo/docs/decisions/2026-10-17-001-separate-product.md" ]; then
    printf 'refuse: %s is not the Husk repository\n' "$repo"
    exit 1
fi
failed=0
targets=(
    prototypes/001-engine-spike/bench/bin prototypes/001-engine-spike/bench/obj
    prototypes/001-engine-spike/js/node_modules
    prototypes/001-engine-spike/msbuild-task/bin prototypes/001-engine-spike/msbuild-task/obj
    prototypes/001-engine-spike/msbuild-task/app/bin prototypes/001-engine-spike/msbuild-task/app/obj
    prototypes/002-dev-compiler-load/app/bin prototypes/002-dev-compiler-load/app/obj
    prototypes/002-dev-compiler-load/compiler/bin prototypes/002-dev-compiler-load/compiler/obj
    tmp/vendor-cache tmp/vendor-first tmp/vendor-second tmp/p200 tmp/partial tmp/feed-try
    tmp/corpus-diff tmp/review
)

for rel in "${targets[@]}"; do
    if [ ! -e "$repo/$rel" ]; then
        printf 'skip:  %s already gone\n' "$rel"
        continue
    fi
    if [ -n "$(git -C "$repo" ls-files -- "$rel")" ]; then
        printf 'refuse: %s holds files tracked by git. Propose it as a commit instead. Left alone.\n' "$rel"
        failed=1
        continue
    fi
    if [ "$execute" -eq 1 ] && [ "$delete_scratch" -eq 1 ]; then
        printf 'delete: %s\n' "$rel"
        rm -rf -- "${repo:?}/$rel"
    else
        printf 'would delete: %s (%s)\n' "$rel" "$(du -sh "$repo/$rel" | cut -f1)"
    fi
done

printf '\nremaining:\n'
for rel in "${targets[@]}"; do
    [ -e "$repo/$rel" ] && printf '  %s\n' "$rel"
done
printf 'done\n'

if [ "$execute" -eq 1 ] && [ "$failed" -eq 1 ]; then exit 1; fi
exit 0
