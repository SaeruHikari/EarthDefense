namespace SkrGui;

// Source: framework/build_owner.hpp + src/framework/build_owner.cpp (611561f8).
public sealed class BuildScope : IDisposable
{
    private WeakReference<BuildOwner>? _owner;
    private Action? _scheduleRebuild;
    internal readonly List<Nexus> DirtyNexus=new();
    internal int DirtyIndex;
    internal bool IsBuildScheduled,Building,DirtyNeedsResort;
    public BuildScope(BuildOwner owner,Action? scheduleRebuild=null){_owner=new(owner);_scheduleRebuild=scheduleRebuild;}
    public BuildOwner? Owner()=>_owner!=null&&_owner.TryGetTarget(out var owner)?owner:null;
    public bool HasPendingBuild()=>DirtyNexus.Count!=0;
    public bool IsBuilding()=>Building;
    internal void ScheduleBuildFor(Nexus nexus)
    {
        if(!nexus.InDirtyList){DirtyNexus.Add(nexus);nexus.InDirtyList=true;}
        if(!IsBuildScheduled&&!Building){IsBuildScheduled=true;_scheduleRebuild?.Invoke();}
        if(Building)DirtyNeedsResort=true;
    }
    internal void FinishBuild()
    {
        foreach(var nexus in DirtyNexus){GuiAssert.Require(nexus.Scope==this,"Queued Nexus must remain in its BuildScope");nexus.InDirtyList=false;}
        DirtyNexus.Clear();DirtyIndex=0;IsBuildScheduled=false;Building=false;DirtyNeedsResort=false;
    }
    public void Dispose(){GuiAssert.Require(!Building,"Cannot destroy building BuildScope");FinishBuild();_scheduleRebuild=null;_owner=null;}
}
public sealed class BuildOwner : IDisposable
{
    public sealed class StateLockScope : IDisposable
    {
        private BuildOwner? _owner;
        internal StateLockScope(BuildOwner owner){_owner=owner;++owner._stateLockCount;}
        public void Dispose(){if(_owner==null)return;GuiAssert.Require(_owner._stateLockCount>0,"StateLockScope requires an acquired lock");--_owner._stateLockCount;_owner=null;}
    }
    internal enum EBuildPhase : byte { Idle,BuildCallback,Rebuilding }
    private readonly Action? _scheduleBuild;
    private bool _isBuildScheduled;
    private readonly Dictionary<Nexus,BuildScope> _rootBuildScopes=new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Nexus> _inactiveNexus=new(ReferenceEqualityComparer.Instance);
    private bool _inactiveNexusLocked;
    private BuildScope? _activeBuildScope;
    private Nexus? _buildContext;
    internal Nexus? CurrentBuildTarget;
    private uint _stateLockCount;
    private EBuildPhase _buildPhase;
    public BuildOwner(Action? scheduleBuild=null){_scheduleBuild=scheduleBuild;}
    public bool HasPendingBuild(){foreach(var scope in _rootBuildScopes.Values)if(scope.HasPendingBuild())return true;return false;}
    public bool IsBuilding()=>_buildPhase!=EBuildPhase.Idle;
    public bool IsStateLocked()=>_stateLockCount>0;
    public StateLockScope LockState()=>new(this);
    public void ScheduleBuildFor(Nexus nexus)
    {
        GuiAssert.Require(nexus.Lifecycle==ENexusLifeCycle.Active&&nexus.Dirty&&nexus.Scope!=null,"Only dirty Active Nexus with a scope may schedule");
        var scope=nexus.Scope!;
        GuiAssert.Require(scope.Owner()==this,"Foreign BuildOwner");
        GuiAssert.Require(IsBuilding()||!nexus.InDirtyList,"Cannot reschedule queued Nexus outside build");
        GuiAssert.Require(!IsStateLocked()||scope==_activeBuildScope,"State-locked builds cannot cross BuildScopes");
        GuiAssert.Require(!IsStateLocked()||(_buildPhase==EBuildPhase.BuildCallback&&(nexus==_buildContext||IsStrictDescendant(nexus,_buildContext!)))||(_buildPhase==EBuildPhase.Rebuilding&&IsStrictDescendant(nexus,CurrentBuildTarget!)),"State-locked build may schedule only its context or strict descendants");
        if(!_isBuildScheduled&&_scheduleBuild!=null){_isBuildScheduled=true;_scheduleBuild();}
        scope.ScheduleBuildFor(nexus);
    }
    public void BuildScope(Nexus context,Action? callback=null)
    {
        GuiAssert.Require(_buildPhase==EBuildPhase.Idle,"Build transaction cannot be re-entered");
        GuiAssert.Require(context.Lifecycle==ENexusLifeCycle.Active&&context.Scope!=null,"Build requires Active context and scope");
        var scope=context.Scope!;
        GuiAssert.Require(scope.Owner()==this&&!scope.Building,"Foreign or recursive BuildScope");
        if(callback==null&&!scope.HasPendingBuild())return;
        using(var stateLock=LockState())
        {
            _isBuildScheduled=true;_activeBuildScope=scope;_buildContext=context;scope.Building=true;scope.DirtyIndex=0;
            try
            {
                if(callback!=null){_buildPhase=EBuildPhase.BuildCallback;callback();}
                _buildPhase=EBuildPhase.Rebuilding;FlushDirtyNexus(scope);
            }
            finally
            {
                scope.FinishBuild();CurrentBuildTarget=null;_buildContext=null;_activeBuildScope=null;_buildPhase=EBuildPhase.Idle;_isBuildScheduled=HasPendingBuild()||_inactiveNexus.Count!=0;
            }
        }
    }
    public void AddRoot(Nexus nexus)
    {
        GuiAssert.Require(nexus.Lifecycle==ENexusLifeCycle.Initial&&!IsStateLocked()&&!_rootBuildScopes.ContainsKey(nexus),"AddRoot requires unregistered Initial Nexus outside lock");
        var scope=new BuildScope(this);_rootBuildScopes.Add(nexus,scope);
        using(var stateLock=LockState())nexus.Initialize(null,ulong.MaxValue,NexusSlot.Invalid(),scope,true);
        GuiAssert.Require(nexus.Lifecycle==ENexusLifeCycle.Active,"AddRoot must activate root");
        if(nexus.Dirty)ScheduleBuildFor(nexus);
    }
    public void UpdateRoot(Nexus nexus,Widget widget)
    {
        GuiAssert.Require(nexus.Root&&nexus.Lifecycle==ENexusLifeCycle.Active&&_rootBuildScopes.ContainsKey(nexus),"UpdateRoot requires registered Active root");
        if(ReferenceEquals(nexus.CurrentWidget,widget))return;
        BuildScope(nexus,()=>nexus.UpdateWidgetInternal(widget));
    }
    public void RemoveRoot(Nexus nexus)
    {
        GuiAssert.Require(nexus.Root&&nexus.Lifecycle==ENexusLifeCycle.Active&&nexus.ParentNode==null&&nexus.Scope!=null&&nexus.Scope.Owner()==this&&!IsStateLocked(),"RemoveRoot requires registered Active root outside lock");
        GuiAssert.Require(_rootBuildScopes.TryGetValue(nexus,out var scope)&&scope==nexus.Scope,"Root must retain its BuildScope");
        using(var stateLock=LockState()){nexus.DetachVisualInternal();DeactivateNexus(nexus);scope!.FinishBuild();}
        if(!_isBuildScheduled&&_scheduleBuild!=null){_isBuildScheduled=true;_scheduleBuild();}
    }
    public void FinalizeNexus()
    {
        GuiAssert.Require(_buildPhase==EBuildPhase.Idle&&!_inactiveNexusLocked&&!HasPendingBuild(),"Finalize requires all BuildScopes to finish");
        using(var stateLock=LockState())
        {
            _inactiveNexusLocked=true;
            try
            {
                var inactive=_inactiveNexus.OrderBy(n=>n.Depth).ThenBy(n=>n.Dirty?1:0).ToArray();_inactiveNexus.Clear();
                for(int index=inactive.Length;index>0;--index)
                {
                    var nexus=inactive[index-1];
                    if(nexus.Root){GuiAssert.Require(_rootBuildScopes.ContainsKey(nexus),"Inactive root must stay registered until finalization");DestroyInactiveNexus(nexus);_rootBuildScopes.Remove(nexus);}
                    else DestroyInactiveNexus(nexus);
                }
                GuiAssert.Require(_inactiveNexus.Count==0,"Cannot deactivate during finalization");
            }
            finally{_inactiveNexusLocked=false;}
        }
        _isBuildScheduled=false;
    }
    internal void DeactivateNexus(Nexus nexus)
    {
        GuiAssert.Require(!_inactiveNexusLocked&&nexus.Lifecycle==ENexusLifeCycle.Active,"Deactivate requires Active Nexus outside finalization");
        GuiAssert.Require(nexus.Root==(nexus.ParentNode==null),"Only a root may deactivate without parent");
        GuiAssert.Require(nexus.ParentNode==null||(nexus.ParentPhysicalIndex<nexus.ParentNode.NumChildren()&&nexus.ParentNode.ChildAt(nexus.ParentPhysicalIndex)==nexus),"Attached Nexus needs committed parent index");
        GuiAssert.Require(nexus.Scope?.Owner()==this&&!_inactiveNexus.Contains(nexus),"Foreign or duplicate inactive Nexus");
        DeactivateRecursively(nexus);nexus.ParentNode=null;nexus.ParentPhysicalIndex=ulong.MaxValue;_inactiveNexus.Add(nexus);
    }
    private void DeactivateRecursively(Nexus nexus)
    {
        GuiAssert.Require(nexus.Lifecycle==ENexusLifeCycle.Active,"Recursive deactivation needs Active Nexus");
        nexus.DeactivateInternal();
        ulong count=nexus.NumChildren();for(ulong i=0;i<count;i++){var child=nexus.ChildAt(i);GuiAssert.Require(child.ParentNode==nexus,"Child must retain parent during deactivation");DeactivateRecursively(child);}
    }
    internal Nexus TakeInactiveNexus(Nexus nexus)
    {
        GuiAssert.Require(!_inactiveNexusLocked&&nexus.Lifecycle==ENexusLifeCycle.Inactive&&nexus.ParentNode==null&&!nexus.Root,"Retake requires an inactive non-root subtree outside finalization");
        GuiAssert.Require(_inactiveNexus.Contains(nexus),"Inactive Nexus must be owned by BuildOwner");var result=nexus;_inactiveNexus.Remove(nexus);return result;
    }
    internal void ActivateNexus(Nexus nexus)
    {
        GuiAssert.Require(nexus.Lifecycle==ENexusLifeCycle.Inactive&&!nexus.Root&&nexus.ParentNode!=null&&nexus.Scope?.Owner()==this,"Activation requires a parented inactive non-root");
        nexus.Depth=nexus.ParentNode!.Depth+1;nexus.ActivateInternal();
        ulong count=nexus.NumChildren();for(ulong i=0;i<count;i++){var child=nexus.ChildAt(i);GuiAssert.Require(child.ParentNode==nexus,"Child must retain parent during activation");ActivateNexus(child);}
    }
    private void DestroyInactiveNexus(Nexus nexus)
    {
        GuiAssert.Require(nexus.Lifecycle==ENexusLifeCycle.Inactive&&!nexus.InDirtyList,"Finalization requires inactive subtree and flushed queues");
        ulong count=nexus.NumChildren();for(ulong i=0;i<count;i++){var child=nexus.ChildAt(i);GuiAssert.Require(child.ParentNode==nexus,"Child must retain parent until finalization");DestroyInactiveNexus(child);}
        nexus.DestroyInternal();
    }
    private bool IsStrictDescendant(Nexus nexus,Nexus ancestor)
    { if(nexus.Depth<=ancestor.Depth)return false;for(var parent=nexus.ParentNode;parent!=null;parent=parent.ParentNode)if(parent==ancestor)return true;return false; }
    private void SortDirtyNexus(BuildScope scope)
    { var sorted=scope.DirtyNexus.OrderBy(n=>n.Depth).ThenBy(n=>n.Dirty?1:0).ToArray();scope.DirtyNexus.Clear();scope.DirtyNexus.AddRange(sorted);scope.DirtyNeedsResort=false; }
    private int DirtyNexusIndexAfter(BuildScope scope,int index)
    {
        if(!scope.DirtyNeedsResort)return index+1;
        ++index;SortDirtyNexus(scope);
        while(index>0){var previous=scope.DirtyNexus[index-1];if(!previous.Dirty)break;--index;}
        return index;
    }
    private void FlushDirtyNexus(BuildScope scope)
    {
        SortDirtyNexus(scope);
        while(scope.DirtyIndex<scope.DirtyNexus.Count){var nexus=scope.DirtyNexus[scope.DirtyIndex];GuiAssert.Require(nexus.Scope==scope&&nexus.InDirtyList,"Queued Nexus must retain its scope and dirty membership");nexus.Rebuild();scope.DirtyIndex=DirtyNexusIndexAfter(scope,scope.DirtyIndex);}
        GuiAssert.Require(scope.DirtyIndex==scope.DirtyNexus.Count,"BuildScope must process every dirty Nexus");
    }
    public void Dispose(){GuiAssert.Require(_buildPhase==EBuildPhase.Idle&&_stateLockCount==0&&!_inactiveNexusLocked&&_inactiveNexus.Count==0&&_rootBuildScopes.Count==0,"BuildOwner requires every root removed and finalized before destruction");}
}
