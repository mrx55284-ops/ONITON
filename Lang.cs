using System.Text.Json;
using Avalonia.Platform;
namespace ONITON;

/// 다국어 문자열 + 설정 저장. 한국어/English 는 코드에, 나머지 언어는 Assets/lang/xx.json (실행 파일 안에 포함).
public static class L
{
    public static string Lang = "ko";
    public static readonly (string Code, string Name)[] Languages =
    {
        ("ko","한국어"), ("en","English"), ("ru","Русский"), ("zh","简体中文"), ("hi","हिन्दी"),
        ("es","Español"), ("fr","Français"), ("ar","العربية"), ("bn","বাংলা"), ("pt","Português"),
    };
    public static bool Rtl => Lang is "ar" or "he" or "fa" or "ur";
    static readonly Dictionary<string, Dictionary<string, string>> Packs = new();
    public static bool LangChosen;       // 언어를 한 번이라도 골랐는지
    public static bool HasSaved => LangChosen;
    static string SettingsPath => Path.Combine(Paths.DataDir, "settings.txt");

    public static void LoadPacks()
    {
        foreach (var (code, _) in Languages)
        {
            if (code is "ko" or "en") continue;
            try
            {
                var uri = new Uri($"avares://ONITON/Assets/lang/{code}.json");
                if (!AssetLoader.Exists(uri)) continue;
                using var s = AssetLoader.Open(uri);
                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(s);
                if (d != null) Packs[code] = d;
            }
            catch { }
        }
    }

