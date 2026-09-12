using System.Diagnostics;
using Earthward.Combat;
using Earthward.Domain;
using Godot;

/// <summary>Behavioral experiment over the live game, not a win-forcing balance test.</summary>
internal static class SlowCampaign
{
    private sealed class Front
    {
        public long Uid;
        public string Id = "";
        public Vector3 Position, LastImpact;
        public double Spawn, Notice, FirstHit = -1, LastHit = -1, FirstBuild = -1, Damage;
        public int Hits;
        public bool Active;
        public DataMap Report() => new() { ["id"] = Id, ["uid"] = Uid, ["spawn"] = Spawn,
            ["first_hit"] = FirstHit, ["hits"] = Hits, ["damage"] = Damage, ["first_defense"] = FirstBuild, ["active"] = Active };
    }

    // Broad milestones, not the cheapest-node optimizer. Missing prerequisites
    // are bought normally and saving for a selected milestone is allowed.
    private static readonly string[] Goals = {
        "K_S01", "K_S21", "M_N1", "I_S03", "I_S23", "L_N1", "D_S41",
        "K_S02", "K_S22", "I_N4", "D_N1", "I_N1", "C_N1", "I_G1",
        "M_N4", "C_G1", "L_N2", "K_N1", "M_N2", "D_N2", "I_N5",
        "C_A2", "C_A3", "C_G2", "D_G1", "M_G1", "L_G1", "I_G2", "D_G2"
    };

