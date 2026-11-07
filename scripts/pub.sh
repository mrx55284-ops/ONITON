#!/usr/bin/env bash
# 사용: scripts/pub.sh <RID> <출력폴더>   예) scripts/pub.sh win-x64 obj/pub-win-x64
# 빌드 → 난독화(Obfuscar) → 단일 파일 publish.  난독화를 끄려면 OBF=0 scripts/pub.sh ...
set -euo pipefail
RID="$1"; OUT="$2"
cd "$(dirname "$0")/.."
COMMON="-c Release -r $RID --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None"

if [ "${OBF:-1}" = "0" ]; then dotnet publish ONITON.csproj $COMMON -o "$OUT"; exit 0; fi

export PATH="$PATH:$HOME/.dotnet/tools"
export DOTNET_ROLL_FORWARD=Major        # 난독화 도구가 최신 .NET 에서도 실행되도록
# 난독화 도구가 .NET 위치를 찾게 한다 (Homebrew 등 기본 경로가 아닐 때 필요)
if [ -z "${DOTNET_ROOT:-}" ]; then
  # 설치된 런타임 경로에서 .NET 루트를 알아낸다: "Microsoft.NETCore.App 10.x [<루트>/shared/Microsoft.NETCore.App]"
  export DOTNET_ROOT="$(dotnet --list-runtimes | grep 'Microsoft.NETCore.App' | tail -1 | sed -E 's/.*\[(.*)\/shared\/.*/\1/')"
fi
command -v obfuscar.console >/dev/null 2>&1 || dotnet tool install --global Obfuscar.GlobalTool >/dev/null

dotnet build ONITON.csproj $COMMON
# 윈도우(Git Bash)에서는 /d/a/... 형식 경로를 Obfuscar 가 읽지 못하므로 D:/a/... 형식(pwd -W)을 쓴다. 맥/리눅스는 pwd 그대로.
ROOT="$(pwd -W 2>/dev/null || pwd)"
IN="$ROOT/bin/Release/net10.0/$RID"; OBJ="obj/Release/net10.0/$RID"
rm -rf obj/obf && mkdir -p obj/obf
sed "s#{IN}#$IN#; s#{OUT}#$ROOT/obj/obf#" scripts/obfuscar.xml > obj/obfuscar.run.xml
obfuscar.console obj/obfuscar.run.xml
[ -f obj/obf/ONITON.dll ] || { echo "난독화 실패: 결과 파일 없음"; exit 1; }
cp obj/obf/ONITON.dll "$OBJ/ONITON.dll"          # 난독화된 DLL 로 교체한 뒤
dotnet publish ONITON.csproj $COMMON --no-build -o "$OUT"   # 다시 컴파일하지 않고 그대로 단일 파일로 묶는다
echo "난독화 적용 완료: $RID"
