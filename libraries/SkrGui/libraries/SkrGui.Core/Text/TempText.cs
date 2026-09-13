using System.Runtime.InteropServices;
namespace SkrGui;
// Source: widgets/temp_text.hpp and cpp @ 611561f8.
public sealed class TempText : VisualWidgetLeaf
{
    private Utf8StringView _text;
    public Utf8StringView Text { get=>_text; set=>_text=new(value.Bytes.ToArray()); }
    private TextStyle _textStyle=new();
    public TextStyle TextStyle { get=>_textStyle; set=>_textStyle=value.Copy(); }
    public SRGBColor Color=new(1,1,1,1);
    public EVisualTempTextAlign TextAlign=EVisualTempTextAlign.Start;
    public ETextDirectionMode TextDirection=ETextDirectionMode.Auto;
    public bool SoftWrap=true;
    public ETextOverflow Overflow=ETextOverflow.Clip;
    public float TextScaleFactor=1;
    public ulong? MaxLines;
    public List<float> TabStops=[];
    public uint EllipsisCodepoint=TextDefaults.EllipsisCodepoint;
    protected override VisualNode CreateVisual(BuildContext context)
    {var result=new VisualTempText();Apply(result);return result;}
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {var result=(VisualTempText)visual;System.Diagnostics.Debug.Assert(result is not null,"TempText requires VisualTempText");Apply(result);}
    private void Apply(VisualTempText result)
    {result.SetText(Text);result.SetTextStyle(TextStyle);result.SetColor(Color);result.SetTextAlign(TextAlign);result.SetTextDirection(TextDirection);result.SetSoftWrap(SoftWrap);result.SetOverflow(Overflow);result.SetTextScaleFactor(TextScaleFactor);result.SetMaxLines(MaxLines);result.SetTabStops(CollectionsMarshal.AsSpan(TabStops));result.SetEllipsisCodepoint(EllipsisCodepoint);}
}
