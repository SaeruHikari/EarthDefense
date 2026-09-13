using System.Diagnostics;
namespace SkrGui;
public sealed class VGMeshEmitter(VGBackend backend)
{
    public readonly VGBackend Backend = backend;
    public ulong VertexCount;
    public uint PushVertex(Offsetf pos,float converge)
    {
        Debug.Assert(VertexCount <= uint.MaxValue);
        Backend.CallPushVertex(new VGVertex(pos,converge)); uint index=(uint)VertexCount; ++VertexCount; return index;
    }
    public void PushTriangle(uint i0,uint i1,uint i2) => Backend.CallPushTriangle(i0,i1,i2);
    public void PushQuad(uint i0,uint i1,uint i2,uint i3) { Backend.CallPushTriangle(i0,i1,i2); Backend.CallPushTriangle(i0,i2,i3); }
}
public struct VGFillAANode
{
    public Offsetf Position,PreviousNormal,NextNormal;
    public uint InnerIndex,IncomingOuterIndex,OutgoingOuterIndex;
    public EVGPathFlattenNodeFlags Flags;
    public EVGPathFlattenNodeCacheFlags CacheFlags;
    public readonly bool IsBevel() => VgFlags.All(CacheFlags,EVGPathFlattenNodeCacheFlags.Bevel) || VgFlags.All(CacheFlags,EVGPathFlattenNodeCacheFlags.InnerBevel);
    public readonly bool IsLeftTurn() => VgFlags.All(Flags,EVGPathFlattenNodeFlags.Left);
}
internal static class VgFlags
{
    public static EVGPathFlattenNodeFlags Erase(EVGPathFlattenNodeFlags value,EVGPathFlattenNodeFlags mask) => value & ~mask;
    public static bool All(EVGPathFlattenNodeFlags value,EVGPathFlattenNodeFlags mask) => (value&mask)==mask;
    public static bool Any(EVGPathFlattenNodeFlags value,EVGPathFlattenNodeFlags mask) => (value&mask)!=0;
    public static bool All(EVGPathFlattenNodeCacheFlags value,EVGPathFlattenNodeCacheFlags mask) => (value&mask)==mask;
    public static bool Any(EVGPathFlattenNodeCacheFlags value,EVGPathFlattenNodeCacheFlags mask) => (value&mask)!=0;
}
public static class VGFillAAHelper
{
    public const float KGeometryEpsilon=1e-6f;
    public static Offsetf CalcJoinDirection(Offsetf previousNormal,Offsetf nextNormal)
    {
        var join=(previousNormal+nextNormal)*.5f;float length=join.LengthSquared();
        if(length>KGeometryEpsilon){float scale=1/length;if(scale>600)scale=600;join*=scale;}
        if(!join.IsFinite()||join.LengthSquared()<=KGeometryEpsilon)join=nextNormal.LengthSquared()>KGeometryEpsilon?nextNormal:Offsetf.Zero();
        return join;
    }
    public static VGFillAANode BuildNode(Offsetf previousPos,Offsetf currentPos,Offsetf nextPos,uint innerIndex,float aaRadius,float aaMiterLimit=2.4f)
    {
        VGFillAANode node=new(){Position=currentPos,InnerIndex=innerIndex};
        var previousDelta=currentPos-previousPos;var nextDelta=nextPos-currentPos;
        float previousLength=previousDelta.Length(),nextLength=nextDelta.Length();
        var previousDirection=previousLength>KGeometryEpsilon?previousDelta/previousLength:Offsetf.Zero();
        var nextDirection=nextLength>KGeometryEpsilon?nextDelta/nextLength:Offsetf.Zero();
        float cross=nextDirection.X*previousDirection.Y-previousDirection.X*nextDirection.Y;
        if(cross>0)node.Flags|=EVGPathFlattenNodeFlags.Left;
        node.PreviousNormal=previousDirection.CcwNormal();node.NextNormal=nextDirection.CcwNormal();
        if(node.PreviousNormal.LengthSquared()<=KGeometryEpsilon)node.PreviousNormal=node.NextNormal;
        if(node.NextNormal.LengthSquared()<=KGeometryEpsilon)node.NextNormal=node.PreviousNormal;
        var joinDirection=CalcJoinDirection(node.PreviousNormal,node.NextNormal);float miterLengthSquared=joinDirection.LengthSquared();
        float minSegmentLength=previousLength<nextLength?previousLength:nextLength;
        float inverseRadius=aaRadius>KGeometryEpsilon?1/aaRadius:0;
        float innerLimit=minSegmentLength*inverseRadius;if(innerLimit<1.01f)innerLimit=1.01f;
        if(aaRadius>KGeometryEpsilon&&miterLengthSquared>innerLimit*innerLimit)node.CacheFlags|=EVGPathFlattenNodeCacheFlags.InnerBevel;
        float miterLimit=aaMiterLimit>KGeometryEpsilon?aaMiterLimit:2.4f;
        if(miterLengthSquared>miterLimit*miterLimit)node.CacheFlags|=EVGPathFlattenNodeCacheFlags.Bevel;
        return node;
    }
    public static void EmitOuterVertices(VGMeshEmitter emitter,ref VGFillAANode node,float aaRadius)
    {
        if(node.IsBevel())
        {
            node.IncomingOuterIndex=emitter.PushVertex(node.Position+node.PreviousNormal*aaRadius,0);
            node.OutgoingOuterIndex=emitter.PushVertex(node.Position+node.NextNormal*aaRadius,0);return;
        }
        uint index=emitter.PushVertex(node.Position+CalcJoinDirection(node.PreviousNormal,node.NextNormal)*aaRadius,0);
        node.IncomingOuterIndex=node.OutgoingOuterIndex=index;
    }
    public static void ConnectSections(VGMeshEmitter emitter,in VGFillAANode previous,in VGFillAANode current,bool reverseWinding=false)
    {
        if(reverseWinding){emitter.PushQuad(previous.InnerIndex,previous.OutgoingOuterIndex,current.IncomingOuterIndex,current.InnerIndex);return;}
        emitter.PushQuad(previous.InnerIndex,current.InnerIndex,current.IncomingOuterIndex,previous.OutgoingOuterIndex);
    }
    public static void EmitJoinTriangle(VGMeshEmitter emitter,in VGFillAANode node)
    {
        if(!node.IsBevel())return;
        if(node.IsLeftTurn()){emitter.PushTriangle(node.InnerIndex,node.OutgoingOuterIndex,node.IncomingOuterIndex);return;}
        emitter.PushTriangle(node.InnerIndex,node.IncomingOuterIndex,node.OutgoingOuterIndex);
    }
    public static void EmitRingNodes(VGMeshEmitter emitter,uint count,Func<uint,VGFillAANode> buildNode,float aaRadius)
    {
        if(aaRadius<=KGeometryEpsilon||count<2)return;
        var first=buildNode(0);EmitOuterVertices(emitter,ref first,aaRadius);var previous=first;
        for(uint i=1;i<count;++i){var current=buildNode(i);EmitOuterVertices(emitter,ref current,aaRadius);ConnectSections(emitter,previous,current);EmitJoinTriangle(emitter,previous);previous=current;}
        ConnectSections(emitter,previous,first);EmitJoinTriangle(emitter,previous);
    }
    public static void EmitIndexedRing(VGMeshEmitter emitter,uint count,Func<uint,Offsetf> pointAt,Func<uint,uint> indexAt,float aaRadius,float aaMiterLimit=2.4f)
    {
        EmitRingNodes(emitter,count,index=>{uint previous=index==0?count-1:index-1;uint next=index+1==count?0:index+1;return BuildNode(pointAt(previous),pointAt(index),pointAt(next),indexAt(index),aaRadius,aaMiterLimit);},aaRadius);
    }
}
