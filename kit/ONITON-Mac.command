#!/bin/bash
# 맥: 이 파일을 더블클릭하면 칩 종류(M1~ / Intel)에 맞는 ONITON 을 실행합니다.
cd "$(dirname "$0")"
if [ "$(uname -m)" = "arm64" ]; then B="ONITON-macos-arm64"; else B="ONITON-macos-x64"; fi
xattr -d com.apple.quarantine "$B" 2>/dev/null
chmod +x "$B" 2>/dev/null
"./$B"
