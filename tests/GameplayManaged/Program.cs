using System.Reflection;
using Earthward;
using Earthward.Domain;
using Earthward.Combat;
using Godot;

CatalogData.Configure(Path.GetFullPath("data/domain"));
int checks = 0, failures = 0;
void Check(bool result, string label) { checks++; if (!result) { failures++; Console.WriteLine("GAMEPLAY_FAIL " + label); } }
void Near(double a, double b, string label) => Check(Math.Abs(a - b) < .00001, label + $" ({a}/{b})");
object? Invoke(object obj, string method, params object?[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(obj, args);
// Production reward path uses deterministic per-enemy rolls and batches permanent writes.
var chipGame = new DefenseState();
var chipOpening = chipGame.Serialize(); chipOpening["run_id"] = "alien-chip-gameplay-contract";
Check(chipGame.Restore(chipOpening), "bind deterministic chip reward run");
Near(PerkCatalog.AlienChipDropChance, .03, "ordinary aircraft default chip probability is three percent");
long chipEvents = 0; int chipNotifications = 0;
chipGame.AlienChipDropped += drop => { chipNotifications++; chipEvents += drop.L("alien_chips"); };
const int chipCandidates = 2000;
for (int i = 0; i < chipCandidates; i++)
    Check(chipGame.RewardEnemy(new() { ["kind"] = i % 2 == 0 ? "scout" : "cruiser", ["hp"] = 0d, ["reward_event_id"] = "chip-candidate:" + i, ["wave"] = 1L }), "real reward accepts unique destroyed aircraft " + i);
long pendingChips = chipGame.PendingAlienChipCount;
Check(pendingChips >= 30 && pendingChips <= 90 && chipGame.FactoryPerks.AlienChips == 0 && chipEvents == 0, "low probability rolls queue individual chips without synchronous profile writes");
Check(chipGame.FlushAlienChipDrops() && chipGame.PendingAlienChipCount == 0 && chipGame.FactoryPerks.AlienChips == pendingChips && chipEvents == pendingChips && chipNotifications == 1, "single batch flush awards exactly one chip per winning enemy and one combined notification");
Check(PerkCatalog.Entries.All(row => !chipGame.FactoryPerks.IsUnlocked(row.S("id"))), "aircraft kills never randomly unlock perks");
long earnedChips = chipGame.FactoryPerks.AlienChips;
Check(chipGame.Restore(chipOpening), "reload pre-kill run state while retaining permanent chip profile");
for (int i = 0; i < chipCandidates; i++)
    chipGame.RewardEnemy(new() { ["kind"] = i % 2 == 0 ? "scout" : "cruiser", ["hp"] = 0d, ["reward_event_id"] = "chip-candidate:" + i, ["wave"] = 1L });
Check(chipGame.FlushAlienChipDrops() && chipGame.FactoryPerks.AlienChips == earnedChips && chipEvents == earnedChips, "replayed kills cannot reroll or duplicate permanent chip rewards");
foreach (string excluded in new[] { "carrier", "medium_boss", "small_boss", "asteroid" })
    for (int i = 0; i < 150; i++)
        chipGame.RewardEnemy(new() { ["kind"] = excluded, ["hp"] = 0d, ["reward_event_id"] = "excluded:" + excluded + ":" + i, ["wave"] = 3L });
for (int i = 0; i < 150; i++)
{
    chipGame.RewardEnemy(new() { ["kind"] = "scout", ["hp"] = 1d, ["reward_event_id"] = "alive:" + i });
    chipGame.RewardEnemy(new() { ["kind"] = "scout", ["hp"] = 0d, ["resource_core_carrier"] = true, ["reward_event_id"] = "core-boss:" + i });
    chipGame.RewardEnemy(new() { ["kind"] = "cruiser", ["hp"] = 0d, ["post_carrier"] = true, ["reward_event_id"] = "post-carrier:" + i });
}
Check(chipGame.PendingAlienChipCount == 0 && chipGame.FlushAlienChipDrops() && chipGame.FactoryPerks.AlienChips == earnedChips, "bosses, motherships, asteroids, live aircraft, and disguised carriers cannot drop chips");
Check(PerkCatalog.Entries.All(row => !chipGame.FactoryPerks.IsUnlocked(row.S("id"))), "old medium-boss source no longer grants free perks");
chipGame.RewardWave(3);
Check(chipGame.FactoryPerks.AlienChips == earnedChips, "wave completion does not grant old permanent currency");
var game = new DefenseState();
foreach (string key in new[] { "minerals", "energy", "science", "resource_cores", "alien_points", "alien_chips" })
{
    double before = game.CheatResourceBalance(key);
    Check(game.AddResourcesCheat(key, 123), "grant " + key);
    Near(game.CheatResourceBalance(key), before + 123, "grant adds balance " + key);
    foreach (double invalid in new[] { -1, 0, double.NaN, double.PositiveInfinity })
    {
        Check(!game.AddResourcesCheat(key, invalid), "reject invalid " + key);
        Near(game.CheatResourceBalance(key), before + 123, "invalid preserves " + key);
    }
}
foreach (string key in new[] { "resource_cores", "alien_points", "alien_chips" }) Check(!game.AddResourcesCheat(key, .5), "whole currency " + key);
var beforeSave = game.Serialize().ToJson();
Check(!game.AddResourcesCheat("science", 100, () => false), "resource persistence failure rejected");
Check(beforeSave == game.Serialize().ToJson(), "resource persistence failure rolls back full run");
Check(!game.AddResourcesCheat("energy", 100, () => throw new IOException("simulated")), "resource save exception handled");
Check(beforeSave == game.Serialize().ToJson(), "resource exception rolls back full run");
Check(!game.UnlockAllTechnologyCheat(() => false), "all-research save failure rejected");
Check(beforeSave == game.Serialize().ToJson(), "all-research persistence failure rolls back full run");
var permanent = game.FactoryPerks.Snapshot().ToJson();
var balances = new[] { game.Minerals, game.Energy, game.Science, game.AlienPoints, game.ResourceCores };
Check(game.UnlockAllTechnologyCheat(), "complete all research at opening wave");
Check(DeepTechnology.Nodes.All(node => game.HasResearch(node.S("id"))), "every static node complete");
foreach (string branch in DeepTechnology.ChainBranches) Check(game.SuccessorCount(branch) == 0 && !game.HasResearch(branch + "_R00001") && !game.HasResearch(branch + "_R10000"), "cheat leaves continuation research untouched " + branch);
Check(game.Wave == 0 && game.CompletedWaves == 0, "all-research grant does not fabricate waves");
Check(permanent == game.FactoryPerks.Snapshot().ToJson(), "all-research grant leaves permanent Perks intact");
Check(balances.SequenceEqual(new[] { game.Minerals, game.Energy, game.Science, game.AlienPoints, game.ResourceCores }), "all-research costs no currency");
Check(new DefenseState().Restore(DataMap.Parse(game.Serialize().ToJson())), "opening-wave full research saves and restores");
string completedSave = game.Serialize().ToJson();
Check(game.UnlockAllTechnologyCheat(), "repeat full research command succeeds");
Check(completedSave == game.Serialize().ToJson(), "repeat command cannot stack effects");
game.Science = 100000; game.AlienPoints = 1000;
Check(game.PurchaseGroup("K_R00001") && game.PurchaseGroup("K_R00002") && game.PurchaseGroup("M_R00001"), "continuations remain available for manual research");
var paidEffects = game.TechEffects().DeepClone();
var paidState = game.Serialize().ToJson();
Check(game.UnlockAllTechnologyCheat() && game.Serialize().ToJson() == paidState, "existing paid continuation levels and balances are unchanged by cheat");
Check(game.SuccessorCount("K") == 2 && game.SuccessorCount("M") == 1 && game.SuccessorCount("L") == 0, "mixed manual continuation progress survives");
Check(DataMap.Equivalent(game.TechEffects(), paidEffects), "cheat neither adds nor removes continuation bonuses");
Check(!game.UnlockAllTechnologyCheat(() => false) && game.Serialize().ToJson() == paidState, "failed save preserves manual continuation progress");
Check(!game.UnlockAllTechnologyCheat(() => throw new IOException("simulated")) && game.Serialize().ToJson() == paidState, "save exception preserves manual continuation progress");
var paidReload = new DefenseState();
Check(paidReload.Restore(game.Serialize()) && paidReload.SuccessorCount("K") == 2 && paidReload.SuccessorCount("M") == 1 && DataMap.Equivalent(paidReload.TechEffects(), paidEffects), "manual continuation progress and effects survive reload after cheat");


// A new frontier is announced during a real, saved, one-cycle arrival countdown.
game = new DefenseState(); game.Expedition.SetEarthLiberated(true);
var battle = new Battlefield(game); var director = new DefenseCampaignDirector(game, battle);
var warning = director.GetFrontierWarning();
double defaultCycle = game.CombatSettings.N("enemy_wave_duration");
Near(warning.N("remaining"), 5 * defaultCycle, "initial peace remains five default waves");
Check(warning.B("pending") && warning.N("remaining") > warning.N("window_duration"), "warning not visible before last cycle");
director.Step(4 * defaultCycle + .05); warning = director.GetFrontierWarning();
Check(warning.N("remaining") < warning.N("window_duration") && warning.N("remaining") > defaultCycle - 1, "first new frontier enters warning window");
double remaining = warning.N("remaining"); director.Paused = true; director.Step(20);
Near(director.GetFrontierWarning().N("remaining"), remaining, "paused countdown stays still");
director.Paused = false; director.SpeedScale = 2; director.Step(1);
Near(director.GetFrontierWarning().N("remaining"), remaining - 2, "arrival follows game speed");
director.SpeedScale = 1; director.Step(defaultCycle + 5);
Check(battle.WaveRunning && !director.GetFrontierWarning().B("pending"), "first arrival clears warning");
for (int stage = 1; stage <= 3; stage++)
{
    battle.Enemies.Clear();
    typeof(Battlefield).GetField("_fixedRunning", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, false);
    typeof(Battlefield).GetProperty("WaveRunning")!.SetValue(battle, false);
    Invoke(director, "WaveCompleted", battle.PostPlan.S("id"));
    warning = director.GetFrontierWarning();
    if (stage < 3)
    {
        Check(warning.B("pending") && warning.I("next_stage") == stage + 1, "new frontier warning stage " + (stage + 1));
        Near(warning.N("remaining"), defaultCycle, "whole cycle offered before relocated fleet");
        Near(warning.N("target_radius"), stage == 1 ? 88 : 128, "warning uses actual next radius");
        director.Step(defaultCycle - .1);
        Check(!battle.WaveRunning && director.GetFrontierWarning().B("pending"), "no early new fleet");
        director.Step(.2);
        Check(battle.WaveRunning && !director.GetFrontierWarning().B("pending"), "relocated fleet arrives at deadline");
    }
    else Check(!warning.B("pending") && director.GetStatus().N("earth_remaining") == 0, "final frontier reinforcements do not announce a nonexistent relocation");
}

// Shared missile count affects both actual firing and assignment damage estimates.
game = new DefenseState { Minerals = 1e8, Energy = 1e8, Science = 1e8, AlienPoints = 10000, Wave = 40, CompletedWaves = 40 };
void Buy(string id) { foreach (string parent in DeepTechnology.Definition(id).List("requires").Cast<string>()) if (!game.HasResearch(parent)) Buy(parent); Check(game.PurchaseGroup(id), "purchase " + id); }
Buy("M_N1");
var surface = new MissileSurface(); battle = new Battlefield(game, surface) { Active = true };
var drone = battle.SpawnFactoryDrone(battle.Factories[0]);
var enemy = battle.SpawnEnemy("scout", new() { ["wave"] = 1L, ["role"] = "claw", ["index"] = 0 }, Vector3.Back * (WorldScale.EarthRadius + 2))!;
enemy["hp"] = 100000d;
Vector3 origin = Vector3.Back * (WorldScale.EarthRadius + .7f);
drone["space_position"] = origin; drone["normal"] = Vector3.Back; drone["launch_age"] = 10d; drone["state"] = "engaging";
drone["aim_target_uid"] = enemy.L("uid"); drone["aim_direction"] = Vector3.Back; drone["aim_up"] = Vector3.Up; drone["fire"] = 0d;
battle.WorldCache[drone.L("uid")] = origin;
Invoke(battle, "UpdateFiring", .01);
int initialShots = battle.Shots.Count;
Check(initialShots == 1, "actual base missile fires one projectile");
Buy("M_N4"); battle.GetFactoryCoverage(0); battle.Shots.Clear(); drone["fire"] = 0d;
Invoke(battle, "UpdateFiring", .01);
Check(battle.Shots.Count == 2, "actual researched missile fires two projectiles");
Check(battle.Shots.All(shot => shot.B("missile") && shot.N("damage") > 0), "volley packets are damaging guided missiles");
var oldWeapons = battle.DroneWeaponStats(drone); double oldRadius = oldWeapons.N("missile_blast_radius"), oldSpeed = oldWeapons.N("missile_speed_multiplier"), oldRange = battle.GetFactoryCoverage(0)!.Bands[0].WeaponRange;
Buy("M_S21"); Buy("M_S22"); Buy("M_S23"); var coverage = battle.GetFactoryCoverage(0)!; var weapons = battle.DroneWeaponStats(drone);
var packet = battle.MakeShot(origin, enemy.Vector3("space_position"), 10, true, 0, weapons);
Check(packet.N("blast_radius") > oldRadius && packet.N("projectile_speed") > oldSpeed * 3.6 && coverage.Bands[0].WeaponRange > oldRange, "three small nodes reach actual blast, flight and lock-range values");
OpeningCombatChecks.Run(Check);
Console.WriteLine($"GAMEPLAY_CHECKS: {checks} checks, {failures} failures");
System.Environment.ExitCode = failures == 0 ? 0 : 1;

internal sealed class MissileSurface : ICombatSurface
{
    public IReadOnlyList<DataMap> GetFactorySites() => new DataMap[] { new() { ["site_id"] = 0L, ["kind"] = "missile", ["normal"] = Vector3.Back, ["launch_position"] = Vector3.Back * (WorldScale.EarthRadius + .108f), ["launch_direction"] = Vector3.Back } };
    public Vector3 SurfaceToSpace(Vector3 normal, double altitude) => normal * (float)(WorldScale.EarthRadius + altitude);
    public Vector3 SpaceToSurface(Vector3 position) => position.Normalized();
    public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals() => new Vector3[] { Vector3.Back };
}
