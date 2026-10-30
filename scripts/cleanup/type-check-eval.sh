#!/usr/bin/env bash
# Cleanup after the type-checking evaluation (decision 005), written 2026-10-17.
# Removes tmp/svelte-check-eval: a copy of saaaat.com at c5d31cb8 with its pnpm install and
# planted errors, the svelte-check-native npm install, and the two checkers' outputs.
#
#   scripts/cleanup/type-check-eval.sh                             # dry run, changes nothing
#   scripts/cleanup/type-check-eval.sh --execute --delete-scratch  # delete
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
targets=(tmp/svelte-check-eval)

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
