using Earthward.Domain;
using System;
using System.Linq;
using System.Collections.Generic;

internal static class Technology127Checks
{
    internal static void Run(Action<bool,string> check, DataMap golden)
    {
        void Check(bool value,string label)=>check(value,"Tech127: "+label);
        void Near(double a,double b,string label)=>Check(Math.Abs(a-b)<=Math.Max(1e-9,Math.Abs(b)*1e-10),label+$" {a} / {b}");
        var definitions=DeepTechnology.Nodes;var byId=definitions.ToDictionary(n=>n.S("id"));
        var migration=CatalogData.Load("technology-migration-127.json");var pairs=migration.Map("pairs");
        var retired=migration.List("retired_small_ids").Cast<string>().ToHashSet();
        string[] forbidden={"laser_turn_bonus","laser_energy_damage_bonus","kinetic_projectile_speed_bonus","missile_turn_bonus","return_speed_bonus","combat_turn_bonus","laser_energy_multiplier","laser_first_energy_multiplier","laser_first_energy_cooldown"};
        Check(definitions.Count==299&&definitions.Count(n=>n.S("size")=="small")==246&&definitions.Count(n=>n.S("size")=="medium")==41&&definitions.Count(n=>n.S("size")=="large")==12,"299=246+41+12 unique fixed nodes");
        foreach(var node in definitions)
        {
            string id=node.S("id");
            Check(node.I("max")==1&&node.Map("values").Count>=1&&(node.S("size")!="small"||node.Map("values").Count<=2),"single purchase and bounded actual effects "+id);
            Check(!node.Map("values").Keys.Intersect(forbidden).Any(),"retired attributes absent even medium/large "+id);
            if(node.S("size")=="small")Check(node.Map("cost").Count==1&&node.Map("cost").N("science")>0&&node.Map("values").Count==1,"science-only one-attribute small "+id);
            foreach(string parent in node.List("requires").Cast<string>())Check(byId.ContainsKey(parent)&&byId[parent].I("layout_depth")<node.I("layout_depth"),"true DAG depth "+parent+" -> "+id);
            Check(node.List("draw_position").Count==2&&node.List("draw_position").All(n=>double.IsFinite(DataMap.Number(n))),"finite explicit layout "+id);
        }
        CheckAdjacentLayout(definitions, check);
        var graph=new DefenseState().GraphNodes();Check(graph.Count==299&&graph.All(n=>n.B("visible")),"locked branches remain visibly connected");
        foreach(var (id,companionValue) in pairs)
        {
            string companion=(string)companionValue!;var original=DeepTechnology.Migration127SourceDefinition(id);var first=byId[id];var second=byId[companion];
            Check(first.S("legacy_pair_id")==id&&second.S("legacy_pair_id")==id&&second.B("is_pair_second")&&second.List("requires").SequenceEqual(new object?[]{id}),"pair retains original stable identity "+id);
            if(!retired.Contains(id))foreach(var(key,value)in original.Map("values"))Near(first.Map("values").N(key)+second.Map("values").N(key),DataMap.Number(value)*(key is "patrol_radius_bonus" or "patrol_outer_bonus"?1.5:1),"two halves preserve useful total including authorized patrol expansion "+id);
            double cost=original.Map("cost").N("science");int tier=original.I("tier");double expected=Math.Ceiling(cost*(1+.1*(tier-1)));
            Near(first.Map("cost").N("science")+second.Map("cost").N("science"),expected,"tier-scaled pair budget "+id);
        }
        var capacityNodes=definitions.Where(n=>n.Map("values").ContainsKey("capacity_add")).ToList();
        Check(capacityNodes.Count==3&&capacityNodes.All(n=>n.S("size")=="medium"&&!n.B("alien")&&n.Map("values").Count==1&&n.Map("values").N("capacity_add")==1),"only three ordinary medium +1 capacity nodes");
        Near(definitions.Sum(n=>n.Map("values").N("missile_blast_damage_bonus")),.30,"new blast-only damage has explicit30 percent whole-tree budget");
        Near(definitions.Sum(n=>n.Map("values").N("kinetic_damage_bonus")),.66,"kinetic fixed-tree bonus stays near old60 percent");
        var full=new DefenseState();Check(full.UnlockAllTechnologyCheat(),"fixed tree completion fixture");
        Near(full.DroneStats().N("missile_blast_damage_multiplier"),1.30,"compiled missile blast multiplier");Near(full.ResearchCapacityBonus,3,"full tree capacity cap");
        Check(full.PurchasedResearchCount==299&&!full.HasResearch("K_R00001"),"unlock-all never grants continuation nodes");
        full.Science=1e12;full.AlienPoints=100000;full.Wave=200;full.CompletedWaves=200;full.SetDefenseReachStage(3);
        Check(full.PurchaseGroup("K_R00001"),"continuation still purchased explicitly");full.UnlockAllTechnologyCheat();Check(full.HasResearch("K_R00001")&&!full.HasResearch("K_R00002"),"cheat preserves rather than extends existing tail");
        var plain=new DefenseState();var statsBefore=plain.DroneStats();plain.DeepResearch["M_S04"]=1L; // malformed direct ownership is used only to inspect the scalar compiler.
        plain.InvalidateFactoryStats();var blastStats=plain.DroneStats();Near(blastStats.N("missile_blast_damage_multiplier"),1+byId["M_S04"].Map("values").N("missile_blast_damage_bonus"),"single blast point compiled");
        foreach(string key in new[]{"damage","laser_damage","missile_damage","missile_blast_radius","missile_fire_rate"})Near(blastStats.N(key),statsBefore.N(key),"blast amplifier cannot change unrelated property "+key);
        // Build legal historical saves using source prerequisites, including mixed legacy + deep research capacity.
        DataMap OldSave(int legacyLevel,int capacityCount)
        {
            var save=new DefenseState().Serialize();save["wave"]=120L;save["completed_waves"]=120L;save["defense_reach_stage"]=3L;
            var tech=save.Map("tech");
            void Legacy(string id,long count)
            {
                if(tech.L(id)>=count)return;tech[id]=count;
                foreach(var(parent,rank)in CatalogData.Definition("legacy-technology.json","definitions",id).Map("requires"))Legacy(parent,DataMap.Integer(rank));
            }
            if(legacyLevel>0)Legacy("hangar_capacity",legacyLevel);
            var nodes=new DataMap();
            void Own(string id){if(nodes.ContainsKey(id))return;foreach(string parent in DeepTechnology.Migration127SourceDefinition(id).List("requires").Cast<string>())Own(parent);nodes[id]=1L;}
            foreach(string id in new[]{"I_S04","I_S09","I_S14","I_S19"}.Take(capacityCount))Own(id);
            save["research_state"]=new DataMap{["version"]=2L,["nodes"]=nodes,["credited"]=new List<object?>(),["successor_levels"]=new DataMap()};return save;
        }
        for(int legacy=0;legacy<=8;legacy++)for(int count=0;count<=4;count++)
        {
            var source=OldSave(legacy,count);string immutable=source.ToJson();var game=new DefenseState();
            Check(game.Restore(source),$"legal mixed capacity archive L{legacy}/N{count}");int points=new[]{0,1,2,4,6}[count];
            Near(game.ResearchCapacityBonus,Math.Min(3,legacy+points),"old capacity converted and capped");
            Near(game.LastResearchMigration.N("previous_capacity"),legacy+points,"migration records actual prior capacity");
            var expectedRefund=new DataMap{["minerals"]=0d,["energy"]=0d,["science"]=0d};
            foreach(var(key,basis)in new[]{("minerals",650d),("energy",370d),("science",500d)})for(int rank=3;rank<legacy;rank++)expectedRefund[key]=expectedRefund.N(key)+Math.Ceiling(basis*Math.Pow(1.32,rank));
            int remaining=Math.Max(0,3-legacy);var oldIds=new[]{"I_S04","I_S09","I_S14","I_S19"};var pointValues=new[]{1,1,2,2};
            for(int i=0;i<count;i++){int accepted=Math.Min(remaining,pointValues[i]);remaining-=accepted;expectedRefund["science"]=expectedRefund.N("science")+DeepTechnology.Migration127SourceDefinition(oldIds[i]).Map("cost").N("science")*(pointValues[i]-accepted)/pointValues[i];}
            foreach(string key in expectedRefund.Keys){Near(game.LastResearchRefund.N(key),expectedRefund.N(key),"refund exact original cost "+key);Near(game.Serialize().N(key),source.N(key)+expectedRefund.N(key),"resources retained plus explicit refund "+key);}
            var current=game.Serialize();var reload=new DefenseState();Check(reload.Restore(current)&&DataMap.Equivalent(reload.Serialize(),current),"newv3 capacity ownership reloads exactly");
            Check(reload.LastResearchRefund.Count==0,"canonical reload cannot farm refund");Check(source.ToJson()==immutable,"source archive never mutated");
            Check(DataMap.Equivalent(game.FactoryPerks.Snapshot().Map("levels"),new FactoryPerks().Snapshot().Map("levels")),"capacity adjustment never invents permanent ownership");
        }
        // All original small investment is retained as two explicit paid nodes, with no accidental free third level.
        foreach(var sample in golden.Map("samples").Values.OfType<DataMap>())
        {
            var source=sample.Map("save");var game=new DefenseState();Check(game.Restore(source),"legacy fixture migrates");
            foreach(string id in source.Map("research_state").Map("nodes").Keys.Where(pairs.ContainsKey))Check(game.HasResearch(id)&&game.HasResearch(pairs.S(id)),"old paid node owns both halves "+id);
            var stable=game.Serialize();foreach(string badKind in new[]{"refund","capacity","schema","unknown","fractional"})
            {
                var bad=stable.DeepClone();var research=bad.Map("research_state");var summary=research.Map("migration");
                switch(badKind){case "refund":summary.Map("refund")["science"]=-1d;break;case "capacity":summary["research_capacity"]=4L;break;case "schema":research["version"]=3.5;break;case "unknown":research.Map("nodes")["X_S99"]=1L;break;case "fractional":research.Map("nodes")["K_S01"]=.5;break;}
                Check(!game.Restore(bad)&&DataMap.Equivalent(stable,game.Serialize()),"malformed migration atomically rejected "+badKind);
            }
        }
        var ranging=new DefenseState{Minerals=10000,Energy=10000,Science=10000,Wave=30,CompletedWaves=29};ranging.RewardKill("boss",3);
        Near(ranging.DroneStats().N("all_target_range_multiplier"),1,"range has neutral default");
        Check(DeepTechnology.Definition("C_N1").List("requires").ToHashSet().SetEquals(new object?[]{"C_S21","C_S22"}),"ranging retains its research prerequisites");
        foreach(string id in new[]{"C_S01","C_S21","C_S02","C_S22"})Check(ranging.PurchaseGroup(id),"real ranging prerequisite "+id);
        var rangeBefore=ranging.DroneStats();Check(ranging.PurchaseGroup("C_N1"),"ranging purchases once");
        Near(ranging.DroneStats().N("all_target_range_multiplier"),1.2,"all targets receive independent final20percent range");
        Check(!ranging.DroneStats().ContainsKey("large_target_range_multiplier"),"current ranging removes target-size filter");
        foreach(string field in new[]{"interceptor_range_multiplier","laser_range_multiplier","missile_range_multiplier","weapon_range_bonus"})Near(ranging.DroneStats().N(field),rangeBefore.N(field),"range not pre-multiplied before Combat final range "+field);
        foreach(var frame in AirframeCatalog.Definitions)Near(ranging.AircraftDroneStats(frame.S("kind"),3,0,frame.S("id")).N("all_target_range_multiplier"),1.2,"physical aircraft carries final range multiplier "+frame.S("id"));
        var rangeCopy=new DefenseState();Check(rangeCopy.Restore(ranging.Serialize())&&rangeCopy.HasResearch("C_N1")&&rangeCopy.DroneStats().N("all_target_range_multiplier")==1.2,"existing purchased ranging persists without repurchase");
        // Base capacities, user coefficients and permanent hangar capacity remain outside the research-only cap.
        var external=new DefenseState();external.UnlockAllTechnologyCheat();int before=external.FactoryCapacity("interceptor");external.SetCombatSetting("factory_capacity_multiplier",4);Check(external.FactoryCapacity("interceptor")>before&&external.ResearchCapacityBonus==3,"configuration is independent of research cap");
        var profile=external.FactoryPerks.Snapshot();profile.Map("levels")["expanded_hangar"]=5L;Check(external.FactoryPerks.ImportSnapshot(profile)&&external.FactoryPerks.Equip("interceptor",-1,0,"expanded_hangar"),"existing capacity perk can be equipped");Near(external.FactoryCapacityForSite("interceptor")-external.FactoryCapacity("interceptor"),5,"permanent capacity not confiscated by research cap");
    }
    // Layout is deliberately excluded from the old visual fixture comparison below.
    // Recompute its contract from actual research prerequisites, not stored row numbers.
    private static void CheckAdjacentLayout(IReadOnlyList<DataMap> nodes, Action<bool,string> check)
    {
        void Check(bool value,string label)=>check(value,"Tech127 layout: "+label);
        var byId=nodes.ToDictionary(n=>n.S("id"));
        var depths=new Dictionary<string,int>();
        var resolving=new HashSet<string>();
        int Depth(string id)
        {
            if(depths.TryGetValue(id,out int known))return known;
            if(!resolving.Add(id))throw new InvalidDataException("Cycle in research layout: "+id);
            var parents=byId[id].List("requires").Cast<string>();
            int depth=1+parents.Select(Depth).DefaultIfEmpty(0).Max();
            resolving.Remove(id);depths[id]=depth;return depth;
        }
        const string branches="KMLIDC";
        static double X(DataMap n)=>DataMap.Number(n.List("draw_position")[0]);
        static double Y(DataMap n)=>DataMap.Number(n.List("draw_position")[1]);
        static double Size(DataMap n)=>n.S("size") switch {"small"=>12,"medium"=>20,_=>32};
        int singles=0,multiples=0,edges=0,paired=0;
        foreach(var node in nodes)
        {
            string id=node.S("id");int depth=Depth(id);
            var parents=node.List("requires").Cast<string>().ToArray();edges+=parents.Length;
            Check(node.N("layout_depth")==depth&&node.N("layout_row")==depth,"metadata matches independently derived depth "+id);
            double lane=node.N("layout_lane");
            Check(lane==Math.Truncate(lane)&&lane>=-4&&lane<=4,"bounded whole spoke "+id);
            int branch=branches.IndexOf(node.S("branch"),StringComparison.Ordinal);
            Check(branch>=0,"recognized branch "+id);
            double angle=(branch*60+lane*6)*Math.PI/180;
            double radius=220+(depth-1)*84;
            Check(Math.Abs(X(node)-Math.Sin(angle)*radius)<.00001&&Math.Abs(Y(node)+Math.Cos(angle)*radius)<.00001,"CSV position matches next-ring polar layout "+id);
            if(parents.Length==1){singles++;Check(depth-Depth(parents[0])==1,"single parent exactly one circle inward "+id);}
            else if(parents.Length>1){multiples++;Check(depth-parents.Max(Depth)==1&&parents.All(p=>Depth(p)<depth),"deepest of multiple parents exactly one circle inward "+id);}
            if(node.B("is_pair_second"))
            {
                paired++;var parent=byId[parents.Single()];
                double dx=X(node)-X(parent),dy=Y(node)-Y(parent);
                Check(node.S("branch")==parent.S("branch")&&node.N("layout_lane")==parent.N("layout_lane")&&Math.Abs(Math.Sqrt(dx*dx+dy*dy)-84)<.00001,"split progression stays on the same ray exactly 84 apart "+id);
            }
        }
        Check(depths.Values.Max()==13,"true DAG has thirteen circles");
        Check(singles==235&&multiples==44&&edges==335&&paired==123,"all adjacent dependencies and split pairs audited");
        double minimumGap=double.PositiveInfinity;
        for(int i=0;i<nodes.Count;i++)for(int j=i+1;j<nodes.Count;j++)
        {
            double dx=X(nodes[i])-X(nodes[j]),dy=Y(nodes[i])-Y(nodes[j]);
            minimumGap=Math.Min(minimumGap,Math.Sqrt(dx*dx+dy*dy)-Size(nodes[i])-Size(nodes[j]));
        }
        Check(minimumGap>=8,"every pair of visible nodes retains at least eight pixels of edge clearance");
    }

}
