using Earthward.Combat;
using Earthward.Domain;
using Godot;

internal static class SpatialIndexChecks
{
    private struct Filter : PointSearchTree<DataMap>.IScoreFilter
    {
        public Vector3 Origin; public bool Weighted;
        public bool Accept(in PointSearchTree<DataMap>.Entry entry) => entry.Value.N("hp") > 0 && entry.Position.Dot(Vector3.Up) >= -5;
        public double Score(in PointSearchTree<DataMap>.Entry entry,double distance) => Weighted ? (distance + entry.Value.N("penalty")) / entry.Value.N("match") : distance;
    }
    private static readonly Func<Vector3,float,Vector3,double,double,bool> ConeBounds = typeof(Battlefield).GetMethod("SphereIntersectsDirectionCone",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<Func<Vector3,float,Vector3,double,double,bool>>();
    private struct ConeFilter : PointSearchTree<DataMap>.IBoundedScoreFilter
    {
        public Vector3 Direction; public double Cosine,Sine;
        public bool MayContain(Vector3 center,float radius)=>ConeBounds(center,radius,Direction,Cosine,Sine);
        public bool Accept(in PointSearchTree<DataMap>.Entry e)=>e.Value.N("hp")>0&&e.Position.Normalized().Dot(Direction)>=Cosine;
        public double Score(in PointSearchTree<DataMap>.Entry e,double distance)=>(distance+e.Value.N("penalty"))/e.Value.N("match");
    }
    public static void Run()
    {
        var rng=new Random(127);var actors=new List<DataMap>();long next=1;int checks=0,failures=0;var tree=new PointSearchTree<DataMap>();var sphere=new List<PointSearchTree<DataMap>.Entry>();
        for(int frame=0;frame<75;frame++)
        {
            int count=frame%17==0?400:frame%7==0?490:500;
            while(actors.Count>count)actors.RemoveAt(rng.Next(actors.Count));
            while(actors.Count<count)actors.Add(new(){["uid"]=next++});
            if(frame%4==0)actors[rng.Next(actors.Count)]=new(){["uid"]=next++};
            tree.Clear(count);
            for(int i=0;i<count;i++)
            {
                var a=actors[i];var p=new Vector3((float)(rng.NextDouble()*20-10),(float)(rng.NextDouble()*20-10),(float)(rng.NextDouble()*20-10));
                if(i%37==0)p=Vector3.One;
                a["space_position"]=p;a["hp"]=i%19==0?0d:100d;a["penalty"]=i%8*.4;a["match"]=(i%5+1)*.4;
                tree.Add(p,a,i,a.L("uid"));
            }
            tree.Build();
            for(int q=0;q<80;q++)
            {
                var origin=new Vector3((float)(rng.NextDouble()*24-12),(float)(rng.NextDouble()*24-12),(float)(rng.NextDouble()*24-12));double limit=q%4==0?0:q%4==1?1:q%4==2?100:double.PositiveInfinity;
                tree.CollectInRadius(origin,limit,sphere);sphere.Sort((a,b)=>a.Order.CompareTo(b.Order));
                var expectedSphere=actors.Where(actor=>origin.DistanceSquaredTo(actor.Vector3("space_position"))<=limit).ToArray();checks++;if(!sphere.Select(entry=>entry.Value).SequenceEqual(expectedSphere)){failures++;Console.WriteLine("FAIL sphere/refit "+frame+":"+q);}
                var filter=new Filter{Origin=origin};DataMap? expected=null;double best=limit;
                foreach(var a in actors){var e=new PointSearchTree<DataMap>.Entry(a.Vector3("space_position"),a,0,a.L("uid"));double d=origin.DistanceSquaredTo(e.Position);if(d<best&&filter.Accept(e)){best=d;expected=a;}}
                bool found=tree.FindNearest(origin,limit,ref filter,out var actual);checks++;if(found!=(expected!=null)||found&&!ReferenceEquals(expected,actual)){failures++;Console.WriteLine("FAIL nearest/refit "+frame+":"+q);}
                filter.Weighted=true;best=double.PositiveInfinity;expected=null;
                foreach(var a in actors){var e=new PointSearchTree<DataMap>.Entry(a.Vector3("space_position"),a,0,a.L("uid"));double score=filter.Score(e,origin.DistanceSquaredTo(e.Position));if(score<best&&filter.Accept(e)){best=score;expected=a;}}
                found=tree.FindBest(origin,.5,ref filter,out actual);checks++;if(found!=(expected!=null)||found&&!ReferenceEquals(expected,actual)){failures++;Console.WriteLine("FAIL weighted/refit "+frame+":"+q);}
            }
        }
        // Compare actual cone-pruned nearest/weighted queries against unpruned
        // tree queries on global, changing geometry, including narrow horizons.
        for(int frame=0;frame<48;frame++)
        {
            tree.Clear(500); actors.Clear();
            for(int i=0;i<500;i++)
            {
                Vector3 normal=new Vector3((float)(rng.NextDouble()*2-1),(float)(rng.NextDouble()*2-1),(float)(rng.NextDouble()*2-1)).Normalized();
                var actor=new DataMap{["uid"]=i+1L,["hp"]=i%13==0?0d:100d,["penalty"]=i%8*.4,["match"]=(i%5+1)*.4};
                var position=normal*(float)(1+rng.NextDouble()*120);
                if(i%23==0)position=Vector3.Back*16.7f;
                tree.Add(position,actor,i,i+1L);actors.Add(actor);
            }
            tree.Build();
            for(int q=0;q<100;q++)
            {
                double angle=new[]{.00001,.01,.15,.25,.7,Math.PI/2-.000001,Math.PI/2,2.2,Math.PI}[q%9];
                var direction=new Vector3((float)(rng.NextDouble()*2-1),(float)(rng.NextDouble()*2-1),(float)(rng.NextDouble()*2-1)).Normalized();
                if(q%11==0)direction=Vector3.Back;
                var origin=direction*(float)(16+rng.NextDouble()*110);
                var filter=new ConeFilter{Direction=direction,Cosine=Math.Cos(angle),Sine=Math.Sin(angle)};
                bool expected=tree.FindNearest(origin,double.PositiveInfinity,ref filter,out var want);
                bool actual=tree.FindNearestBounded(origin,double.PositiveInfinity,ref filter,out var got);
                checks++;if(expected!=actual||actual&&!ReferenceEquals(want,got)){failures++;Console.WriteLine("FAIL cone nearest "+frame+":"+q);}
                expected=tree.FindBest(origin,.5,ref filter,out want);actual=tree.FindBestBounded(origin,.5,ref filter,out got);
                checks++;if(expected!=actual||actual&&!ReferenceEquals(want,got)){failures++;Console.WriteLine("FAIL cone score "+frame+":"+q);}
            }
        }
        tree.Clear(0);tree.Build();var emptyFilter=new Filter();checks++;if(tree.FindNearest(Vector3.Zero,100,ref emptyFilter,out _))failures++;
        Console.WriteLine($"SPATIAL_INDEX checks={checks} failed={failures}");if(failures>0)System.Environment.ExitCode=1;
    }
}
