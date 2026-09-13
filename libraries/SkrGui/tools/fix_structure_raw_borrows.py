from pathlib import Path
base=Path('libraries/SkrGui.Core')
patches={
 'Framework/Nexus.cs':('    internal Nexus? ParentNode;', '    private BorrowedReference<Nexus> _parentBorrow;\n    internal Nexus? ParentNode {get=>_parentBorrow.Value;set=>_parentBorrow.Value=value;}'),
 'Framework/NexusWidgets.cs':('    private NexusVisual? _ancestorVisualNexus;', '    private BorrowedReference<NexusVisual> _ancestorVisualBorrow;\n    private NexusVisual? _ancestorVisualNexus {get=>_ancestorVisualBorrow.Value;set=>_ancestorVisualBorrow.Value=value;}'),
 'Framework/Widget.cs':('    internal Nexus? AttachedNexus;', '    private BorrowedReference<Nexus> _attachedNexusBorrow;\n    internal Nexus? AttachedNexus {get=>_attachedNexusBorrow.Value;set=>_attachedNexusBorrow.Value=value;}'),
}
for rel,(old,new) in patches.items():
    p=base/rel;s=p.read_text();assert old in s,rel;p.write_text(s.replace(old,new))
p=base/'Visual/VisualNode.cs';s=p.read_text();s=s.replace('    private VisualNode? _parent;', '    private BorrowedReference<VisualNode> _parentBorrow;\n    private VisualNode? _parent {get=>_parentBorrow.Value;set=>_parentBorrow.Value=value;}').replace('    private VisualOwner? _owner;', '    private BorrowedReference<VisualOwner> _ownerBorrow;\n    private VisualOwner? _owner {get=>_ownerBorrow.Value;set=>_ownerBorrow.Value=value;}').replace('    internal VisualNode? AttachedNode;', '    private BorrowedReference<VisualNode> _nodeBorrow;\n    internal VisualNode? AttachedNode {get=>_nodeBorrow.Value;set=>_nodeBorrow.Value=value;}');p.write_text(s)
