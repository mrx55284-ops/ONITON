#!/usr/bin/env bash
# 금고 암호/저장/백업/가져오기 자동 테스트. 실패하면 배포하지 마세요.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet test tests/ONITON.Tests/ONITON.Tests.csproj -c Release
