#!/usr/bin/env sh
# Removes the three leftover worktrees from the T6/T7 work. Dry run by default; -e / --execute acts.
# The branches stay (never deleted); only the worktree folders go.
set -eu
execute=0
case "${1:-}" in -e|--execute) execute=1 ;; esac
for tree in .worktrees/base-check .worktrees/worker-t6-hashed .worktrees/worker-t7-deploy; do
  if [ -d "$tree" ]; then
    if [ "$execute" = 1 ]; then git worktree remove --force "$tree"; else echo "would remove $tree"; fi
  fi
done
