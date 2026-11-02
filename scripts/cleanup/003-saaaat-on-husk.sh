#!/usr/bin/env bash
# Cleanup for spec 003 (saaaat on Husk), written 2026-11-02. Never run by an agent.
# Dry run unless --execute or -e. Branches are not deleted here; list them with --branches.
# Removes untracked scratch under tmp/ only; refuses anything git tracks.
set -euo pipefail
exec_=0; branches=0
for a in "$@"; do case "$a" in --execute|-e) exec_=1;; --branches) branches=1;; esac; done
repo="$(git rev-parse --show-toplevel)"
cd "$repo"
[ "$(git branch --show-current)" = dev ] || { echo "refuse: run from dev"; exit 1; }

echo "1. scratch under tmp/"
for rel in tmp/port-out tmp/port-out2 tmp/port-src tmp/port-obj tmp/parity tmp/parity-actual tmp/diag.txt; do
  if [ ! -e "$rel" ]; then echo "skip: $rel is already gone"; continue; fi
  if git ls-files --error-unmatch -- "$rel" >/dev/null 2>&1; then
    echo "refuse: $rel is tracked by git. Left alone."; continue
  fi
  if [ "$exec_" = 1 ]; then rm -rf -- "$rel"; echo "removed $rel"; else echo "would remove $rel"; fi
done

echo "2. branches merged into dev (git branch -d only)"
if [ "$branches" = 1 ]; then
  for b in $(git branch --merged dev --format='%(refname:short)' | grep -E '^(fix|chore|docs|test)/' || true); do
    if [ "$exec_" = 1 ]; then git branch -d "$b"; else echo "would delete $b"; fi
  done
else
  echo "skip: pass --branches to include them"
fi

echo "3. saaaat port worktree (user decision)"
echo "   .worktrees/feat-husk-port in the saaaat repo holds unmerged, unpushed branch feat/husk-port."
echo "   Remove it only after that branch is merged or pushed: git worktree remove <path>"

echo "remaining tmp/:"; ls tmp 2>/dev/null || true
