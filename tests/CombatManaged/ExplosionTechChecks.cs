using Earthward.Combat;
using Earthward.Domain;
using Godot;
using System.Reflection;

internal static class ExplosionTechChecks
{
    private static object? Call(Battlefield b,string method,params object?[] args) => typeof(Battlefield).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(b,args);
    public static void Run()
    {
        int checks=0,failed=0;
        void Check(bool ok,string message){checks++;if(!ok){failed++;Console.WriteLine("FAIL "+message);}}
        void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<.00001,message+$" {actual}/{expected}");
        var game=new DefenseState();var battle=new Battlefield(game);var at=Vector3.Back*(float)(CombatScale.EarthRadius+2);
        var enemy=battle.SpawnEnemy("scout",new(){["role"]="claw",["wave"]=1L},at)!;
        enemy["armor_type"]="legacy";enemy["energy_hp"]=0d;enemy["hp"]=enemy["max_hp"]=10000d;
        Call(battle,"RebuildTargets");
        var source=new DataMap{["damage_type"]="explosive",["missile_blast_damage_multiplier"]=2d};
        battle.DetonateMissile(at,100,.2,source);Near(enemy.N("hp"),9800,"explosion multiplier applies once");Near(source.N("missile_blast_damage_multiplier"),2,"in-flight source remains immutable");
        battle.ApplyEnemyDamage(enemy,100,context:source);Near(enemy.N("hp"),9700,"direct hit does not use explosion-only multiplier");
        battle.DetonateDroneDeath(at,new(){["death_blast_damage"]=100d,["death_blast_radius"]=.2},source);Near(enemy.N("hp"),9600,"self-destruction does not use explosion-only multiplier");
        enemy["hp"]=10000d;
        var split=new DataMap{["damage_type"]="explosive",["primary"]=true,["airframe_id"]="M2",["missile_blast_damage_multiplier"]=2d,["effects"]=new DataMap{["m2_delayed_fraction"] = .35,["m2_delayed_seconds"] = .35}};
        battle.DetonateMissile(at,100,.2,split);Near(enemy.N("hp"),9870,"M2 first region uses 65 percent of amplified budget");
        Check(battle.Shots.Count==1,"M2 emits one delayed region");battle.UpdateShots(.4);Near(enemy.N("hp"),9800,"M2 delayed region does not amplify twice");Check(battle.Shots.Count==0,"delayed region cannot recurse");
        var stats=new DataMap{["missile_blast_damage_multiplier"]=2d,["missile_blast_radius"]=.2,["cluster_fragments"]=3L,["cluster_damage_retention"]=.15};
        var shot=battle.MakeShot(at+Vector3.Right,at,100,true,stats:stats);stats["missile_blast_damage_multiplier"]=9d;Near(shot.Map("source").N("missile_blast_damage_multiplier"),2,"projectile freezes launch multiplier");
        Call(battle,"SpawnCluster",shot,at,enemy);var children=battle.Shots.Where(s=>s.S("proc_kind")=="cluster").ToArray();Check(children.Length==3,"cluster still has exactly three real children");
        enemy["hp"]=10000d;foreach(var child in children)battle.DetonateMissile(at,child.N("damage"),.2,child.Map("source"));Near(enemy.N("hp"),9910,"three child explosions each multiply once");
        var layer=new DataMap{["hp"]=1000d,["max_hp"]=1000d,["energy_hp"]=1000d,["energy_max_hp"]=1000d,["armor_type"]="light"};
        var laser=new DataMap{["tech_abilities"]=new DataMap{["L_G1"]=true,["L_A3"]=true}};
        Near(EnemyArmor.Multiplier(layer,"beam",laser),1.8,"reassigned laser node has no implicit extra armor multiplier");
        laser["laser_energy_multiplier"]=2d;Near(EnemyArmor.Multiplier(layer,"beam",laser),3.6,"explicit old in-flight multiplier is retained");
        var all=new DataMap();foreach(var row in DeepTechnology.Nodes)if(row.S("size")!="small")all[row.S("id")]=true;
        shot.Map("source")["tech_abilities"]=all;
        var validate=typeof(Battlefield).GetMethod("ValidatePerkShot",BindingFlags.Static|BindingFlags.NonPublic)!;
        Check((bool)validate.Invoke(null,new object[]{shot})!,"all current researched flags accepted without hard count cap");
        all["K_UNKNOWN"]=false;Check(!(bool)validate.Invoke(null,new object[]{shot})!,"unknown ability ID rejected even if false");all.Remove("K_UNKNOWN");
        all[all.Keys.First()]=1L;Check(!(bool)validate.Invoke(null,new object[]{shot})!,"non-bool ability payload rejected");all[all.Keys.First()]=true;
        shot.Map("source")["missile_blast_damage_multiplier"]=-1d;Check(!(bool)validate.Invoke(null,new object[]{shot})!,"negative blast multiplier rejected");
        Console.WriteLine($"EXPLOSION_TECH checks={checks} failed={failed}");if(failed>0)System.Environment.ExitCode=1;
    }
}
