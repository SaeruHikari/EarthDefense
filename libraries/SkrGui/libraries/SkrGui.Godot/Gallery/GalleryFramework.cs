namespace SkrGui.Gallery;
// Source: gallery_common/include/.../gallery_framework.hpp and src/gallery_framework.cpp.
public struct GalleryPageFrameDesc
{
 public Utf8StringView AppTitle,PageId,PageTitle,PageSummary;
 public GalleryPageFrameDesc(){}
 public uint PageIndex,PageCount,CaseCount;
}
public struct GalleryCaseFrameDesc
{
 public Utf8StringView CaseId,CaseTitle,CaseSummary;
 public GalleryCaseFrameDesc(){}
 public uint PageIndex,CaseIndex;
 public ulong CaseSummaryMaxLines=5;
 public float CaseSummaryPixelSize=12.5f,CaseInfoWidth=186;
}
public static class GalleryFramework
{
 private const float PageMargin=22,CaseGapX=18,CaseGapY=18,CaseCardPadding=12,CaseContentGap=14,CaseContentPadding=10;
 private static T SlotOf<T>(VisualNode node) where T:VisualSlot
 { if(node?.Slot() is not T slot)throw new InvalidOperationException("Source gallery_slot_of requires a node with the requested slot.");return slot; }
 private static TextStyle MakeTextStyle(float pixelSize)=>new(){FontFamilies=GalleryShared.DefaultFontFamilies(),FontSize=pixelSize};
 public static VisualBorder MakeVisualRect(SRGBColor background,SRGBColor border,Radius radius=default,EdgeInsets borderThickness=default)
 {var node=new VisualBorder();node.SetBackgroundColor(background);node.SetBorderColor(border);node.SetBorderThickness(borderThickness);node.SetRadius(radius);return node;}
 public static VisualPadding MakePadding(VisualNode? child,EdgeInsets padding)
 {var node=new VisualPadding();node.SetPadding(padding);node.SetChild(child);return node;}
 public static VisualConstrained MakeSizedBox(VisualNode? child,float? width,float? height)
 {var node=new VisualConstrained();node.SetSized(width,height);node.SetChild(child);return node;}
 public static VisualTempText MakeTextNode(Utf8StringView text,float pixelSize,SRGBColor color,EVisualTempTextAlign hAlign,ETextOverflow overflow,ulong maxLines)
 {var node=new VisualTempText();node.SetText(GalleryShared.CString(text));node.SetTextStyle(MakeTextStyle(pixelSize));node.SetColor(color);node.SetTextAlign(hAlign);node.SetOverflow(overflow);node.SetMaxLines(maxLines==ulong.MaxValue?null:maxLines);return node;}
 public static VisualNode MakeTextBox(Utf8StringView text,float pixelSize,SRGBColor color,EVisualTempTextAlign hAlign=EVisualTempTextAlign.Left,ETextOverflow overflow=ETextOverflow.Ellipsis,ulong maxLines=1)
 {ulong count=maxLines==ulong.MaxValue?1:Math.Max(1,Math.Min(maxLines,32));return MakeSizedBox(MakeTextNode(text,pixelSize,color,hAlign,overflow,maxLines),null,pixelSize*1.35f*(float)count+4);}
 public static VisualNode MakeRuleNode(SRGBColor color)=>MakeSizedBox(MakeVisualRect(color,new(0,0,0,0)),null,1);
 public static VisualNode MakeCaseInfoNode(GalleryPalette palette,GalleryCaseFrameDesc desc)
 {
  var column=new VisualFlex();column.SetDirection(EAxis.Vertical);column.SetSpacing(7);column.SetCrossAxisAlignment(ECrossAxisAlignment.Stretch);
  string number=$"P{desc.PageIndex+1:D2}-C{desc.CaseIndex+1:D2}";
  column.AddChild(MakeTextBox(number,11.5f,palette.Blue,EVisualTempTextAlign.Left,ETextOverflow.Visible,1));
  column.AddChild(MakeTextBox(desc.CaseTitle,15.5f,palette.Ink,EVisualTempTextAlign.Left,ETextOverflow.Ellipsis,2));
  column.AddChild(MakeTextBox(desc.CaseSummary,desc.CaseSummaryPixelSize,palette.Muted,EVisualTempTextAlign.Left,ETextOverflow.Ellipsis,desc.CaseSummaryMaxLines));return column;
 }
 public static VisualNode MakeCaseCardNode(GalleryPalette palette,GalleryCaseFrameDesc desc,VisualNode? content,bool clipContent=false)
 {
  var grid=new VisualGrid();grid.SetColumns([VisualGridTrackSize.Fixed(desc.CaseInfoWidth),VisualGridTrackSize.Star(1)]);grid.SetRows([VisualGridTrackSize.Star(1)]);
  grid.SetColumnGap(CaseContentGap);grid.SetAlignItems(EVisualGridItemAlignment.Stretch);grid.SetJustifyItems(EVisualGridItemAlignment.Stretch);
  var info=MakeCaseInfoNode(palette,desc);grid.AddChild(info);SlotOf<VisualGridSlot>(info).SetArea(0,0);
  VisualNode? contentChild=content;if(clipContent){var clip=new VisualClipRect();clip.SetChild(contentChild);contentChild=clip;}
  var stage=MakeVisualRect(palette.SurfaceSubtle,palette.GridLine,Radius.Circular(4),EdgeInsets.All(1));stage.SetChild(MakePadding(contentChild,EdgeInsets.All(CaseContentPadding)));grid.AddChild(stage);SlotOf<VisualGridSlot>(stage).SetArea(0,1);
  var card=MakeVisualRect(palette.Surface,palette.Border,Radius.Circular(6),EdgeInsets.All(1));card.SetChild(MakePadding(grid,EdgeInsets.All(CaseCardPadding)));return card;
 }
 public static VisualNode MakePageHeaderNode(GalleryPalette palette,GalleryPageFrameDesc desc)
 {
  var grid=new VisualGrid();grid.SetColumns([VisualGridTrackSize.Fixed(220),VisualGridTrackSize.Star(1),VisualGridTrackSize.Fixed(118)]);
  grid.SetRows([VisualGridTrackSize.Fixed(29),VisualGridTrackSize.Fixed(22),VisualGridTrackSize.Fixed(1)]);grid.SetColumnGap(16);grid.SetAlignItems(EVisualGridItemAlignment.Center);
  var appTitle=MakeTextBox(desc.AppTitle,16,palette.Ink,EVisualTempTextAlign.Left,ETextOverflow.Visible,1);grid.AddChild(appTitle);SlotOf<VisualGridSlot>(appTitle).SetArea(0,0);
  var pageTitle=MakeTextBox(desc.PageTitle,18,palette.Ink,EVisualTempTextAlign.Left,ETextOverflow.Ellipsis,1);grid.AddChild(pageTitle);SlotOf<VisualGridSlot>(pageTitle).SetArea(0,1);
  string pageLine=$"P{desc.PageIndex+1:D2}/{desc.PageCount:D2}";var pageCount=MakeTextBox(pageLine,13,palette.Blue,EVisualTempTextAlign.Right,ETextOverflow.Visible,1);grid.AddChild(pageCount);SlotOf<VisualGridSlot>(pageCount).SetArea(0,2);
  var pageId=MakeTextBox(desc.PageId,12,palette.Blue,EVisualTempTextAlign.Left,ETextOverflow.Ellipsis,1);grid.AddChild(pageId);SlotOf<VisualGridSlot>(pageId).SetArea(1,0);
  var summary=MakeTextBox(desc.PageSummary,13,palette.Muted,EVisualTempTextAlign.Left,ETextOverflow.Ellipsis,1);grid.AddChild(summary);SlotOf<VisualGridSlot>(summary).SetArea(1,1);
  string caseLine=$"{desc.CaseCount} 个案例";var caseCount=MakeTextBox(caseLine,12,palette.Muted,EVisualTempTextAlign.Right,ETextOverflow.Visible,1);grid.AddChild(caseCount);SlotOf<VisualGridSlot>(caseCount).SetArea(1,2);
  var rule=MakeRuleNode(palette.Border);grid.AddChild(rule);SlotOf<VisualGridSlot>(rule).SetArea(2,0,1,3);return grid;
 }
 public static VisualGrid MakeCaseGrid(uint visibleCaseCount)
 {
  var grid=new VisualGrid();grid.SetGap(CaseGapX,CaseGapY);
  if(visibleCaseCount<=1){grid.SetColumns([VisualGridTrackSize.Star(1)]);grid.SetRows([VisualGridTrackSize.Star(1)]);}
  else if(visibleCaseCount==2){grid.SetColumns([VisualGridTrackSize.Star(1),VisualGridTrackSize.Star(1)]);grid.SetRows([VisualGridTrackSize.Star(1)]);}
  else{grid.SetColumns([VisualGridTrackSize.Star(1),VisualGridTrackSize.Star(1)]);grid.SetRows([VisualGridTrackSize.Star(1),VisualGridTrackSize.Star(1)]);}
  return grid;
 }
 public static void PlaceCaseCard(VisualGrid grid,VisualNode card,uint visibleCaseCount,uint caseIndex)
 {
  grid.AddChild(card);var slot=SlotOf<VisualGridSlot>(card);
  if(visibleCaseCount<=2)slot.SetArea(0,caseIndex);else if(visibleCaseCount==3&&caseIndex==2)slot.SetArea(1,0,1,2);else slot.SetArea(caseIndex/2,caseIndex%2);
 }
 public static VisualNode MakePageRoot(GalleryPalette palette,VisualNode pageContent)
 {var root=MakeVisualRect(palette.Background,new(0,0,0,0));root.SetChild(MakePadding(pageContent,EdgeInsets.All(PageMargin)));return root;}
}
