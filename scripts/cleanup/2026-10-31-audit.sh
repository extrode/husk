#!/bin/sh
# Dry run by default; pass --execute to act.
# The throwaway worktree used to check whether the Safari sample failures predate the Svelte bump.
set -eu
run() { if [ "${1:-}" = "--execute" ]; then shift; "$@"; else echo "would run: $*"; fi; }
EXEC=""; [ "${1:-}" = "--execute" ] || [ "${1:-}" = "-e" ] && EXEC="--execute"
if [ -n "$EXEC" ]; then git worktree remove .worktrees/base-safari; else echo "would run: git worktree remove .worktrees/base-safari"; fi
