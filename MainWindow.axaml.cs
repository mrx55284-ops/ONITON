using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Win32;

namespace ONITON;

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = false)]
public partial class MainWindow : Window
{
    const int ClipSeconds = 15;

    readonly VaultStore _store;
    readonly AttemptTracker _tracker;
    readonly ObservableCollection<Entry> _items = new();
    Entry? _current;
    readonly List<Bitmap> _bgs = new();
    int _bgIndex = -1;
    readonly DispatcherTimer _bgTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromMinutes(5) };
    readonly DispatcherTimer _lockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _sleepTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly DispatcherTimer _usbTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    int _fileMissing;
    DateTime _lastTick = DateTime.UtcNow;
    bool _populating, _refreshing;
    Func<string>? _lockMsg, _status;
    (string Name, string Url)? _sug;
    byte[]? _keyBytes; string? _keyName;
    bool _launcherOpen;

    // 자주 쓰는 사이트 (이름/별칭 → 주소). 사이트 이름 칸에 입력하면 흐리게 제안, Tab으로 완성.
    static readonly (string[] Names, string Url)[] Popular =
    {
        (new[]{"Google","구글"},"google.com"), (new[]{"YouTube","유튜브"},"youtube.com"), (new[]{"Naver","네이버"},"naver.com"),
        (new[]{"Kakao","카카오"},"accounts.kakao.com"), (new[]{"Daum","다음"},"daum.net"), (new[]{"Discord","디스코드"},"discord.com"),
        (new[]{"GitHub","깃허브"},"github.com"), (new[]{"Instagram","인스타그램"},"instagram.com"), (new[]{"Facebook","페이스북"},"facebook.com"),
        (new[]{"X","Twitter","트위터"},"x.com"), (new[]{"Netflix","넷플릭스"},"netflix.com"), (new[]{"Amazon","아마존"},"amazon.com"),
        (new[]{"Apple","애플"},"appleid.apple.com"), (new[]{"Microsoft","마이크로소프트"},"login.microsoftonline.com"),
        (new[]{"Steam","스팀"},"steampowered.com"), (new[]{"Coupang","쿠팡"},"coupang.com"), (new[]{"TikTok","틱톡"},"tiktok.com"),
        (new[]{"Twitch","트위치"},"twitch.tv"), (new[]{"Reddit","레딧"},"reddit.com"), (new[]{"LinkedIn","링크드인"},"linkedin.com"),
        (new[]{"Spotify","스포티파이"},"spotify.com"), (new[]{"Notion","노션"},"notion.so"), (new[]{"Slack","슬랙"},"slack.com"),
        (new[]{"Dropbox","드롭박스"},"dropbox.com"), (new[]{"PayPal","페이팔"},"paypal.com"), (new[]{"Gmail","지메일"},"mail.google.com"),
        (new[]{"Outlook","아웃룩"},"outlook.com"), (new[]{"Zoom","줌"},"zoom.us"), (new[]{"Adobe","어도비"},"adobe.com"),
        (new[]{"Samsung","삼성"},"account.samsung.com"), (new[]{"Toss","토스"},"toss.im"), (new[]{"Tistory","티스토리"},"tistory.com"),
        (new[]{"Epic Games","에픽게임즈"},"epicgames.com"), (new[]{"Riot Games","라이엇"},"riotgames.com"), (new[]{"Nexon","넥슨"},"nexon.com"),
        (new[]{"Battle.net","블리자드"},"battle.net"), (new[]{"Canva","캔바"},"canva.com"), (new[]{"ChatGPT","챗지피티"},"chatgpt.com"),
        (new[]{"Claude","클로드"},"claude.ai"),
    };

    static readonly HashSet<string> CommonPasswords = new()
    {
        "password","1234567890","12345678910","qwertyuiop","password1","password123","iloveyou","1111111111","0000000000",
        "asdfghjkl;","abcd123456","qwerty1234","1q2w3e4r5t","admin12345","letmein123","a123456789","zaq12wsx","passw0rd123","123456789a","1234512345",
        "password12","qwertyuiop1","1qaz2wsx3e","0123456789","987654321","abcdefghij","welcome123","changeme12",
    };

    static string Master(string id, string pw) => id.Trim().ToLowerInvariant() + "\n" + pw;

    // 이미지는 실행 파일 안에 포함(Assets → exe). 별도 파일 없이 동작.
    static Bitmap? LoadAsset(string baseName)
    {
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp" })
        {
            var uri = new Uri($"avares://ONITON/Assets/{baseName}{ext}");
            if (!AssetLoader.Exists(uri)) continue;
            try { using var s = AssetLoader.Open(uri); return new Bitmap(s); } catch { }
        }
        return null;
    }

    static string StrengthKey(string p)
    {
        int s = 0;
        if (p.Length >= 12) s++;
        if (p.Length >= 16) s++;
        if (p.Any(char.IsUpper) && p.Any(char.IsLower)) s++;
        if (p.Any(char.IsDigit)) s++;
        if (p.Any(c => !char.IsLetterOrDigit(c))) s++;
        return s <= 1 ? "weak" : s <= 3 ? "fair" : "strong";
    }

    static void WireEye(Button b, Image img, TextBox t, Bitmap? icon)
    {
        img.Source = icon; img.Opacity = 0.35;
        b.Click += (_, _) =>
        {
            bool show = t.PasswordChar != '\0';
            t.PasswordChar = show ? '\0' : '●';
            img.Opacity = show ? 1.0 : 0.35;
        };
    }

    void SetLockMsg(Func<string>? f) { _lockMsg = f; LockMsg.Text = f?.Invoke() ?? ""; }
    void SetStatus(Func<string>? f) { _status = f; StatusText.Text = f?.Invoke() ?? ""; }

    public MainWindow()
    {
        InitializeComponent();
        L.LoadPacks();
        L.Load();
        LangCombo.ItemsSource = L.Languages.Select(l => l.Name).ToList();
        LangCombo.SelectionChanged += (_, _) => { if (!_populating && LangCombo.SelectedIndex >= 0) SetLang(L.Languages[LangCombo.SelectedIndex].Code); };
        LangList.ItemsSource = L.Languages.Select(l => l.Name).ToList(); LangList.SelectedIndex = 0;
        LangOkBtn.Click += (_, _) => ChooseLang(L.Languages[Math.Max(0, LangList.SelectedIndex)].Code);
        _store = new VaultStore(Path.Combine(Paths.DataDir, "vault.dat"));
        _tracker = AttemptTracker.Load(Path.Combine(Paths.DataDir, "attempts.json"));
        List.ItemsSource = _items;

        var icon = LoadAsset("icon");
        if (icon != null) { try { Icon = new WindowIcon(icon); } catch { } }

        for (int i = 1; i <= 30; i++) { var b = LoadAsset($"bg{i}"); if (b == null) break; _bgs.Add(b); }
        if (_bgs.Count > 0) { _bgIndex = Random.Shared.Next(_bgs.Count); BgImage.Source = _bgs[_bgIndex]; }
        _bgTimer.Tick += (_, _) => NextBg();
        _bgTimer.Start();

        TitleBar.PointerPressed += (_, e) => { if (!Dlg.OnButton(e) && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); };
        MinBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        CloseBtn.Click += (_, _) => Close();

        var eyeIcon = LoadAsset("eye");
        WireEye(EyeBtn1, EyeImg1, MasterBox, eyeIcon); WireEye(EyeBtn2, EyeImg2, MasterBox2, eyeIcon); WireEye(EyeEntry, EyeImgEntry, PassBox, eyeIcon);

        if (!L.HasSaved) { LangPanel.IsVisible = true; LockPanel.IsVisible = false; }

        MasterBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateStrength(); };
        SiteBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateGhost(); };
        SiteBox.AddHandler(KeyDownEvent, OnSiteKey, RoutingStrategies.Tunnel);
        UnlockBtn.Click += (_, _) => DoUnlock();
        MasterBox.KeyDown += (_, e) => { if (e.Key == Key.Enter && !MasterBox2.IsVisible) DoUnlock(); };
        KeyBtn.Click += async (_, _) => await Guard(PickKeyFile);
        KeyNewBtn.Click += async (_, _) => await Guard(NewKeyFile);
        List.SelectionChanged += (_, _) => { if (!_refreshing) Select(List.SelectedItem as Entry); };
        AddBtn.Click += (_, _) => { var n = new Entry { Site = L.T("add") }; _store.Entries.Add(n); _store.Save(); SearchBox.Text = ""; Refresh(n); Select(n); };
        SaveBtn.Click += (_, _) => SaveCurrent();
        DelBtn.Click += (_, _) => { if (_current != null) { _store.Entries.Remove(_current); _store.Save(); Refresh(null); } };
        CopyBtn.Click += async (_, _) => { if (_current != null) await CopyText(PassBox.Text ?? ""); };
        CopyUserBtn.Click += async (_, _) => await CopyText(UserBox.Text ?? "");
        OpenBtn.Click += (_, _) => OpenUrl();
        GenBtn.Click += async (_, _) => await Guard(async () => { var r = await new GeneratorWindow(true).ShowDialog<string?>(this); if (r != null) PassBox.Text = r; });
        SearchBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && _store.IsUnlocked) Refresh(_current); };
        LogoutBtn.Click += (_, _) => LockAll("lock_manual");
        MLock.Click += (_, _) => LockAll("lock_manual");
        MExport.Click += async (_, _) => await Guard(Export);
        MImport.Click += async (_, _) => await Guard(Import);
        MImportCsv.Click += async (_, _) => await Guard(ImportCsv);
        MUsb.Click += async (_, _) => await Guard(MakeUsb);
        MChangeMaster.Click += async (_, _) => await Guard(ChangeMaster);
        MGen.Click += async (_, _) => await Guard(() => new GeneratorWindow(false).ShowDialog(this));
        MLog.Click += async (_, _) => await Guard(async () => { if (_store.IsUnlocked) await new LogWindow(_store).ShowDialog(this); });
        MLauncher.Click += async (_, _) => await Guard(() => OpenLauncher());
        MPanic.Click += (_, _) => Panic();
        MUsage.Click += async (_, _) => await Guard(() => new TextWindow(L.T("m_usage").TrimEnd('.'), L.T("help_body")).ShowDialog(this));
        MAbout.Click += async (_, _) => await Guard(async () =>
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var text = $"ONITON {v?.Major}.{v?.Minor}.{v?.Build}\n\n" + L.F("about_body", Paths.DataDir) + "\n\n────────────────\nUI : MRX\nScript : MRX";
            await new TextWindow(L.T("m_about").TrimEnd('.'), text).ShowDialog(this);
        });

        // 5분 무입력 → 잠금
        _idle.Tick += (_, _) => { _idle.Stop(); LockAll("lock_idle"); };
        AddHandler(KeyDownEvent, (_, e) => { ResetIdle(); Shortcuts(e); }, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => ResetIdle(), RoutingStrategies.Tunnel);
        Closing += (_, _) => { try { SecretClip.Clear(this); } catch { } try { _store.Lock(); } catch { } };

        // 절전/덮개 닫힘 감지(모든 OS): 타이머가 갑자기 오래 멈췄다 돌아오면 기기가 잠들었던 것 → 잠금
        _sleepTimer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            if (_store.IsUnlocked && (now - _lastTick).TotalSeconds > 30) LockAll("lock_sleep");
            _lastTick = now;
        };
        _sleepTimer.Start();
        // USB 를 뽑았는데 프로그램이 해독된 채로 남아 있으면 위험하다 → 금고 파일에 접근할 수 없으면 즉시 잠그고 메모리를 비운다 (연속 2회 확인)
        _usbTimer.Tick += (_, _) =>
        {
            if (!_store.IsUnlocked) { _fileMissing = 0; return; }
            if (File.Exists(_store.Path)) { _fileMissing = 0; return; }
            if (++_fileMissing >= 2) { _fileMissing = 0; LockAll("lock_usb", save: false); }
        };
        _usbTimer.Start();
        if (OperatingSystem.IsWindows())
        {
            SystemEvents.SessionSwitch += (_, e) => { if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff) Dispatcher.UIThread.Post(() => LockAll("lock_screen")); };
            SystemEvents.PowerModeChanged += (_, e) => { if (e.Mode == PowerModes.Suspend) Dispatcher.UIThread.Post(() => LockAll("lock_sleep")); };
            SystemEvents.SessionEnding += (_, _) => Dispatcher.UIThread.Post(() => LockAll("lock_shutdown"));
        }

        Opened += async (_, _) => { Native.ExcludeFromCapture(this); Activate(); if (L.HasSaved) await Guard(OfferMigration); };

        _lockTimer.Tick += (_, _) => TickLock();
        ApplyLanguage();
        if (_store.Exists && _tracker.Remaining > TimeSpan.Zero) StartCountdown();
    }

    // ---------- 단축키 / 빠른 실행 / 보안 청소 ----------
    void Shortcuts(KeyEventArgs e)
    {
        bool ctrl = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (!ctrl || !_store.IsUnlocked) return;
        if (e.Key == Key.K && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)) { e.Handled = true; _ = Guard(() => OpenLauncher()); }
        else if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { e.Handled = true; Panic(); }
    }

    /// 빠른 검색창: 이름/아이디/한글 초성으로 찾아 선택하거나(Enter) 비밀번호를 복사(Ctrl+Enter)
    async Task OpenLauncher()
    {
        if (_launcherOpen || !_store.IsUnlocked) return;
        var w = new LauncherWindow(_store.Entries);
        var scr = Screens.ScreenFromWindow(this) ?? Screens.Primary;   // ONITON 이 떠 있는(= 지금 보고 있는) 화면 가운데 위쪽
        if (scr != null)
        {
            var wa = scr.WorkingArea; double sc = scr.Scaling;
            w.Position = new PixelPoint(wa.X + (int)((wa.Width - 540 * sc) / 2), wa.Y + (int)(wa.Height * 0.2));
        }
        var done = new TaskCompletionSource();
        w.Closed += (_, _) => done.TrySetResult();
        _launcherOpen = true;
        w.Show(); w.Activate();
        await done.Task;
        _launcherOpen = false;

        var en = w.Picked;
        if (en == null) return;
        if (w.Mode == "copy") { await CopyText(en.Password); return; }
        SearchBox.Text = ""; Refresh(en); Select(en);
    }

    // 보안 청소: 잠금 + 클립보드 삭제 + 메모리 정리(GC)
    void Panic()
    {
        LockAll("lock_panic");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }

    // ---------- 배경 / 언어 ----------
    async void NextBg()
    {
        if (_bgs.Count < 2) return;
        BgImage.Opacity = 0;
        await Task.Delay(650);
        int n; do { n = Random.Shared.Next(_bgs.Count); } while (n == _bgIndex);
        _bgIndex = n; BgImage.Source = _bgs[n]; BgImage.Opacity = 0.35;
    }

    void ChooseLang(string lang) { SetLang(lang); LangPanel.IsVisible = false; LockPanel.IsVisible = true; _ = Guard(OfferMigration); }

    // USB(휴대용 폴더)에서 처음 실행했는데 이 컴퓨터에 예전 금고가 있으면, 한 번 물어보고 그대로 복사해 준다
    async Task OfferMigration()
    {
        if (!Paths.Portable || _store.Exists || !Paths.LegacyVaultExists) return;
        var yes = await new ChoiceWindow(L.T("mig_title"), L.T("mig_body"), L.T("mig_yes"), L.T("mig_no")).ShowDialog<bool>(this);
        if (!yes) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_store.Path)!);
        File.Copy(Path.Combine(Paths.LegacyDir, "vault.dat"), _store.Path, false);
        Paths.Private(_store.Path);
        ApplyLanguage();
    }

    // 버튼 동작 중 오류가 나도 조용히 멈추지 않고 상태줄에 원인을 보여준다
    // 버튼 동작 중 오류가 나도 조용히 멈추지 않고 상태줄에 원인을 보여준다
    async Task Guard(Func<Task> f)
    {
        try { await f(); }
        catch (Exception ex) { SetStatus(() => ex.GetType().Name + ": " + ex.Message); }
    }

    void SetLang(string lang) { L.Lang = lang; L.LangChosen = true; L.Save(); ApplyLanguage(); }

    void UpdateStrength() => StrengthText.Text = L.T("strength") + L.T(StrengthKey(MasterBox.Text ?? ""));

    void UpdateKeyText() => KeyText.Text = _keyName != null ? L.F("key_loaded", _keyName) : L.T("key_none");

    void ApplyLanguage()
    {
        bool first = !_store.Exists;
        LockTitle.Text = L.T(first ? "welcome_title" : "login_title");
        WelcomeText.Text = L.T("welcome_body");
        WelcomeText.IsVisible = StrengthText.IsVisible = LblPw2.IsVisible = MasterBox2.IsVisible = KeyNewBtn.IsVisible = first;
        LblId.Text = L.T("id"); LblPw.Text = L.T("pw"); LblPw2.Text = L.T("pw2");
        UnlockBtn.Content = L.T(first ? "create" : "login");
        KeyBtn.Content = L.T("key_use"); KeyNewBtn.Content = L.T("key_new"); UpdateKeyText();
        UpdateStrength();
        LocText.Text = L.T(Paths.Portable ? "loc_portable" : "loc_pc") + "\n" + Paths.DataDir;

        AddBtn.Content = L.T("add"); LogoutBtn.Content = L.T("logout");
        MFile.Header = L.T("menu_file"); MTools.Header = L.T("menu_tools"); MHelp.Header = L.T("menu_help");
        MExport.Header = L.T("m_export"); MUsb.Header = L.T("m_usb"); MImport.Header = L.T("m_import"); MImportCsv.Header = L.T("m_import_csv");
        MChangeMaster.Header = L.T("m_change"); MLock.Header = L.T("logout");
        MGen.Header = L.T("m_gen"); MLog.Header = L.T("m_log"); MUsage.Header = L.T("m_usage"); MAbout.Header = L.T("m_about");
        MLauncher.Header = L.T("m_launcher"); MPanic.Header = L.T("m_panic");
        LblSearch.Text = L.T("search"); GrpList.Text = L.T("grp_list"); GrpDetail.Text = L.T("grp_detail");
        OpenBtn.Content = L.T("open"); CopyUserBtn.Content = L.T("copy"); GenBtn.Content = L.T("gen");
        LblSite.Text = L.T("site"); LblUrl.Text = L.T("url"); LblUser.Text = L.T("user"); LblPass.Text = L.T("pass"); LblNote.Text = L.T("note");
        SaveBtn.Content = L.T("save"); CopyBtn.Content = L.T("copy"); DelBtn.Content = L.T("delete");
        foreach (var b in new[] { EyeBtn1, EyeBtn2, EyeEntry }) ToolTip.SetTip(b, L.T("show_hide"));

        _populating = true; LangCombo.SelectedIndex = Array.FindIndex(L.Languages, l => l.Code == L.Lang); _populating = false;
        FlowDirection = L.Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        SetLockMsg(_lockMsg); SetStatus(_status);
    }

    // ---------- 사이트 자동완성 (흐리게 + Tab) ----------
    void UpdateGhost()
    {
        _sug = null;
        var t = SiteBox.Text ?? "";
        if (_current != null && t.Length > 0)
            foreach (var p in Popular)
            {
                var n = p.Names.FirstOrDefault(x => x.Length > t.Length && x.StartsWith(t, StringComparison.OrdinalIgnoreCase));
                if (n != null) { _sug = (n, p.Url); break; }
            }
        GhostText.IsVisible = GhostHint.IsVisible = _sug != null;
        GhostText.Text = _sug != null ? t + _sug.Value.Name[t.Length..] : "";
    }

    void OnSiteKey(object? s, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || _sug == null) return;
        var sug = _sug.Value;
        SiteBox.Text = sug.Name;
        if (string.IsNullOrWhiteSpace(UrlBox.Text)) UrlBox.Text = sug.Url;
        SiteBox.CaretIndex = SiteBox.Text.Length;
        e.Handled = true;
    }

    void ResetIdle() { if (_store.IsUnlocked) { _idle.Stop(); _idle.Start(); } }

    // ---------- 키 파일 ----------
    async Task PickKeyFile()
    {
        var r = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        var f = r.FirstOrDefault(); var p = f?.TryGetLocalPath();
        if (p == null) return;
        var fi = new FileInfo(p);
        if (!fi.Exists || fi.Length == 0 || fi.Length > 1_000_000) { SetLockMsg(() => L.T("err_need_key")); return; }
        _keyBytes = File.ReadAllBytes(p); _keyName = fi.Name; UpdateKeyText();
    }

    async Task NewKeyFile()
    {
        var f = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "ONITON.key" });
        var p = f?.TryGetLocalPath();
        if (p == null) return;
        var bytes = RandomNumberGenerator.GetBytes(64);
        File.WriteAllBytes(p, bytes); Paths.Private(p);
        _keyBytes = bytes; _keyName = Path.GetFileName(p); UpdateKeyText();
        SetLockMsg(() => L.T("key_saved"));
    }

    void ForgetKeyFile()
    {
        if (_keyBytes != null) CryptographicOperations.ZeroMemory(_keyBytes);
        _keyBytes = null; _keyName = null; UpdateKeyText();
    }

    // ---------- 로그인 ----------
    void StartCountdown() { UnlockBtn.IsEnabled = false; _lockTimer.Start(); TickLock(); }

    void TickLock()
    {
        var r = _tracker.Remaining;
        if (r <= TimeSpan.Zero) { _lockTimer.Stop(); UnlockBtn.IsEnabled = true; SetLockMsg(null); return; }
        int s = (int)Math.Ceiling(r.TotalSeconds);
        SetLockMsg(() => L.F("locked_wait", s));
    }

    static bool IsAscii(string t) => t.All(c => c >= 0x20 && c < 0x7F);

    static string? PolicyError(string id, string pw)
    {
        if (id.Trim().Length < 3) return "err_id";
        if (pw.Length < 12) return "err_pw";
        // 한글·악센트 문자는 OS/입력기마다 코드가 달라(NFC/NFD) 다른 컴퓨터에서 같은 비밀번호로 안 열릴 수 있다 → 새로 정할 때는 ASCII 만 허용
        if (!IsAscii(id) || !IsAscii(pw)) return "err_ascii";
        if (pw.Distinct().Count() < 5) return "err_common";
        if (CommonPasswords.Contains(pw.ToLowerInvariant()) || pw.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase)) return "err_common";
        return null;
    }

    async void DoUnlock()
    {
        if (!UnlockBtn.IsEnabled) return;
        try
        {
            SetLockMsg(null);
            var id = IdBox.Text ?? ""; var pw = MasterBox.Text ?? "";
            bool creating = !_store.Exists;
            if (!creating && _tracker.Remaining > TimeSpan.Zero) { StartCountdown(); return; }
            if (id.Trim().Length == 0 || pw.Length == 0) { SetLockMsg(() => L.T("err_empty")); return; }
            UnlockBtn.IsEnabled = false;
            if (creating)
            {
                var pe = PolicyError(id, pw);
                if (pe != null) { SetLockMsg(() => L.T(pe)); return; }
                if (pw != MasterBox2.Text) { SetLockMsg(() => L.T("err_mismatch")); return; }
                var key = _keyBytes;
                await Task.Run(() => _store.Create(Master(id, pw), key));
                _store.AddLog("created");
            }
            else
            {
                if (_store.NeedsKeyFile && _keyBytes == null) { SetLockMsg(() => L.T("err_need_key")); return; }
                var key = _keyBytes;
                await Task.Run(() => _store.Unlock(Master(id, pw), key));
            }
            OnLoggedIn(creating);
        }
        catch (CryptographicException)
        {
            _tracker.RecordFail();
            var n = _tracker.Fails;
            SetLockMsg(() => L.F("err_wrong", n));
            if (_tracker.Remaining > TimeSpan.Zero) StartCountdown();
        }
        catch (InvalidDataException) { _tracker.AddPending("tamper"); SetLockMsg(() => L.T("err_corrupt")); }
        catch (Exception ex) { SetLockMsg(() => ex.Message); }
        finally { if (_tracker.Remaining <= TimeSpan.Zero) UnlockBtn.IsEnabled = true; }
    }

    void OnLoggedIn(bool creating)
    {
        var prev = _store.Log.LastOrDefault(e => e.Kind == "login_ok");
        int fails = _tracker.Pending.Count(p => p.Kind == "login_fail");
        foreach (var p in _tracker.Pending) _store.Log.Add(new LogEvent { Utc = p.Utc, Kind = p.Kind });
        _store.AddLog("login_ok", Environment.MachineName + " / " + RuntimeInformation.OSDescription);
        _store.Save();
        _tracker.Reset();

        IdBox.Text = MasterBox.Text = MasterBox2.Text = "";
        ForgetKeyFile();
        LockPanel.IsVisible = false; VaultPanel.IsVisible = true; MainMenu.IsVisible = true;
        ApplyLanguage(); SearchBox.Text = ""; Refresh(null); ResetIdle();
        if (creating || prev == null) SetStatus(() => L.T("first_login"));
        else { var when = prev.Utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"); SetStatus(() => L.F("last_login", when, fails)); }
    }

    // ---------- 금고 ----------
    void Refresh(Entry? keep)
    {
        var q = (SearchBox.Text ?? "").Trim();
        _refreshing = true;
        _items.Clear();
        foreach (var e in _store.Entries
            .Where(e => q.Length == 0 || e.Site.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || e.Username.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Url.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Site, StringComparer.OrdinalIgnoreCase))
            _items.Add(e);
        List.SelectedItem = keep != null && _items.Contains(keep) ? keep : null;
        _refreshing = false;
        if (keep == null) Select(null);
    }

    void Select(Entry? e)
    {
        _current = e;
        SiteBox.Text = e?.Site; UrlBox.Text = e?.Url; UserBox.Text = e?.Username;
        PassBox.Text = e?.Password; NoteBox.Text = e?.Note;
        PassBox.PasswordChar = '●'; EyeImgEntry.Opacity = 0.35;
        UpdateGhost();
    }

    void SaveCurrent()
    {
        if (_current == null) return;
        _current.Site = SiteBox.Text ?? ""; _current.Url = UrlBox.Text ?? "";
        _current.Username = UserBox.Text ?? ""; _current.Password = PassBox.Text ?? ""; _current.Note = NoteBox.Text ?? "";
        _store.Save();
        Refresh(_current);
        bool dup = _current.Password.Length > 0 && _store.Entries.Any(e => e != _current && e.Password == _current.Password);
        SetStatus(() => L.T(dup ? "saved_dup" : "saved"));
    }

    void OpenUrl()
    {
        var u = (UrlBox.Text ?? "").Trim();
        if (u.Length == 0) return;
        if (!u.Contains("://")) u = "https://" + u;
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
    }

    // 클립보드 기록에 남지 않게 복사하고, 15초 뒤(내용이 안 바뀌었으면) 삭제
    async Task CopyText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        SetStatus(() => L.F("copied", ClipSeconds));
        await SecretClip.Copy(this, text, ClipSeconds);
    }

    async Task ChangeMaster()
    {
        if (!_store.IsUnlocked) return;
        var r = await new ChangeMasterWindow().ShowDialog<string[]?>(this);
        if (r == null) return;
        var err = PolicyError(r[2], r[3]);
        if (err != null) { SetStatus(() => L.T(err)); return; }
        if (r[3] != r[4]) { SetStatus(() => L.T("err_mismatch")); return; }
        bool ok = await Task.Run(() => _store.VerifyMaster(Master(r[0], r[1])));
        if (!ok) { SetStatus(() => L.T("err_cur")); return; }
        await Task.Run(() => _store.ChangeMaster(Master(r[2], r[3])));
        _store.AddLog("master_changed"); _store.Save();
        SetStatus(() => L.T("master_changed"));
    }

    async Task ImportCsv()
    {
        if (!_store.IsUnlocked) return;
        var r = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        var p = r.FirstOrDefault()?.TryGetLocalPath();
        if (p == null) return;
        if (new FileInfo(p).Length > 50_000_000) { SetStatus(() => L.T("csv_bad")); return; }
        try
        {
            var list = CsvImport.Parse(File.ReadAllText(p));
            int added = 0, skipped = 0;
            foreach (var e in list)
            {
                if (_store.Entries.Any(x => x.Url == e.Url && x.Username == e.Username && x.Password == e.Password)) { skipped++; continue; }
                _store.Entries.Add(e); added++;
            }
            if (list.Count == 0) { SetStatus(() => L.T("csv_bad")); return; }
            _store.AddLog("import_csv", added.ToString()); _store.Save();
            Refresh(_current);
            SetStatus(() => L.F("csv_done", added, skipped));
        }
        catch { SetStatus(() => L.T("csv_bad")); return; }
        // 평문 비밀번호가 든 CSV 는 가장 흔한 유출 경로 → 바로 덮어써서 지울지 묻는다
        var wipe = await new ChoiceWindow(L.T("m_import_csv").TrimEnd('.'), L.T("csv_wipe_q"), L.T("csv_wipe_yes"), L.T("csv_wipe_no")).ShowDialog<bool>(this);
        if (wipe)
        {
            try { WipeFile(p); SetStatus(() => L.T("csv_wiped")); }
            catch (Exception ex) { SetStatus(() => ex.Message); }
        }
    }

    static void WipeFile(string p)
    {
        long left = new FileInfo(p).Length;
        using (var fs = new FileStream(p, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            var buf = new byte[65536];
            while (left > 0) { RandomNumberGenerator.Fill(buf); int n = (int)Math.Min(buf.Length, left); fs.Write(buf, 0, n); left -= n; }
            fs.Flush(true);
        }
        File.Delete(p);
    }

    static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    // 지금 실행 중인 프로그램 + 내 금고(암호화된 상태)를 USB 폴더로 복사 → 별도 다운로드 없이 휴대용 완성
    async Task MakeUsb()
    {
        if (!_store.IsUnlocked) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        var dest = folders.FirstOrDefault()?.TryGetLocalPath();
        if (dest == null) return;
        var exe = Environment.ProcessPath ?? "";
        if (!File.Exists(exe) || !Path.GetFileNameWithoutExtension(exe).StartsWith("ONITON", StringComparison.OrdinalIgnoreCase))
        { SetStatus(() => L.F("usb_fail", "ProcessPath")); return; }
        string src = exe; bool bundle = false;
        var k = exe.IndexOf(".app/", StringComparison.OrdinalIgnoreCase);
        if (k >= 0) { src = exe.Substring(0, k + 4); bundle = true; }   // 맥 .app 은 번들 전체를 복사
        var target = Path.Combine(dest, "ONITON-USB");
        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(target);
                if (bundle) CopyDir(src, Path.Combine(target, Path.GetFileName(src)));
                else File.Copy(src, Path.Combine(target, Path.GetFileName(src)), true);
                File.WriteAllBytes(Path.Combine(target, "ONITON.portable"), Array.Empty<byte>());
                var data = Path.Combine(target, "data"); Directory.CreateDirectory(data);
                File.Copy(_store.Path, Path.Combine(data, "vault.dat"), true);
            });
        }
        catch (Exception ex) { SetStatus(() => L.F("usb_fail", ex.Message)); return; }
        await new TextWindow(L.T("m_usb").TrimEnd('.'), L.F("usb_done", target)).ShowDialog(this);
    }

    // 잠금: 중간 단계가 하나 실패해도 화면은 반드시 잠금 화면으로 돌아가도록 단계별로 보호
    void LockAll(string reasonKey, bool save = true)
    {
        if (!_store.IsUnlocked && !VaultPanel.IsVisible) return;
        try { _idle.Stop(); } catch { }
        try { if (save && _store.IsUnlocked) { _store.AddLog("lock", reasonKey); _store.Save(); } } catch { }
        try { _store.Lock(); } catch { }
        try { _items.Clear(); Select(null); SearchBox.Text = ""; } catch { }
        try { SecretClip.Clear(this); } catch { }
        VaultPanel.IsVisible = false; MainMenu.IsVisible = false; LockPanel.IsVisible = true;
        try { ApplyLanguage(); SetLockMsg(() => L.T(reasonKey)); } catch { }
        if (_tracker.Remaining > TimeSpan.Zero) StartCountdown();
    }

    async Task Export()
    {
        var f = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "vault.dat" });
        var p = f?.TryGetLocalPath();
        if (p != null) { File.Copy(_store.Path, p, true); SetStatus(() => L.T("exported")); }
    }

    async Task Import()
    {
        var start = await StorageProvider.TryGetFolderFromPathAsync(Paths.DataDir);   // 백업 파일(.bak1~5)이 있는 데이터 폴더에서 시작
        var r = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = start });
        var p = r.FirstOrDefault()?.TryGetLocalPath();
        if (p == null) return;
        byte[] bytes;
        try { if (new FileInfo(p).Length > VaultCrypto.MaxFileBytes) throw new InvalidDataException(); bytes = File.ReadAllBytes(p); VaultCrypto.Parse(bytes); }
        catch { _store.AddLog("tamper"); SetStatus(() => L.T("err_badimport")); return; }
        _store.ForceBackup();   // 선택한 파일을 메모리에 먼저 읽어 둔 뒤 백업하므로, 백업 파일 자체를 골라도 안전하다
        var tmp = _store.Path + ".tmp";
        File.WriteAllBytes(tmp, bytes); File.Move(tmp, _store.Path, true); Paths.Private(_store.Path);
        LockAll("lock_import", save: false);   // 저장하면 방금 가져온 파일을 지금 금고로 덮어쓰므로 저장하지 않고 잠근다
    }
}
