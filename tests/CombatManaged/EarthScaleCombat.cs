using Earthward;
using Earthward.Domain;
using Earthward.Combat;
using Godot;
using System.Reflection;
using System.Globalization;

internal static class EarthScaleCombat
{
    private static int _checks, _failures;
    private const double R = WorldScale.EarthRadius;
    private static double _migrationDelta = WorldScale.EarthRadiusDelta;
    private static void Check(bool condition, string label)
    {
        _checks++;
        if (!condition) { _failures++; Console.WriteLine("EARTH_SCALE_FAIL " + label); }
    }
    private static void Near(double value, double expected, string label, double tolerance=.00001) => Check(double.IsFinite(value)&&Math.Abs(value-expected)<=tolerance,label+$" ({value}/{expected})");
    private static object? Invoke(Battlefield battle,string method,params object?[] values) => typeof(Battlefield).GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(battle,values);
    private static DataMap Decode(DataMap snapshot) { Check(CombatSnapshotCodec.TryDecode(snapshot.Value("payload"),out var value)&&value is DataMap,"decode combat payload"); return (DataMap)value!; }
    private static void Compare(object? actual,object? expected,string label)
    {
        if(expected is DataMap map) { Check(actual is DataMap,label+" map"); if(actual is DataMap a) foreach(var(k,v) in map)Compare(a.Value(k),v,label+"."+k); return; }
        if(expected is List<object?> array) { var a=(actual as System.Collections.IEnumerable)?.Cast<object?>().ToList();Check(a!=null&&a.Count==array.Count,label+" count");if(a!=null)for(int i=0;i<Math.Min(a.Count,array.Count);i++)Compare(a[i],array[i],label+"."+i);return; }
        if(CombatSnapshotCodec.IsNumber(expected)) { double e=DataMap.Number(expected);Near(DataMap.Number(actual,double.NaN),e,label,Math.Max(1e-8,Math.Abs(e)*1e-7));return; }
        Check(Equals(actual,expected),label);
    }
    private sealed class Surface : ICombatSurface
    {
        public List<DataMap> Sites {get;}=new();
        public IReadOnlyList<DataMap> GetFactorySites()=>Sites;
        public Vector3 SurfaceToSpace(Vector3 normal,double altitude)=>normal.Normalized()*(float)(WorldScale.EarthRadius+altitude);
        public Vector3 SpaceToSurface(Vector3 position)=>position.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>Sites.Select(x=>x.Vector3("normal")).ToList();
        public Surface(string kind="interceptor") { Sites.Add(new(){["site_id"]=18L,["kind"]=kind,["normal"]=Vector3.Back,["launch_position"]=Vector3.Back*(WorldScale.EarthRadius+.108f),["launch_direction"]=Vector3.Right}); }
    }
    public static void Run()
    {
        _checks=_failures=0;
        Near(WorldScale.EarthRadius,16,"actual shared Earth radius");Near(CombatScale.EarthRadius,16,"combat aliases shared Earth");Near(CombatScale.EarthCollisionRadius,16.07,"collision preserves .07 altitude");Near(CombatScale.ShieldShellRadius,WorldScale.EarthRadius+WorldScale.ShieldAltitude,"shield shell radius tracks the shield altitude");Near(CombatScale.CloseAssault,WorldScale.EarthRadius+WorldScale.ShieldAltitude+.35,"bombard standoff stays outside the shield shell");Near(CombatScale.DroneAltitude,.7,"drone altitude unchanged");Near(CombatScale.KineticRange,1.65,"kinetic range unchanged");Near(CombatScale.WeaponRange,4.5,"other weapon range unchanged");Near(CombatScale.PlanetPixelRadius,208,"model/effect conversion unchanged");
        RunNonSpatialGoldens();RunSpawningAndCombat();RunSnapshotMigration();RunRadius8Migration();RunFrontierReach();AllDirectionsCombat.Run(Check, Near);FactoryCoverageChecks.Run(Check, Near);StationaryBombardmentChecks.Run(Check, Near);LocalShieldChecks.Run(Check, Near);
        Console.WriteLine($"EARTH_SCALE_COMBAT {_checks} checks; {_failures} failures");System.Environment.ExitCode=_failures==0?0:1;
    }
    private static void RunNonSpatialGoldens()
    {
        CombatSnapshotCodec.TryDecode(DataMap.Parse(File.ReadAllText("artifacts/csharp-combat-golden.json")),out var decoded);var golden=(DataMap)decoded!;var random=golden.Map("random");var rng=new CombatRandom(122);
        foreach(var value in random.List("uints"))Compare((long)rng.NextUInt(),value,"PCG integer");rng.Seed=122;foreach(var value in random.List("floats"))Compare((double)rng.Randf(),value,"PCG float");rng.Seed=122;foreach(var value in random.List("ranges"))Compare(rng.Range(-3.7,22.8),value,"PCG range");Check(unchecked((long)rng.State).ToString(CultureInfo.InvariantCulture)==random.S("state"),"PCG final state unchanged");
        foreach(var row in golden.List("armor").OfType<DataMap>()) { var actor=new DataMap{["hp"]=1000d,["max_hp"]=1000d,["armor_type"]=row.S("armor"),["energy_hp"]=row.N("shield"),["energy_max_hp"]=row.N("shield")};var r=EnemyArmor.Resolve(actor,100,row.S("family"),new(),10);Compare(new DataMap{["total"]=r.Total,["energy"]=r.Energy,["hull"]=r.Hull,["broke"]=r.Broke,["immune"]=r.Immune},row.Map("result"),"armor unchanged");Compare(actor,row.Map("actor"),"armor actor unchanged"); }
        foreach(var row in golden.List("wave_plans").OfType<DataMap>()) { var plan=row.Map("plan");long wave=plan.L("wave");var actual=DefenseWavePlan.Build(wave,36,30,45,wave==3||wave%5==0,wave>=31?1:0,wave>=31?2:0);Compare(actual,plan,"fixed wave plan unchanged");for(int i=0;i<row.List("entries").Count;i++)Compare(DefenseWavePlan.Entry(actual,i),row.List("entries")[i],"fixed entry unchanged"); }
        foreach(var row in golden.List("enemy_catalog").OfType<DataMap>()) { var actual=new DataMap{["kind"]=row.Map("actor").S("kind")};EnemyCatalog.Apply(actual,row.Map("entry"),new());Compare(actual,row.Map("actor"),"enemy damage health speed unchanged"); }
        foreach(var sample in DataMap.Parse(File.ReadAllText("data/domain/golden-reference.json")).Map("samples").Values.OfType<DataMap>())
        {
            var game=new DefenseState();Check(game.Restore(sample.Map("save")),"original numerical profile restore");
            // Research 1.27 deliberately changes several old small-node values. Verify that
            // the migrated CURRENT model remains identical through a new-format roundtrip;
            // the archived Domain runtime separately preserves the original numerical oracle.
            game.SetCombatSetting("patrol_coverage_multiplier",1);
            var reopened=new DefenseState();Check(reopened.Restore(game.Serialize()),"migrated current profile roundtrip");
            foreach(string key in new[]{"damage","fire_rate","laser_damage","laser_fire_rate","missile_damage","missile_fire_rate","missile_blast_radius","missile_speed_multiplier","projectile_speed_multiplier","interceptor_range_multiplier","laser_range_multiplier","missile_range_multiplier","weapon_range_bonus"})
                if(sample.Map("weapons").ContainsKey(key))Compare(reopened.DroneStats().Value(key),game.DroneStats().Value(key),"current weapon value preserved through migration roundtrip "+key);
            foreach(var family in new[]{("interceptor","patrol_k"),("missile","patrol_m"),("laser","patrol_l")})
                foreach(string key in new[]{"health","patrol_speed","patrol_radius","patrol_outer_range"})
                    if(sample.Map(family.Item2).ContainsKey(key))Compare(reopened.PatrolStats(family.Item1).Value(key),game.PatrolStats(family.Item1).Value(key),"current patrol value preserved through migration roundtrip "+family.Item1+key);
        }
    }
    private static void RunSpawningAndCombat()
    {
        var invasion=new InvasionDirector();invasion.ConfigureAnchor(Vector3.Back);var rng=new CombatRandom(9918);foreach(var front in invasion.FrontsForWave(29))Near(front.Vector3("world_position").Length(),21.65,"near mothership old altitude preserved",.00001);
        for(int i=0;i<128;i++){var spawn=invasion.SpawnPoint(29,rng);double radius=spawn.Vector3("position").Length();Check(radius>=20-.00001&&radius<=21.2+.00001,"fresh enemies outside enlarged Earth at old altitude");}
        var game=new DefenseState();var surface=new Surface();var battle=new Battlefield(game,surface){Active=true};var craft=battle.SpawnFactoryDrone(battle.Factories[18]);double hitRadius=craft.N("hit_radius");Near(hitRadius,.11*game.CombatSettings.N("aircraft_scale"),"aircraft hit body not scaled");Near(battle.GetDroneWorldPosition(craft).Length(),16.108,"real factory launch point",.00001);
        for(int i=0;i<40;i++){Invoke(battle,"UpdateLaunch",craft,1d/30);Check(battle.GetDroneWorldPosition(craft).Length()>CombatScale.EarthCollisionRadius,"launch path stays outside Earth");}
        Near(battle.GetDroneWorldPosition(craft).Length(),16.7,"real launch reaches unchanged altitude",.00001);Near(craft.N("hit_radius"),hitRadius,"launch keeps actual aircraft size");
        craft["state"]="engaging";craft["space_position"]=Vector3.Back*16.7f;craft["aim_direction"]=Vector3.Back;craft["aim_up"]=Vector3.Up;battle.WorldCache[craft.L("uid")]=craft.Vector3("space_position");
        var target=battle.SpawnEnemy("scout",new(){["wave"]=1L,["stage"]=0,["role"]="claw"},Vector3.Back*17.8f)!;target["hp"]=1000d;target["max_hp"]=1000d;target["velocity"]=Vector3.Zero;
        Check(battle.CanDroneEngagePosition(craft,target.Vector3("space_position")),"unchanged gun range hits nearby enemy above larger ground");var shot=battle.MakeShot(battle.GetDroneMuzzlePosition(craft),target.Vector3("space_position"),10,false,0,battle.DroneWeaponStats(craft));Near(shot.N("projectile_speed"),6.4,"kinetic projectile speed unchanged");battle.UpdateShots(.3);Near(target.N("hp"),984,"actual kinetic hit keeps armor damage");
        double contact=CombatGeometry.SphereHitFraction(new Vector3(0,0,17),new Vector3(0,0,15),Vector3.Zero,CombatScale.EarthCollisionRadius);Near(contact,(17-16.07)/2,"enlarged swept collision boundary",.000001);Check(!CombatGeometry.HasLineOfSight(new(0,0,17),new(0,0,-17)),"far-side line of sight uses actual Earth size");
        var ground=battle.SpawnEnemy("scout",new(){["wave"]=1L,["stage"]=0,["role"]="claw"},Vector3.Right*(float)CombatScale.CloseAssault)!;ground["phase"]="ground_attack";Check((bool)Invoke(battle,"CanBombardEarth",ground)!,"enemy parks at the shield-shell standoff for bombardment");double shield=game.EarthHp;Invoke(battle,"FireHostile",ground,2d,false);battle.UpdateShots(1.0);Near(game.EarthHp,shield-2,"real planetary shot from the outer standoff reaches Earth");
        var far=battle.SpawnEnemy("scout",new(){["wave"]=28L,["stage"]=1,["role"]="claw"},Vector3.Back*56f)!;Near(far.N("combat_entry_radius"),19,"strategic entry boundary translated");Invoke(battle,"AdvanceEnemyRoute",far,.1);Near(far.N("route_speed"),(56-19)/24d,"strategic transit keeps altitude-distance timing",.0001);
        var legacyCarrier=battle.SpawnEnemy("carrier",new(){["wave"]=28L,["stage"]=1},Vector3.Back*17.44f)!;legacyCarrier["post_carrier"]=true;Invoke(battle,"AdvancePostCarrier",legacyCarrier,.1);Near(legacyCarrier.Vector3("space_position").Length(),17.45,"old mobile carrier standoff translated",.00001);
    }
    private static readonly string[] Points={"space_position","spawn_space","landing_start","target_space","aim_point","launch_position","from_space","to_space"};
    private static readonly string[] Radii={"spawn_radius","combat_entry_radius","frontier_radius"};
    private static void CompareMigrated(object? actual,object? old,string key="",string label="migration")
    {
        if(old is DataMap map){Check(actual is DataMap,label+" map");if(actual is DataMap current)foreach(var(k,v)in map)CompareMigrated(current.Value(k),v,k,label+"."+k);return;}
        if(old is List<object?> list){var current=(actual as System.Collections.IEnumerable)?.Cast<object?>().ToList();Check(current!=null&&current.Count==list.Count,label+" count");if(current!=null)for(int i=0;i<Math.Min(current.Count,list.Count);i++)CompareMigrated(current[i],list[i],"",label+"."+i);return;}
        if(old is Vector3 p&&Points.Contains(key)){Check(actual is Vector3,label+" point");if(actual is Vector3 moved){Near(moved.Length(),p.Length()+(p.Length()>.000001?_migrationDelta:0),label+" radial displacement",.00002);if(p.Length()>.000001)Check(moved.Normalized().DistanceTo(p.Normalized())<.000001,label+" same world direction");}return;}
        if(CombatSnapshotCodec.IsNumber(old)&&Radii.Contains(key)){Near(DataMap.Number(actual),DataMap.Number(old)+_migrationDelta,label+" radius");return;}
        Compare(actual,old,label+" unchanged");
    }
    private static void RunSnapshotMigration()
    {
        var oldLive=DataMap.Parse(File.ReadAllText("artifacts/csharp-live-golden.json"));DataMap? active=null,oldGame=null;int snapshots=0;
        foreach(string file in new[]{"artifacts/csharp-live-golden.json","artifacts/csharp-weapons-golden.json","artifacts/csharp-roles-golden.json"})foreach(var scenario in DataMap.Parse(File.ReadAllText(file)).List("scenarios").OfType<DataMap>())foreach(var sample in scenario.List("samples").OfType<DataMap>())
        {
            var source=sample.Map("battle");Check(Battlefield.ValidateCombatSnapshot(source),"actual old snapshot accepted");var before=source.ToJson();var normalized=Battlefield.NormalizeCombatSnapshot(source);Check(normalized!=null&&normalized.I("version")==4&&normalized.N("earth_radius")==16,"explicit new radius marker");if(normalized==null)continue;Check(before==source.ToJson(),"migration never modifies old player input");CompareMigrated(Decode(normalized),Decode(source));Check(DataMap.Equivalent(normalized,Battlefield.NormalizeCombatSnapshot(normalized)),"normalization idempotent");snapshots++;
            if(file.Contains("live")&&scenario.S("mode")=="base"&&sample.I("frame")==600){active=source;oldGame=sample.Map("state");}
        }
        Check(snapshots>=100,"real saved actor/proc/status coverage");Check(active!=null&&oldGame!=null,"old running battle fixture");if(active==null||oldGame==null)return;
        var game=new DefenseState();Check(game.Restore(oldGame),"old game settings restore");var battle=new Battlefield(game);Check(battle.RestoreCombatSnapshot(active),"old live actors actually restore");foreach(var d in battle.Drones)Check(battle.GetDroneWorldPosition(d).Length()>16,"restored aircraft never embedded in Earth");var firstSave=battle.SerializeCombatSnapshot();Check(firstSave.I("version")==4&&firstSave.N("earth_radius")==16,"fresh save records world radius");var copy=new Battlefield(game);Check(copy.RestoreCombatSnapshot(DataMap.Parse(firstSave.ToJson())),"new radius snapshot restores");foreach(var d in battle.Drones)Near(copy.GetDroneWorldPosition(copy.Drones.Single(x=>x.L("uid")==d.L("uid"))).DistanceTo(battle.GetDroneWorldPosition(d)),0,"new save does not apply displacement twice",.000001);
        var oldFields=Decode(active);var drone=oldFields.List("_drones").OfType<DataMap>().First();drone["landing_start"]=new Vector3(.2f,0,5.2f);drone["aim_point"]=new Vector3(.3f,0,6.4f);oldFields.Map("_post_plan")["frontier_radius"]=44d;oldFields.Map("_post_plan")["defense_stage"]=1;oldFields.Map("_post_plan")["sortie_elapsed"]=2d;oldFields.Map("_post_plan")["sortie_round"]=1;oldFields.Map("_post_plan")["sortie_started"]=true;
        var augmented=active.DeepClone();augmented["payload"]=CombatSnapshotCodec.Encode(oldFields);var migrated=Battlefield.NormalizeCombatSnapshot(augmented);Check(migrated!=null,"landing and outer cohort metadata migration");if(migrated!=null)CompareMigrated(Decode(migrated),oldFields);
        var invalid=firstSave.DeepClone();invalid["earth_radius"]=32d;var beforeRestore=copy.SerializeCombatSnapshot().ToJson();Check(!copy.RestoreCombatSnapshot(invalid)&&beforeRestore==copy.SerializeCombatSnapshot().ToJson(),"unsupported radius rejected atomically");invalid=firstSave.DeepClone();invalid.Remove("earth_radius");Check(!Battlefield.ValidateCombatSnapshot(invalid),"new-format missing radius rejected");
        var legacy=active.DeepClone();legacy["version"]=1;var fields=Decode(legacy);foreach(var k in new[]{"_fixed_cycle_elapsed","_fixed_cycle_running","_fixed_cohort_complete"})fields.Remove(k);legacy["payload"]=CombatSnapshotCodec.Encode(fields);Check(Battlefield.ValidateCombatSnapshot(legacy),"old v1 still accepted");var legacyBattle=new Battlefield(game);Check(legacyBattle.RestoreCombatSnapshot(legacy),"old v1 uses owning-game timer and radial migration");Check(legacyBattle.SerializeCombatSnapshot().I("version")==4,"v1 upgraded after real restore");
        DefenseCampaignDirector.RegisterSnapshotMigration();var director=new DefenseCampaignDirector(game,battle);var outer=director.Serialize();outer["earth"]=active;var normalizedOuter=DefenseCampaignDirector.NormalizeSnapshot(outer);Check(normalizedOuter!=null&&normalizedOuter.Map("earth").N("earth_radius")==16,"director embeds migrated battle");Check(game.Expedition.SetRuntimeSnapshot(outer)&&game.Expedition.RuntimeSnapshot.Map("earth").N("earth_radius")==16,"nested domain runtime snapshot uses shared migration hook");var domainRoundtrip=game.Serialize();var reopened=new DefenseState();Check(reopened.Restore(domainRoundtrip)&&reopened.Expedition.RuntimeSnapshot.Map("earth").N("earth_radius")==16,"nested runtime reloading is idempotent");
    }
    private static void RunRadius8Migration()
    {
        var path = "tests/CombatManaged/Fixtures/combat-radius8.json";
        var fixture = DataMap.Parse(File.ReadAllText(path));
        var source = fixture.Map("battle");
        Check(source.N("earth_radius") == 8 && Battlefield.ValidateCombatSnapshot(source), "actual radius8 snapshot accepted");
        string input = source.ToJson();
        var normalized = Battlefield.NormalizeCombatSnapshot(source);
        Check(normalized != null && normalized.N("earth_radius") == 16, "radius8 explicitly becomes radius16");
        if (normalized == null) return;
        _migrationDelta = 8;
        CompareMigrated(Decode(normalized), Decode(source));
        _migrationDelta = WorldScale.EarthRadiusDelta;
        Check(input == source.ToJson() && DataMap.Equivalent(normalized, Battlefield.NormalizeCombatSnapshot(normalized)), "radius8 input untouched, upgrade idempotent");
        var game = new DefenseState();
        Check(game.Restore(fixture.Map("game")), "radius8 game companion restored");
        var battle = new Battlefield(game);
        Check(battle.RestoreCombatSnapshot(source), "actual radius8 battle restored");
        Check(battle.Drones.Count > 0 && battle.Drones.All(d => battle.GetDroneWorldPosition(d).Length() > R), "all real radius8 aircraft above radius16 ground");
        var director = new DefenseCampaignDirector(game, battle);
        var saved = director.Serialize(); saved["earth"] = source;
        Check(game.Expedition.SetRuntimeSnapshot(saved) && game.Expedition.RuntimeSnapshot.Map("earth").N("earth_radius") == 16, "nested explicit radius8 actor snapshot upgraded exactly once");
    }
    private static void RunFrontierReach()
    {
        foreach(string kind in new[]{"interceptor","missile","laser"})
        {
            var game=new DefenseState();foreach(string id in new[]{"C_G1","C_N2","M_N1","L_N1"})game.DeepResearch[id]=1L;game.InvalidateFactoryStats();var surface=new Surface(kind);var battle=new Battlefield(game,surface){Active=true};battle.Random.Seed=122;var craft=battle.SpawnFactoryDrone(battle.Factories[18]);craft["launch_age"]=1.3;craft["state"]="patrol";
            for(int stage=0;stage<3;stage++)
            {
                double radius=new double[]{56,88,128}[stage];battle.Enemies.Clear();battle.Shots.Clear();craft["space_position"]=Vector3.Back*16.7f;craft["target_uid"]=-1L;craft["aim_target_uid"]=-1L;battle.WorldCache[craft.L("uid")]=craft.Vector3("space_position");var enemy=battle.SpawnEnemy("carrier",new(){["wave"]=28L,["stage"]=1},Vector3.Back*(float)radius)!;enemy["hp"]=1e8;enemy["max_hp"]=1e8;enemy["energy_hp"]=0d;enemy["energy_max_hp"]=0d;enemy["velocity"]=Vector3.Zero;
                Check(!battle.AssignmentValid(craft,enemy),"previous range cannot acquire next layer "+kind+radius);game.DeepResearch[new[]{"C_A2","C_A3","C_G2"}[stage]]=1L;game.InvalidateFactoryStats();Invoke(battle,"RefreshConfiguration");Check(battle.AssignmentValid(craft,enemy),"new command tier acquires shifted mothership "+kind+radius);bool hit=false;double started=battle.Clock;
                for(int tick=0;tick<6000;tick++){typeof(Battlefield).GetProperty("Clock")!.SetValue(battle,battle.Clock+1d/30);battle.UpdateDrones(1d/30);Invoke(battle,"UpdateFiring",1d/30);battle.UpdateShots(1d/30);if(enemy.N("hp")<1e8){hit=true;break;}}
                Check(hit&&battle.GetDroneWorldPosition(craft).Length()<=battle.DronePatrolStats(craft).N("action_radius")+.001,"physical flight and actual hit in translated envelope "+kind+radius);Console.WriteLine($"EARTH_SCALE_FLIGHT {kind} radius={radius} seconds={battle.Clock-started:0.00}");
            }
        }
        var g=new DefenseState{Wave=29};g.Expedition.SetEarthLiberated(true);var b=new Battlefield(g,new Surface());var director=new DefenseCampaignDirector(g,b);Near(director.GetStatus().N("ceasefire_remaining"),225,"five normal 45-second rest rounds unchanged");director.Step(225);Near(b.PostPlan.N("frontier_radius"),56,"new director uses shifted configured first layer");b.UpdateSpawning(30);Check(b.Enemies.Where(e=>e.B("post_carrier")).All(e=>Math.Abs(e.Vector3("space_position").Length()-56)<.0001),"real frontier mother spawn radius56");var snapshot=director.Serialize();Check(DefenseCampaignDirector.ValidateSnapshot(snapshot),"current active outer battle save valid");
    }
}
