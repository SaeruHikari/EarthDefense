using Earthward.Domain;
using Earthward.Combat;
using Godot;

/// <summary>A reactive novice-player sample; not a complete balance proof.</summary>
internal static class NaturalOpening
{
    private sealed class Surface : ICombatSurface
    {
        public readonly List<DataMap> Factories = new(), Shields = new();
        private long _nextSite = 1;
        public long SpatialRevision { get; private set; }
        public IReadOnlyList<DataMap> GetFactorySites() => Factories;
        public IReadOnlyList<DataMap> GetShieldSites() => Shields;
        public Vector3 SurfaceToSpace(Vector3 n, double altitude) => n.Normalized() * (float)(CombatScale.EarthRadius + altitude);
        public Vector3 SpaceToSurface(Vector3 p) => p.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals() => Factories.Concat(Shields).Select(s => s.Vector3("normal")).ToArray();
        public bool TryGetCoordinateFrame(out Transform3D frame) { frame = Transform3D.Identity; return true; }
        public void Add(string kind, Vector3 normal)
        {
            normal = normal.Normalized();
            var tangent = Vector3.Up.Cross(normal).Normalized();
            if (tangent.LengthSquared() < .001) tangent = Vector3.Right;
            var site = new DataMap { ["site_id"] = _nextSite++, ["kind"] = kind, ["normal"] = normal,
                ["launch_position"] = SurfaceToSpace(normal, .108), ["launch_direction"] = tangent };
            (kind == "shield" ? Shields : Factories).Add(site);
            SpatialRevision++;
        }
    }

    private sealed class Encounter
    {
        public string Id = "";
        public long Uid;
        public Vector3 Direction, LastImpact;
        public double SpawnAt, FirstHit = -1, SecondHit = -1, ReactionAt = -1, DefenseAt = -1;
        public double Damage, DamageBeforeDefense;
        public int Hits;
        public DataMap Report() => new() {
            ["front_id"] = Id, ["mother_uid"] = Uid, ["spawn_seconds"] = SpawnAt,
            ["first_attack_seconds"] = FirstHit, ["second_attack_seconds"] = SecondHit,
            ["first_reaction_seconds"] = ReactionAt, ["first_defense_seconds"] = DefenseAt,
            ["reaction_delay_after_second_hit"] = ReactionAt >= 0 && SecondHit >= 0 ? ReactionAt - SecondHit : -1,
            ["defense_delay_after_first_hit"] = DefenseAt >= 0 && FirstHit >= 0 ? DefenseAt - FirstHit : -1,
            ["defense_delay_after_spawn"] = DefenseAt >= 0 ? DefenseAt - SpawnAt : -1,
            ["earth_hits"] = Hits, ["at_least_two_real_hits"] = Hits >= 2,
            ["earth_damage"] = Damage, ["earth_damage_before_first_defense"] = DamageBeforeDefense };
    }

    // Prerequisites are resolved through the real current CSV, never force-unlocked.
    private static readonly string[] ResearchPlan = { "K_S01", "K_S02", "I_S03", "K_S21", "M_N1", "I_S23",
        "K_S06", "K_S22", "I_S08", "L_N1", "M_S01", "M_S02", "K_S26", "K_S07", "D_S01", "D_S02" };

