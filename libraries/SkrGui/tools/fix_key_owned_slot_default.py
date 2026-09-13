from pathlib import Path
p=Path('libraries/SkrGui.Core/Framework/Key.cs');s=p.read_text().replace('private Utf8StringView _string;','private OwnedUtf8String _string;').replace('_string=new Utf8StringView(value.Bytes.ToArray());','_string=new OwnedUtf8String(value);').replace('ref readonly Utf8StringView GetString()', 'ref readonly OwnedUtf8String GetString()').replace('ref Utf8StringView GetStringMutable()', 'ref OwnedUtf8String GetStringMutable()')
s=s.replace('private readonly ulong _index;','// Encoded zero is invalid, including default(T) and array-zero initialization.\n    private readonly ulong _encodedIndex;\n    private ulong RawIndex=>unchecked(_encodedIndex-1);')
s=s.replace('public NexusSlot(){_index=ulong.MaxValue;}','public NexusSlot(){_encodedIndex=0;}').replace('public NexusSlot(ulong index){_index=index;}','public NexusSlot(ulong index){_encodedIndex=unchecked(index+1);}')
s=s.replace('return _index;','return RawIndex;').replace('_index!=ulong.MaxValue','_encodedIndex!=0').replace('a._index==b._index','a._encodedIndex==b._encodedIndex').replace('_index.CompareTo(b._index)','RawIndex.CompareTo(b.RawIndex)').replace('=>_index.GetHashCode();','=>RawIndex.GetHashCode();')
p.write_text(s)
p=Path('tests/SkrGui.Core.Tests/Framework/KeySourceContractTests.cs');s=p.read_text().replace('"changed",borrowed)', '"changed",borrowed.View())').replace('"mutable",key.GetString())','"mutable",key.GetString().View())')
s=s.replace('        Check.Equal(key.GetHashCode(),Key.FromString("mutable").GetHashCode());', '''        Check.Equal(key.GetHashCode(),Key.FromString("mutable").GetHashCode());
        byte[] replacement={0x71,0xff};mutable=new Utf8StringView(replacement);replacement[0]=0x72;
        Check.That(key==Key.FromString(new Utf8StringView(new byte[]{0x71,0xff})));''')
p.write_text(s)
