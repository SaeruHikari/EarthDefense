using Earthward.Combat;
using Earthward.Domain;
using Earthward.Tests;
using Godot;

internal static class WorldStressLayoutChecks
{
    public static void Run()
    {
        FleetStressFixture.ConfigureCatalog();var game=new DefenseState();var battle=new Battlefield(game);var fixture=new FleetStressFixture(game,battle,5000,1000,FleetStressLayout.World);fixture.Setup(FleetStressMode.Combat);
        int checks=0,failed=0;
        void Check(bool ok,string label){checks++;if(!ok){failed++;Console.WriteLine("FAIL world stress "+label);}}
        Check(fixture.Layout==FleetStressLayout.World&&battle.Drones.Count==5000&&battle.Enemies.Count==1000&&fixture.Sites.Count==75,"fixed populations and factory count");
        Check(!fixture.FrozenProfilesLoaded&&fixture.StatsHash.Length==64,"current catalogs compile reproducible combat profiles without retired snapshot data");
        Check(battle.Drones.Select(d=>d.S("airframe_id")).Distinct().Count()==9,"all nine physical airframes");Check(battle.Enemies.All(e=>e.S("kind")=="scout"&&e.S("enemy_role_id")=="claw"),"only the ordinary aircraft participates in the stress fixture");
        var normals=fixture.Sites.Select(s=>s.Vector3("normal")).ToArray();var center=normals.Aggregate(Vector3.Zero,(sum,n)=>sum+n)/normals.Length;
        Check(center.Length()<.03f,"factory directions evenly balance around Earth");
        for(int octant=0;octant<8;octant++){int value=octant;Check(normals.Count(n=>(n.X>=0?1:0)+(n.Y>=0?2:0)+(n.Z>=0?4:0)==value)>=6,"every octant receives factories "+octant);}
        var enemyCounts=new Dictionary<long,int>();
        foreach(var enemy in battle.Enemies)
        {
            long site=enemy.L("benchmark_factory_site_id",-1);Check(battle.Factories.ContainsKey(site),"enemy has a real nearby factory");
            if(!battle.Factories.TryGetValue(site,out var factory))continue;
            enemyCounts[site]=enemyCounts.GetValueOrDefault(site)+1;var at=enemy.Vector3("space_position");var home=battle.SurfaceToSpace(factory.Map("site").Vector3("normal"),0).Normalized();
            Check(at.DistanceTo(home*at.Length())<.8f,"enemy is in front of populated defense, not empty space");
            Check(enemy.N("hp")==4000&&enemy.N("max_hp")==4000,"original enemy health");
        }
        foreach(var site in fixture.Sites)
        {
            long id=site.L("site_id");Check(enemyCounts.GetValueOrDefault(id) is 13 or 14,"balanced enemy pressure per factory");
            var defenders=battle.Drones.Where(d=>d.L("factory_site_id")==id).ToArray();Check(defenders.Length>0,"real aircraft production belongs to every factory");
            var target=battle.Enemies.First(e=>e.L("benchmark_factory_site_id")==id).Vector3("space_position");Check(defenders.Any(d=>battle.CanDroneEngagePosition(d,target)),"each factory begins within an actual counterfire envelope");
        }
        Console.WriteLine($"WORLD_STRESS_LAYOUT checks={checks} failed={failed}");if(failed>0)System.Environment.ExitCode=1;
    }
}