    static readonly Dictionary<string, (string ko, string en)> D = new()
    {
        ["welcome_title"] = ("ONITON에 오신 것을 환영합니다", "Welcome to ONITON"),
        ["welcome_body"] = ("처음 사용하시네요. 사용할 아이디와 비밀번호를 만들어 주세요.\n이 정보는 내 컴퓨터 안에서만 쓰이고 서버로 전송되지 않습니다. 잊어버리면 복구할 수 없으니 꼭 기억해 두세요.",
                            "Looks like your first time. Create an ID and password.\nEverything stays on this computer and is never sent to a server. If you forget it, it cannot be recovered."),
        ["login_title"] = ("로그인", "Log in"),
        ["id"] = ("아이디", "ID"), ["pw"] = ("비밀번호", "Password"), ["pw2"] = ("비밀번호 확인", "Confirm password"),
        ["login"] = ("로그인", "Log in"), ["create"] = ("계정 만들기", "Create account"),
        ["strength"] = ("비밀번호 강도: ", "Password strength: "),
        ["weak"] = ("약함", "Weak"), ["fair"] = ("보통", "Fair"), ["strong"] = ("강함", "Strong"),
        ["err_id"] = ("아이디는 3자 이상 입력하세요.", "ID must be at least 3 characters."),
        ["err_pw"] = ("비밀번호는 10자 이상 입력하세요.", "Password must be at least 10 characters."),
        ["err_empty"] = ("아이디와 비밀번호를 입력하세요.", "Enter your ID and password."),
        ["err_mismatch"] = ("비밀번호가 서로 다릅니다.", "Passwords do not match."),
        ["err_wrong"] = ("아이디, 비밀번호 또는 키 파일이 맞지 않습니다. (연속 {0}회)", "Wrong ID, password or key file. ({0} in a row)"),
        ["err_common"] = ("너무 흔하거나 아이디와 같은 비밀번호입니다. 다른 비밀번호를 쓰세요.", "That password is too common or the same as the ID. Choose another."),
        ["locked_wait"] = ("로그인이 제한되었습니다. {0}초 후 다시 시도하세요.", "Login is restricted. Try again in {0}s."),
        ["first_login"] = ("처음 접속했습니다.", "First login."),
        ["last_login"] = ("마지막 접속: {0}  /  그 사이 로그인 실패 {1}회", "Last login: {0}  /  failed attempts since: {1}"),
        ["key_use"] = ("키 파일 선택...", "Choose key file..."),
        ["key_new"] = ("새 키 파일 만들기...", "Create new key file..."),
        ["key_none"] = ("키 파일: 사용 안 함 (선택 사항 — 비밀번호가 노출돼도 키 파일 없이는 못 엽니다)", "Key file: none (optional — a leaked password alone cannot open the vault)"),
        ["key_loaded"] = ("키 파일: {0}", "Key file: {0}"),
        ["err_need_key"] = ("이 금고는 키 파일이 필요합니다. 키 파일을 선택하세요.", "This vault needs a key file. Choose it first."),
        ["key_saved"] = ("키 파일을 저장했습니다. 컴퓨터와 분리된 USB 등에 보관하세요. 잃어버리면 금고를 열 수 없습니다.", "Key file saved. Keep it on a separate USB drive. If you lose it, the vault cannot be opened."),
        ["log_title"] = ("보안 로그", "Security log"),
        ["log_summary"] = ("로그인 성공 {0}회 / 실패 {1}회", "Successful logins {0} / failed {1}"),
        ["log_empty"] = ("기록이 없습니다.", "No records."),
        ["log_login_ok"] = ("로그인 성공", "Login OK"), ["log_login_fail"] = ("로그인 실패", "Login failed"),
        ["log_tamper"] = ("잘못된 금고 파일", "Invalid vault file"), ["log_lock"] = ("잠금", "Locked"), ["log_created"] = ("계정 생성", "Account created"),
        ["log_master_changed"] = ("아이디/비밀번호 변경", "ID/password changed"), ["log_import_csv"] = ("CSV 가져오기", "CSV imported"),
        ["add"] = ("새 항목", "New entry"), ["logout"] = ("잠금", "Lock"),
        ["site"] = ("사이트 이름", "Site name"), ["url"] = ("주소 (URL)", "Address (URL)"),
        ["user"] = ("아이디", "ID"), ["pass"] = ("비밀번호", "Password"), ["note"] = ("메모", "Note"),
        ["save"] = ("저장", "Save"), ["copy"] = ("복사", "Copy"), ["delete"] = ("삭제", "Delete"),
        ["show_hide"] = ("보기 / 숨기기", "Show / Hide"),
        ["saved"] = ("저장됨", "Saved"), ["saved_dup"] = ("저장됨 — 다른 항목과 같은 비밀번호를 쓰고 있습니다.", "Saved — another entry uses the same password."),
        ["copied"] = ("복사됨 — {0}초 후 클립보드 삭제", "Copied — clipboard clears in {0}s"),
        ["exported"] = ("내보냈습니다 (암호화된 파일)", "Exported (encrypted file)"),
        ["lock_manual"] = ("수동 잠금", "Locked manually"),
        ["lock_idle"] = ("5분간 입력이 없어 잠금", "Locked after 5 minutes of inactivity"),
        ["lock_screen"] = ("화면 잠금", "Screen locked"), ["lock_sleep"] = ("절전", "Sleep"), ["lock_shutdown"] = ("종료", "Shutdown"),
        ["lock_import"] = ("가져왔습니다. 가져온 금고의 아이디/비밀번호로 로그인하세요.", "Imported. Log in with the imported vault's ID/password."),
        ["lock_panic"] = ("보안 청소를 실행했습니다.", "Security cleanup done."),
        ["err_corrupt"] = ("금고 파일이 손상되었거나 올바른 형식이 아닙니다.", "The vault file is damaged or not a valid vault."),
        ["err_badimport"] = ("올바르지 않은 금고 파일입니다. 가져오지 않았습니다.", "Not a valid vault file. Nothing was imported."),
        ["ok"] = ("확인", "OK"), ["cancel"] = ("취소", "Cancel"), ["close"] = ("닫기", "Close"),
        ["menu_file"] = ("파일", "File"), ["menu_tools"] = ("도구", "Tools"), ["menu_help"] = ("도움말", "Help"),
        ["m_export"] = ("금고 내보내기...", "Export vault..."), ["m_import"] = ("금고 가져오기...", "Import vault..."),
        ["m_import_csv"] = ("CSV 가져오기 (크롬/엣지/파이어폭스)...", "Import CSV (Chrome/Edge/Firefox)..."),
        ["m_change"] = ("아이디/비밀번호 변경...", "Change ID / password..."),
        ["m_gen"] = ("비밀번호 생성기...", "Password generator..."), ["m_log"] = ("보안 로그...", "Security log..."),
        ["m_launcher"] = ("빠른 검색 (Ctrl+K)", "Quick search (Ctrl+K)"),
        ["m_panic"] = ("보안 청소 (잠금 + 클립보드 삭제)", "Security cleanup (lock + clear clipboard)"),
        ["m_usage"] = ("사용법...", "How to use..."), ["m_about"] = ("ONITON 정보...", "About ONITON..."),
        ["launcher_hint2"] = ("이름·아이디·초성 입력 / Enter: 선택 / Ctrl+Enter: 비번 복사 / Esc: 닫기", "Type name, ID or Korean initials / Enter: select / Ctrl+Enter: copy password / Esc: close"),
        ["grp_list"] = ("항목 목록", "Entries"), ["grp_detail"] = ("항목 정보", "Details"), ["search"] = ("검색", "Search"),
        ["open"] = ("열기", "Open"), ["gen"] = ("생성", "Generate"),
        ["gen_title"] = ("비밀번호 생성기", "Password generator"), ["gen_len"] = ("길이", "Length"),
        ["gen_up"] = ("대문자 (A-Z)", "Uppercase (A-Z)"), ["gen_lo"] = ("소문자 (a-z)", "Lowercase (a-z)"),
        ["gen_di"] = ("숫자 (0-9)", "Digits (0-9)"), ["gen_sy"] = ("특수문자 (!@#$...)", "Symbols (!@#$...)"),
        ["gen_make"] = ("다시 생성", "Regenerate"), ["gen_use"] = ("사용", "Use"),
        ["chg_title"] = ("아이디/비밀번호 변경", "Change ID / password"),
        ["chg_cur_id"] = ("현재 아이디", "Current ID"), ["chg_cur_pw"] = ("현재 비밀번호", "Current password"),
        ["chg_new_id"] = ("새 아이디", "New ID"), ["chg_new_pw"] = ("새 비밀번호", "New password"), ["chg_conf"] = ("새 비밀번호 확인", "Confirm new password"),
        ["chg_ok"] = ("변경", "Change"),
        ["err_cur"] = ("현재 아이디/비밀번호가 맞지 않습니다.", "Current ID/password is incorrect."),
        ["master_changed"] = ("아이디/비밀번호가 변경되었습니다.", "ID/password changed."),
        ["csv_done"] = ("{0}개를 가져왔습니다 (중복 {1}개 건너뜀). 평문 CSV 파일은 꼭 삭제하세요.", "Imported {0} (skipped {1} duplicates). Delete the plain-text CSV file!"),
        ["csv_bad"] = ("읽을 수 있는 계정이 없는 파일입니다.", "No readable accounts in this file."),
        ["err_running"] = ("ONITON이 이 데이터 폴더에서 이미 실행 중입니다. 열려 있는 창을 확인하세요. 같은 금고를 두 창에서 동시에 쓰면 저장이 서로 덮어써질 수 있어 두 번째 실행을 막았습니다.", "ONITON is already running with this data folder. Check for an open window. Using the same vault in two windows could overwrite saves, so the second start was blocked."),
        ["mig_title"] = ("이 컴퓨터의 금고 발견", "Vault found on this computer"),
        ["mig_body"] = ("이 컴퓨터에 저장된 ONITON 금고가 있습니다.\n이 폴더(USB 등)로 복사해서 쓸까요?\n\n복사하면 이 폴더의 금고가 기준이 됩니다. 이 컴퓨터의 금고는 그대로 남지만 둘이 따로 갈라질 수 있으니, 이후에는 이 폴더의 금고만 쓰세요.", "A vault saved on this computer was found.\nCopy it into this folder (e.g. your USB drive) and use it from here?\n\nFrom then on this folder's vault is the one to use. The vault on this computer stays untouched, but the two can drift apart, so use only this folder's copy afterwards."),
        ["mig_yes"] = ("복사해서 사용", "Copy and use"),
        ["mig_no"] = ("새로 시작", "Start fresh"),
        ["loc_portable"] = ("저장 위치: 이 폴더의 data (휴대용 — 폴더째 옮기면 금고도 같이 갑니다)", "Saved in: this folder's data (portable — the vault travels with the folder)"),
        ["loc_pc"] = ("저장 위치: 이 컴퓨터 (다른 곳에서 쓰려면 ONITON을 USB에 복사해 거기서 실행하세요)", "Saved on: this computer (to carry it, copy ONITON to a USB drive and run it from there)"),
        ["help_body"] = (
            "■ 시작\n처음 실행하면 아이디와 비밀번호를 만듭니다. 잊어버리면 아무도 복구할 수 없습니다. (선택) '키 파일'을 만들면 비밀번호가 노출돼도 키 파일 없이는 열 수 없습니다.\n\n" +
            "■ 항목 저장\n[새 항목] → 사이트·아이디·비밀번호 입력 → [저장]. 비밀번호 칸의 [생성]으로 강력한 비밀번호를 만들 수 있습니다.\n\n" +
            "■ 빠른 검색\nCtrl+K: 검색창이 열립니다. Enter = 항목 선택, Ctrl+Enter = 비밀번호 복사. 한글 초성 검색(ㄱㅎㅂ)도 됩니다.\n\n" +
            "■ 복사\n복사한 비밀번호는 15초 뒤 자동으로 지워지고, 윈도우 클립보드 기록(Win+V)에는 남지 않습니다.\n\n" +
            "■ 잠금\n5분간 입력이 없거나 화면 잠금·절전 시 자동으로 잠깁니다. Ctrl+Shift+L: 즉시 잠금 + 클립보드 삭제.\n\n" +
            "■ 백업\n저장할 때마다 파일이 정상인지 검증하고, 데이터 폴더에 최근 백업 5개(vault.dat.bak1~5)를 보관합니다. 문제가 생기면 [파일 → 금고 가져오기]에서 백업 파일을 골라 되살리세요. 같은 데이터 폴더로 ONITON을 두 번 동시에 실행할 수 없습니다. 아이디/비밀번호를 바꾸면 옛 백업은 삭제됩니다.\n\n" +
            "■ 다른 사람의 컴퓨터에서\nUSB에 ONITON 을 넣어 실행하면(USB는 자동으로 휴대용) 데이터가 USB에만 저장되어 그 컴퓨터에 남지 않습니다. 단, 키로거가 깔린 컴퓨터에서는 어떤 프로그램도 입력하는 비밀번호를 지킬 수 없으니 믿을 수 있는 컴퓨터에서만 로그인하세요. ONITON 창은 화면 캡처·원격 공유에 보이지 않습니다.",
            "■ Getting started\nOn first run you create an ID and password. Nobody can recover them if you forget. (Optional) a key file means a leaked password alone cannot open the vault.\n\n" +
            "■ Saving entries\n[New entry] → site, ID, password → [Save]. Use [Generate] to create a strong password.\n\n" +
            "■ Quick search\nCtrl+K opens a search box. Enter = select entry, Ctrl+Enter = copy password. Korean initial search works too.\n\n" +
            "■ Copying\nA copied password is cleared after 15 seconds and is kept out of the Windows clipboard history (Win+V).\n\n" +
            "■ Locking\nLocks after 5 minutes idle, on screen lock or sleep. Ctrl+Shift+L: lock now + clear clipboard.\n\n" +
            "■ Backups\nEvery save is verified, and the 5 most recent backups (vault.dat.bak1-5) are kept in the data folder. If something goes wrong, choose a backup file in [File → Import vault]. ONITON cannot be started twice on the same data folder. Changing your ID/password deletes the old backups.\n\n" +
            "■ On someone else's computer\nRun ONITON from a USB drive (a USB drive is automatically portable): data stays on the USB and nothing is left on that PC. But no program can protect a password typed on a computer with a keylogger — only log in on computers you trust. The ONITON window is hidden from screenshots and screen sharing."),
        ["about_body"] = ("서버 없이 이 컴퓨터에만 저장되는 비밀번호 금고입니다.\n데이터 위치: {0}", "A password vault stored only on this computer, with no server.\nData location: {0}"),
    };

    public static string T(string k)
    {
        if (Lang != "ko" && Lang != "en" && Packs.TryGetValue(Lang, out var p) && p.TryGetValue(k, out var s)) return s;
        return D.TryGetValue(k, out var v) ? (Lang == "ko" ? v.ko : v.en) : k;
    }
    public static string F(string k, params object[] a) => string.Format(T(k), a);

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            foreach (var line in File.ReadAllLines(SettingsPath))
            {
                var kv = line.Split('=', 2);
                if (kv.Length != 2) continue;
                if (kv[0] == "lang") { var c = kv[1].Trim(); Lang = Languages.Any(l => l.Code == c) ? c : "ko"; LangChosen = true; }
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, (LangChosen ? $"lang={Lang}\n" : ""));
        }
        catch { }
    }
}
