using System.Runtime.InteropServices;

namespace SkrGui;

// FreeType 2.14.3 Windows-x64 ABI: C long / FT_Pos / FT_Fixed remain 32-bit.
[StructLayout(LayoutKind.Sequential)] internal struct FT_Generic { public nint Data, Finalizer; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_Vector { public int X, Y; public FT_Vector(int x,int y) { X=x;Y=y; } }
[StructLayout(LayoutKind.Sequential)] internal struct FT_BBox { public int XMin,YMin,XMax,YMax; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_Matrix { public int Xx,Xy,Yx,Yy; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_Bitmap_Size { public short Height,Width; public int Size,XPpem,YPpem; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_Size_Metrics { public ushort XPpem,YPpem; public int XScale,YScale,Ascender,Descender,Height,MaxAdvance; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_SizeRec { public nint Face; public FT_Generic Generic; public FT_Size_Metrics Metrics; public nint Internal; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_FaceRec
{
    public int NumFaces,FaceIndex,FaceFlags,StyleFlags,NumGlyphs;
    public nint FamilyName,StyleName;
    public int NumFixedSizes; public nint AvailableSizes;
    public int NumCharmaps; public nint Charmaps;
    public FT_Generic Generic; public FT_BBox Bbox;
    public ushort UnitsPerEm; public short Ascender,Descender,Height,MaxAdvanceWidth,MaxAdvanceHeight,UnderlinePosition,UnderlineThickness;
    public nint Glyph,Size,Charmap;
}
[StructLayout(LayoutKind.Sequential)] internal struct FT_Glyph_Metrics
{
    public int Width,Height,HoriBearingX,HoriBearingY,HoriAdvance,VertBearingX,VertBearingY,VertAdvance;
}
[StructLayout(LayoutKind.Sequential)] internal struct FT_Bitmap
{
    public uint Rows,Width; public int Pitch; public nint Buffer;
    public ushort NumGrays; public byte PixelMode,PaletteMode; public nint Palette;
}
[StructLayout(LayoutKind.Sequential)] internal struct FT_Outline
{
    public short NContours,NPoints; public nint Points,Tags,Contours; public int Flags;
}
[StructLayout(LayoutKind.Sequential)] internal struct FT_GlyphSlotRec
{
    public nint Library,Face,Next; public uint GlyphIndex; public FT_Generic Generic;
    public FT_Glyph_Metrics Metrics; public int LinearHoriAdvance,LinearVertAdvance;
    public FT_Vector Advance; public uint Format; public FT_Bitmap Bitmap;
    public int BitmapLeft,BitmapTop; public FT_Outline Outline;
    public uint NumSubglyphs; public nint Subglyphs,ControlData;
    public int ControlLen,LsbDelta,RsbDelta; public nint Other,Internal;
}
[StructLayout(LayoutKind.Sequential)] internal struct FT_Size_Request { public int Type; public int Width,Height; public uint HoriResolution,VertResolution; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_MM_Var { public uint NumAxis,NumDesigns,NumNamedStyles; public nint Axis,NamedStyle; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_Var_Axis { public nint Name; public int Minimum,Default,Maximum; public uint Tag,Strid; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_SfntName { public ushort PlatformId,EncodingId,LanguageId,NameId; public nint String; public uint StringLength; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_Palette_Data
{
    public ushort NumPalettes; public nint PaletteNameIds,PaletteFlags;
    public ushort NumPaletteEntries; public nint PaletteEntryNameIds;
}
[StructLayout(LayoutKind.Sequential)] internal struct FT_Color { public byte Blue,Green,Red,Alpha; }
[StructLayout(LayoutKind.Sequential)] internal struct FT_Outline_Funcs
{
    public nint MoveTo,LineTo,ConicTo,CubicTo; public int Shift,Delta;
}

internal static unsafe partial class TextNative
{
    internal static ref FT_FaceRec Face(nint face) => ref *(FT_FaceRec*)face;
    internal static ref FT_GlyphSlotRec Slot(nint slot) => ref *(FT_GlyphSlotRec*)slot;
    internal static ref FT_SizeRec Size(nint size) => ref *(FT_SizeRec*)size;
    internal const int FT_FACE_FLAG_SCALABLE=1, FT_FACE_FLAG_FIXED_SIZES=2, FT_FACE_FLAG_FIXED_WIDTH=4,
        FT_FACE_FLAG_KERNING=1<<6, FT_FACE_FLAG_TRICKY=1<<13, FT_FACE_FLAG_COLOR=1<<14;
    internal const int FT_STYLE_FLAG_ITALIC=1, FT_STYLE_FLAG_BOLD=2;
    internal const int FT_LOAD_DEFAULT=0, FT_LOAD_NO_SCALE=1, FT_LOAD_NO_HINTING=1<<1, FT_LOAD_RENDER=1<<2,
        FT_LOAD_NO_BITMAP=1<<3, FT_LOAD_FORCE_AUTOHINT=1<<5, FT_LOAD_COLOR=1<<20,
        FT_LOAD_TARGET_NORMAL=0, FT_LOAD_TARGET_LIGHT=1<<16, FT_LOAD_TARGET_MONO=2<<16,
        FT_LOAD_TARGET_LCD=3<<16, FT_LOAD_TARGET_LCD_V=4<<16;
    internal const uint FT_GLYPH_FORMAT_OUTLINE=0x6F75746C, FT_GLYPH_FORMAT_BITMAP=0x62697473;
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void FT_Library_Version(nint library,out int major,out int minor,out int patch);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Reference_Face(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint FT_Get_Char_Index(nint face,uint character);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint FT_Face_GetCharVariantIndex(nint face,uint character,uint selector);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint FT_Get_First_Char(nint face,out uint glyph);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint FT_Get_Next_Char(nint face,uint character,out uint glyph);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_New_Size(nint face,out nint size);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Done_Size(nint size);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Activate_Size(nint size);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Request_Size(nint face,ref FT_Size_Request request);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Select_Size(nint face,int strike);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Set_Char_Size(nint face,int width,int height,uint horizontalResolution,uint verticalResolution);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Set_Pixel_Sizes(nint face,uint width,uint height);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Load_Glyph(nint face,uint glyph,int flags);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Render_Glyph(nint slot,int mode);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void FT_GlyphSlot_Embolden(nint slot);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_MulFix(int a,int b);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Get_Kerning(nint face,uint left,uint right,uint mode,out FT_Vector vector);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Get_MM_Var(nint face,out nint axes);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Done_MM_Var(nint library,nint axes);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Set_Var_Design_Coordinates(nint face,uint count,int* coordinates);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint FT_Get_Sfnt_Name_Count(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Get_Sfnt_Name(nint face,uint index,out FT_SfntName name);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Palette_Data_Get(nint face,out FT_Palette_Data data);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Palette_Select(nint face,ushort palette,out nint colors);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Palette_Set_Foreground_Color(nint face,FT_Color color);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Outline_Decompose(ref FT_Outline outline,in FT_Outline_Funcs callbacks,nint user);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void FT_Outline_Transform(ref FT_Outline outline,in FT_Matrix matrix);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void FT_Outline_Translate(ref FT_Outline outline,int x,int y);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Outline_EmboldenXY(ref FT_Outline outline,int x,int y);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void FT_Outline_Get_CBox(in FT_Outline outline,out FT_BBox box);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_ft_face_create_referenced(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_ft_font_create_referenced(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_font_create(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_font_destroy(nint font);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_face_destroy(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_font_get_face(nint font);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_font_set_scale(nint font,int x,int y);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_font_set_ppem(nint font,uint x,uint y);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_font_set_ptem(nint font,float size);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_ft_font_set_load_flags(nint font,int flags);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_ft_font_changed(nint font);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_ot_font_set_funcs(nint font);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int hb_ot_color_has_paint(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int hb_ot_color_has_layers(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint hb_ot_layout_table_get_feature_tags(nint face,uint table,uint start,ref uint count,uint* tags);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int hb_ot_layout_table_find_script(nint face,uint table,uint script,out uint scriptIndex);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint hb_ot_layout_script_get_language_tags(nint face,uint table,uint scriptIndex,uint start,ref uint count,uint* tags);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_language_from_string(byte* text,int length);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint hb_script_from_string(byte* text,int length);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_ot_tags_from_script_and_language(uint script,nint language,ref uint scriptCount,uint* scriptTags,ref uint languageCount,uint* languageTags);
}
