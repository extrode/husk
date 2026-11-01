#!/bin/sh
# Removes the T13 worker worktree and scratch. Dry run unless --execute / -e.
cd "$(dirname "$0")/../.." || exit 1
run=0; case "$1" in --execute|-e) run=1;; esac
for f in .worktrees/worker-t13 tmp/t13 tmp/t13.md tmp/worker-t13.log tmp/watch-test.log tmp/route-watcher-*; do
  [ -e "$f" ] || continue
  if [ $run = 1 ]; then rm -rf "$f"; else echo "would remove $f"; fi
done
