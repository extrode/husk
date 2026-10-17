#!/bin/sh
# The Node reference over each corpus present locally, into tmp/corpus/<name>.tsv, for
# tests/Extrode.Husk.Integration/CorpusTests.cs. Run by hand from the repo root; a corpus that is
# not there is skipped here and in the test. Corpora (paths as CorpusTests has them):
#   svelte    ../pare/.corpus/realworld/svelte   (cd ../pare && dotnet run scripts/get-bench-corpus.cs)
#   saaaat    ../../ikhbat.org/saaaat.com
#   riwalqalb ../../ikhbat.org/riwalqalb.com
set -eu
for corpus in svelte=../pare/.corpus/realworld/svelte saaaat=../../ikhbat.org/saaaat.com riwalqalb=../../ikhbat.org/riwalqalb.com; do
  name=${corpus%%=*}
  dir=${corpus#*=}
  if [ ! -d "$dir" ]; then
    echo "skipped $name: no $dir"
    continue
  fi
  mkdir -p "tmp/corpus/$name"
  dotnet run scripts/node-reference/strip-fixtures.cs "$dir" "tmp/corpus/$name" > /dev/null
  node scripts/node-reference/reference.mjs "$dir" "tmp/corpus/$name.tsv" "tmp/corpus/$name"
done
