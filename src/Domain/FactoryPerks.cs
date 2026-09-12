using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace Earthward.Domain;

/// <summary>Permanent profile, explicitly bound to a file; never restored from run checkpoints.</summary>
public sealed class FactoryPerks
{
    public const int ProfileVersion = 4, SlotCount = 2, MaxSites = 65536, MaxBerths = 65536, MaxBerthIndex = 32767, MaxClaims = 200000, MaxFileBytes = 32 * 1024 * 1024;
    public const long MaxCurrency = 9007199254740991;
    public const string CurrencyName = "外星芯片";
    public event Action? Changed;
    public event Action? CurrencyChanged;
    private DataMap _data;
    private HashSet<string> _claims = new(StringComparer.Ordinal);
    private string _path = "", _digest = "";
    private bool _loadBlocked;
    public string LastError { get; private set; } = "";
    public string LoadWarning { get; private set; } = "";
    public string ProfilePath => _path;
    public long AlienChips => _data.L("alien_chips");
    public string ActiveRunId => _data.S("active_run");
    public bool AdvancedUnlocked => _data.B("advanced_unlocked");
    public DataMap Settings => _data.Map("settings").DeepClone();
    public FactoryPerks()
    {
        var levels = new DataMap();
        foreach (var row in PerkCatalog.Entries)
            levels[row.S("id")] = 0L;
        _data = new()
        {
            ["version"] = ProfileVersion,
            ["alien_chips"] = 0L,
            ["settings"] = PerkCatalog.DefaultSettings,
            ["levels"] = levels,
            ["templates"] = BlankTemplates(),
            ["active_run"] = "",
            ["sites"] = new DataMap(),
            ["claims"] = new List<object?>(),
            ["aircraft_templates"] = BlankTemplates(),
            ["aircraft_sites"] = new DataMap(),
            ["berths"] = new DataMap(),
            ["advanced_unlocked"] = false,
            ["known_intel"] = new List<object?>()
        };
    }
    private static DataMap BlankTemplates()
    {
        var result = new DataMap();
        foreach (string kind in PerkCatalog.Kinds)
            result[kind] = new List<object?> { "", "" };
        return result;
    }
    public int GetLevel(string id) => _data.Map("levels").I(id);
    public bool IsUnlocked(string id) => GetLevel(id) > 0;
    private int MaxLevel(string id) => PerkCatalog.Definition(id).I("stage") > 0 ? PerkEffectRules.Tier(1).I("max_level") : _data.Map("settings").I("max_level");
    private double UpgradeBase(string id) => PerkCatalog.Definition(id).I("stage") > 0 ? PerkEffectRules.Tier(1).N("upgrade_base_cost") : _data.Map("settings").N("upgrade_base_cost");
    private double UpgradeGrowth(string id) => PerkCatalog.Definition(id).I("stage") > 0 ? PerkEffectRules.Tier(1).N("upgrade_growth") : _data.Map("settings").N("upgrade_growth");
    public string EffectText(string id, int level = -1) => PerkCatalog.EffectText(id, level < 0 ? Math.Max(1, GetLevel(id)) : level, _data.Map("settings"));
    public string UnlockReason(string id) => !_data.Map("levels").ContainsKey(id) ? "未知特性" : IsUnlocked(id) ? "" : PerkCatalog.Definition(id).I("stage") > 0 && !AdvancedUnlocked ? "首次清空 8 艘近地母舰后开放购买" : $"使用 {PurchaseCost(id)} 枚外星芯片购买";
    public List<DataMap> Definitions(string layer = "", string kind = "", string airframe = "")
    {
        var result = new List<DataMap>();
        foreach (var row in PerkCatalog.Entries)
        {
            string id = row.S("id");
            if (layer.Length > 0 && row.S("layer") != layer)
                continue;
            if (kind.Length > 0 && (!PerkCatalog.Kinds.Contains(kind) || (row.List("kinds").Count > 0 && !row.List("kinds").Contains(kind))))
                continue;
            if (airframe.Length > 0 && !PerkCatalog.Compatible(id, row.S("layer"), kind, airframe))
                continue;
            var item = PerkCatalog.Definition(id);
            item["level"] = GetLevel(id);
            item["unlocked"] = IsUnlocked(id);
            item["max_level"] = MaxLevel(id);
            item["upgrade_base_cost"] = UpgradeBase(id);
            item["upgrade_growth"] = UpgradeGrowth(id);
            item["upgrade_cost"] = UpgradeCost(id);
            item["purchase_cost"] = PurchaseCost(id);
            item["purchase_lock_reason"] = PurchaseLockReason(id);
            item["effect_text"] = EffectText(id);
            item["next_effect_text"] = GetLevel(id) < MaxLevel(id) ? EffectText(id, GetLevel(id) + 1) : "已满级";
            item["unlock_reason"] = UnlockReason(id);
            result.Add(item);
        }
        return result;
    }
    private static string SiteKey(long site) => site.ToString(CultureInfo.InvariantCulture);
    private static string BerthKey(long site, int berth) => SiteKey(site) + ":" + berth.ToString(CultureInfo.InvariantCulture);
    private static string Field(string layer, int berth) => layer == "factory" ? "sites" : berth < 0 ? "aircraft_sites" : "berths";
    public List<string> SlotsFor(string kind, long site = -1, string layer = "factory", int berth = -1)
    {
        if (!PerkCatalog.Kinds.Contains(kind) || !PerkCatalog.Layers.Contains(layer))
            return new() { "", "" };
        var source = _data.Map(layer == "factory" ? "templates" : "aircraft_templates").List(kind);
        var sites = _data.Map(layer == "factory" ? "sites" : "aircraft_sites");
        if (site >= 0 && sites.Map(SiteKey(site)).S("kind") == kind)
            source = sites.Map(SiteKey(site)).List("slots");
        if (layer == "aircraft" && berth >= 0 && HasSiteOverride(kind, site, layer, berth))
            source = _data.Map("berths").Map(BerthKey(site, berth)).List("slots");
        return source.Cast<string>().ToList();
    }
    public bool HasSiteOverride(string kind, long site, string layer = "factory", int berth = -1) => PerkCatalog.Kinds.Contains(kind) && site >= 0 && PerkCatalog.Layers.Contains(layer) && _data.Map(Field(layer, berth)).Map(layer == "aircraft" && berth >= 0 ? BerthKey(site, berth) : SiteKey(site)).S("kind") == kind;
    public string EquipLockReason(string kind, long site, int slot, string id, string layer = "factory", int berth = -1, string airframe = "")
    {
        if (!PerkCatalog.Kinds.Contains(kind))
            return "只有三类战机工厂和所属飞机可以装备 Perk";
        if (!PerkCatalog.Layers.Contains(layer))
            return "未知的装备层";
        if (site < -1 || site > MaxCurrency)
            return "无效的工厂编号";
        if (berth < -1 || berth > MaxBerthIndex || (berth >= 0 && (site < 0 || layer != "aircraft")))
            return "飞机编制需要选择具体工厂";
        if (slot < 0 || slot >= 2)
            return "无效的插槽";
        if (site >= 0 && ActiveRunId.Length == 0)
            return "请先绑定本局编号";
        foreach (string field in new[] { "sites", "aircraft_sites" })
            if (site >= 0 && _data.Map(field).ContainsKey(SiteKey(site)) && _data.Map(field).Map(SiteKey(site)).S("kind") != kind)
                return "工厂类型与本局配置不一致";
        if (id.Length > 0)
        {
            if (!PerkCatalog.Compatible(id, layer, kind, airframe))
                return "此特性不适用于当前工厂/飞机类型和装备层";
            if (!IsUnlocked(id))
                return UnlockReason(id);
            if (SlotsFor(kind, site, layer, berth)[1 - slot] == id)
                return "同一配置不能重复装备同种 Perk";
        }
        if (site >= 0)
        {
            var records = _data.Map(Field(layer, berth));
            string key = layer == "aircraft" && berth >= 0 ? BerthKey(site, berth) : SiteKey(site);
            if (records.ContainsKey(key) && records.Map(key).S("kind") != kind)
                return "工厂类型与本局配置不一致";
            if (!records.ContainsKey(key) && records.Count >= (layer == "aircraft" && berth >= 0 ? MaxBerths : MaxSites))
                return "本局装备配置数量已达到上限";
        }
        return "";
    }
    private static void PutSlots(DataMap candidate, string kind, long site, string layer, int berth, List<string> slots)
    {
        if (site < 0)
            candidate.Map(layer == "factory" ? "templates" : "aircraft_templates")[kind] = slots.Cast<object?>().ToList();
        else
            candidate.Map(Field(layer, berth))[layer == "aircraft" && berth >= 0 ? BerthKey(site, berth) : SiteKey(site)] = new DataMap { ["kind"] = kind, ["slots"] = slots.Cast<object?>().ToList() };
    }
    public bool Equip(string kind, long site, int slot, string id, string layer = "factory", int berth = -1, string airframe = "")
    {
        LastError = EquipLockReason(kind, site, slot, id, layer, berth, airframe);
        if (LastError.Length > 0)
            return false;
        var slots = SlotsFor(kind, site, layer, berth);
        if (slots[slot] == id)
            return true;
        slots[slot] = id;
        var copy = Snapshot();
        PutSlots(copy, kind, site, layer, berth, slots);
        return Commit(copy);
    }
    public bool ResetSiteToTemplate(string kind, long site, string layer = "factory", int berth = -1)
    {
        if (!PerkCatalog.Kinds.Contains(kind) || site < 0 || site > MaxCurrency || !PerkCatalog.Layers.Contains(layer) || berth < -1 || berth > MaxBerthIndex || (berth >= 0 && layer != "aircraft"))
            return Fail("无效的工厂编号、类型或装备层");
        if (!HasSiteOverride(kind, site, layer, berth))
            return true;
        var copy = Snapshot();
        copy.Map(Field(layer, berth)).Remove(layer == "aircraft" && berth >= 0 ? BerthKey(site, berth) : SiteKey(site));
        return Commit(copy);
    }
    public bool RemoveSiteOverrides(long site)
    {
        if (site < 0)
            return Fail("无效的工厂编号");
        var copy = Snapshot();
        copy.Map("sites").Remove(SiteKey(site));
        copy.Map("aircraft_sites").Remove(SiteKey(site));
        foreach (string key in copy.Map("berths").Keys.Where(k => k.StartsWith(SiteKey(site) + ":", StringComparison.Ordinal)).ToList())
            copy.Map("berths").Remove(key);
        return Commit(copy);
    }
    public bool ResetRunSites(string runId)
    {
        if (!ValidToken(runId))
            return Fail("本局编号无效");
        if (runId == ActiveRunId)
        {
            LastError = "";
            return true;
        }
        var copy = Snapshot();
        copy["active_run"] = runId;
        copy["sites"] = new DataMap();
        copy["aircraft_sites"] = new DataMap();
        copy["berths"] = new DataMap();
        return Commit(copy);
    }
    public long PurchaseCost(string id)
    {
        var definition = PerkCatalog.Definition(id);
        return definition.Count == 0 || IsUnlocked(id) ? 0 : PerkEffectRules.Tier(definition.I("stage") > 0 ? 1 : 0).L("purchase_cost");
    }
    public string PurchaseLockReason(string id) => !_data.Map("levels").ContainsKey(id) ? "未知特性" : IsUnlocked(id) ? "已拥有此特性" : PerkCatalog.Definition(id).I("stage") > 0 && !AdvancedUnlocked ? "首次清空 8 艘近地母舰后开放购买" : AlienChips < PurchaseCost(id) ? "外星芯片不足" : "";
    public bool Purchase(string id)
    {
        LastError = PurchaseLockReason(id);
        if (LastError.Length > 0)
            return false;
        var copy = Snapshot();
        copy["alien_chips"] = AlienChips - PurchaseCost(id);
        copy.Map("levels")[id] = 1L;
        return Commit(copy);
    }
    public long UpgradeCost(string id)
    {
        int level = GetLevel(id);
        return level <= 0 || level >= MaxLevel(id) ? 0 : (long)Math.Min(MaxCurrency, Math.Ceiling(UpgradeBase(id) * Math.Pow(UpgradeGrowth(id), level - 1)));
    }
    public string UpgradeLockReason(string id) => !_data.Map("levels").ContainsKey(id) ? "未知特性" : !IsUnlocked(id) ? "请先购买此特性" : GetLevel(id) >= MaxLevel(id) ? "此特性已达到等级上限" : AlienChips < UpgradeCost(id) ? "外星芯片不足" : "";
    public bool Upgrade(string id)
    {
        LastError = UpgradeLockReason(id);
        if (LastError.Length > 0)
            return false;
        var copy = Snapshot();
        copy["alien_chips"] = AlienChips - UpgradeCost(id);
        copy.Map("levels")[id] = GetLevel(id) + 1;
        return Commit(copy);
    }
    public DataMap Modifiers(string kind, long site = -1, string layer = "factory", int berth = -1, string airframe = "")
    {
        var result = PerkCatalog.NeutralModifiers();
        foreach (string id in SlotsFor(kind, site, layer, berth))
            if (IsUnlocked(id) && PerkCatalog.Compatible(id, layer, kind, airframe))
                PerkCatalog.Accumulate(result, PerkCatalog.Effects(id, GetLevel(id), _data.Map("settings")));
        return result;
    }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static bool ValidToken(object? value) => value is string text && text.Length > 0 && text.Length <= 160 && text.All(c => c >= 32);
    private static string ClaimKey(string run, object? id)
    {
        if (!ValidToken(run))
            return "";
        string token = id switch
        {
            int n when n >= 0 => n.ToString(CultureInfo.InvariantCulture),
            long n when n >= 0 && n <= MaxCurrency => n.ToString(CultureInfo.InvariantCulture),
            string text when ValidToken(text) => text,
            _ => ""
        };
        return token.Length == 0 ? "" : Hash(run + "\u001f" + token);
    }
    public bool HasClaimed(string run, object? id)
    {
        string key = ClaimKey(run, id);
        return key.Length > 0 && _claims.Contains(key);
    }
    /// <summary>Credits exactly one successful aircraft drop, atomically with its permanent receipt.</summary>
    public DataMap ClaimAlienChip(string run, object? eventId) => ClaimAlienChipBatch(run, new[] { eventId });
    /// <summary>Persists a frame-independent batch once; duplicate receipts never mint a second chip.</summary>
    public DataMap ClaimAlienChips(string run, IReadOnlyList<string> eventIds) => ClaimAlienChipBatch(run, eventIds);
    private DataMap ClaimAlienChipBatch(string run, IEnumerable<object?> eventIds)
    {
        if (!ValidToken(run) || eventIds == null)
            return ClaimFailure("外星芯片掉落的本局编号无效");
        var additions = new HashSet<string>(StringComparer.Ordinal);
        foreach (object? eventId in eventIds)
        {
            string key = ClaimKey(run, eventId);
            if (key.Length == 0)
                return ClaimFailure("外星芯片掉落的事件编号无效");
            if (!_claims.Contains(key))
                additions.Add(key);
        }
        if (additions.Count == 0)
        {
            LastError = "";
            return new() { ["ok"] = true, ["claimed"] = false, ["duplicate"] = true, ["alien_chips"] = 0L, ["reason"] = "" };
        }
        if (additions.Count > MaxClaims - _claims.Count)
            return ClaimFailure("永久奖励账本已达到上限");
        if (additions.Count > MaxCurrency - AlienChips)
            return ClaimFailure("外星芯片余额已达到上限");
        var copy = Snapshot();
        copy["alien_chips"] = AlienChips + additions.Count;
        copy.List("claims").AddRange(additions.Cast<object?>());
        if (!Commit(copy, currencyOnly: true))
            return ClaimFailure(LastError);
        return new() { ["ok"] = true, ["claimed"] = true, ["duplicate"] = false, ["alien_chips"] = (long)additions.Count, ["reason"] = "" };
    }
    private DataMap ClaimFailure(string reason)
    {
        Fail(reason);
        return new() { ["ok"] = false, ["claimed"] = false, ["duplicate"] = false, ["alien_chips"] = 0L, ["reason"] = LastError };
    }
    public bool KnowsIntel(string id) => _data.List("known_intel").Contains(id);
    public bool RememberIntel(string id)
    {
        if (id is not ("M1" or "L1"))
            return false;
        if (KnowsIntel(id))
            return true;
        var copy = Snapshot();
        copy.List("known_intel").Add(id);
        return Commit(copy);
    }
    public bool MarkDefenseStage(int stage)
    {
        if (stage < 1 || AdvancedUnlocked)
            return true;
        var copy = Snapshot();
        copy["advanced_unlocked"] = true;
        return Commit(copy);
    }
    public bool ReconcileAirframe(string kind, long site, int berth, string frame)
    {
        var current = SlotsFor(kind, site, "aircraft", berth);
        var next = current.Select(id => id.Length > 0 && !PerkCatalog.Compatible(id, "aircraft", kind, frame) ? "" : id).ToList();
        if (current.SequenceEqual(next))
            return true;
        var copy = Snapshot();
        PutSlots(copy, kind, site, "aircraft", berth, next);
        return Commit(copy);
    }
    public bool Configure(DataMap values)
    {
        var copy = Snapshot();
        foreach (var (key, value) in values)
        {
            if (!PerkCatalog.DefaultSettings.ContainsKey(key))
                return Fail("未知 Perk 参数");
            copy.Map("settings")[key] = value;
        }
        return Commit(copy);
    }
    public bool SetSetting(string id, object? value) => Configure(new() { [id] = value });
    public DataMap Snapshot()
    {
        var copy = _data.DeepClone();
        copy["claims"] = copy.List("claims").Cast<string>().Order(StringComparer.Ordinal).Cast<object?>().ToList();
        return copy;
    }
    public bool CreditAlienChips(long amount)
    {
        if (amount <= 0 || amount > MaxCurrency - AlienChips)
            return Fail("外星芯片增量无效或超过余额上限");
        var copy = Snapshot();
        copy["alien_chips"] = AlienChips + amount;
        return Commit(copy, currencyOnly: true);
    }
    public bool ImportSnapshot(object? value) => Commit(value);
    public bool LoadProfile(string path)
    {
        LastError = "";
        LoadWarning = "";
        if (string.IsNullOrEmpty(path) || path.Length > 4096 || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !Path.IsPathFullyQualified(path))
            return Fail("永久档案路径无效");
        string primary = ReadFile(path), backup = "";
        var data = Validate(DataMap.ParseValue(primary));
        if (data == null && File.Exists(path + ".bak"))
        {
            backup = ReadFile(path + ".bak");
            data = Validate(DataMap.ParseValue(backup));
            if (data != null)
                LoadWarning = "永久档案主文件不可用，已读取最近的有效备份";
        }
        if (data == null && (File.Exists(path) || File.Exists(path + ".bak")))
        {
            _loadBlocked = true;
            return Fail("永久档案及备份损坏；原文件与当前进度已保留");
        }
        string previousPath = _path, previousDigest = _digest;
        _path = path;
        _digest = File.Exists(path) ? Hash(primary) : "";
        _loadBlocked = false;
        if (data == null)
        {
            if (!SaveProfile())
            {
                _path = previousPath;
                _digest = previousDigest;
                _loadBlocked = true;
                return false;
            }
        }
        else
        {
            _data = data;
            _claims = data.List("claims").Cast<string>().ToHashSet(StringComparer.Ordinal);
            Changed?.Invoke();
            CurrencyChanged?.Invoke();
        }
        return true;
    }
    public bool SaveProfile() => _loadBlocked ? Fail("永久档案读取失败，修复并重新载入前暂停永久改动") : _path.Length == 0 ? Fail("永久档案尚未绑定路径") : Persist(Snapshot());
    private static string ReadFile(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length <= MaxFileBytes ? File.ReadAllText(path, Encoding.UTF8) : "";
        }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
    }
    private bool Persist(DataMap data)
    {
        string temporary = _path + ".tmp";
        try
        {
            string previous = ReadFile(_path);
            if ((File.Exists(_path) ? Hash(previous) : "") != _digest)
                return Fail("永久档案已被另一窗口修改，请重新载入后操作");
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string encoded = data.ToJson();
            byte[] bytes = Encoding.UTF8.GetBytes(encoded);
            if (bytes.Length > MaxFileBytes)
                return Fail("永久档案超过容量上限");
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (Validate(DataMap.ParseValue(ReadFile(temporary))) == null)
                return Fail("永久档案写入校验失败");
            if (previous.Length > 0 && Validate(DataMap.ParseValue(previous)) != null)
                File.Copy(_path, _path + ".bak", true);
            File.Move(temporary, _path, true);
            _digest = Hash(encoded);
            LastError = "";
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { return Fail("无法原子写入永久档案：" + error.Message); }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    private bool Commit(object? value, bool currencyOnly = false)
    {
        if (_loadBlocked)
            return Fail("永久档案读取失败，修复并重新载入前暂停永久改动");
        var data = Validate(value);
        if (data == null)
            return Fail("永久档案数据或参数无效，未更改当前进度");
        if (DataMap.Equivalent(data, _data))
        {
            LastError = "";
            return true;
        }
        if (_path.Length > 0 && !Persist(data))
            return false;
        bool currencyChanged = AlienChips != data.L("alien_chips");
        _data = data;
        _claims = data.List("claims").Cast<string>().ToHashSet(StringComparer.Ordinal);
        LastError = "";
        if (!currencyOnly)
            Changed?.Invoke();
        if (currencyChanged)
            CurrencyChanged?.Invoke();
        return true;
    }
    private bool Fail(string message)
    {
        LastError = message;
        return false;
    }
    private static bool ValidSite(string text) => long.TryParse(text, out long number) && number >= 0 && number <= MaxCurrency && SiteKey(number) == text;
    private static bool ValidSlots(object? value, DataMap levels, string layer, string kind)
    {
        if (value is not List<object?> slots || slots.Count != 2)
            return false;
        return slots.All(x => x is string id && (id.Length == 0 || (levels.I(id) > 0 && PerkCatalog.Compatible(id, layer, kind, "*")))) && (slots[0] is string first && first.Length == 0 || !Equals(slots[0], slots[1]));
    }
    public static DataMap? Validate(object? source)
    {
        if (source is not DataMap original)
            return null;
        var data = original.DeepClone();
        string[] keys = { "version", "alien_chips", "settings", "levels", "templates", "active_run", "sites", "claims", "aircraft_templates", "aircraft_sites", "berths", "advanced_unlocked", "known_intel" };
        if (data.Count != 13 || keys.Any(k => !data.ContainsKey(k)) || !DataMap.ValidNumber(data.Value("version"), ProfileVersion, ProfileVersion, true) || !DataMap.ValidNumber(data.Value("alien_chips"), 0, MaxCurrency, true) || data.Value("advanced_unlocked") is not bool)
            return null;
        if (data.Value("settings") is not DataMap settings || settings.Count != PerkCatalog.DefaultSettings.Count)
            return null;
        foreach (var key in PerkCatalog.DefaultSettings.Keys)
        {
            if (!settings.ContainsKey(key))
                return null;
            var bounds=PerkEffectRules.Bounds(key);double min=bounds.N("minimum"),max=bounds.N("maximum");bool whole=bounds.B("integer");
            if (!DataMap.ValidNumber(settings[key], min, max, whole))
                return null;
        }
        if (data.Value("levels") is not DataMap levels || levels.Count != PerkCatalog.Entries.Count)
            return null;
        foreach (var row in PerkCatalog.Entries)
        {
            string id = row.S("id");
            bool advanced = row.I("stage") > 0;
            if (!DataMap.ValidNumber(levels.Value(id), 0, advanced ? PerkEffectRules.Tier(1).I("max_level") : settings.I("max_level"), true) || (advanced && levels.I(id) > 0 && !data.B("advanced_unlocked")))
                return null;
            levels[id] = levels.L(id);
        }
        foreach (string field in new[] { "templates", "aircraft_templates" })
        {
            if (data.Value(field) is not DataMap templates || templates.Count != 3)
                return null;
            foreach (string kind in PerkCatalog.Kinds)
                if (!ValidSlots(templates.Value(kind), levels, field == "templates" ? "factory" : "aircraft", kind))
                    return null;
        }
        if (data.Value("active_run") is not string active || (active.Length > 0 && !ValidToken(active)))
            return null;
        var siteKinds = new Dictionary<string, string>();
        foreach (string field in new[] { "sites", "aircraft_sites", "berths" })
        {
            if (data.Value(field) is not DataMap records || records.Count > (field == "berths" ? MaxBerths : MaxSites) || (active.Length == 0 && records.Count > 0))
                return null;
            foreach (var (key, value) in records)
            {
                var parts = key.Split(':');
                if (parts.Length != (field == "berths" ? 2 : 1) || parts.Any(p => !ValidSite(p)) || (field == "berths" && long.Parse(parts[1]) > MaxBerthIndex))
                    return null;
                if (value is not DataMap row || row.Count != 2 || !PerkCatalog.Kinds.Contains(row.S("kind")) || !ValidSlots(row.Value("slots"), levels, field == "sites" ? "factory" : "aircraft", row.S("kind")))
                    return null;
                if (siteKinds.TryGetValue(parts[0], out var kind) && kind != row.S("kind"))
                    return null;
                siteKinds[parts[0]] = row.S("kind");
            }
        }
        if (data.Value("claims") is not List<object?> claims || claims.Count > MaxClaims || claims.Distinct().Count() != claims.Count || claims.Any(x => x is not string text || text.Length != 64 || text.Any(c => !"0123456789abcdef".Contains(c))))
            return null;
        if (data.Value("known_intel") is not List<object?> intel || intel.Count > 2 || intel.Distinct().Count() != intel.Count || intel.Any(x => x is not string id || id is not ("M1" or "L1")))
            return null;
        data["version"] = (long)ProfileVersion;
        data["alien_chips"] = data.L("alien_chips");
        data["claims"] = claims.Cast<string>().Order(StringComparer.Ordinal).Cast<object?>().ToList();
        return data;
    }
}
