using Earthward.Domain;
using Earthward.Combat;
using Earthward.Tests;
using System.Diagnostics;

internal static class FleetLoadBench
{
    public static void Run(string[] args)
    {
        string Arg(string name,string fallback)=>args.FirstOrDefault(a=>a.StartsWith("--"+name+"=",StringComparison.Ordinal))?.Split('=',2)[1]??fallback;
        int enemies=int.Parse(Arg("enemies","1000")),friends=int.Parse(Arg("friends","5000")),warmup=int.Parse(Arg("warmup","60")),frames=int.Parse(Arg("frames","120"));
        string label=Arg("label","baseline"),mode=Arg("mode","combat");
        if (!Enum.TryParse<FleetStressLayout>(Arg("layout","cluster"),true,out var layout)) throw new ArgumentException("layout must be cluster or world");
        string catalog=FleetStressFixture.ConfigureCatalog();var g=new DefenseState();var b=new Battlefield(g){ForceSerialFlight=args.Contains("--serial-flight")};var fixture=new FleetStressFixture(g,b,friends,enemies,layout);fixture.Setup(mode=="patrol"?FleetStressMode.Patrol:FleetStressMode.Combat);
        if(args.Contains("--export-profiles")){fixture.ExportCompiledProfiles();Console.WriteLine("FROZEN_PROFILES_EXPORTED "+fixture.StatsHash);return;}
        bool profile=args.Contains("--profile");if(profile)b.Performance=new();var stages=Enum.GetValues<CombatStage>().ToDictionary(stage=>stage,stage=>new List<double>(frames));int assignments=0;double step=double.Parse(Arg("dt",(1d/60).ToString(System.Globalization.CultureInfo.InvariantCulture)),System.Globalization.CultureInfo.InvariantCulture);
        var samples=new List<double>(frames);var allocations=new List<long>(frames);long kills=0,losses=0,damage=0,transform=0,initialFriendlyCreated=0;int gc=0;
        Console.WriteLine($"FLEET_LOAD_START mode={mode} friends={b.Drones.Count} enemies={b.Enemies.Count} sites={fixture.Sites.Count} hash={fixture.StatsHash}");
        for(int tick=0;tick<warmup+frames;tick++)
        {
            fixture.MaintainPopulation();long bytes=GC.GetTotalAllocatedBytes(false),begin=Stopwatch.GetTimestamp();b.Step(step);double elapsed=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;long allocated=GC.GetTotalAllocatedBytes(false)-bytes;g.Tick(step);fixture.CaptureCounters();
            if(tick==warmup-1){kills=g.Kills;losses=b.DestroyedDrones;damage=b.NumberSequence;transform=fixture.SurfaceTransformCalls;gc=GC.CollectionCount(0);initialFriendlyCreated=fixture.FriendlyCreated;}
            if(tick>=warmup){samples.Add(elapsed);allocations.Add(allocated);if(b.Performance!=null){foreach(var stage in stages.Keys)stages[stage].Add(b.Performance.Milliseconds(stage));assignments+=b.Performance.Calls(CombatStage.Assignments);}}
        }
        samples.Sort();allocations.Sort();
        var report=new DataMap{["label"]=label,["mode"]=mode,["layout"]=layout.ToString().ToLowerInvariant(),["aircraft_with_targets_current"]=fixture.AircraftWithTargets,["aircraft_with_targets_peak"]=fixture.PeakAircraftWithTargets,["aircraft_lives_observed_firing"]=fixture.ObservedFiringLives,["factories_observed_engaged"]=fixture.ObservedEngagedFactories,["allied_target"]=friends,["enemy_target"]=mode=="patrol"?0:enemies,["factory_count"]=fixture.Sites.Count,["frames"]=frames,["warmup"]=warmup,["tick_hz"]=1/step,["simulation_step"]=step,["simulated_seconds"]=(warmup+frames)*step,["frozen_compiled_profiles"]=fixture.FrozenProfilesLoaded,["p50_ms"]=samples[frames/2],["p95_ms"]=samples[Math.Min(frames-1,(int)(frames*.95))],["worst_ms"]=samples[^1],["alloc_bytes_p50"]=allocations[frames/2],["alloc_bytes_p95"]=allocations[Math.Min(frames-1,(int)(frames*.95))],["allocation_scope"]="all_managed_threads",["gen0_collections"]=GC.CollectionCount(0)-gc,["sample_kills"]=g.Kills-kills,["sample_friendly_losses"]=b.DestroyedDrones-losses,["sample_damage_events"]=b.NumberSequence-damage,["peak_projectiles"]=fixture.PeakProjectiles,["friendly_shots_live_counter"]=fixture.ShotsFired,["sample_world_transform_calls"]=fixture.SurfaceTransformCalls-transform,["friendly_count_end"]=b.Drones.Count,["enemy_count_end"]=b.Enemies.Count,["sample_friendly_replacements"]=fixture.FriendlyCreated-initialFriendlyCreated,["stats_hash"]=fixture.StatsHash,["seed"]=FleetStressFixture.Seed,["planet_damage"]=fixture.PlanetDamage,["cpu"]=System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),["runtime"]=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,["catalog"]=catalog,["notes"]="Isolated synthetic load; population maintenance outside timing; actual shots, damage, deaths and regeneration active; no rendering."};
        if(profile){var parts=new DataMap();foreach(var(stage,values)in stages){values.Sort();parts[stage.ToString()]=new DataMap{["p50_ms"]=values[frames/2],["p95_ms"]=values[Math.Min(frames-1,(int)(frames*.95))],["mean_ms"]=values.Average()};}report["stages"]=parts;report["assignment_calls"]=assignments;}
        string layoutSuffix=layout==FleetStressLayout.World?"-world":"";string file=$"artifacts/fleet5000-{label}{layoutSuffix}-{mode}-{enemies}.json";File.WriteAllText(file,report.ToJson());Console.WriteLine("FLEET_LOAD_RESULT "+report.ToJson());
    }
}
