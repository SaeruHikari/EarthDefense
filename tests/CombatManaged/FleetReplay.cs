using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Earthward.Combat;
using Earthward.Domain;
using Earthward.Tests;

internal static class FleetReplay
{
    public static void Run(bool writeGolden, bool serial=false)
    {
        FleetStressFixture.ConfigureCatalog(); var game=new DefenseState();var battle=new Battlefield(game);
        typeof(Battlefield).GetProperty("ForceSerialFlight")?.SetValue(battle,serial);
        var fixture=new FleetStressFixture(game,battle,1200,200);fixture.Setup(FleetStressMode.Combat);
        var expected=writeGolden?new DataMap():DataMap.Parse(File.ReadAllText("tests/CombatManaged/Fixtures/fleet127-replay.json"));var rows=new List<object?>();int checks=0,failed=0;
        for(int tick=0;tick<180;tick++)
        {
            fixture.MaintainPopulation();
            if(tick==15)for(int i=47;i<battle.Drones.Count;i+=113)battle.Drones[i]["hp"]=battle.Drones[i].N("max_hp")*.25;
            battle.Step(1d/60);game.Tick(1d/60);fixture.CaptureCounters();
            if(tick%15!=14)continue;
            var snapshot=battle.SerializeCombatSnapshot();CombatSnapshotCodec.TryDecode(snapshot.Value("payload"),out var canonicalPayload);snapshot["payload"]=canonicalPayload;if(canonicalPayload is DataMap payload)
            {
                foreach(var enemy in payload.List("enemies").OfType<DataMap>())
                    if(enemy.ContainsKey("telegraph"))enemy["telegraph"]=Math.Clamp(enemy.N("telegraph"),0,1);
                // The production codec omits this rebuilt spatial-index slot. It is
                // not persistent actor state and must not distinguish equivalent saves.
                foreach(var drone in payload.List("_drones").OfType<DataMap>())drone.Remove("_actor_order");
                foreach(var beam in payload.List("_beams").OfType<DataMap>())if(beam.S("style")=="laser")beam.Remove("style");
            }string text=snapshot.ToJson();if(writeGolden && tick==14)File.WriteAllText("artifacts/fleet127-replay-original.json",text);string digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            var row=new DataMap{["tick"]=tick,["digest"]=digest,["allies"]=battle.Drones.Count,["enemies"]=battle.Enemies.Count,["losses"]=battle.DestroyedDrones,["damage_events"]=battle.NumberSequence,["rng_state"]=battle.Random.State.ToString()};rows.Add(row);
            if(!writeGolden)
            {
                var want=(DataMap)expected.List("samples")[rows.Count-1]!;checks++;
                if(!DataMap.Equivalent(row,want)){failed++;Console.WriteLine("FAIL replay tick "+tick+" "+row.ToJson()+" expected "+want.ToJson());if(failed==1)File.WriteAllText("artifacts/fleet127-replay-mismatch.json",text);}
            }
        }
        if(writeGolden)File.WriteAllText("tests/CombatManaged/Fixtures/fleet127-replay.json",new DataMap{["source"]="Unoptimized combat from pre-tech-depth-fleet5000 backup; frozen Domain/runtime and catalog; actual lifecycle/shots; canonical entire combat snapshots hashed; historical negative UI-only telegraph clamped to the now-valid 0..1 interval without altering skill timers; rebuilt transient _actor_order omitted from both old/new snapshot canonicalization; explicit cosmetic laser style omitted while all beam endpoints/colors/lifetimes remain strict.",["stats_hash"]=fixture.StatsHash,["samples"]=rows}.ToJson());
        Console.WriteLine($"FLEET_REPLAY {(writeGolden?"GOLDEN":serial?"SERIAL":"PARALLEL")} checks={checks} failed={failed}");if(failed>0)System.Environment.ExitCode=1;
    }
}
