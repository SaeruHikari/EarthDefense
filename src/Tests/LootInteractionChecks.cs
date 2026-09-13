using Godot;
using System.Reflection;
using Earthward.Application;
using Earthward.Combat;
using Earthward.Domain;
using Earthward.Rendering;

namespace Earthward.Tests;

/// <summary>Real enemy deaths, native pointer input, rendered pickups and arrival-only wallet credit.</summary>
public partial class LootInteractionChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private static readonly string[] Currencies = ["minerals", "energy", "science", "resource_cores", "alien_points", "alien_chips"];

    private void Check(bool value, string label)
    {
        _checks++;
        if (value) return;
        _failures++;
        GD.PrintErr("LOOT_INTERACTION_FAIL: " + label);
    }

    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static object? Invoke(object target, string method, params object?[] arguments) =>
        (target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
         ?? throw new MissingMethodException(target.GetType().Name, method)).Invoke(target, arguments);

    private double Balance(string currency) => currency switch
    {
        "minerals" => _app.Game.Minerals, "energy" => _app.Game.Energy, "science" => _app.Game.Science,
        "resource_cores" => _app.Game.ResourceCores, "alien_points" => _app.Game.AlienPoints,
        "alien_chips" => _app.Game.FactoryPerks.AlienChips, _ => throw new ArgumentOutOfRangeException(nameof(currency))
    };

    private Vector3 WorldAt(Vector2 normalizedScreen)
    {
        Vector2 point = normalizedScreen * _app.WorldSize;
        float distance = _app.Planet.Camera.GlobalPosition.Length() - WorldScale.EarthRadius - 3.5f;
        return _app.Planet.Camera.GlobalPosition + _app.Planet.ProjectViewRay(point) * distance;
    }

    private DataMap Kill(Vector3 at, string identity)
    {
        var enemy = _app.Battle.SpawnEnemy("scout", new() { ["wave"] = 1L, ["role"] = "claw" }, at)
            ?? throw new InvalidOperationException("Actual ordinary enemy spawn failed.");
        enemy["reward_event_id"] = identity;
        long kills = _app.Game.Kills;
        Check(_app.Battle.Enemies.Contains(enemy), "ordinary aircraft enters the actual battle roster");
        Check(_app.Battle.ApplyEnemyDamage(enemy, 1e9) > 0 && !_app.Battle.Enemies.Contains(enemy)
              && _app.Game.Kills == kills + 1, "actual lethal damage removes the aircraft and records its death once");
        return enemy;
    }

    private string WinningIdentity(string currency)
    {
        double chance = currency switch
        {
            "resource_cores" => DomainBalance.Value("resource_core_drop_chance"),
            "alien_points" => DomainBalance.Value("alien_point_drop_chance"),
            _ => PerkCatalog.AlienChipDropChance
        };
        for (int i = 0; i < 100000; i++)
        {
            string id = $"native-loot:{currency}:{i}";
            var candidate = new DataMap { ["kind"] = "scout", ["hp"] = 0d, ["reward_event_id"] = id };
            if (Invoke(_app.Game, "EnemyDropsRareLoot", candidate, currency, chance) is true) return id;
        }
        throw new InvalidOperationException("No deterministic rare ordinary-aircraft drop found for " + currency);
    }

    private async Task Pointer(Vector2 point, bool pressed)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left,
            ButtonMask = pressed ? MouseButtonMask.Left : 0, Pressed = pressed
        });
        await Frames(2);
    }

    private async Task ClickPickup(DataMap pickup)
    {
        Vector2 point = _app.Planet.GetSpaceScreenPosition(pickup.Vector3("space_position"));
        long uid = pickup.L("uid");
        double before = Balance(pickup.S("currency"));
        Check(!_app.IsOverUi(point) && _app.PickLootTarget(point, true).L("uid", -1) == uid,
            "real pointer coordinate resolves the intended " + pickup.S("currency") + " pickup");
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        await Frames(2);
        await Pointer(point, true);
        Check(pickup.S("phase") == "world" && Balance(pickup.S("currency")) == before,
            "left-button press alone neither reserves nor credits the pickup");
        await Pointer(point, false);
        Check(pickup.S("phase") == "flying" && Balance(pickup.S("currency")) == before,
            "matching native release starts the flight without crediting the wallet");
    }

    private async Task Capture(string name)
    {
        // The interaction fixture intentionally freezes simulation at exact
        // arrival boundaries. Clear death-only effects before a beauty capture
        // so paused damage numbers and explosion bursts cannot cover the loot.
        _app.Battle.DamageNumbers.Clear();
        _app.Battle.Bursts.Clear();
        _app.SyncRender();
        _app.BattleHud.QueueRedraw();
        _app.QueueRedraw();
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = ProjectSettings.GlobalizePath("res://artifacts");
        System.IO.Directory.CreateDirectory(directory);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "capture native game " + name);
    }

    private void CheckAssets()
    {
        var assets = LootRenderer.ValidateAssets();
        Check(assets.Count == 6 && assets.Select(row => row.S("currency")).Order().SequenceEqual(Currencies.Order()),
            "all six actual Blender pickup assets are validated");
        foreach (var asset in assets)
        {
            Check(asset.B("ok") && asset.I("surfaces") == 4 && asset.I("triangles") is > 0 and < 1000
                  && asset.N("radius") <= .4801, "PBR GLB contract: " + asset.S("currency") + " " + asset.S("error"));
            string path = $"res://assets/ui/loot/loot_{asset.S("currency")}.png";
            var texture = GD.Load<Texture2D>(path);
            Check(texture != null && texture.GetWidth() == 192 && texture.GetHeight() == 192,
                "same-model HUD thumbnail imports at 192x192: " + asset.S("currency"));
        }
    }

    private async Task CheckCheckpointRoundtrip()
    {
        _app.UserPaused = true;
        _app.DockOpen = false;
        _app._Process(0);
        await Frames(3);
        var item = _app.Battle.LootPickups.First(row => row.S("currency") == "energy" && row.S("phase") == "world"
            && _app.Planet.IsSpaceVisible(row.Vector3("space_position")));
        string currency = item.S("currency");
        long uid = item.L("uid");
        double before = Balance(currency), amount = item.N("amount");
        await ClickPickup(item);
        double duration = item.N("flight_duration");
        _app._Process(duration * .35);
        Check(item.S("phase") == "flying" && Balance(currency) == before,
            "Main checkpoint fixture contains a genuinely clicked unpaid item partway through flight");

        var snapshot = (DataMap)Invoke(_app, "CaptureCheckpointData", new object?[] { null })!;
        Check(snapshot.I("version") == 4 && _app.ValidateCheckpoint(snapshot),
            "actual Main.CaptureCheckpointData produces a valid version-four top-level checkpoint");
        Check(CombatSnapshotCodec.TryDecode(snapshot.Map("expedition_battle").Map("earth").Value("payload"), out var decoded)
              && decoded is DataMap payload && payload.List("_loot").OfType<DataMap>().Any(row => row.S("phase") == "world")
              && payload.List("_loot").OfType<DataMap>().Any(row => row.S("phase") == "flying"),
            "Main snapshot includes both world pickups and an unfinished flight in its actual campaign payload");
        var previousVersion = snapshot.DeepClone(); previousVersion["version"] = 3;
        Check(!_app.ValidateCheckpoint(previousVersion), "Main rejects the retired top-level checkpoint version");
        var balances = Currencies.ToDictionary(key => key, Balance);
        var savedRows = _app.Battle.LootPickups.ToDictionary(row => row.L("uid"), row => row.DeepClone());
        Check(_app.SaveCheckpoint(), "Main.SaveCheckpoint durably writes a mixed world/in-flight save");
        string path = ProjectSettings.GlobalizePath("user://earthward_checkpoint.json");
        // This profile is isolated by _Ready. Remove only its known checkpoint
        // to prove the close notification itself captures before tree teardown.
        System.IO.File.Delete(path);
        _app.PreserveCheckpoint = false;
        _app._Notification((int)NotificationWMCloseRequest);
        _app.PreserveCheckpoint = true;
        Check(System.IO.File.Exists(path) && Balance(currency) == before && item.S("phase") == "flying",
            "window-close capture saves the unfinished flight without auto-collecting it");
        var stored = DataMap.Parse(System.IO.File.ReadAllText(path));
        Check(stored.I("version") == 4 && _app.ValidateCheckpoint(stored),
            "the actual saved checkpoint on disk passes Main's full top-level validation");

        // Deliberately diverge only the isolated in-memory fixture, demonstrating
        // that LoadCheckpoint restores the file rather than keeping current data.
        _app.Game.Energy += 19;
        item["flight_elapsed"] = duration * .75;
        Check(_app.LoadCheckpoint(), "Main.LoadCheckpoint restores the mixed-phase file through the actual application path");
        _app.PreserveCheckpoint = true;
        _app.UserPaused = true;
        _app.DockOpen = false;
        var restored = _app.Battle.FindLootPickup(uid);
        Check(restored != null && restored.S("phase") == "flying"
              && Math.Abs(restored.N("flight_elapsed") - duration * .35) < .000001
              && _app.Battle.LootPickups.Count == savedRows.Count
              && _app.Battle.LootPickups.All(row => savedRows.TryGetValue(row.L("uid"), out var saved) && DataMap.Equivalent(row, saved)),
            "Main load preserves every pickup UID, amount, position, receipt and exact unfinished flight state");
        Check(Currencies.All(key => Balance(key) == balances[key]),
            "loading the mixed-phase checkpoint restores wallets without automatically collecting any item");
        _app._Process(duration * .64);
        Check(_app.Battle.FindLootPickup(uid) != null && Balance(currency) == before,
            "restored flight still waits through 99 percent total progress");
        _app._Process(duration * .02);
        Check(_app.Battle.FindLootPickup(uid) == null && Balance(currency) == before + amount
              && _app.Battle.LootPickups.Count == savedRows.Count - 1
              && _app.Battle.LootPickups.Any(row => row.S("phase") == "world"),
            "restored arrival pays exactly once while all unclicked world items remain available");
        var after = (DataMap)Invoke(_app, "CaptureCheckpointData", new object?[] { null })!;
        Check(_app.ValidateCheckpoint(after), "Main checkpoint remains valid after the restored flight settles");
    }

    public override async void _Ready()
    {
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Loot interaction checks require native rendering.");
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Loot interaction checks require an isolated runtime-tests profile.");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main(); AddChild(_app);
            _app.PreserveCheckpoint = true; _app.Sounds.Muted = true;
            await Frames(7);
            Check(_app.Restart(), "fixture starts through the actual new-run reset");
            _app.PreserveCheckpoint = true; _app.Sounds.Muted = true;
            _app.SetProcess(false); _app.Planet.SetProcess(false);
            _app.UserPaused = true; _app.Started = false; _app.Battle.Active = false;
            _app.DockOpen = false; _app.SelectedBuild = ""; _app.Modal = "";
            _app._Process(0);
            await Frames(5);
            CheckAssets();

            var opening = Currencies.ToDictionary(currency => currency, Balance);
            Kill(WorldAt(new(.36f, .55f)), "native-loot:ordinary");
            var mineral = _app.Battle.LootPickups.First(row => row.S("currency") == "minerals");
            foreach (var (currency, point) in new[]
            {
                ("resource_cores", new Vector2(.51f, .38f)),
                ("alien_points", new Vector2(.68f, .53f)),
                ("alien_chips", new Vector2(.52f, .66f))
            }) Kill(WorldAt(point), WinningIdentity(currency));
            Check(Currencies.All(currency => Balance(currency) == opening[currency]),
                "real ordinary and rare-drop deaths leave every resource wallet unchanged");
            Check(_app.Battle.LootPickups.Any(row => row.S("currency") == "resource_cores")
                  && _app.Battle.LootPickups.Any(row => row.S("currency") == "alien_points")
                  && _app.Battle.LootPickups.Any(row => row.S("currency") == "alien_chips"),
                "core, technology point and chip drops all come from actual ordinary-aircraft deaths");
            _app._Process(2);
            Check(Currencies.All(currency => Balance(currency) == opening[currency]) && _app.LootFlightCount == 0,
                "unclicked world items remain unpaid after presentation time advances");
            Check(_app.Planet.RenderedLootBatchCount == 6 && _app.Planet.RenderedLootCount == _app.Battle.LootPickups.Count,
                "real world pickups reach six native MultiMesh batches");
            await Capture("loot-world-native");

            long mineralUid = mineral.L("uid"); double mineralAmount = mineral.N("amount");
            Vector2 dragOrigin = _app.Planet.GetSpaceScreenPosition(mineral.Vector3("space_position"));
            await Pointer(dragOrigin, true);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = dragOrigin + new Vector2(20, 0),
                GlobalPosition = dragOrigin + new Vector2(20, 0), Relative = new(20, 0), ButtonMask = MouseButtonMask.Left });
            await Frames(2);
            await Pointer(dragOrigin + new Vector2(20, 0), false);
            Check(mineral.S("phase") == "world" && _app.Game.Minerals == opening["minerals"],
                "dragging from a pickup rotates normally without accidentally collecting it");
            await ClickPickup(mineral);
            Check(_app.LootFlightCount == 1, "native release creates exactly one flight");
            _app.UserPaused = false;
            double duration = mineral.N("flight_duration");
            _app._Process(duration * .4);
            Check(mineral.S("phase") == "flying" && _app.Game.Minerals == opening["minerals"],
                "the actual Main presentation loop pays nothing in the middle of a flight");
            Check(_app.Planet.RenderedLootCount == _app.Battle.LootPickups.Count - 1,
                "flying item leaves the world mesh batch while other world items remain");
            Vector2 halfway = (Vector2)Invoke(_app, "LootFlightPosition", mineral, .4d)!;
            Check(halfway.DistanceTo(_app.LootHudTarget("minerals")) > 10,
                "captured flying model has not already reached its resource counter");
            await Capture("loot-flight-native");
            _app._Process(duration * .59);
            Check(_app.Game.Minerals == opening["minerals"] && _app.Battle.FindLootPickup(mineralUid) != null,
                "99 percent flight progress remains unpaid");
            _app._Process(duration * .02);
            Check(_app.Game.Minerals == opening["minerals"] + mineralAmount && _app.Battle.FindLootPickup(mineralUid) == null,
                "arrival credits exactly the carried amount and consumes the physical pickup");
            Check(!_app.TryCollectLoot(mineralUid) && _app.Game.Energy == opening["energy"],
                "consumed UID cannot pay twice and unclicked energy stays unpaid");

            var header = (IReadOnlyList<DataMap>)Invoke(_app, "HeaderData")!;
            Vector2 chipTarget = _app.LootHudTarget("alien_chips");
            Check(header.Any(row => row.S("id") == "alien_chips") && new Rect2(0, 0, _app.WorldSize.X, 65).HasPoint(chipTarget)
                  && Currencies.Where(currency => currency != "alien_chips").All(currency => chipTarget.DistanceTo(_app.LootHudTarget(currency)) > 12),
                "chips have their own visible HUD counter and a distinct flight destination");
            var chip = _app.Battle.LootPickups.First(row => row.S("currency") == "alien_chips");
            long chipUid = chip.L("uid"); double chipAmount = chip.N("amount"), chipsBefore = Balance("alien_chips");
            _app.UserPaused = true; _app._Process(0);
            await ClickPickup(chip);
            Vector2 endpoint = (Vector2)Invoke(_app, "LootFlightPosition", chip, 1d)!;
            Check(endpoint.IsEqualApprox(chipTarget), "chip flight terminates at its actual HUD icon");
            double pausedClock = _app.Battle.Clock;
            _app.SetProcess(true);
            ulong deadline = Time.GetTicksMsec() + 5000;
            while (_app.Battle.FindLootPickup(chipUid) != null && Time.GetTicksMsec() < deadline) await Frames(1);
            _app.SetProcess(false);
            Check(_app.UserPaused && _app.Battle.Paused && _app.Planet.Paused && _app.Battle.Clock == pausedClock,
                "automatic native frames preserve paused simulation during pickup flight");
            Check(_app.Battle.FindLootPickup(chipUid) == null && Balance("alien_chips") == chipsBefore + chipAmount,
                "automatic Main processing completes a permanent-chip pickup while paused");

            var known = _app.Battle.LootPickups.Select(row => row.L("uid")).ToHashSet();
            Vector3 back = -_app.Planet.Camera.GlobalPosition.Normalized() * (WorldScale.EarthRadius + 3.5f);
            Kill(back, "native-loot:behind-earth");
            var hidden = _app.Battle.LootPickups.First(row => !known.Contains(row.L("uid")) && row.S("currency") == "minerals");
            Vector2 hiddenPoint = _app.Planet.GetSpaceScreenPosition(hidden.Vector3("space_position"));
            Check(!_app.Planet.IsSpaceVisible(hidden.Vector3("space_position"))
                  && _app.PickLootTarget(hiddenPoint, true).L("uid", -1) != hidden.L("uid")
                  && !_app.TryCollectLoot(hidden.L("uid")), "Earth occlusion excludes a rear-side item from targeting and collection");
            double hiddenBalance = _app.Game.Minerals;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = hiddenPoint, GlobalPosition = hiddenPoint });
            await Pointer(hiddenPoint, true); await Pointer(hiddenPoint, false);
            Check(hidden.S("phase") == "world" && _app.Game.Minerals == hiddenBalance,
                "actual pointer press/release through Earth cannot collect its hidden rear-side item");
            Check(Battlefield.ValidateLootConsistency(_app.Battle.SerializeCombatSnapshot(), _app.Game),
                "native deaths and pointer-driven arrivals retain a valid paired loot ledger");
            await Capture("loot-collected-native");
            await CheckCheckpointRoundtrip();
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app))
        {
            _app.PreserveCheckpoint = true;
            _app.FlushCheckpointWrites();
            _app.QueueFree();
            await Frames(5);
        }
        GD.Print($"LOOT_INTERACTION_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
