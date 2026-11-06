<<<<<<< HEAD
# ONITON 1.0 — PRIVATE SOURCE (공개 금지)
UI: MRX / Script: MRX

이 폴더는 소스입니다. 공개 배포용에 넣지 마세요. 공개용 = 빌드 결과물(Setup + 단일 파일)만.

## 빌드
- 맥: `./scripts/build-mac-setup.sh` → `dist-mac/ONITON-Setup.pkg` (설치형)
- 맥/리눅스: `./scripts/build-all.sh` → win/linux/mac 단일 파일 + `ONITON-1.0-release.zip`
- 윈도우 Setup(ONITON-Setup.exe): 비공개 GitHub 저장소에서 Actions → release, 또는 윈도우에서 `scripts/build-release.ps1`

## 배포 전 체크
- 배포물에 vault.dat / data 폴더 / ONITON.portable / settings.txt 가 없어야 함 (스크립트가 자동 검사)
- 첫 실행 시 언어 선택 → "계정 만들기" 화면이 나오면 정상

## 언어
ko, en, ru, zh, hi, es, fr, ar, bn, pt. ko/en 은 Lang.cs, 나머지는 Assets/lang/*.json.
=======
# ONITON
ONITON - serverless local password vault (binaries only)
>>>>>>> d3ff682c8713ed6fafceacc69142b02705f1fee4
