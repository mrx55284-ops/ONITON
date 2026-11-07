#!/usr/bin/env bash
# GitHub Actions 를 태그(v4) 대신 "커밋 해시"로 고정한다 (태그가 나중에 악성 코드로 바뀌는 공급망 공격 방지).
# 필요: gh 로그인. 한 번 실행하고 결과를 커밋한다.  실행 후 확인: git diff .github/workflows/release.yml
set -euo pipefail
cd "$(dirname "$0")/.."
f=.github/workflows/release.yml
for u in $(grep -oE 'uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@v[0-9]+' "$f" | sort -u | sed 's/uses: //'); do
  repo="${u%@*}"; tag="${u#*@}"
  read -r type sha < <(gh api "repos/$repo/git/ref/tags/$tag" --jq '.object.type + " " + .object.sha')
  if [ "$type" = "tag" ]; then sha="$(gh api "repos/$repo/git/tags/$sha" --jq .object.sha)"; fi
  sed -i.bak "s|uses: $repo@$tag|uses: $repo@$sha # $tag|" "$f"
done
rm -f "$f.bak"
grep -n "uses:" "$f"
