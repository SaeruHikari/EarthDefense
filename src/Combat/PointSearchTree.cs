using System;
using System.Collections.Generic;
using Godot;

namespace Earthward.Combat;

/// <summary>Reusable managed point BVH. Queries visit spatially nearest nodes and preserve source order for exact ties.</summary>
internal sealed class PointSearchTree<T>
{
    internal struct Entry { public Vector3 Position; public T Value; public int Order; public long Identity; public bool Active; public Entry(Vector3 position,T value,int order,long identity) { Position=position;Value=value;Order=order;Identity=identity;Active=true; } }
    private struct Node { public Vector3 Low,High,Center; public float Radius; public int Start,Count,Left,Right; }
    internal interface IFilter { bool Accept(in Entry entry); }
    internal interface IScoreFilter : IFilter { double Score(in Entry entry, double distanceSquared); }
    internal interface IBoundsFilter : IFilter { bool MayContain(Vector3 center, float radius); }
    internal interface IBoundedScoreFilter : IScoreFilter, IBoundsFilter { }
    private Entry[] _entries=Array.Empty<Entry>();
    private Node[] _nodes=Array.Empty<Node>();
    private int _count,_nodeCount,_treeCount,_generation,_builds;
    private Entry[] _incoming=Array.Empty<Entry>();
    private int[] _seen=Array.Empty<int>(),_newEntries=Array.Empty<int>();
    private readonly Dictionary<long,int> _slots=new();
    private sealed class AxisComparer : IComparer<Entry> { private readonly int _axis; public AxisComparer(int axis)=>_axis=axis; public int Compare(Entry a,Entry b) { int c=a.Position[_axis].CompareTo(b.Position[_axis]);return c!=0?c:a.Order.CompareTo(b.Order); } }
    private static readonly AxisComparer[] Axes={new(0),new(1),new(2)};
    public void Clear(int capacity)
    {
        _count=0;_generation++;
        if(_entries.Length<capacity)
        {
            int next=Math.Max(capacity,_entries.Length*2);
            Array.Resize(ref _entries,next);Array.Resize(ref _incoming,next);Array.Resize(ref _seen,next);Array.Resize(ref _newEntries,next);
        }
        if(_nodes.Length<capacity*2+1)Array.Resize(ref _nodes,Math.Max(capacity*2+1,_nodes.Length*2));
    }
    public void Add(Vector3 position,T value,int order,long identity)=>_incoming[_count++]=new(position,value,order,identity);
    public void Build()
    {
        if(_count==0){_nodeCount=0;_slots.Clear();return;}
        if(_nodeCount>0&&_count<=_treeCount&&_count*2>=_treeCount&&++_builds%32!=0)
        {
            int newcomers=0;
            for(int i=0;i<_count;i++)
            {
                ref var entry=ref _incoming[i];
                if(_slots.TryGetValue(entry.Identity,out int slot)){_entries[slot]=entry;_seen[slot]=_generation;}
                else _newEntries[newcomers++]=i;
            }
            int next=0;
            for(int i=0;i<_treeCount;i++)
            {
                if(_seen[i]==_generation)continue;
                _slots.Remove(_entries[i].Identity);
                if(next<newcomers){_entries[i]=_incoming[_newEntries[next++]];_slots[_entries[i].Identity]=i;}
                else _entries[i].Active=false;
            }
            for(int id=_nodeCount-1;id>=0;id--)
            {
                ref var node=ref _nodes[id];
                if(node.Count>0)
                {
                    node.Low=node.High=_entries[node.Start].Position;
                    for(int j=node.Start+1;j<node.Start+node.Count;j++){node.Low=node.Low.Min(_entries[j].Position);node.High=node.High.Max(_entries[j].Position);}
                }
                else{node.Low=_nodes[node.Left].Low.Min(_nodes[node.Right].Low);node.High=_nodes[node.Left].High.Max(_nodes[node.Right].High);}
                node.Center=(node.Low+node.High)*.5f;node.Radius=(node.High-node.Low).Length()*.5f;
            }
            return;
        }
        _nodeCount=0;_treeCount=_count;Array.Copy(_incoming,_entries,_count);BuildNode(0,_count);_slots.Clear();
        for(int i=0;i<_count;i++)_slots[_entries[i].Identity]=i;
    }
    private int BuildNode(int start,int count)
    {
        int id=_nodeCount++;var low=_entries[start].Position;var high=low;
        for(int i=start+1;i<start+count;i++){low=low.Min(_entries[i].Position);high=high.Max(_entries[i].Position);}
        var node=new Node{Low=low,High=high,Center=(low+high)*.5f,Radius=(high-low).Length()*.5f,Start=start,Count=count,Left=-1,Right=-1};
        if(count>12)
        {
            var size=high-low;int axis=size.X>=size.Y&&size.X>=size.Z?0:size.Y>=size.Z?1:2;
            int half=count/2;Partition(start,start+count-1,start+half,axis);
            node.Left=BuildNode(start,half);node.Right=BuildNode(start+half,count-half);node.Count=0;
        }
        _nodes[id]=node;return id;
    }
    private void Partition(int low,int high,int middle,int axis)
    {
        while(low<high)
        {
            var pivot=_entries[(low+high)/2];int i=low,j=high;
            while(i<=j)
            {
                while(Axes[axis].Compare(_entries[i],pivot)<0)i++;
                while(Axes[axis].Compare(_entries[j],pivot)>0)j--;
                if(i<=j){(_entries[i],_entries[j])=(_entries[j],_entries[i]);i++;j--;}
            }
            if(middle<=j)high=j;else if(middle>=i)low=i;else break;
        }
    }
    private static double LowerBound(Vector3 p,in Node node)
    {
        float x=Math.Max(node.Low.X-p.X,Math.Max(0,p.X-node.High.X));
        float y=Math.Max(node.Low.Y-p.Y,Math.Max(0,p.Y-node.High.Y));
        float z=Math.Max(node.Low.Z-p.Z,Math.Max(0,p.Z-node.High.Z));
        return new Vector3(x,y,z).LengthSquared();
    }
    public bool FindNearest<TFilter>(Vector3 origin,double rangeSquared,ref TFilter filter,out T value) where TFilter:struct,IFilter
    {
        int best=-1;double distance=rangeSquared;
        if(_nodeCount>0)Search(0,origin,ref distance,ref best,ref filter);
        value=best<0?default!:_entries[best].Value;return best>=0;
    }
    public bool FindNearestBounded<TFilter>(Vector3 origin,double rangeSquared,ref TFilter filter,out T value) where TFilter:struct,IBoundsFilter
    {
        int best=-1;double distance=rangeSquared;
        if(_nodeCount>0)SearchBounded(0,origin,ref distance,ref best,ref filter);
        value=best<0?default!:_entries[best].Value;return best>=0;
    }
    public bool FindBestBounded<TFilter>(Vector3 origin,double lowerMultiplier,ref TFilter filter,out T value) where TFilter:struct,IBoundedScoreFilter
    {
        int best=-1;double score=double.PositiveInfinity;
        if(_nodeCount>0)SearchScoreBounded(0,origin,lowerMultiplier,ref score,ref best,ref filter);
        value=best<0?default!:_entries[best].Value;return best>=0;
    }
    public void CollectInRadius(Vector3 origin,double radiusSquared,List<Entry> result)
    {
        result.Clear();if(_nodeCount>0)Collect(0,origin,radiusSquared,result);
    }
    private void Collect(int id,Vector3 origin,double radiusSquared,List<Entry> result)
    {
        ref var node=ref _nodes[id];
        if(LowerBound(origin,node)>radiusSquared+Math.Max(1e-6,radiusSquared*1e-6))return;
        if(node.Count>0)
        {
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                ref var entry=ref _entries[i];
                if(entry.Active&&origin.DistanceSquaredTo(entry.Position)<=radiusSquared)result.Add(entry);
            }
            return;
        }
        Collect(node.Left,origin,radiusSquared,result);Collect(node.Right,origin,radiusSquared,result);
    }
    public bool FindBest<TFilter>(Vector3 origin,double lowerMultiplier,ref TFilter filter,out T value) where TFilter:struct,IScoreFilter
    {
        int best=-1;double score=double.PositiveInfinity;
        if(_nodeCount>0)SearchScore(0,origin,lowerMultiplier,ref score,ref best,ref filter);
        value=best<0?default!:_entries[best].Value;return best>=0;
    }
    private void SearchScore<TFilter>(int id,Vector3 origin,double multiplier,ref double bestScore,ref int best,ref TFilter filter) where TFilter:struct,IScoreFilter
    {
        ref var node=ref _nodes[id];
        if(LowerBound(origin,node)*multiplier>bestScore+Math.Max(1e-6,bestScore*1e-6))return;
        if(node.Count>0)
        {
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                ref var entry=ref _entries[i];if(!entry.Active)continue;
                double score=filter.Score(entry,origin.DistanceSquaredTo(entry.Position));
                if(score>bestScore||score==bestScore&&(best<0||entry.Order>=_entries[best].Order))continue;
                if(!filter.Accept(entry))continue;
                bestScore=score;best=i;
            }
            return;
        }
        double a=LowerBound(origin,_nodes[node.Left]),b=LowerBound(origin,_nodes[node.Right]);
        if(a<=b){SearchScore(node.Left,origin,multiplier,ref bestScore,ref best,ref filter);SearchScore(node.Right,origin,multiplier,ref bestScore,ref best,ref filter);}
        else{SearchScore(node.Right,origin,multiplier,ref bestScore,ref best,ref filter);SearchScore(node.Left,origin,multiplier,ref bestScore,ref best,ref filter);}
    }
    private void Search<TFilter>(int id,Vector3 origin,ref double bestDistance,ref int best,ref TFilter filter) where TFilter:struct,IFilter
    {
        ref var node=ref _nodes[id];
        // Conservatively include a few float ulps at bounds, then apply the original strict distance comparison at the leaf.
        if(LowerBound(origin,node)>bestDistance+Math.Max(1e-6,bestDistance*1e-6))return;
        if(node.Count>0)
        {
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                ref var entry=ref _entries[i];if(!entry.Active)continue;double distance=origin.DistanceSquaredTo(entry.Position);
                if(distance>bestDistance || distance==bestDistance&&(best<0||entry.Order>=_entries[best].Order))continue;
                if(!filter.Accept(entry))continue;
                bestDistance=distance;best=i;
            }
            return;
        }
        double a=LowerBound(origin,_nodes[node.Left]),b=LowerBound(origin,_nodes[node.Right]);
        if(a<=b){Search(node.Left,origin,ref bestDistance,ref best,ref filter);Search(node.Right,origin,ref bestDistance,ref best,ref filter);}
        else{Search(node.Right,origin,ref bestDistance,ref best,ref filter);Search(node.Left,origin,ref bestDistance,ref best,ref filter);}
    }    private void SearchScoreBounded<TFilter>(int id,Vector3 origin,double multiplier,ref double bestScore,ref int best,ref TFilter filter) where TFilter:struct,IBoundedScoreFilter
    {
        ref var node=ref _nodes[id];
        if(!filter.MayContain(node.Center,node.Radius))return;
        if(LowerBound(origin,node)*multiplier>bestScore+Math.Max(1e-6,bestScore*1e-6))return;
        if(node.Count>0)
        {
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                ref var entry=ref _entries[i];if(!entry.Active)continue;
                double score=filter.Score(entry,origin.DistanceSquaredTo(entry.Position));
                if(score>bestScore||score==bestScore&&(best<0||entry.Order>=_entries[best].Order))continue;
                if(!filter.Accept(entry))continue;
                bestScore=score;best=i;
            }
            return;
        }
        double a=LowerBound(origin,_nodes[node.Left]),b=LowerBound(origin,_nodes[node.Right]);
        if(a<=b){SearchScoreBounded(node.Left,origin,multiplier,ref bestScore,ref best,ref filter);SearchScoreBounded(node.Right,origin,multiplier,ref bestScore,ref best,ref filter);}
        else{SearchScoreBounded(node.Right,origin,multiplier,ref bestScore,ref best,ref filter);SearchScoreBounded(node.Left,origin,multiplier,ref bestScore,ref best,ref filter);}
    }
    private void SearchBounded<TFilter>(int id,Vector3 origin,ref double bestDistance,ref int best,ref TFilter filter) where TFilter:struct,IBoundsFilter
    {
        ref var node=ref _nodes[id];
        if(!filter.MayContain(node.Center,node.Radius))return;
        // Conservatively include a few float ulps at bounds, then apply the original strict distance comparison at the leaf.
        if(LowerBound(origin,node)>bestDistance+Math.Max(1e-6,bestDistance*1e-6))return;
        if(node.Count>0)
        {
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                ref var entry=ref _entries[i];if(!entry.Active)continue;double distance=origin.DistanceSquaredTo(entry.Position);
                if(distance>bestDistance || distance==bestDistance&&(best<0||entry.Order>=_entries[best].Order))continue;
                if(!filter.Accept(entry))continue;
                bestDistance=distance;best=i;
            }
            return;
        }
        double a=LowerBound(origin,_nodes[node.Left]),b=LowerBound(origin,_nodes[node.Right]);
        if(a<=b){SearchBounded(node.Left,origin,ref bestDistance,ref best,ref filter);SearchBounded(node.Right,origin,ref bestDistance,ref best,ref filter);}
        else{SearchBounded(node.Right,origin,ref bestDistance,ref best,ref filter);SearchBounded(node.Left,origin,ref bestDistance,ref best,ref filter);}
    }
}
