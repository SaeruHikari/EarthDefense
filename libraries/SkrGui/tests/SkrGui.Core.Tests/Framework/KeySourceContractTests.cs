namespace SkrGui.Tests;

internal static class KeySourceContractTests
{
    [GuiTest("audit/framework/key-utf8-value-and-reference-semantics")]
    private static void KeyUtf8ValueAndReferenceSemantics()
    {
        byte[] source={0x61,0,0xff};var key=Key.FromString(new Utf8StringView(source));
        source[0]=0x62;
        Check.That(key==Key.FromString(new Utf8StringView(new byte[]{0x61,0,0xff})));
        Check.False(key==Key.FromString(new Utf8StringView(new byte[]{0x61,0,0xfe})));
        var copy=key;ref readonly var borrowed=ref key.GetString();key.SetString("changed");
        Check.Equal((Utf8StringView)"changed",borrowed.View());Check.False(copy==key);
        ref var mutable=ref key.GetStringMutable();mutable="mutable";Check.Equal((Utf8StringView)"mutable",key.GetString().View());
        Check.Equal(key.GetHashCode(),Key.FromString("mutable").GetHashCode());
        byte[] replacement={0x71,0xff};mutable=new Utf8StringView(replacement);replacement[0]=0x72;
        Check.That(key==Key.FromString(new Utf8StringView(new byte[]{0x71,0xff})));
    }
}
