#!/bin/bash
# 리눅스: 터미널에서 ./ONITON-Linux.sh
cd "$(dirname "$0")"
chmod +x ONITON-linux-x64 2>/dev/null
./ONITON-linux-x64 || echo "실행 실패: USB 가 noexec 로 마운트된 경우 'mount -o remount,exec <경로>' 가 필요합니다."
