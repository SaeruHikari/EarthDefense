using Earthward.Combat;
using Earthward.Domain;
using Godot;

internal static class UniformEnemyChecks
{
    public static void Run(Action<bool, string> check)
    {
        void Check(bool okay, string label) => check(okay, "uniform enemy: " + label);
        var catalog = CombatCatalog.Current;
        Check(catalog.RoleOrder.SequenceEqual(new[] { "claw" }) && catalog.Roles.Count == 1,
            "the live catalog exposes exactly one ordinary aircraft role");
        Check(catalog.Enemies.Keys.Order().SequenceEqual(new[] { "carrier", "scout" }),
            "the only non-aircraft enemy definition is the continuing invasion carrier");

        foreach (long wave in new long[] { 1, 3, 5, 10, 20, 28, 60, 120 })
        {
            var game = new DefenseState { Wave = wave - 1 };
            var battle = new Battlefield(game);
            battle.StartWave();
            var plan = battle.GetWaveSpawnPlan();
            long count = plan.L("planned_count");
            Check(count > 0 && plan.Map("composition").Count == 1 && plan.Map("composition").L("claw") == count,
                "wave " + wave + " assigns its complete ordinary aircraft budget");
            Check(!plan.ContainsKey("medium") && !plan.ContainsKey("stride"),
                "wave " + wave + " contains no removed variant scheduling state");
            battle.UpdateSpawning(plan.N("duration"));
            Check(battle.Enemies.Count == count && battle.WaveRemaining == 0,
                "wave " + wave + " physically spawns exactly its declared count");
            Check(battle.Enemies.All(enemy => enemy.S("kind") == "scout" && enemy.S("enemy_role_id") == "claw"
                && enemy.S("armor_type") == "light" && !enemy.ContainsKey("energy_hp")
                && !enemy.ContainsKey("boss_variant_id") && !enemy.ContainsKey("skill_phase")
                && !enemy.ContainsKey("elite") && !enemy.ContainsKey("cargo")),
                "wave " + wave + " spawns only ordinary light aircraft without special layers or abilities");
            Check(battle.Enemies.Select(enemy => enemy.N("max_hp")).Distinct().Count() == 1
                && battle.Enemies.Select(enemy => enemy.N("attack_damage")).Distinct().Count() == 1,
                "wave " + wave + " cannot turn a deployment ordinal into an elite variant");
            Check(battle.Enemies.All(enemy => enemy.S("front_id").StartsWith("front_", StringComparison.Ordinal)),
                "wave " + wave + " still launches aircraft from real invasion fronts");
        }

        var continuing = DefenseWavePlan.Build(60, 40, 45, 60, 2, 3);
        var entries = Enumerable.Range(0, 40).Select(index => DefenseWavePlan.Entry(continuing, index)).ToArray();
        Check(entries.Take(3).All(entry => entry.S("kind") == "carrier")
            && entries.Skip(3).All(entry => entry.S("kind") == "scout" && entry.S("role") == "claw"),
            "continuing front carriers consume three slots and the other thirty-seven are ordinary aircraft");
        Check(entries.Select(entry => entry.S("planned_uid")).Distinct().Count() == entries.Length,
            "every deployment keeps a unique stable reward identity");

        var probeGame = new DefenseState { Wave = 1 };
        var probeBattle = new Battlefield(probeGame);
        foreach (string removedKind in new[] { "small_boss", "boss", "cruiser" })
        {
            bool rejected = false;
            try { probeBattle.SpawnEnemy(removedKind, originOverride: Vector3.Back * (float)(CombatScale.EarthRadius + 3)); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && probeBattle.Enemies.Count == 0, "removed kind " + removedKind + " cannot re-enter the live roster");
        }
        var ordinary = probeBattle.SpawnEnemy("scout", originOverride: Vector3.Back * (float)(CombatScale.EarthRadius + 3))!;
        double initialHealth = ordinary.N("max_hp");
        probeGame.SetCombatSetting("enemy_health", probeGame.CombatSettings.N("enemy_health") * 2);
        probeBattle.RefreshEnemyHealth();
        Check(Math.Abs(ordinary.N("max_hp") - initialHealth * 2) < .00001 && !ordinary.ContainsKey("energy_hp"),
            "live health edits update the ordinary hull without fabricating energy layers");
    }
}
