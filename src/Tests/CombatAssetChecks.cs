using Godot;
using Earthward.Combat;
using Earthward.Domain;
using Earthward.Rendering;
namespace Earthward.Tests;

/// <summary>Exercises actual baked resources and the full combat packet-to-model contract.</summary>
public partial class CombatAssetChecks : Node
{
    private int _checks;
    private readonly List<string> _failures = new();

    private void Check(bool condition, string detail)
    {
        _checks++;
        if (!condition)
        {
            _failures.Add(detail);
            GD.PrintErr("COMBAT_ASSET_FAIL: " + detail);
        }
    }

    private static DataMap Packet(string kind, long uid) => new()
    {
        ["uid"] = uid,
        ["kind"] = kind,
        ["space_position"] = new Vector3((uid % 9) * .3f, (uid / 9 % 5) * .3f, 0),
        ["velocity"] = Vector3.Forward,
        ["tangent"] = Vector3.Forward,
        ["aim_direction"] = Vector3.Forward,
        ["aim_up"] = Vector3.Up,
        ["hp"] = 100d,
        ["max_hp"] = 100d
    };

    private void CheckKey(DataMap actor, bool enemy, bool projectile, string expected)
    {
        string key = FleetRenderer.VisualKey(actor, enemy, projectile);
        Check(key == expected, $"{actor.S("kind")} resolves to {expected}, got {key}");
        Check(ResourceLoader.Exists(RenderAssets.ModelPath(key), "PackedScene"), "Resolved baked resource exists: " + key);
    }

