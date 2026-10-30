#!/usr/bin/env bash
# Cleanup after T6 (hashed output), written 2026-10-30.
# Removes the worker's scratch outside the repository: a source backup used for break-checks and a
# browser diagnostic folder. Neither is under git.
#
#   scripts/cleanup/t6-hashed-output.sh            # dry run, changes nothing
#   scripts/cleanup/t6-hashed-output.sh --execute  # delete
set -euo pipefail

execute=0
for arg in "$@"; do
    case "$arg" in
        --execute|-e) execute=1 ;;
        *) printf 'unknown argument: %s\n' "$arg" >&2; exit 2 ;;
    esac
done

targets=(/tmp/husk-t6-bk /tmp/husk-t6-diag.KRwy)

for path in "${targets[@]}"; do
    if [ ! -e "$path" ]; then
        printf 'skip:  %s already gone\n' "$path"
        continue
    fi
    if git -C "$path" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        printf 'refuse: %s is inside a git work tree. Left alone.\n' "$path"
        continue
    fi
    if [ "$execute" -eq 1 ]; then
        printf 'delete: %s\n' "$path"
        rm -rf -- "$path"
    else
        printf 'would delete: %s (%s)\n' "$path" "$(du -sh "$path" | cut -f1)"
    fi
done

printf '\nremaining:\n'
for path in "${targets[@]}"; do
    [ -e "$path" ] && printf '  %s\n' "$path"
done
printf 'done\n'