    public static void Run()
    {
        var runs = new List<object?>();
        foreach (int seed in new[] { 11, 9918, 49127 }) runs.Add(Play(seed));
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/opening-novice-playthrough.json", new DataMap {
            ["sample"] = "Three reactive novice players. New directions are reinforced only after two real Earth hits and a 10-20 second delayed reaction; operations every 10 seconds. No resource grants, perks, achievements or damage overrides.",
            ["impact_attribution"] = "Actual EarthDamaged positions assigned to the nearest live mothership direction; no fabricated hits. Buildings remain fixed; planet rotation is not simulated.",
            ["duration_seconds"] = 600d, ["simulation_hz"] = 30L, ["paid_research_minimum_interval_seconds"] = 60d,
            ["action_interval_seconds"] = 10d, ["reaction_delay_after_two_hits_min_seconds"] = 10d,
            ["reaction_delay_after_two_hits_max_seconds"] = 20d,
            ["first_hit_guide_reading_paused_seconds"] = 10d,
            ["launch_seconds"] = 30d, ["earth_radius"] = CombatScale.EarthRadius, ["runs"] = runs }.ToJson());
        int failures = runs.Cast<DataMap>().Count(r => !r.B("survived") || !r.B("legitimate_run") || !r.B("all_new_fronts_hit_earth_twice"));
        Console.WriteLine($"NATURAL_OPENING runs={runs.Count} failures={failures}");
        System.Environment.ExitCode = failures == 0 ? 0 : 1;
    }

    private static DataMap Play(int seed)
    {
        var game = new DefenseState();
        var initial = game.Serialize(); initial["run_id"] = $"opening-novice-{seed}";
        if (!game.Restore(initial)) throw new InvalidOperationException("Fresh run restore failed.");
        if (game.Science != 0 || game.Rates().N("science") != 0 || game.Achievements.UnlockedCount != 0
            || PerkCatalog.Entries.Any(r => game.FactoryPerks.IsUnlocked(r.S("id"))))
            throw new InvalidOperationException("Requires default zero-science, no-perk, no-achievement state.");
        var surface = new Surface(); surface.Add("interceptor", Vector3.Back);
        var battle = new Battlefield(game, surface); battle.Random.Seed = (ulong)seed;
        var purchases = new List<object?>(); var buildings = new List<object?>(); var waves = new List<object?>();
        double elapsed = 0, playerTime = 0, nextAction = 0, lastPaid = -60, damageReceived = 0, firstImpactAt = -1, satelliteAt = -1;
        double guideResumeAt = -1, guidePauseDuration = 0;
        var encounters = new Dictionary<long, Encounter>();
        var hitLog = new List<object?>();
        var queueLog = new List<object?>();
        Encounter? reactingTo = null;
        int impacts = 0, peakEnemies = 0, peakDrones = 0; long lastWave = 0;

        bool Build(string kind, Vector3 normal)
        {
            if (!game.CanBuild(kind) || !game.Build(kind)) return false;
            if (kind is "interceptor" or "missile" or "laser" or "shield") surface.Add(kind, normal);
            buildings.Add(new DataMap { ["kind"] = kind, ["seconds"] = Math.Round(elapsed, 2), ["wave"] = game.Wave,
                ["minerals_after"] = game.Minerals, ["energy_after"] = game.Energy,
                ["front_id"] = reactingTo?.Id ?? "opening",
                ["normal"] = new List<object?> { (double)normal.X, (double)normal.Y, (double)normal.Z } });
            if (reactingTo != null && kind is "interceptor" or "missile" or "laser" or "shield" && reactingTo.DefenseAt < 0)
                reactingTo.DefenseAt = elapsed;
            return true;
        }
        bool Buy(string id, bool tutorial = false)
        {
            if (!game.GetGroupStatus(id).B("can_purchase")) return false;
            double before = game.Science;
            if (!game.PurchaseGroup(id)) return false;
            double paid = before - game.Science; if (paid > 0) lastPaid = elapsed;
            purchases.Add(new DataMap { ["id"] = id, ["name"] = DeepTechnology.Definition(id).S("name"),
                ["seconds"] = Math.Round(elapsed, 2), ["wave"] = game.Wave, ["science_paid"] = paid,
                ["science_after"] = game.Science, ["tutorial"] = tutorial });
            return true;
        }
        bool TryPath(string id)
        {
            if (game.HasResearch(id)) return false;
            foreach (string parent in DeepTechnology.Definition(id).List("requires").Cast<string>())
                if (!game.HasResearch(parent)) return TryPath(parent);
            return Buy(id);
        }
        void RecordMothers()
        {
            foreach (var mother in battle.Motherships.Values)
            {
                long uid = mother.L("uid");
                if (!encounters.ContainsKey(uid)) encounters[uid] = new Encounter {
                    Id = mother.S("front_id"), Uid = uid, Direction = mother.Vector3("space_position").Normalized(), SpawnAt = elapsed };
            }
        }
        Vector3 Placement(string family, Encounter front)
        {
            var n = front.LastImpact;
            int count = surface.Factories.Count(s => s.S("kind") == family && s.Vector3("normal").Dot(n) > .88f);
            return n.Rotated(Vector3.Up, (float)((count % 2 == 0 ? 1 : -1) * (.075 + .035 * count))).Normalized();
        }
        string DefenseAction(Encounter front)
        {
            if (game.HasResearch("D_N4") && game.CanBuild("shield")
                && !surface.Shields.Any(s => s.Vector3("normal").Dot(front.LastImpact) > .92f)) return "shield";
            if (surface.Factories.Count(s => s.Vector3("normal").Dot(front.LastImpact) > .88f) >= 4) return "";
            foreach (string family in new[] { "missile", "laser", "interceptor" })
            {
                int desired = family == "interceptor" ? 3 : 1;
                if (game.CanBuild(family) && surface.Factories.Count(s => s.S("kind") == family
                    && s.Vector3("normal").Dot(front.LastImpact) > .88f) < desired) return family;
            }
            return "";
        }
        void Act()
        {
            if (game.EarthHp <= 0) return;
            // A novice reacts only to damage they experienced. No advance
            // construction at a newly revealed direction or its future sites.
            var candidates = encounters.Values.Where(f => f.Hits >= 2 && elapsed - f.SecondHit >= 10)
                .OrderBy(f => f.DefenseAt >= 0).ThenBy(f => f.SpawnAt).ToArray();
            // Eligibility and execution use exactly the same predicate. A
            // three-interceptor direction must not monopolize this queue while
            // its fourth (missile) factory is still locked or unaffordable.
            reactingTo = candidates.FirstOrDefault(f => DefenseAction(f).Length > 0);
            if (candidates.Any(f => DefenseAction(f).Length == 0)) queueLog.Add(new DataMap {
                ["seconds"] = elapsed, ["chosen_front"] = reactingTo?.Id ?? "",
                ["skipped_unactionable_fronts"] = candidates.Where(f => DefenseAction(f).Length == 0)
                    .Select(f => (object?)f.Id).ToList(),
                ["missile_unlocked"] = game.BuildingUnlocked("missile"),
                ["shield_cooldown_remaining"] = game.LocalShieldBuildCooldownRemaining,
                ["minerals"] = game.Minerals, ["energy"] = game.Energy });
            if (reactingTo != null)
            {
                if (reactingTo.ReactionAt < 0) reactingTo.ReactionAt = elapsed;
                string action = DefenseAction(reactingTo);
                if (!Build(action, action == "shield" ? reactingTo.LastImpact : Placement(action, reactingTo)))
                    throw new InvalidOperationException("Executable novice build action unexpectedly failed.");
                reactingTo = null;
                return;
            }
            reactingTo = null;
            if (elapsed - lastPaid >= 60 - 1e-6)
                foreach (string id in ResearchPlan) if (TryPath(id)) return;
            foreach (string family in new[] { "mine", "solar" })
                if (game.Buildings.I(family) < Math.Min(4, 1 + game.Wave / 3)
                    && game.Minerals >= game.BuildingCost(family).N("minerals") + 80
                    && game.Energy >= game.BuildingCost(family).N("energy") + 40)
                    if (Build(family, Vector3.Back)) return;
        }

        battle.EarthDamaged += (damage, at) => {
            impacts++; damageReceived += damage;
            RecordMothers();
            var normal = surface.SpaceToSurface(at);
            var front = encounters.Values.OrderByDescending(f => f.Direction.Dot(normal)).First();
            front.Hits++; front.Damage += damage; front.LastImpact = normal;
            if (front.DefenseAt < 0) front.DamageBeforeDefense += damage;
            if (front.FirstHit < 0) front.FirstHit = elapsed;
            if (front.Hits == 2) front.SecondHit = elapsed;
            hitLog.Add(new DataMap { ["front_id"] = front.Id, ["mother_uid"] = front.Uid,
                ["seconds"] = elapsed, ["player_seconds"] = playerTime, ["damage"] = damage,
                ["earth_hp_after"] = game.EarthHp, ["front_hit_index"] = front.Hits });
            if (firstImpactAt < 0) firstImpactAt = elapsed;
            if (impacts == 1 && !game.HasResearch("D_N4") && game.EarthHp > 0)
            {
                battle.Paused = true;
                guideResumeAt = playerTime + 10;
            }
        };
        if (!Build("satellite_launcher", Vector3.Back) || !game.BeginResearchSatelliteLaunch())
            throw new InvalidOperationException("Free opening launcher flow failed.");
        if (!Build("interceptor", Vector3.Back.Rotated(Vector3.Up, -.10f))
            || !Build("interceptor", Vector3.Back.Rotated(Vector3.Up, .10f)))
            throw new InvalidOperationException("Could not pay for two extra starting factories.");
        battle.StartWave(); RecordMothers(); const double step = 1d / 30;
        while (elapsed < 600 - 1e-6 && !battle.Dead)
        {
            if (battle.Paused)
            {
                playerTime += step; guidePauseDuration += step;
                if (playerTime + 1e-6 >= guideResumeAt)
                {
                    Buy("D_N4", true);
                    battle.Paused = false;
                }
                continue;
            }
            if (game.ResearchSatelliteLaunchInProgress && elapsed >= 30 - 1e-6)
            {
                if (!game.CompleteResearchSatelliteLaunch()) throw new InvalidOperationException("Satellite insertion failed.");
                satelliteAt = elapsed;
            }
            if (elapsed + 1e-6 >= nextAction) { Act(); nextAction = elapsed + 10; }
            game.Tick(step); battle.Step(step); elapsed += step; playerTime += step;
            RecordMothers();
            peakEnemies = Math.Max(peakEnemies, battle.Enemies.Count); peakDrones = Math.Max(peakDrones, battle.Drones.Count);
            if (game.Wave == lastWave) continue; lastWave = game.Wave;
            waves.Add(new DataMap { ["wave"] = game.Wave, ["seconds"] = Math.Round(elapsed, 2), ["earth_hp"] = game.EarthHp,
                ["shield_hp"] = battle.GetLocalShieldSummary().N("hp"), ["kills"] = game.Kills,
                ["enemies_alive"] = battle.Enemies.Count, ["drones_alive"] = battle.Drones.Count,
                ["science"] = game.Science, ["factories"] = game.Buildings.DeepClone(), ["wave_plan"] = battle.GetWaveSpawnPlan() });
        }
        bool noPerks = !PerkCatalog.Entries.Any(r => game.FactoryPerks.IsUnlocked(r.S("id")));
        var result = new DataMap {
            ["seed"] = seed, ["survived"] = !battle.Dead && game.EarthHp > 0 && elapsed >= 600 - 1e-6,
            ["legitimate_run"] = noPerks && game.Achievements.UnlockedCount == 0 && game.LocalShieldAchievementBonus == 0
                && satelliteAt >= 30 && initial.N("science") == 0
                && game.CombatSettings.ToJson() == DefenseState.DefaultCombatSettings.ToJson(),
            ["seconds"] = Math.Round(elapsed, 2), ["player_seconds"] = Math.Round(playerTime, 2),
            ["guide_pause_seconds"] = guidePauseDuration, ["wave"] = game.Wave, ["completed_waves"] = game.CompletedWaves,
            ["earth_hp"] = game.EarthHp, ["shield_hp"] = battle.GetLocalShieldSummary().N("hp"),
            ["kills"] = game.Kills, ["destroyed_drones"] = battle.DestroyedDrones, ["earth_impacts"] = impacts,
            ["earth_damage_received"] = damageReceived, ["first_impact_seconds"] = firstImpactAt,
            ["satellite_ready_seconds"] = satelliteAt, ["science"] = game.Science, ["science_per_second"] = game.Rates().N("science"),
            ["paid_research_count"] = purchases.Cast<DataMap>().Count(r => r.N("science_paid") > 0),
            ["research_count"] = purchases.Count, ["peak_enemies"] = peakEnemies, ["peak_drones"] = peakDrones,
            ["perks_owned"] = !noPerks, ["achievements_unlocked"] = game.Achievements.UnlockedCount,
            ["default_settings_unchanged"] = game.CombatSettings.ToJson() == DefenseState.DefaultCombatSettings.ToJson(),
            ["all_new_fronts_hit_earth_twice"] = encounters.Values.All(f => f.Hits >= 2),
            ["front_encounters"] = encounters.Values.Select(f => (object?)f.Report()).ToList(), ["hit_log"] = hitLog,
            ["action_queue_log"] = queueLog,
            ["purchases"] = purchases, ["buildings"] = buildings, ["waves"] = waves };
        Console.WriteLine($"NOVICE_CURRENT seed={seed} survived={result.B("survived")} seconds={elapsed:0.00} wave={game.Wave} HP={game.EarthHp:0.000} kills={game.Kills} paid_research={result.I("paid_research_count")} damage_taken={damageReceived:0.000}");
        foreach (var f in encounters.Values) Console.WriteLine($"NOVICE_FRONT seed={seed} id={f.Id} spawn={f.SpawnAt:0.00} first_hit={f.FirstHit:0.00} hits={f.Hits} first_reaction={f.ReactionAt:0.00} defense={f.DefenseAt:0.00} damage={f.Damage:0.000} before_defense={f.DamageBeforeDefense:0.000}");
        return result;
    }
}
