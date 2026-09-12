using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

/// <summary>Validated immutable combat balance. CSV parsing never occurs in a tick.
/// CatalogData.Revision changes only when a host explicitly selects a data source.</summary>
public static class CombatCatalog
{
    public sealed record EnemyDefinition(string Id, string Kind, string Role, string BossVariant, string Name, string Armor, long MinimumWave, double Health, double Dps, double Speed, double Cooldown, double Energy);
    public sealed record EnemyKind(string Id, double Size, double BombardSeconds, double LegacySpeed, double DroneCooldown, double EarthCooldown);
    public sealed record FrontDefinition(string Id, int Ordinal, string Name, Color Color, Vector3 Direction, long UnlockWave);
    public sealed record WaveComposition(long From, long Through, DataMap Weights);
    public sealed class Tuning
    {
        public double HealthBase { get; }
        public double HealthSettingReference { get; }
        public double DamageBase { get; }
        public double OpeningDamageMultiplier { get; }
        public double OpeningDamageHoldThroughWave { get; }
        public double OpeningDamageFullWave { get; }
        public double TacticalSpeedBase { get; }
        public double EliteFirstWave { get; }
        public double EliteInterval { get; }
        public double EliteOrdinal { get; }
        public double EliteHealthMultiplier { get; }
        public double EliteDamageMultiplier { get; }
        public double GroundDamageFloor { get; }
        public double GroundDamageMultiplier { get; }
        public double SmallBossSpawnFraction { get; }
        public double MediumBossSpawnFraction { get; }
        public double MotherAltitude { get; }
        public double SpawnConeMin { get; }
        public double SpawnConeMax { get; }
        public double SpawnMotherClearance { get; }
        public double FrontierLateralRadius { get; }
        public double FrontierLateralMinimum { get; }
        public double FrontierForwardMin { get; }
        public double FrontierForwardMax { get; }
        public double ContinuingVolleyCount { get; }
        public double ContinuingHangarInterval { get; }
        public double ContinuingCarrierHealthMultiplier { get; }
        public double LegacySpawnSpeedBase { get; }
        public double LegacySpawnSpeedWaveGrowth { get; }
        public double ArmorExposedMultiplier { get; }
        public double WeaverCooldown { get; }
        public double WeaverRange { get; }
        public double WeaverMaxLinks { get; }
        public double WeaverShieldFraction { get; }
        public double WeaverRecoveryBudget { get; }
        public double WeaverRecoveryFraction { get; }
        public double JammerCooldown { get; }
        public double JammerDuration { get; }
        public double JammerFireRateMultiplier { get; }
        public double HeavyChargeSeconds { get; }
        public double LightChargeSeconds { get; }
        public double ForgeExposureSeconds { get; }
        public double SporeProjectileSpeedMultiplier { get; }
        internal Tuning(CsvTable table)
        {
            table.RequireHeaders("id", "value", "minimum", "maximum", "description");
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                double value = row.Number("value"), min = row.Number("minimum"), max = row.Number("maximum");
                if (min > max || value < min || value > max) throw row.Error("value", "outside declared range");
                if (!values.TryAdd(row.String("id"), value)) throw row.Error("id", "duplicate parameter");
            }
            double Get(string id) => values.TryGetValue(id, out var value) ? value : throw new InvalidDataException(table.SourceName + ": missing parameter " + id);
            HealthBase = Get("HealthBase");
            HealthSettingReference = Get("HealthSettingReference");
            DamageBase = Get("DamageBase");
            OpeningDamageMultiplier = Get("OpeningDamageMultiplier");
            OpeningDamageHoldThroughWave = Get("OpeningDamageHoldThroughWave");
            OpeningDamageFullWave = Get("OpeningDamageFullWave");
            TacticalSpeedBase = Get("TacticalSpeedBase");
            EliteFirstWave = Get("EliteFirstWave");
            EliteInterval = Get("EliteInterval");
            EliteOrdinal = Get("EliteOrdinal");
            EliteHealthMultiplier = Get("EliteHealthMultiplier");
            EliteDamageMultiplier = Get("EliteDamageMultiplier");
            GroundDamageFloor = Get("GroundDamageFloor");
            GroundDamageMultiplier = Get("GroundDamageMultiplier");
            SmallBossSpawnFraction = Get("SmallBossSpawnFraction");
            MediumBossSpawnFraction = Get("MediumBossSpawnFraction");
            MotherAltitude = Get("MotherAltitude");
            SpawnConeMin = Get("SpawnConeMin");
            SpawnConeMax = Get("SpawnConeMax");
            SpawnMotherClearance = Get("SpawnMotherClearance");
            FrontierLateralRadius = Get("FrontierLateralRadius");
            FrontierLateralMinimum = Get("FrontierLateralMinimum");
            FrontierForwardMin = Get("FrontierForwardMin");
            FrontierForwardMax = Get("FrontierForwardMax");
            ContinuingVolleyCount = Get("ContinuingVolleyCount");
            ContinuingHangarInterval = Get("ContinuingHangarInterval");
            ContinuingCarrierHealthMultiplier = Get("ContinuingCarrierHealthMultiplier");
            LegacySpawnSpeedBase = Get("LegacySpawnSpeedBase");
            LegacySpawnSpeedWaveGrowth = Get("LegacySpawnSpeedWaveGrowth");
            ArmorExposedMultiplier = Get("ArmorExposedMultiplier");
            WeaverCooldown = Get("WeaverCooldown");
            WeaverRange = Get("WeaverRange");
            WeaverMaxLinks = Get("WeaverMaxLinks");
            WeaverShieldFraction = Get("WeaverShieldFraction");
            WeaverRecoveryBudget = Get("WeaverRecoveryBudget");
            WeaverRecoveryFraction = Get("WeaverRecoveryFraction");
            JammerCooldown = Get("JammerCooldown");
            JammerDuration = Get("JammerDuration");
            JammerFireRateMultiplier = Get("JammerFireRateMultiplier");
            HeavyChargeSeconds = Get("HeavyChargeSeconds");
            LightChargeSeconds = Get("LightChargeSeconds");
            ForgeExposureSeconds = Get("ForgeExposureSeconds");
            SporeProjectileSpeedMultiplier = Get("SporeProjectileSpeedMultiplier");
            var known = new HashSet<string>(new[] { "OpeningDamageMultiplier", "OpeningDamageHoldThroughWave", "OpeningDamageFullWave", "HealthBase", "HealthSettingReference", "DamageBase", "TacticalSpeedBase", "EliteFirstWave", "EliteInterval", "EliteOrdinal", "EliteHealthMultiplier", "EliteDamageMultiplier", "GroundDamageFloor", "GroundDamageMultiplier", "SmallBossSpawnFraction", "MediumBossSpawnFraction", "MotherAltitude", "SpawnConeMin", "SpawnConeMax", "SpawnMotherClearance", "FrontierLateralRadius", "FrontierLateralMinimum", "FrontierForwardMin", "FrontierForwardMax", "ContinuingVolleyCount", "ContinuingHangarInterval", "ContinuingCarrierHealthMultiplier", "LegacySpawnSpeedBase", "LegacySpawnSpeedWaveGrowth", "ArmorExposedMultiplier", "WeaverCooldown", "WeaverRange", "WeaverMaxLinks", "WeaverShieldFraction", "WeaverRecoveryBudget", "WeaverRecoveryFraction", "JammerCooldown", "JammerDuration", "JammerFireRateMultiplier", "HeavyChargeSeconds", "LightChargeSeconds", "ForgeExposureSeconds", "SporeProjectileSpeedMultiplier" }, StringComparer.Ordinal);
            foreach (var row in table.Rows) if (!known.Contains(row.String("id"))) throw row.Error("id", "unknown tuning parameter");
            foreach (double integer in new[] {EliteFirstWave,EliteInterval,EliteOrdinal,ContinuingVolleyCount,WeaverMaxLinks})
                if (Math.Floor(integer) != integer) throw new InvalidDataException(table.SourceName + ": count parameters require integers");
            if (EliteOrdinal >= EliteInterval || MediumBossSpawnFraction < SmallBossSpawnFraction || SpawnConeMin > SpawnConeMax || FrontierForwardMin > FrontierForwardMax)
                throw new InvalidDataException(table.SourceName + ": inconsistent paired parameters");
            if (OpeningDamageMultiplier <= 0 || OpeningDamageMultiplier > 1 || OpeningDamageHoldThroughWave < 1
                || OpeningDamageFullWave <= OpeningDamageHoldThroughWave
                || Math.Floor(OpeningDamageHoldThroughWave) != OpeningDamageHoldThroughWave
                || Math.Floor(OpeningDamageFullWave) != OpeningDamageFullWave)
                throw new InvalidDataException(table.SourceName + ": invalid opening damage ramp");
        }
        public double DamageMultiplierForWave(long wave)
        {
            double progress = Math.Clamp((wave - OpeningDamageHoldThroughWave) / (OpeningDamageFullWave - OpeningDamageHoldThroughWave), 0, 1);
            return OpeningDamageMultiplier + (1 - OpeningDamageMultiplier) * progress;
        }
    }
    public sealed class Data
    {
        public readonly Tuning Values;
        public readonly Dictionary<string, EnemyDefinition> Roles = new(StringComparer.Ordinal);
        public readonly Dictionary<string, EnemyDefinition[]> Variants = new(StringComparer.Ordinal);
        public readonly Dictionary<string, EnemyKind> Kinds = new(StringComparer.Ordinal);
        public readonly WaveComposition[] Composition;
        public readonly FrontDefinition[] Fronts;
        public readonly long[] UnlockWaves;
        public readonly Vector3[] LocalDirections;
        public readonly double[] StageHealth = new double[4], StageDamage = new double[4];
        public readonly double[,] Armor = new double[3,3];
        public readonly string[] RoleOrder;
        internal Data()
        {
            Values = new(CatalogData.ReadCsv("combat_tuning.csv"));
            var enemies = CatalogData.ReadCsv("combat_enemies.csv");
            enemies.RequireHeaders("id","kind","role_id","boss_variant_id","name","armor","minimum_wave","health_multiplier","dps_multiplier","speed_multiplier","attack_cooldown","energy_ratio");
            var ids = new HashSet<string>(); var variants = new Dictionary<string,List<EnemyDefinition>>();
            foreach (var row in enemies.Rows)
            {
                var value = new EnemyDefinition(row.String("id"),row.String("kind"),row.String("role_id"),row.String("boss_variant_id"),row.String("name"),row.String("armor"),row.Integer("minimum_wave"),row.Number("health_multiplier"),row.Number("dps_multiplier"),row.Number("speed_multiplier"),row.Number("attack_cooldown"),row.Number("energy_ratio"));
                if (!ids.Add(value.Id) || value.Id.Length==0) throw row.Error("id","empty or duplicate enemy definition");
                if (value.Armor is not ("light" or "heavy") || value.MinimumWave < 1 || value.Health <= 0 || value.Dps < 0 || value.Speed <= 0 || value.Cooldown <= 0 || value.Energy < 0) throw row.Error("id","invalid enemy balance");
                if (value.Role.Length > 0)
                {
                    if (value.Kind is not ("scout" or "cruiser") || !Roles.TryAdd(value.Role,value)) throw row.Error("role_id","duplicate or unsupported role kind");
                }
                else
                {
                    if (value.Kind is not ("small_boss" or "boss" or "carrier")) throw row.Error("kind","unsupported enemy variant kind");
                    if (value.Kind=="boss" && value.BossVariant is not ("brood" or "forge" or "prism")) throw row.Error("boss_variant_id","unknown boss model/behavior");
                    if (!variants.TryGetValue(value.Kind,out var list)) variants[value.Kind]=list=new();
                    list.Add(value);
                }
            }
            RoleOrder = Roles.Keys.ToArray();
            var expectedRoles = new[]{"claw","needle","rock","siege","prism","weaver","hatcher","jammer"};
            if (!RoleOrder.SequenceEqual(expectedRoles)) throw new InvalidDataException(enemies.SourceName+": role ordering or required role IDs changed; these IDs map to existing models and behaviors");
            foreach (string kind in new[]{"small_boss","boss","carrier"})
            {
                if (!variants.TryGetValue(kind,out var list)) throw new InvalidDataException(enemies.SourceName+": missing variants for "+kind);
                var sorted = list.OrderBy(v=>v.MinimumWave).ToArray();
                if (sorted[0].MinimumWave!=1 || sorted.Select(v=>v.MinimumWave).Distinct().Count()!=sorted.Length) throw new InvalidDataException(enemies.SourceName+": variants must start at wave 1 with distinct thresholds for "+kind);
                Variants[kind]=sorted;
            }
            var kinds = CatalogData.ReadCsv("combat_enemy_kinds.csv");
            kinds.RequireHeaders("id","base_size","bombard_seconds","legacy_speed_multiplier","drone_fire_cooldown","earth_fire_cooldown");
            foreach (var row in kinds.Rows)
            {
                var value = new EnemyKind(row.String("id"),row.Number("base_size"),row.Number("bombard_seconds"),row.Number("legacy_speed_multiplier"),row.Number("drone_fire_cooldown"),row.Number("earth_fire_cooldown"));
                if (value.Size<=0 || value.BombardSeconds<=0 || value.LegacySpeed<=0 || value.DroneCooldown<=0 || value.EarthCooldown<=0 || !Kinds.TryAdd(value.Id,value)) throw row.Error("id","invalid or duplicate enemy kind");
            }
            foreach(string kind in new[]{"scout","cruiser","small_boss","boss","carrier","default"}) if(!Kinds.ContainsKey(kind))throw new InvalidDataException(kinds.SourceName+": missing kind "+kind);
            var stages=CatalogData.ReadCsv("combat_defense_stages.csv");stages.RequireHeaders("stage","health_multiplier","damage_multiplier");var seen=new HashSet<long>();
            foreach(var row in stages.Rows)
            {
                long stage=row.Integer("stage");double hp=row.Number("health_multiplier"),damage=row.Number("damage_multiplier");
                if(stage<0 || stage>3 || !seen.Add(stage) || hp<=0 || damage<=0)throw row.Error("stage","expected unique stages 0 through 3 and positive multipliers");
                StageHealth[stage]=hp;StageDamage[stage]=damage;
            }
            if(seen.Count!=4)throw new InvalidDataException(stages.SourceName+": all four reach stages are required");
            var compositions=CatalogData.ReadCsv("combat_wave_composition.csv");compositions.RequireHeaders(new[]{"from_wave","through_wave"}.Concat(RoleOrder).ToArray());var ranges=new List<WaveComposition>();long next=1;
            foreach(var row in compositions.Rows)
            {
                long from=row.Integer("from_wave"),through=row.Integer("through_wave");var weights=new DataMap();
                if(from!=next || through!=0 && through<from)throw row.Error("from_wave","wave ranges must be contiguous, ordered, and end with through_wave=0");
                foreach(string role in RoleOrder)if(row.String(role).Length>0){double weight=row.Number(role);if(weight<0)throw row.Error(role,"weight cannot be negative");weights[role]=weight;}
                if(weights.Values.Sum(v=>DataMap.Number(v))<=0)throw row.Error("from_wave","at least one positive role weight is required");
                ranges.Add(new(from,through,weights));next=through==0?-1:through+1;
            }
            if(next!=-1)throw new InvalidDataException(compositions.SourceName+": final range must have through_wave=0");Composition=ranges.ToArray();
            var fronts=CatalogData.ReadCsv("combat_fronts.csv");fronts.RequireHeaders("id","ordinal","name","color","direction_x","direction_y","direction_z","unlock_wave");var frontList=new List<FrontDefinition>();
            foreach(var row in fronts.Rows)
            {
                int ordinal=checked((int)row.Integer("ordinal"));long wave=row.Integer("unlock_wave");string color=row.String("color");var direction=new Vector3((float)row.Number("direction_x"),(float)row.Number("direction_y"),(float)row.Number("direction_z"));
                if(ordinal!=frontList.Count+1 || row.String("id")!=$"front_{ordinal:00}" || wave<1 || frontList.Count>0 && wave<frontList[^1].UnlockWave || !direction.IsFinite() || direction.LengthSquared()<.0001 || color.Length!=6 || !color.All(Uri.IsHexDigit))throw row.Error("id","invalid front ID, ordered wave, direction, or RGB color");
                frontList.Add(new(row.String("id"),ordinal,row.String("name"),new Color(color),direction,wave));
            }
            if(frontList.Count!=8 || frontList[0].UnlockWave!=1)throw new InvalidDataException(fronts.SourceName+": eight stable fronts are required and the first opens at wave 1");Fronts=frontList.ToArray();UnlockWaves=Fronts.Select(f=>f.UnlockWave).ToArray();LocalDirections=Fronts.Select(f=>f.Direction).ToArray();
            var armor=CatalogData.ReadCsv("combat_armor.csv");armor.RequireHeaders("layer","weapon_family","damage_multiplier");var pairs=new HashSet<(int,int)>();
            foreach(var row in armor.Rows)
            {
                int layer=LayerIndex(row.String("layer")),family=FamilyIndex(row.String("weapon_family"));double damage=row.Number("damage_multiplier");
                if(layer<0 || family<0 || damage<0 || !pairs.Add((layer,family)))throw row.Error("damage_multiplier","unknown or duplicate layer/family, or negative multiplier");Armor[layer,family]=damage;
            }
            if(pairs.Count!=9)throw new InvalidDataException(armor.SourceName+": all nine armor/weapon interactions are required");
        }
        public EnemyDefinition Enemy(string kind,string role,long wave)
        {
            if(kind=="mothership")kind="carrier";
            if(Variants.TryGetValue(kind,out var list)) {for(int i=list.Length-1;i>=0;i--)if(wave>=list[i].MinimumWave)return list[i];return list[0];}
            return Roles.GetValueOrDefault(role,Roles["claw"]);
        }
        public EnemyKind Kind(string kind)=>Kinds.GetValueOrDefault(kind,Kinds["default"]);
    }
    private static long _revision=long.MinValue;
    private static Data? _current;
    public static Data Current
    {
        get
        {
            if(_revision!=CatalogData.Revision || _current==null){var next=new Data();_current=next;_revision=CatalogData.Revision;}
            return _current;
        }
    }
    public static void Validate()=>_ = Current;
    public static int LayerIndex(string layer)=>layer switch{"light"=>0,"heavy"=>1,"energy"=>2,_=>-1};
    public static int FamilyIndex(string family)=>family switch{"kinetic"=>0,"explosive"=>1,"beam"=>2,_=>-1};
}
