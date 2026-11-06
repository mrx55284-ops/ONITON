ONITON 1.0.2 - USB 키트 / USB Kit
=====================================

이 폴더(ONITON-USB)를 통째로 USB에 복사하면 끝입니다. 설치는 필요 없습니다.
Copy this whole folder (ONITON-USB) to a USB drive. No installation needed.

[폴더 구성]
  ONITON.exe                 윈도우용
  ONITON-macos-arm64 / -x64  맥용 (M1 이상 / Intel)
  ONITON-linux-x64           리눅스용
  ONITON-Windows.cmd / ONITON-Mac.command / ONITON-Linux.sh   경고·권한 문제를 대신 처리하는 실행 도우미
  ONITON.portable            있으면 금고가 이 폴더의 data 폴더에 저장됩니다 (지우지 마세요)
  data/                      내 금고가 저장되는 곳. 이 폴더가 곧 "세이브"입니다.

[처음 한 번: 내 금고를 USB로 옮기기]
 1) 윈도우에서 zip 을 받았다면, 압축을 풀기 전에 zip 파일 우클릭 > 속성 > "차단 해제" 체크 > 확인.
    (맥은 아래 [맥] 의 명령 한 줄)
 2) 폴더를 USB(가능하면 exFAT 로 포맷)에 복사합니다.
 3) 지금까지 ONITON 을 쓰던 컴퓨터에 USB 를 꽂고, USB 안의 ONITON 을 실행합니다.
 4) "이 컴퓨터의 금고 발견" 창이 뜨면 [복사해서 사용] 을 누릅니다. 금고가 USB 의 data 폴더로 복사됩니다.
    (처음 쓰는 분은 그냥 계정을 만들면 됩니다.)
 5) 이제 이 USB 를 어느 컴퓨터에 꽂아도 같은 금고가 열립니다.
    복사 후에는 USB 쪽만 쓰세요. 두 곳에서 따로 쓰면 내용이 갈라집니다.

[다른 컴퓨터에서 쓸 때]
 - 윈도우: ONITON.exe 더블클릭 (경고가 뜨면 "추가 정보 > 실행", 또는 ONITON-Windows.cmd)
 - 맥: 터미널에서 한 번  xattr -dr com.apple.quarantine /Volumes/USB이름   실행한 뒤 ONITON-Mac.command 더블클릭
 - 리눅스: ./ONITON-Linux.sh
 - 로그인 화면 아래의 "저장 위치"가 USB 의 data 로 나오는지 확인하세요.
 - 끝나면 [잠금] > 컴퓨터의 "안전하게 제거" 후 뽑으세요.

[꼭 알아둘 것]
 - 비밀번호를 잊으면 복구할 수 없습니다.
 - 키 파일을 쓴다면 USB 에 같이 두지 말고 따로 보관하세요.
 - 회사/학교/PC방처럼 USB 프로그램 실행을 막은 컴퓨터에서는 실행되지 않습니다.
 - 아직 코드 서명 전 버전이라 보안 경고가 뜰 수 있습니다. 받은 파일은 CHECKSUMS.txt 의 값과 비교해 확인하세요.
 - 복구: data 폴더의 vault.dat.bak1~5 를 [파일 > 금고 가져오기] 로 되살릴 수 있습니다.

[English quick start]
 1) On Windows, right-click the downloaded zip > Properties > Unblock, then extract and copy the folder to a USB drive (exFAT recommended).
 2) On the computer that has your current vault, run ONITON from the USB and click "Copy and use". New users just create an account.
 3) On any other computer: run ONITON.exe (Windows) / ONITON-Mac.command (macOS, after running
    xattr -dr com.apple.quarantine /Volumes/YOUR_USB once) / ./ONITON-Linux.sh (Linux).
 4) Lock, eject safely, unplug. The vault lives in the data folder - keep ONITON.portable.
 Blocked USB execution (corporate/school/internet cafe PCs) cannot be bypassed. Binaries are unsigned: expect OS warnings; verify CHECKSUMS.txt.
