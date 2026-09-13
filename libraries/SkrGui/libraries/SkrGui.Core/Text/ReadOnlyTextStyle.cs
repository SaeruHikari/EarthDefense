using System.Collections;
namespace SkrGui;

// C++ const TextStyle& / const TextStyle* is a live borrowed read-only view.
// Keeping the owner accessor (rather than a snapshot) preserves observation of
// later style assignment, while edits must pass through the source mutation APIs.
public sealed class ReadOnlyTextStyle
{
    private readonly Func<TextStyle> _read;
    private readonly LiveReadOnlyList<TextFontFaceId> _fontFaces;
    private readonly LiveReadOnlyList<FontOpenTypeFeatureValue> _features;
    internal ReadOnlyTextStyle(Func<TextStyle> read)
    {
        _read=read;
        _fontFaces=new(()=>_read().FontFaces);
        _features=new(()=>_read().OpenTypeFeatures);
    }
    public Utf8StringView FontFamilies=>_read().FontFamilies;
    public float FontSize=>_read().FontSize;
    public EFontWeight FontWeight=>_read().FontWeight;
    public EFontStyle FontStyle=>_read().FontStyle;
    public EFontStretch FontStretch=>_read().FontStretch;
    public IReadOnlyList<TextFontFaceId> FontFaces=>_fontFaces;
    public IReadOnlyList<FontOpenTypeFeatureValue> OpenTypeFeatures=>_features;
    public TextStyle Copy()=>_read().Copy();
}
internal sealed class LiveReadOnlyList<T>(Func<IReadOnlyList<T>> read) : IReadOnlyList<T>
{
    public int Count=>read().Count;
    public T this[int index]=>read()[index];
    public IEnumerator<T> GetEnumerator()=>read().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}