    public static void Run(string[] args)
    {
        string Option(string name, string fallback) => args.FirstOrDefault(s => s.StartsWith(name + "=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? fallback;
        int target = int.Parse(Option("--target-wave", "60"));
        int[] seeds = Option("--seeds", "11,9918,49127,62026,2026").Split(',').Select(int.Parse).ToArray();
        bool attentive = Option("--policy", "inattentive") == "attentive";
        var runs = new List<object?>();
        foreach (int seed in seeds)
        {
            Console.WriteLine($"SLOW_CAMPAIGN_BEGIN seed={seed} target={target} policy={(attentive ? "attentive" : "inattentive")}");
            runs.Add(new Player(seed, target, attentive).Play());
        }
        Directory.CreateDirectory("artifacts");
        string path = "artifacts/" + (attentive ? "attentive-control-60" : "inattentive-campaign-60") + ".json";
        var report = new DataMap {
            ["policy"] = attentive ? "attentive diagnostic control" : "inattentive, gradually learning player",
            ["target_completed_wave"] = target, ["simulation_hz"] = 30, ["production_balance_unchanged"] = true,
            ["initial_meta_progress"] = "none; perks can only be bought with chips earned in this run",
            ["operations"] = attentive ? "Every 8s, no skipped review; same construction/research priorities" : "Every 22-30s; skips every fifth review. At most one building and one research per attended review.",
            ["economy"] = attentive ? "Review every 30s" : "Review resources every 150s, no income expansion before wave4; at most one income building per review. Core/perk review every240s.",
            ["research_review"] = attentive ? "At each attended review when affordable" : "After a purchase, waits at least60s before another research review; funds may sit idle until an attended22-30s check. This is player behavior, not a game cooldown.",
            ["defense"] = "First directions react to real Earth hits; later directions are noticed after a delay. At an attended review, noticed recent damage takes priority over economic expansion. One building maximum, no artificial second construction cooldown. Defenses are never removed to fabricate leaks.",
            ["surface"] = "Real starter coordinates, level6 hex topology and occupancy; all resource sites affect targeting; rotation at production rate, paused during building actions and first-hit guide.",
            ["limitations"] = "Foundation heights use a sphere instead of terrain texture displacement (up to0.048 world units). No graphics, input pointing errors or camera search time; impact front attribution uses nearest mothership direction.",
            ["runs"] = runs
        };
        File.WriteAllText(path, report.ToJson());
        int wins = runs.Cast<DataMap>().Count(r => r.B("completed_target"));
        Console.WriteLine($"SLOW_CAMPAIGN_RESULT completed={wins}/{runs.Count} report={path}");
        // Losing is an experimental result. Only invalid simulation is an error.
        System.Environment.ExitCode = runs.Cast<DataMap>().All(r => r.B("legitimate")) ? 0 : 1;
    }

    private sealed class Player
    {
        private readonly DefenseState _game = new();
        private readonly CampaignTestSurface _surface = new();
        private readonly Battlefield _battle;
        private readonly DefenseCampaignDirector _campaign;
        private readonly Random _attention;
        private readonly int _seed, _target;
        private readonly bool _attentive;
        private readonly List<object?> _actions = new(), _waves = new(), _hits = new(), _scienceWaits = new();
        private readonly Dictionary<long, Front> _fronts = new();
        private double _time, _wallTime, _nextReview, _nextResearch, _nextEconomy = 180, _nextPerk = 240;
        private double _launchFinish = -1, _satelliteOnline = -1, _guideFinish = -1, _buildingUntil, _damage, _paidScience;
        private double _firstAffordable = -1, _liberatedAt = -1;
        private string _affordableId = "";
        private int _reviews, _skipped, _paidResearch, _peakEnemies, _peakDrones, _buildSerial;
        private long _lastCompleted;

        public Player(int seed, int target, bool attentive)
        {
            _seed = seed; _target = target; _attentive = attentive; _attention = new Random(seed ^ 0x6a12);
            var state = _game.Serialize(); state["run_id"] = "slow-campaign-" + seed;
            if (!_game.Restore(state) || _game.Science != 0 || _game.FactoryPerks.AlienChips != 0
                || _game.Achievements.UnlockedCount != 0 || _surface.GridCellCount != 40962)
                throw new InvalidOperationException("Fresh campaign setup failed.");
            _battle = new(_game, _surface); _battle.Random.Seed = (ulong)seed;
            _campaign = new(_game, _battle);
            _battle.InvasionDefeated += _campaign.SyncCampaign;
            _battle.EarthDamaged += EarthHit;
            _nextReview = attentive ? 8 : 24;
        }

        public DataMap Play()
        {
            var watch = Stopwatch.StartNew();
            _battle.StartWave(); ObserveFronts(); Snapshot("start");
            const double dt = 1d / 30;
            while (_game.CompletedWaves < _target && !_battle.Dead && _time < 7200)
            {
                if (_battle.Paused)
                {
                    _wallTime += dt;
                    if (_wallTime >= _guideFinish)
                    {
                        if (!_game.HasResearch("D_N4") && !BuyResearch("D_N4", "first-hit guide"))
                            throw new InvalidOperationException("Free shield guide could not purchase.");
                        _battle.Paused = false; _campaign.Paused = false;
                    }
                    continue;
                }
                if (_launchFinish >= 0 && _time >= _launchFinish && !_game.ResearchSatelliteDeployed)
                {
                    if (!_game.CompleteResearchSatelliteLaunch()) throw new InvalidOperationException("Satellite insertion failed.");
                    _satelliteOnline = _time; Log("satellite_online", "science", "8s actual rocket flight");
                }
                TrackResearchAffordability();
                if (_time >= _nextReview) Review();
                _surface.Step(dt, _time < _buildingUntil);
                _game.Tick(dt); _battle.Step(dt); _campaign.Step(dt);
                _time += dt; _wallTime += dt;
                ObserveFronts();
                _peakEnemies = Math.Max(_peakEnemies, _battle.Enemies.Count);
                _peakDrones = Math.Max(_peakDrones, _battle.Drones.Count);
                if (_liberatedAt < 0 && _game.Expedition.EarthLiberated) _liberatedAt = _time;
                if (_lastCompleted != _game.CompletedWaves)
                {
                    _lastCompleted = _game.CompletedWaves; Snapshot("wave_completed");
                    if (_lastCompleted % 5 == 0) Console.WriteLine($"SLOW_PROGRESS seed={_seed} completed={_lastCompleted} HP={_game.EarthHp:0.0} research={_paidResearch} drones={_battle.Drones.Count} enemies={_battle.Enemies.Count}");
                }
            }
            Snapshot(_battle.Dead ? "defeat" : _game.CompletedWaves >= _target ? "target_completed" : "timeout");
            bool valid = DataMap.Equivalent(_game.CombatSettings, DefenseState.DefaultCombatSettings)
                && _game.Achievements.UnlockedCount == 0 && _game.FactoryPerks.ProfilePath.Length == 0
                && _surface.OccupiedCellCount == _surface.AllSites.Sum(s => s.S("kind") == "satellite_launcher" ? 7 : 1)
                && _game.FacilityCount() == _surface.AllSites.Count;
            var result = new DataMap {
                ["seed"] = _seed, ["legitimate"] = valid, ["completed_target"] = _game.CompletedWaves >= _target && _game.EarthHp > 0,
                ["death_wave"] = _battle.Dead ? _game.Wave : 0, ["completed_waves"] = _game.CompletedWaves,
                ["current_wave"] = _game.Wave, ["game_seconds"] = _time, ["player_seconds"] = _wallTime,
                ["compute_seconds"] = watch.Elapsed.TotalSeconds, ["earth_hp"] = _game.EarthHp, ["damage_received"] = _damage,
                ["satellite_online_seconds"] = _satelliteOnline, ["paid_research"] = _paidResearch, ["science_spent"] = _paidScience,
                ["reviews"] = _reviews, ["skipped_reviews"] = _skipped, ["peak_drones"] = _peakDrones, ["peak_enemies"] = _peakEnemies,
                ["kills"] = _game.Kills, ["destroyed_drones"] = _battle.DestroyedDrones,
                ["near_mothers_destroyed"] = _battle.GetDestroyedFronts().Count, ["earth_liberated_seconds"] = _liberatedAt,
                ["research_goals_unmet"] = Goals.Where(id => !_game.HasResearch(id)).Select(id => (object?)new DataMap {
                    ["id"] = id, ["name"] = DeepTechnology.Definition(id).S("name"), ["reason"] = _game.GetGroupStatus(id).S("lock_reason") }).ToList(),
                ["fronts"] = _fronts.Values.Select(f => (object?)f.Report()).ToList(), ["waves"] = _waves,
                ["actions"] = _actions, ["hits"] = _hits, ["research_affordability_waits"] = _scienceWaits
            };
            Console.WriteLine($"SLOW_END seed={_seed} complete={result.B("completed_target")} wave={_game.Wave} completed={_game.CompletedWaves} hp={_game.EarthHp:0.000} seconds={_time:0.0} valid={valid}");
            return result;
        }

        private void ObserveFronts()
        {
            foreach (var front in _fronts.Values) front.Active = false;
            foreach (var mother in _battle.Motherships.Values.Concat(_battle.Enemies.Where(e => e.B("post_carrier"))))
            {
                long uid = mother.L("uid");
                if (!_fronts.TryGetValue(uid, out var f))
                    _fronts[uid] = f = new() { Uid = uid, Id = mother.S("front_id"), Spawn = _time,
                        Notice = _time + (_attentive ? 8 : 36 + Math.Abs(uid % 25)) };
                f.Position = mother.Vector3("space_position"); f.Active = true;
            }
        }

        private void EarthHit(double damage, Vector3 at)
        {
            ObserveFronts(); _damage += damage;
            var f = _fronts.Values.Where(f => f.Active).OrderByDescending(f => f.Position.Normalized().Dot(at.Normalized())).FirstOrDefault();
            if (f != null)
            {
                f.Hits++; f.Damage += damage; f.LastImpact = at; f.LastHit = _time;
                if (f.FirstHit < 0) f.FirstHit = _time;
            }
            _hits.Add(new DataMap { ["seconds"] = _time, ["wave"] = _game.Wave, ["damage"] = damage,
                ["hp_after"] = _game.EarthHp, ["front_uid"] = f?.Uid ?? 0 });
            if (_hits.Count == 1 && !_game.HasResearch("D_N4") && _game.EarthHp > 0)
            {
                _battle.Paused = true; _campaign.Paused = true; _guideFinish = _wallTime + 10;
            }
        }

        private void Review()
        {
            _reviews++; _nextReview = _time + (_attentive ? 8 : 22 + _attention.Next(9));
            if (!_attentive && _reviews % 5 == 0) { _skipped++; Log("attention_skipped", "", "missed this review"); return; }
            if (_game.Buildings.L("satellite_launcher") == 0)
            {
                Build("satellite_launcher", _surface.InitialFactoryNormal.Rotated(Vector3.Up, .15f), null); return;
            }
            if (!_game.ResearchSatelliteDeployed && !_game.ResearchSatelliteLaunchInProgress)
            {
                if (!_game.BeginResearchSatelliteLaunch()) throw new InvalidOperationException("Launch action failed.");
                _launchFinish = _time + 8; Log("satellite_launch", "", "delayed second visit to launcher"); return;
            }
            if (_time >= _nextResearch)
            {
                string next = NextResearch();
                if (next.Length > 0 && BuyResearch(next, "delayed research review"))
                    _nextResearch = _time + (_attentive ? 0 : 60);
            }
            bool constructed = _fronts.Values.Any(f => f.Active && Known(f) && f.LastHit >= 0 && _time - f.LastHit < 60) && Defend();
            if (!constructed && _time >= _nextEconomy && _game.Wave >= (_attentive ? 2 : 4))
            {
                _nextEconomy = _time + (_attentive ? 30 : 150);
                string kind = _game.Buildings.L("mine") <= _game.Buildings.L("solar") ? "mine" : "solar";
                if (_game.Buildings.L(kind) < 1 + _game.Wave / 3 && _game.CanBuild(kind))
                    constructed = Build(kind, _surface.InitialFactoryNormal.Rotated(Vector3.Up, (float)(.45 + _game.FacilityCount() * .03)), null);
                else Log("economy_wait", kind, "late review: " + (_game.CanBuild(kind) ? "current desired count reached" : "cannot afford or missing core"));
            }
            if (!constructed) Defend();
            if (_time >= _nextPerk)
            {
                _nextPerk = _time + (_attentive ? 60 : 240); UpgradePerkOrCore();
            }
        }

        private bool Known(Front f) => _attentive ? _time >= f.Notice :
            f.Spawn < .1 ? f.FirstHit >= 0 && _time >= f.FirstHit + 20 || _time >= 120 :
            _time >= f.Notice && (_game.Wave > 8 || f.FirstHit >= 0 && _time >= f.FirstHit + 20);
        private Vector3 Aim(Front f) => _surface.SpaceToSurface(f.LastHit >= 0 && _time - f.LastHit < 100 ? f.LastImpact : f.Position);
        private int Nearby(string kind, Vector3 normal) => _surface.Factories.Count(s => s.S("kind") == kind &&
            s.Vector3("normal").Dot(normal) > Math.Cos(Math.Min(.8, (_game.FactoryPatrolStats(kind, s.L("site_id")).N("patrol_radius") + _game.FactoryPatrolStats(kind, s.L("site_id")).N("patrol_outer_range")) / CombatScale.EarthRadius * .85)));
        private string DefenseKind(Front f)
        {
            var normal = Aim(f);
            if (_game.CanBuild("shield") && !_surface.Shields.Any(s => s.Vector3("normal").Dot(normal) > Math.Cos(_game.ShieldFacilityStats().N("surface_radius") / CombatScale.EarthRadius * .8))) return "shield";
            foreach (string kind in new[] { "laser", "missile", "interceptor" })
            {
                int desired = kind == "interceptor" ? (_game.Wave < 10 ? 3 : 4) : (_game.Wave < 20 ? 1 : 2);
                if (Nearby(kind, normal) < desired && _game.CanBuild(kind)) return kind;
            }
            return "";
        }
        private bool Defend()
        {
            var f = _fronts.Values.Where(f => f.Active && Known(f)).Where(f => DefenseKind(f).Length > 0)
                .OrderByDescending(f => f.LastHit >= 0 && _time - f.LastHit < 60)
                .ThenBy(f => f.FirstBuild >= 0).ThenBy(f => Nearby("interceptor", Aim(f)) + Nearby("missile", Aim(f)) + Nearby("laser", Aim(f)))
                .FirstOrDefault();
            return f != null && Build(DefenseKind(f), Aim(f).Rotated(Vector3.Up, (float)((_buildSerial % 3 - 1) * .07)), f);
        }

        private bool Build(string kind, Vector3 normal, Front? front)
        {
            if (!_game.CanBuild(kind)) return false;
            var site = _surface.FindBuildSite(kind, normal);
            if (site == null || !_surface.CanPlace(kind, site)) return false;
            if (!_game.Build(kind, site.L("site_id"))) throw new InvalidOperationException("Legal building purchase failed.");
            _surface.CommitBuild(kind, site); _buildSerial++;
            _buildingUntil = _time + (_attentive ? 2 : 6); // Placement keeps building mode open briefly, just like live rotation.
            if (front != null && front.FirstBuild < 0) front.FirstBuild = _time;
            Log("build", kind, front?.Id ?? "economic/orbital", site.L("site_id"));
            return true;
        }
        private string FirstMissing(string id)
        {
            if (_game.HasResearch(id)) return "";
            foreach (string parent in DeepTechnology.Definition(id).List("requires").Cast<string>())
            {
                string need = FirstMissing(parent); if (need.Length > 0) return need;
            }
            return id;
        }
        private string NextResearch()
        {
            foreach (string goal in Goals)
            {
                if (goal == "D_S41" && _game.EarthHp > 85) continue;
                string next = FirstMissing(goal);
                if (next.Length == 0) continue;
                // Do not let a wave/stage-locked milestone freeze all learning.
                var unlock = DeepTechnology.Definition(next).Map("unlock");
                if (unlock.L("completed_wave") > _game.CompletedWaves || unlock.L("defense_stage") > _game.GetDefenseReachStage()) continue;
                if (unlock.B("first_medium_boss_defeated") && _game.GetGroupStatus(next).S("lock_reason") == "先击败一只中型Boss") continue;
                return next;
            }
            return "";
        }
        private void TrackResearchAffordability()
        {
            string id = NextResearch();
            if (id != _affordableId) { _affordableId = id; _firstAffordable = -1; }
            if (_firstAffordable < 0 && id.Length > 0 && _game.CanResearch(id)) _firstAffordable = _time;
        }
        private bool BuyResearch(string id, string reason)
        {
            if (!_game.CanResearch(id)) return false;
            double before = _game.Science;
            if (!_game.PurchaseGroup(id)) throw new InvalidOperationException("Research purchase failed.");
            double paid = before - _game.Science;
            if (paid > 0) { _paidResearch++; _paidScience += paid; }
            if (_affordableId == id && _firstAffordable >= 0) _scienceWaits.Add(new DataMap {
                ["id"] = id, ["affordable_at"] = _firstAffordable, ["purchased_at"] = _time, ["wait"] = _time - _firstAffordable });
            Log("research", id, reason);
            return true;
        }
        private void UpgradePerkOrCore()
        {
            foreach (string id in new[] { "expanded_hangar", "targeting", "a_kinetic_core" })
            {
                var perks = _game.FactoryPerks;
                if (!perks.IsUnlocked(id))
                {
                    if (!perks.Purchase(id)) continue;
                    if (id == "a_kinetic_core")
                    {
                        if (!perks.Equip("interceptor", -1, 0, id, "aircraft", -1, "K1")) throw new InvalidOperationException(perks.LastError);
                    }
                    else foreach (string kind in new[] { "interceptor", "missile", "laser" })
                        if (!perks.Equip(kind, -1, id == "expanded_hangar" ? 0 : 1, id)) throw new InvalidOperationException(perks.LastError);
                    Log("perk_purchase", id, "earned chips, delayed equipment review"); return;
                }
            }
            foreach (string id in new[] { "targeting", "a_kinetic_core" })
                if (_game.FactoryPerks.GetLevel(id) < 5 && _game.FactoryPerks.Upgrade(id)) { Log("perk_upgrade", id, "earned chips"); return; }
            var site = _surface.AllSites.Where(s => s.S("kind") is "mine" or "solar")
                .OrderBy(s => _game.GetResourceFacilityLevel(s.L("site_id"))).FirstOrDefault();
            if (site != null && _game.UpgradeResourceFacility(site.L("site_id"), site.S("kind"))) Log("resource_core_upgrade", site.S("kind"), "late review", site.L("site_id"));
            foreach (var factory in _surface.Factories.Where(s => s.L("site_id") % 3 == 0))
            {
                string kind = factory.S("kind"), frame = kind == "interceptor" ? "K2" : kind == "missile" ? "M2" : "L2";
                if (_game.FactoryAirframe(kind, factory.L("site_id")) != frame && _game.SetAirframe(kind, factory.L("site_id"), -1, frame))
                { Log("airframe", frame, "one suitable factory conversion", factory.L("site_id")); break; }
            }
        }
        private void Log(string action, string id, string reason, long site = -1) => _actions.Add(new DataMap {
            ["seconds"] = _time, ["wave"] = _game.Wave, ["action"] = action, ["id"] = id, ["reason"] = reason, ["site"] = site,
            ["minerals"] = _game.Minerals, ["energy"] = _game.Energy, ["science"] = _game.Science,
            ["resource_cores"] = _game.ResourceCores, ["alien_points"] = _game.AlienPoints, ["chips"] = _game.FactoryPerks.AlienChips });
        private void Snapshot(string reason) => _waves.Add(new DataMap {
            ["reason"] = reason, ["seconds"] = _time, ["wave"] = _game.Wave, ["completed"] = _game.CompletedWaves,
            ["earth_hp"] = _game.EarthHp, ["earth_damage_total"] = _damage, ["shield"] = _battle.GetLocalShieldSummary(),
            ["minerals"] = _game.Minerals, ["energy"] = _game.Energy, ["science"] = _game.Science, ["science_rate"] = _game.Rates().N("science"),
            ["resource_cores"] = _game.ResourceCores, ["alien_points"] = _game.AlienPoints, ["chips"] = _game.FactoryPerks.AlienChips,
            ["buildings"] = _game.Buildings.DeepClone(), ["drones"] = _battle.Drones.Count, ["enemies"] = _battle.Enemies.Count,
            ["enemy_roles"] = new DataMap(_battle.Enemies.GroupBy(e => e.S("enemy_role_id").Length > 0 ? e.S("enemy_role_id") : e.S("kind")).ToDictionary(g => g.Key, g => (object?)g.Count())),
            ["paid_research"] = _paidResearch, ["research_owned"] = _game.DeepResearch.DeepClone(), ["next_research"] = NextResearch(),
            ["near_mothers_destroyed"] = _battle.GetDestroyedFronts().Count, ["campaign"] = _campaign.GetStatus(), ["spawn_plan"] = _battle.GetWaveSpawnPlan()
        });
    }
}
