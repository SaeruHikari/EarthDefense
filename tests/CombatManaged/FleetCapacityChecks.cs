using Earthward.Combat;
using Earthward.Domain;
using Earthward.Tests;
using System.Reflection;
using System.Diagnostics;

internal static class FleetCapacityChecks
{
    public static void Run()
    {
        FleetStressFixture.ConfigureCatalog();var g=new DefenseState();var b=new Battlefield(g);var fixture=new FleetStressFixture(g,b,1200,0);fixture.Setup(FleetStressMode.Patrol);int checks=0,failed=0;
        void Check(bool ok,string label){checks++;if(!ok){failed++;Console.WriteLine("FAIL "+label);}}
        DataMap Original()
        {
            int capacity=0,used=b.Drones.Sum(d=>d.I("capacity_cost",1)),pending=0;
            foreach(var f in b.Factories.Values)
            {
                capacity+=f.I("capacity");var occupied=new HashSet<int>();
                foreach(var d in b.Drones)if(d.L("factory_site_id")==f.L("site_id"))for(int i=0;i<d.I("capacity_cost",1);i++)occupied.Add(d.I("patrol_slot")+i);
                var plan=(List<(int Berth,string Frame,int Cost)>)typeof(Battlefield).GetMethod("ProductionPlan",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(b,new object[]{f})!;
                foreach(var row in plan)if(Enumerable.Range(row.Berth,row.Cost).All(p=>!occupied.Contains(p))){pending++;for(int i=0;i<row.Cost;i++)occupied.Add(row.Berth+i);}
            }
            return new(){["active"]=b.Drones.Count,["capacity_points"]=capacity,["used_points"]=used,["pending"]=pending};
        }
        var rng=new Random(127);
        for(int round=0;round<30;round++)
        {
            for(int j=0;j<round%7&&b.Drones.Count>0;j++)b.Drones.RemoveAt(rng.Next(b.Drones.Count));
            if(round%4==0){var f=b.Factories.Values.ElementAt(round%b.Factories.Count);f["capacity"]=Math.Max(1,f.I("capacity")-3);}
            Check(DataMap.Equivalent(b.FleetCapacityState(),Original()),"single-pass occupancy preserves holes/heavy points/capacity shrink round "+round);
        }
        Console.WriteLine($"FLEET_CAPACITY checks={checks} failed={failed}");if(failed>0)System.Environment.ExitCode=1;
    }
}
