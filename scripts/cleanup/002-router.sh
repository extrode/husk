#!/bin/sh
# Removes scratch from the spec 002 build. Dry run unless --execute / -e.
cd "$(dirname "$0")/../.." || exit 1
run=0; case "$1" in --execute|-e) run=1;; esac
for f in tmp/t8-sample tmp/feed tmp/t9-*.log tmp/t15.log tmp/t16*.log tmp/saaaat-pages*.txt tmp/full-*.log tmp/review-*; do
  [ -e "$f" ] || continue
  if [ $run = 1 ]; then rm -rf "$f"; else echo "would remove $f"; fi
done
