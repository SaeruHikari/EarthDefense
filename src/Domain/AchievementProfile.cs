using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace Earthward.Domain;

/// <summary>
/// One account's permanent achievements. No run snapshot can restore or reset
/// this object. Rewards become active only after an atomic, durable file write.
/// </summary>
public sealed class AchievementProfile
{
    public const int ProfileVersion = 1, MaxFileBytes = 65536;
    public event Action? Changed;
    public event Action<string>? Unlocked;
    private DataMap _data = Empty();
    private string _path = "", _digest = "";
    private bool _loadBlocked;
    public string LastError { get; private set; } = "";
    public string ProfilePath => _path;
    public bool IsAvailable => _path.Length > 0 && !_loadBlocked;
    public int UnlockedCount => _data.Map("unlocked").Count;

    private static DataMap Empty() => new() { ["version"] = ProfileVersion, ["unlocked"] = new DataMap() };
    public bool HasUnlocked(string id) => _data.Map("unlocked").ContainsKey(id);
    public string UnlockedAt(string id) => _data.Map("unlocked").S(id);
    public DataMap Snapshot() => _data.DeepClone();
    public int PerResearchBonus(string attribute)
        => AchievementCatalog.Entries.Where(row => HasUnlocked(row.S("id")) && row.S("reward_attribute") == attribute).Sum(row => row.I("reward_per_research"));

    public bool LoadProfile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || !Path.IsPathFullyQualified(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BlockLoad("成就档案路径无效，现有进度已保留");
        try
        {
            path = Path.GetFullPath(path);
            string? encoded = ReadExisting(path);
            var data = encoded == null ? Empty() : Validate(DataMap.ParseValue(encoded));
            if (data == null) return BlockLoad("成就档案损坏，原文件已保留；修复并重新载入后才能保存新成就");
            _path = path;
            _digest = encoded == null ? "" : Hash(encoded);
            _data = data;
            _loadBlocked = false;
            LastError = "";
            Changed?.Invoke();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return BlockLoad("无法读取成就档案，原文件已保留：" + error.Message);
        }
    }

    /// <summary>Call only after an actual Earth defeat; duplicates never add another reward.</summary>
    public DataMap RecordDefeat()
    {
        const string id = AchievementCatalog.FirstDefeatId;
        if (HasUnlocked(id)) return Result(true, false, true, id, "");
        if (!IsAvailable)
            return Failure(_loadBlocked ? "成就档案读取失败，修复并重新载入前暂停永久改动" : "成就档案尚未绑定保存路径");
        var next = Snapshot();
        next.Map("unlocked")[id] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (!Persist(next)) return Result(false, false, false, id, LastError);
        _data = next;
        LastError = "";
        Changed?.Invoke();
        Unlocked?.Invoke(id);
        return Result(true, true, false, id, "");
    }

    private bool Persist(DataMap next)
    {
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            // Keep the lock file in place. Removing it after close can race a
            // second process that already opened the same permanent profile.
            using var ownership = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            string? previous = ReadExisting(_path);
            if ((previous == null ? "" : Hash(previous)) != _digest)
                return Fail("成就档案已被另一窗口修改，请重新载入后重试");
            if (previous != null && Validate(DataMap.ParseValue(previous)) == null)
                return Fail("成就档案损坏，未覆盖原文件");
            string encoded = next.ToJson();
            byte[] bytes = Encoding.UTF8.GetBytes(encoded);
            if (bytes.Length > MaxFileBytes) return Fail("成就档案超过容量上限");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (Validate(DataMap.ParseValue(ReadExisting(temporary) ?? "")) == null)
                return Fail("成就档案写入校验失败，未更改原文件");
            if (previous == null) File.Move(temporary, _path);
            else File.Replace(temporary, _path, null);
            _digest = Hash(encoded);
            LastError = "";
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Fail("成就尚未保存，原文件已保留：" + error.Message);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string? ReadExisting(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("成就档案超过容量上限");
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
            return reader.ReadToEnd();
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public static DataMap? Validate(object? source)
    {
        if (source is not DataMap map || map.Count != 2 || !DataMap.ValidNumber(map.Value("version"), ProfileVersion, ProfileVersion, true) || map.Value("unlocked") is not DataMap unlocked)
            return null;
        foreach (var (id, value) in unlocked)
            if (AchievementCatalog.Definition(id).Count == 0 || value is not string stamp || !DateTimeOffset.TryParseExact(stamp, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return null;
        return map.DeepClone();
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private bool Fail(string reason) { LastError = reason; return false; }
    private bool BlockLoad(string reason) { _loadBlocked = true; return Fail(reason); }
    private DataMap Failure(string reason) { Fail(reason); return Result(false, false, false, AchievementCatalog.FirstDefeatId, reason); }
    private static DataMap Result(bool ok, bool unlocked, bool duplicate, string id, string reason)
        => new() { ["ok"] = ok, ["unlocked"] = unlocked, ["duplicate"] = duplicate, ["id"] = id, ["reason"] = reason };
}
