#!/usr/bin/env bash
# Removes the worktrees left by T6, T7 and the base check. Dry run unless --execute or -e.
set -eu
exec_=0; case "${1:-}" in --execute|-e) exec_=1;; esac
for d in .worktrees/worker-t6-hashed .worktrees/worker-t7-deploy .worktrees/base-check; do
  if [ "$exec_" = 1 ]; then git worktree remove --force "$d"; else echo "would remove $d"; fi
done
