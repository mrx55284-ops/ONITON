# ONITON 보안 설계 / Security design

## 한국어
**ONITON은 서버 없이 이 컴퓨터(또는 USB)에만 저장되는 비밀번호 금고입니다.** 계정이나 서버가 없고, 아이디/비밀번호는 금고 파일을 푸는 열쇠로만 쓰입니다.

### 암호 설계
- 키 유도: **Argon2id** (메모리 64 MiB, 반복 3, 병렬 2, salt 32바이트, 금고마다 새 salt). 키 파일을 쓰면 SHA-256(키 파일)을 Argon2id의 secret(pepper)으로 함께 넣습니다.
- 암호화: **AES-256-GCM**, 저장할 때마다 새 96비트 nonce. 파일 헤더 전체(형식, 플래그, KDF 값, salt)가 인증(AAD)에 묶여 있어 헤더를 바꾸면 열리지 않습니다.
- 파일 형식 `ONV2`: `magic(4) | flags(1) | mem(4) | iter(4) | par(4) | salt(32) | nonce(12) | tag(16) | ciphertext`. 파일 안의 KDF 값은 상한을 검사합니다(메모리 고갈 방지).
- 저장: 임시 파일에 쓰고 디스크에 반영한 뒤 **다시 열어 복호화해 같은 내용인지 검증**하고 교체합니다. 최근 백업 5개를 보관하며, 비밀번호를 바꾸면 옛 백업은 삭제합니다.

### 보호하는 것 / 보호하지 못하는 것
| 보호함 | 보호하지 못함 |
|---|---|
| 금고 파일을 훔쳐도(강한 비밀번호 + 선택적 키 파일이면) 오프라인에서 풀기 어려움 | 키로거·악성코드가 있는 컴퓨터에서 입력하는 비밀번호 |
| 복사한 비밀번호가 윈도우 클립보드 기록에 남지 않음, 15초 뒤 삭제 | 약한 비밀번호 (보호 수준은 마스터 비밀번호 강도에 좌우됨) |
| 화면 캡처/화면 공유에 ONITON 창이 보이지 않음 (Windows) | 메모리 덤프 (.NET 문자열은 즉시 지울 수 없음) |
| 같은 데이터 폴더의 중복 실행, 저장 중 깨짐으로 인한 손실 | 코드 서명이 없는 상태의 배포 파일 위조 (SHA256.txt로 확인하세요) |

### 검증 상태 (솔직하게)
- **독립적인 보안 감사는 받지 않았습니다.**
- 자동 테스트(`scripts/test.sh`)가 확인하는 것: RFC 9106 Argon2id 시험 값, NIST AES-GCM 시험 값, **다른 구현(Python `cryptography`)이 만든 금고 파일을 ONITON이 열 수 있는지**(`tests/ONITON.Tests/make_fixtures.py`), 변조 거부, 키 파일, 백업 회전, 옛 형식 변환, CSV 가져오기.
- 이 테스트는 "표준대로 동작한다"를 확인할 뿐, 전체 설계의 안전을 보증하지는 않습니다. 외부 전문가의 검토를 환영합니다.

## English
ONITON is a serverless local password vault. There are no accounts or servers; the ID/password are only the key that unlocks the vault file.

- KDF: Argon2id (64 MiB, t=3, p=2, 32-byte random salt per vault; optional key file mixed in as the Argon2 secret via SHA-256).
- Encryption: AES-256-GCM with a fresh 96-bit nonce on every save; the whole header is bound as AAD.
- Saving: write to a temp file, flush to disk, re-read and decrypt to verify, then replace. Five rotating backups; changing the master deletes old backups.
- Not protected: keyloggers/malware on the machine you type on, weak master passwords, memory dumps, forged unsigned binaries (verify SHA256.txt).
- Status: **no independent security audit yet.** Automated tests check RFC 9106 / NIST vectors and interoperability with an independent implementation (Python `cryptography`). Expert review is welcome.
