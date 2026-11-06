# ONITON 보안 설계 / Security design

## 한국어

**ONITON은 서버 없이 이 컴퓨터(또는 USB)에만 저장되는 비밀번호 금고입니다.** 계정이나 서버가 없고, 프로그램에는 네트워크 통신 코드가 없습니다. 아이디/비밀번호는 금고 파일을 푸는 열쇠로만 쓰이며 저장되지 않습니다.

### 암호 설계
- 키 유도: **Argon2id** (메모리 128 MiB, 반복 3, 병렬 2, salt 32바이트, 금고마다 새 salt). 옛 금고(64 MiB)는 로그인할 때 자동으로 이 강도로 올립니다. 키 파일을 쓰면 SHA-256(키 파일)이 Argon2id의 secret(pepper)으로 함께 들어갑니다.
- 암호화: **AES-256-GCM**, 저장할 때마다 새 96비트 nonce. 파일 헤더 전체(형식, 플래그, KDF 값, salt)가 인증(AAD)에 묶여 있어 헤더를 바꾸면 열리지 않습니다.
- 파일 형식 `ONV2`: `magic(4) | flags(1) | mem(4) | iter(4) | par(4) | salt(32) | nonce(12) | tag(16) | ciphertext`. 파일 안의 KDF 값과 파일 크기(64 MB)는 상한을 검사합니다(메모리 고갈 방지).
- 저장: 임시 파일에 쓰고 디스크에 반영한 뒤 **다시 열어 복호화해 같은 내용인지 검증**하고 교체합니다. 최근 백업 5개를 보관하며, 비밀번호를 바꾸면 옛 백업은 삭제합니다.

### 위협 시나리오와 대응
| # | 공격 / 사고 | 대응 | 남는 위험 |
|---|---|---|---|
| 1 | 금고 파일을 훔쳐 오프라인으로 비밀번호 대입 | Argon2id 128 MiB, 키 파일(선택), 10자 이상·흔한 비밀번호 차단 | 약한 비밀번호는 결국 뚫림 |
| 2 | 금고 파일 변조(헤더/salt/암호문 수정) | AES-GCM 인증 + 헤더 AAD, 변조 시 열리지 않음 | 파일 삭제·교체(가용성)는 막을 수 없음 → 백업 5개 |
| 3 | 악의적인 파일로 메모리 고갈(KDF 값 조작, 거대 파일) | KDF 값 상한, 64 MB 상한 | – |
| 4 | 화면에서 입력값 훔쳐보기 | 비밀번호 칸 가림, 복사한 비밀번호 15초 뒤 삭제 | 어깨너머, 카메라 |
| 5 | 윈도우 클립보드 기록(Win+V)/클라우드 클립보드 | 해당 포맷으로 기록 제외 표시 (Windows) | 맥/리눅스는 15초 삭제만 |
| 6 | 스크린샷/화면 공유/원격 | 창 캡처 제외 (Windows) | 맥/리눅스 미지원 |
| 7 | 자리를 비운 사이 열려 있는 금고 | 5분 무입력·화면잠금·절전 시 자동 잠금, 잠금 시 메모리 비움 | 비밀번호 칸 앞의 몇 분 |
| 8 | 로그인 반복 시도 | 5회 실패부터 대기 시간 증가(재시작해도 유지) | 파일을 지우면 풀림 — 진짜 방어는 #1 |
| 9 | 저장 중 정전/USB 분리로 금고 손상 | 검증 후 교체, 디스크 반영, 백업 5개 | 저장 직전 변경은 사라질 수 있음 |
| 10 | 같은 금고를 두 창에서 열어 서로 덮어씀 | 같은 데이터 폴더 중복 실행 차단 | – |
| 11 | 비밀번호를 바꿨는데 옛 백업으로 열림 | 변경 시 모든 백업 삭제 | 사용자가 따로 복사해 둔 파일 |
| 12 | 평문 CSV(크롬 내보내기) 유출 | 가져온 뒤 덮어써서 삭제할지 묻기 | SSD/클라우드 폴더에서는 완전 삭제 보장 없음 |
| 13 | 다른 컴퓨터에서 한글 입력 형태가 달라 못 엶 | 새 아이디/비밀번호는 ASCII만 허용 | 기존 금고는 영향 없음 |
| 14 | 가짜/변조된 설치 파일 배포 | SHA256, (코드 서명은 아직 없음) | 서명 전에는 OS 경고가 뜸 |
| 15 | 의존 패키지 취약점 | 빌드 때 `dotnet list package --vulnerable` 확인 | 정기 점검 필요 |
| 16 | 프로그램이 몰래 외부로 전송 | 네트워크 코드 없음 (직접 검색해 확인 가능) | – |

### 보호하지 못하는 것
- **키로거·악성코드·원격 제어가 있는 컴퓨터에서 입력하는 아이디/비밀번호.** (키 파일을 쓰면 비밀번호만으로는 열 수 없게 해 피해를 줄일 수 있습니다.)
- **메모리 덤프**: .NET 문자열은 즉시 지울 수 없어, 잠금 해제 중 메모리에 비밀번호가 남을 수 있습니다.
- **빌려 쓰는 컴퓨터의 흔적**: 실행 기록, 임시 폴더에 풀린 라이브러리(비밀번호는 아님).
- **강제로 비밀번호를 알려 달라는 상황**.

### 검증 상태 (솔직하게)
- **독립적인 보안 감사와 실제 침투 테스트는 받지 않았습니다.** 위 표는 설계 점검(위협 모델링)과 코드 검토의 결과이지, 모의해킹 결과가 아닙니다.
- 자동 테스트(`scripts/test.sh`)가 확인하는 것: RFC 9106 Argon2id 시험 값, NIST AES-GCM 시험 값, **다른 구현(Python `cryptography`)이 만든 금고 파일을 ONITON이 열 수 있는지**(`tests/ONITON.Tests/make_fixtures.py`), 변조 거부, 키 파일, 백업 회전, 옛 형식 변환, KDF 자동 강화, 파일 크기 제한, CSV 가져오기.
- 이 테스트는 "표준대로 동작한다"를 확인할 뿐, 전체 설계의 안전을 보증하지는 않습니다. 외부 전문가의 검토를 환영합니다.

## English
ONITON is a serverless local password vault: no accounts, no servers, and no network code. The ID/password are never stored; they only derive the key that unlocks the vault file.

- KDF: Argon2id (128 MiB, t=3, p=2, 32-byte random salt per vault; older 64 MiB vaults are upgraded at login; optional key file mixed in as the Argon2 secret via SHA-256).
- Encryption: AES-256-GCM, fresh 96-bit nonce on every save; the whole header is bound as AAD; KDF parameters and file size are bounded.
- Saving: temp file → flush → re-read and decrypt to verify → rotate 5 backups → replace. Changing the master deletes old backups.
- Not protected: keyloggers/malware on the machine you type on, memory dumps, unsigned binary forgery (verify SHA256.txt), coercion.
- Status: **no independent audit and no real penetration test yet.** The threat table above is a design review. Automated tests cover RFC 9106 / NIST vectors and interoperability with an independent implementation (Python `cryptography`). Expert review is welcome.
