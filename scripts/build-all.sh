#!/usr/bin/env bash
# macOS / Linux 에서 실행. 공개용 단일 파일들을 dist/ 에 만들고 ONITON-1.0-release.zip 으로 묶는다. (소스·data 폴더는 절대 포함되지 않음)
# 필요: .NET SDK (이 프로젝트의 TargetFramework 와 같은 버전)
set -euo pipefail
cd "$(dirname "$0")/.."
./scripts/test.sh
rm -rf dist obj/pub-* && mkdir dist
pub() { # rid 출력이름 원본파일이름
  ./scripts/pub.sh "$1" "obj/pub-$1"
  cp "obj/pub-$1/$3" "dist/$2"
}
pub win-x64    ONITON.exe          ONITON.exe
pub linux-x64  ONITON-linux-x64    ONITON
pub osx-arm64  ONITON-macos-arm64  ONITON
pub osx-x64    ONITON-macos-x64    ONITON
chmod +x dist/ONITON-linux-x64 dist/ONITON-macos-*

# Setup(ONITON-Setup.exe)은 Windows 의 Inno Setup 으로만 만들 수 있다 → GitHub Actions(.github/workflows/release.yml) 또는 Windows 에서 scripts/build-release.ps1
[ -f dist/ONITON-Setup.exe ] || echo "※ ONITON-Setup.exe 가 없습니다 (Windows/Actions 에서 만든 뒤 dist/ 에 넣고 다시 zip 하세요)"

# 안전 검사: 내 비밀번호 데이터가 배포물에 섞이면 중단
if find dist -iname 'vault.dat*' -o -iname 'attempts.json' -o -iname 'settings.txt' -o -iname 'ONITON.portable' -o -type d -name data | grep -q .; then
  echo "중단: dist 에 개인 데이터가 있습니다"; exit 1; fi

cd dist && zip -q ../ONITON-1.0-release.zip $(ls) && cd ..
shasum -a 256 ONITON-1.0-release.zip
echo "완료: ONITON-1.0-release.zip"