    private static IEnumerable<MultiMeshInstance3D> Batches(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is MultiMeshInstance3D batch)
                yield return batch;
            foreach (var nested in Batches(child))
                yield return nested;
        }
    }

    public override async void _Ready()
    {
        Node3D? space = null;
        Node3D? surface = null;
        FleetRenderer? renderer = null;
        try
        {
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            Check(RenderAssets.ModelKeys.Count == 17, "The 17 active aircraft, carrier and projectile model keys are registered");
            Check(!RenderAssets.ModelKeys.Any(key => key.StartsWith("boss/") || key is "cruiser" or "small_boss" or "boss"),
                "Retired enemy and boss models are absent from the active renderer catalog");
            foreach (string key in RenderAssets.ModelKeys)
            {
                string path = RenderAssets.ModelPath(key);
                Check(ResourceLoader.Exists(path, "PackedScene"), "Packed scene exists: " + key);
                var model = RenderAssets.Model(key);
                try
                {
                    Check(RenderAssets.HasRenderableGeometry(model), "Visible mesh geometry: " + key);
                    Check(!model.HasMeta("model_fallback"), "Registered model does not use fallback: " + key);
                    Check(RenderAssets.Meshes(model).All(part => part.Mesh != null && part.Mesh.GetAabb().Size.LengthSquared() > 0), "Nonempty model bounds: " + key);
                }
                finally
                {
                    model.Free();
                }
            }

            long uid = 1;
            var drones = new List<DataMap>();
            foreach (var frame in AirframeCatalog.Definitions)
            {
                var drone = Packet(frame.S("kind"), uid++);
                drone["airframe_id"] = frame.S("id");
                CheckKey(drone, false, false, "airframe/" + frame.S("id"));
                drones.Add(drone);
            }
            foreach (var pair in new[] { ("interceptor", "K1"), ("missile", "M1"), ("laser", "L1") })
            {
                var legacy = Packet(pair.Item1, uid++);
                CheckKey(legacy, false, false, "airframe/" + pair.Item2);
                legacy["airframe_id"] = "invalid-frame";
                CheckKey(legacy, false, false, "airframe/" + pair.Item2);
            }

            var state = new DefenseState { Wave = 40 };
            var battle = new Battlefield(state);
            var enemies = new List<DataMap>();
            Check(DefenseWavePlan.RoleOrder.SequenceEqual(new[] { "claw" }), "The wave catalog has one ordinary aircraft role");
            foreach (string role in DefenseWavePlan.RoleOrder)
            {
                var actor = battle.SpawnEnemy("scout", new() { ["wave"] = 40L, ["stage"] = 0, ["role"] = role }, new Vector3(0, 0, WorldScale.SpawnMinRadius));
                Check(actor != null, "Actual SpawnEnemy produced role " + role);
                if (actor == null)
                    continue;
                CheckKey(actor, true, false, "enemy/" + role);
                enemies.Add(actor);
            }
            // Stale optional visual metadata must never resurrect a retired model.
            foreach (string variant in new[] { "brood", "forge", "prism" })
            {
                var actor = Packet("scout", uid++);
                actor["boss_variant_id"] = variant;
                actor["enemy_role_id"] = "retired-role";
                CheckKey(actor, true, false, "enemy/claw");
                enemies.Add(actor);
            }
            foreach (string kind in new[] { "scout", "carrier", "mothership" })
            {
                var actor = Packet(kind, uid++);
                CheckKey(actor, true, false, kind == "scout" ? "enemy/claw" : kind);
                enemies.Add(actor);
            }

            // Exercise the exact producer that emitted kind="friendly" in the crash log.
            var bullets = new List<DataMap>();
            foreach (bool missile in new[] { false, true })
            {
                var shot = battle.MakeShot(Vector3.Zero, Vector3.Forward, 15, missile);
                Check(shot.S("kind") == (missile ? "missile" : "friendly"), "Actual MakeShot packet kind");
                CheckKey(shot, false, true, missile ? "projectile/missile" : "projectile/interceptor");
                bullets.Add(shot);
            }
            foreach (var pair in new[] { ("friendly", "interceptor"), ("interceptor", "interceptor"), ("laser", "laser"), ("missile", "missile"), ("hostile", "hostile") })
            {
                var shot = Packet(pair.Item1, uid++);
                CheckKey(shot, false, true, "projectile/" + pair.Item2);
                bullets.Add(shot);
            }
            foreach (var pair in new[] { ("interceptor", "kinetic"), ("laser", "beam"), ("missile", "explosive") })
            {
                var shot = Packet("friendly", uid++);
                shot["source"] = new DataMap { ["kind"] = pair.Item1, ["damage_type"] = pair.Item2 };
                CheckKey(shot, false, true, "projectile/" + pair.Item1);
                bullets.Add(shot);
            }
            var perkState = new DefenseState { Wave = 1 };
            var profile = perkState.FactoryPerks.Snapshot();
            profile.Map("levels")["a_kinetic_ricochet"] = 1L;
            profile["advanced_unlocked"] = true;
            Check(perkState.FactoryPerks.ImportSnapshot(profile), "Memory-only ricochet profile is valid");
            Check(perkState.FactoryPerks.Equip("interceptor", -1, 0, "a_kinetic_ricochet", "aircraft", -1, "K1"), "Actual ricochet Perk equips a K1 default berth");
            var ricochetBattle = new Battlefield(perkState);
            ricochetBattle.SpawnEnemy("scout", new() { ["wave"] = 1L, ["role"] = "claw" }, new Vector3(0, 0, WorldScale.EarthRadius + 1));
            ricochetBattle.SpawnEnemy("scout", new() { ["wave"] = 1L, ["role"] = "claw" }, new Vector3(.4f, 0, WorldScale.EarthRadius + 1.2f));
            var originalShot = ricochetBattle.MakeShot(new Vector3(0, 0, WorldScale.EarthRadius + .5f), new Vector3(0, 0, WorldScale.EarthRadius + 1), 1, false, stats: perkState.FactoryDroneStats("interceptor"));
            Check(originalShot.Map("source").Map("effects").I("ricochet_targets") > 0, "Actual Perk modifier enters the shot source");
            ricochetBattle.UpdateShots(.1);
            var generatedRicochet = ricochetBattle.Shots.FirstOrDefault(shot => shot.S("proc_kind") == "ricochet");
            Check(generatedRicochet != null, "Actual segment collision and Perk spawn a ricochet projectile");
            if (generatedRicochet != null)
            {
                CheckKey(generatedRicochet, false, true, "projectile/interceptor");
                bullets.Add(generatedRicochet);
            }
            var ricochet = Packet("friendly", uid++);
            ricochet["proc_kind"] = "ricochet";
            ricochet["secondary"] = true;
            CheckKey(ricochet, false, true, "projectile/interceptor");
            bullets.Add(ricochet);
            var cluster = Packet("missile", uid++);
            cluster["missile"] = true;
            cluster["proc_kind"] = "cluster";
            CheckKey(cluster, false, true, "projectile/missile");
            bullets.Add(cluster);
            foreach (string delayed in new[] { "blast", "shield_field" })
            {
                var effect = Packet(delayed == "blast" ? "missile" : "friendly", uid++);
                effect["delayed_effect"] = delayed;
                CheckKey(effect, false, true, delayed == "blast" ? "projectile/missile" : "projectile/interceptor");
                bullets.Add(effect);
            }
            var hostileInterceptable = Packet("hostile", uid++);
            hostileInterceptable["interceptable"] = true;
            hostileInterceptable["target_kind"] = "drone";
            CheckKey(hostileInterceptable, false, true, "projectile/hostile");
            bullets.Add(hostileInterceptable);
            var hostileAgainstEarth = Packet("hostile", uid++);
            hostileAgainstEarth["target_kind"] = "earth";
            CheckKey(hostileAgainstEarth, false, true, "projectile/hostile");
            bullets.Add(hostileAgainstEarth);
            Check(RenderAssets.ModelPath("projectile/friendly") == RenderAssets.ModelPath("projectile/interceptor"), "Legacy direct model lookup uses the kinetic alias");

            space = new Node3D { Name = "AssetContractSpace" };
            surface = new Node3D { Name = "AssetContractSurface" };
            AddChild(space);
            AddChild(surface);
            renderer = new FleetRenderer(space, surface);
            renderer.Sync("drones", drones, .5f, 0);
            renderer.Sync("enemies", enemies, .5f, 0);
            renderer.Sync("projectiles", bullets, 1f, 0);
            var batches = Batches(space).ToArray();
            Check(batches.Length >= 16, "Native buckets cover nine airframes, three enemy/carrier visuals and four projectile families");
            Check(batches.All(batch => batch.Multimesh.Mesh != null && batch.Multimesh.VisibleInstanceCount > 0), "Every populated batch has visible native instances");
            foreach (string family in new[] { "interceptor", "laser", "missile", "hostile" })
                Check(batches.Any(batch => batch.Name.ToString().Contains("projectile_" + family, StringComparison.Ordinal)), "Native projectile family uploaded: " + family);
            int stableCount = batches.Length;
            for (int frame = 0; frame < 30; frame++)
                renderer.Sync("projectiles", bullets, 1, frame / 60d);
            Check(Batches(space).Count() == stableCount, "Repeated sync reuses healthy model and batch caches");
            Check(RenderAssets.ModelFallbackDiagnostics.Count == 0, "All valid gameplay packet forms render without an asset fallback");

            // Fault contracts use only transient test nodes, never modify a baked asset.
            foreach (string key in new[] { "airframe/K1", "airframe/M1", "airframe/L1", "enemy/claw", "carrier", "projectile/hostile", "projectile/missile" })
            {
                var fallback = RenderAssets.CreateFallbackModel(key);
                Check(RenderAssets.HasRenderableGeometry(fallback), "Last-resort fallback remains visible: " + key);
                Check(fallback.HasMeta("model_fallback"), "Fallback identity is diagnosable: " + key);
                fallback.Free();
            }
            string absent = "res://assets/managed/models/__contract_missing_asset__.scn";
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    RenderAssets.Instantiate(absent).Free();
                    Check(false, "Missing required scene must report an explicit loading failure");
                }
                catch (InvalidDataException error)
                {
                    Check(error.Message.Contains(absent, StringComparison.Ordinal), "Missing resource exception identifies its exact path");
                }
                Check(!RenderAssets.IsSceneCached(absent), "Missing scene does not poison the positive scene cache");
            }
        }
        catch (Exception error)
        {
            _failures.Add(error.ToString());
            GD.PrintErr("COMBAT_ASSET_FAIL: " + error);
        }
        finally
        {
            renderer?.Clear();
            space?.QueueFree();
            surface?.QueueFree();
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_failures.Count == 0)
            await RunLiveCombat();
        GD.Print($"COMBAT_ASSET_RESULT: {_checks} checks / {_failures.Count} failures");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
    private async System.Threading.Tasks.Task RunLiveCombat()
    {
        // Exercise Main.Step -> Planet -> native MultiMesh submission while real
        // weapons fire, a path that a pure managed simulation test cannot cover.
        if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
        {
            Check(false, "Live combat asset regression requires an isolated runtime-tests profile");
            return;
        }
        Earthward.Application.Main? app = null;
        try
        {
            app = new Earthward.Application.Main();
            AddChild(app);
            app.Sounds.Muted = true;
            app.Modal = "";
            app.Speed = 1;
            app.StartWave();
            app.PreserveCheckpoint = true;
            var sites = app.Planet.GetFactorySites();
            Check(sites.Count > 0 && app.Started && app.Battle.Active, "live fixture starts its real starter factory and wave");
            var friendlyIds = new HashSet<long>();
            var hostileIds = new HashSet<long>();
            var firingDrones = new HashSet<long>();
            int nativeFrames = 0;
            int visibleProjectileFrames = 0;
            int friendlyNativeFrames = 0;
            bool encounterSeeded = false, resumedFirstImpactGuide = false;
            ulong started = Time.GetTicksMsec();
            GD.Print("COMBAT_ASSET_LIVE: starting 30 seconds of real Main/native-render combat");
            while (Time.GetTicksMsec() - started < 30000)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                nativeFrames++;
                // Close-range enemies used to be inserted before the starter
                // factory's five-second build plus launch animation completed.
                // Their first Earth hit paused Main for the shield tutorial,
                // leaving the test to count frozen hostile meshes for 30 seconds.
                // Wait for a real production aircraft, then give it a valid
                // engagement inside its unchanged weapon/home coverage.
                if (!encounterSeeded)
                {
                    var ready = app.Battle.Drones.FirstOrDefault(drone => drone.S("kind") == "interceptor"
                        && drone.N("hp") > 0 && drone.N("launch_age") >= CombatScale.LaunchDuration
                        && drone.S("state") is "patrol" or "engaging");
                    if (ready != null)
                    {
                        Vector3 origin = app.Battle.GetDroneWorldPosition(ready);
                        Vector3 radial = origin.Normalized();
                        Vector3 across = radial.Cross(Vector3.Up);
                        across = across.LengthSquared() > .0001f ? across.Normalized() : Vector3.Right;
                        Vector3 target = origin + radial * 1.25f;
                        if (app.Battle.CanDroneEngagePosition(ready, target))
                        {
                            for (int i = 0; i < 3; i++)
                                app.Battle.SpawnEnemy("scout", new() { ["wave"] = 1L, ["role"] = "claw" }, target + across * ((i - 1) * .16f));
                            encounterSeeded = true;
                            GD.Print($"COMBAT_ASSET_LIVE: factory aircraft {ready.L("uid")} completed launch at {app.Battle.Clock:F2}s; close encounter deployed");
                        }
                    }
                }
                // A player's Space input explicitly resumes the one-time guide.
                // Exercise that input rather than suppressing production onboarding.
                if (!resumedFirstImpactGuide && app.LocalShieldGuidePending && app.UserPaused && !app.Defeated)
                {
                    Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
                    Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
                    resumedFirstImpactGuide = true;
                    GD.Print("COMBAT_ASSET_LIVE: acknowledged first-impact guide with native Space input");
                }
                foreach (var shot in app.Battle.Shots)
                    friendlyIds.Add(shot.L("uid"));
                foreach (var shot in app.Battle.HostileShots)
                    hostileIds.Add(shot.L("uid"));
                foreach (var drone in app.Battle.Drones)
                    if (drone.L("total_weapon_shots") > 0) firingDrones.Add(drone.L("uid"));
                if (Batches(app.Planet.SpaceRoot).Any(batch => batch.Visible && batch.Multimesh.VisibleInstanceCount > 0 && batch.Name.ToString().Contains("projectiles_", StringComparison.Ordinal)))
                    visibleProjectileFrames++;
                if (Batches(app.Planet.SpaceRoot).Any(batch => batch.Visible && batch.Multimesh.VisibleInstanceCount > 0 && batch.Name.ToString().Contains("projectiles_projectile_interceptor_", StringComparison.Ordinal)))
                    friendlyNativeFrames++;
            }
            Check(nativeFrames > 120, "Thirty-second live battle submitted more than 120 native frames");
            Check(encounterSeeded, "a factory-produced aircraft finishes launch and receives an in-range encounter");
            Check(firingDrones.Count > 0, "real factory aircraft weapon counters confirm actual firing");
            Check(friendlyIds.Count > 0, "Actual factory aircraft fired during real Main rendering");
            Check(hostileIds.Count > 0, "Actual enemies fired during real Main rendering");
            Check(visibleProjectileFrames > 0, "Real projectile packets reached visible native MultiMesh batches");
            Check(friendlyNativeFrames > 0, "actual friendly kinetic shots reached visible native MultiMesh batches");
            Check(RenderAssets.ModelFallbackDiagnostics.Count == 0, "Thirty-second valid gameplay requires no fallback model");
            GD.Print($"COMBAT_ASSET_LIVE: frames={nativeFrames}, friendly={friendlyIds.Count}, hostile={hostileIds.Count}, visible_projectile_frames={visibleProjectileFrames}, friendly_native_frames={friendlyNativeFrames}, firing_drones={firingDrones.Count}, simulation_seconds={app.Battle.Clock:F2}, guide_resumed={resumedFirstImpactGuide}");
        }
        catch (Exception error)
        {
            Check(false, "Live native combat failed: " + error);
        }
        finally
        {
            app?.QueueFree();
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
    }

}
