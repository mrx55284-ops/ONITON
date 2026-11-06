# Windows 에서 실행: 공개용 ONITON-1.0-release.zip (Setup + 단일 파일들만) 을 만든다. 소스는 들어가지 않는다.
# 필요: .NET 8 SDK, Inno Setup 6 (iscc)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
Remove-Item dist -Recurse -Force -ErrorAction SilentlyContinue; New-Item dist -ItemType Directory | Out-Null
function Pub($rid, $out) {
  dotnet publish ONITON.csproj -c Release -r $rid --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o "obj/pub-$rid"
  $exe = if ($rid -like 'win*') { 'ONITON.exe' } else { 'ONITON' }
  Copy-Item "obj/pub-$rid/$exe" "dist/$out"
}
Pub 'win-x64' 'ONITON.exe'
iscc installer/oniton.iss
# Linux/macOS 단일 실행 파일은 해당 OS(또는 GitHub Actions)에서 빌드해 dist/ 에 같은 이름으로 넣으세요.
$files = Get-ChildItem dist -File | Where-Object { $_.Name -in 'ONITON-Setup.exe','ONITON.exe','ONITON-linux-x64','ONITON-macos-arm64','ONITON-macos-x64' }
Compress-Archive -Path $files.FullName -DestinationPath ONITON-1.0-release.zip -Force
Get-FileHash ONITON-1.0-release.zip -Algorithm SHA256
