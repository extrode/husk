#!/bin/sh
# Removes the worker worktrees and scratch left by proving L004-L007. Dry run unless --execute / -e.
cd "$(dirname "$0")/../.." || exit 1
run=0; case "$1" in --execute|-e) run=1;; esac
for f in .worktrees/worker-prove-l005 .worktrees/worker-prove-l007 tmp/prove-*.md tmp/worker-l00*.log tmp/gate-l00*.log tmp/gates.done tmp/l005-criteria.md tmp/laws.log tmp/l00*.log; do
  [ -e "$f" ] || continue
  if [ $run = 1 ]; then rm -rf "$f"; else echo "would remove $f"; fi
done
