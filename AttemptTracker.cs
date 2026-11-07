using System.Text.Json;
using System.Text.Json.Serialization;
namespace ONITON;

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public class PendingEvent { public DateTime Utc { get; set; } public string Kind { get; set; } = ""; }

/// 로그인 전(금고를 못 여는 상태)에도 남겨야 하는 최소 기록: 실패 횟수/잠금 시각/대기 이벤트.
/// 비밀 정보는 없고, 다음 로그인 성공 때 암호화된 금고 로그로 옮겨진다. 프로그램을 껐다 켜도 실패 횟수가 초기화되지 않는다.
[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public class AttemptTracker
{
    public int Fails { get; set; }
    public DateTime LockedUntilUtc { get; set; }
    public List<PendingEvent> Pending { get; set; } = new();
    string _path = "";

    [JsonIgnore]
    public TimeSpan Remaining => LockedUntilUtc > DateTime.UtcNow ? LockedUntilUtc - DateTime.UtcNow : TimeSpan.Zero;

    public static AttemptTracker Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var t = JsonSerializer.Deserialize<AttemptTracker>(File.ReadAllText(path));
                if (t != null) { t._path = path; return t; }
            }
        }
        catch { }
        return new AttemptTracker { _path = path };
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, JsonSerializer.Serialize(this)); } catch { }
    }

    public void AddPending(string kind)
    {
        Pending.Add(new PendingEvent { Utc = DateTime.UtcNow, Kind = kind });
        if (Pending.Count > 300) Pending.RemoveAt(0);
        Save();
    }

    /// 5회 연속 실패부터 30초, 1분, 2분, 4분... 최대 1시간까지 대기 시간이 늘어난다.
    public void RecordFail()
    {
        Fails++;
        if (Fails >= 5) LockedUntilUtc = DateTime.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Fails - 5)));
        AddPending("login_fail");
    }

    public void Reset() { Fails = 0; LockedUntilUtc = default; Pending.Clear(); Save(); }
}
