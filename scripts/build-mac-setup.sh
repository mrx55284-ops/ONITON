#!/usr/bin/env bash
# macOS 에서 실행 → dist-mac/ONITON-Setup.pkg (더블클릭하면 /Applications/ONITON.app 이 설치됨). 데이터·계정은 전혀 들어 있지 않다.
set -euo pipefail
cd "$(dirname "$0")/.."
./scripts/test.sh
RID=osx-x64; [ "$(uname -m)" = "arm64" ] && RID=osx-arm64
rm -rf "obj/pub-$RID" dist-mac obj/stage obj/ONITON.iconset && mkdir -p dist-mac obj/stage

./scripts/pub.sh "$RID" "obj/pub-$RID"

APP=obj/stage/ONITON.app
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "obj/pub-$RID/ONITON" "$APP/Contents/MacOS/ONITON" && chmod +x "$APP/Contents/MacOS/ONITON"

# 아이콘 (Assets/icon.png → icns)
mkdir -p obj/ONITON.iconset
for s in 16 32 128 256 512; do
  sips -z $s $s Assets/icon.png --out "obj/ONITON.iconset/icon_${s}x${s}.png" >/dev/null
  sips -z $((s*2)) $((s*2)) Assets/icon.png --out "obj/ONITON.iconset/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns obj/ONITON.iconset -o "$APP/Contents/Resources/ONITON.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>ONITON</string>
<key>CFBundleDisplayName</key><string>ONITON</string>
<key>CFBundleIdentifier</key><string>com.mrx.oniton</string>
<key>CFBundleExecutable</key><string>ONITON</string>
<key>CFBundleIconFile</key><string>ONITON</string>
<key>CFBundleVersion</key><string>1.0.2</string>
<key>CFBundleShortVersionString</key><string>1.0.2</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>LSMinimumSystemVersion</key><string>11.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST

codesign --force --deep -s - "$APP"        # 서명 없는(ad-hoc) 서명: Apple Silicon 에서 실행되려면 최소한 필요

# 안전 검사: 개인 데이터가 설치 파일에 섞이면 중단
if find obj/stage -iname 'vault.dat*' -o -iname 'attempts.json' -o -iname 'settings.txt' -o -iname 'ONITON.portable' -o -type d -name data | grep -q .; then
  echo "중단: 설치 파일에 개인 데이터가 있습니다"; exit 1; fi

pkgbuild --analyze --root obj/stage obj/comp.plist >/dev/null
plutil -replace 0.BundleIsRelocatable -bool false obj/comp.plist
pkgbuild --root obj/stage --component-plist obj/comp.plist --install-location /Applications \
  --identifier com.mrx.oniton --version 1.0.2 dist-mac/ONITON-Setup.pkg
shasum -a 256 dist-mac/ONITON-Setup.pkg
echo "완료: dist-mac/ONITON-Setup.pkg  (받는 사람: 우클릭 → 열기, 또는 시스템 설정 → 개인정보 보호 및 보안 → 그래도 열기)"
