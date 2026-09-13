// Exact enum values from SkrGuiCore/text @ 611561f8.
namespace SkrGui;

public enum EFontWeight : int
{
    Thin = 100,
    ExtraLight = 200,
    UltraLight = 200,
    Light = 300,
    SemiLight = 350,
    Normal = 400,
    Regular = 400,
    Medium = 500,
    DemiBold = 600,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
    UltraBold = 800,
    Black = 900,
    Heavy = 900,
    ExtraBlack = 950,
    UltraBlack = 950,
}

public enum EFontStyle : int
{
    /// Upright face design without synthetic slant.
    Normal,
    /// Slanted upright design or a request that may be synthesized by shear.
    Oblique,
    /// Dedicated italic face design.
    Italic,
}

public enum EFontStretch : int
{
    UltraCondensed = 1,
    ExtraCondensed = 2,
    Condensed = 3,
    SemiCondensed = 4,
    Normal = 5,
    Medium = 5,
    SemiExpanded = 6,
    Expanded = 7,
    ExtraExpanded = 8,
    UltraExpanded = 9,
}

public enum ETextHAlign : byte
{
    /// Align to the physical left edge.
    Left,
    /// Center the content without changing glyph advances.
    Center,
    /// Align to the physical right edge.
    Right,
    /// Expand eligible gaps toward the configured maximum width.
    Fill,
}

public enum ETextOverflow : byte
{
    /// Preserve all content outside the nominal layout bounds.
    Visible,
    /// Keep layout unchanged and clip painting to the available bounds.
    Clip,
    /// Replace trimmed content with the configured ellipsis glyphs.
    Ellipsis,
}

public enum ETextDirectionMode : byte
{
    /// Infer the base direction from source text and language metadata.
    Auto,
    /// Force a left-to-right base direction.
    LTR,
    /// Force a right-to-left base direction.
    RTL,
    /// Reserved inherited policy. Standalone buffers currently resolve it with
    /// the same source/language inference used by `Auto`.
    Inherited,
}

public enum ETextOrientation : byte
{
    /// Advance glyphs along the x axis and lines along the y axis.
    Horizontal,
    /// Reserved vertical flow.
    Vertical,
}

[Flags]
public enum ETextFeature : uint
{
    None = 0,
    /// Basic source decoding, face selection, glyph placement, and line layout.
    SimpleLayout = 1 << 0,
    /// Bidirectional analysis and visual run reordering.
    BidiLayout = 1 << 1,
    /// Vertical shaping and layout.
    VerticalLayout = 1 << 2,
    /// Script/language-aware OpenType shaping.
    Shaping = 1 << 3,
    /// Contextual Kashida insertion during justification.
    KashidaJustification = 1 << 4,
    /// Unicode word/grapheme break iterators.
    BreakIterators = 1 << 5,
    /// Bitmap-strike font faces.
    FontBitmap = 1 << 6,
    /// Runtime loading of scalable font data.
    FontDynamic = 1 << 7,
    /// Multi-channel signed distance field rasterization.
    FontMSDF = 1 << 8,
    /// Platform font discovery and character fallback.
    FontSystem = 1 << 9,
    /// Variable-font axis discovery and selection.
    FontVariable = 1 << 10,
    /// Locale-sensitive case conversion.
    ContextSensitiveCaseConversion = 1 << 11,
    /// External text support-data packages.
    SupportData = 1 << 12,
    /// Unicode identifier validation.
    UnicodeIdentifiers = 1 << 13,
    /// Confusable and spoof detection.
    UnicodeSecurity = 1 << 14,
}

public enum ETextSpacing : byte
{
    /// Extra spacing between adjacent non-whitespace source clusters.
    Glyph,
    /// Extra spacing contributed by whitespace between source clusters.
    Space,
    /// Added above the baseline through face ascent.
    Top,
    /// Added below the baseline through face descent.
    Bottom,
}

public enum ETextStructuredTextParser : byte
{
    /// No domain-specific token handling.
    Default,
    /// URI token structure.
    URI,
    /// File-system path token structure.
    File,
    /// Email-address token structure.
    Email,
    /// Delimited list structure.
    List,
    /// Programming-language token structure.
    Script,
    /// Caller-defined parser selected by arguments.
    Custom,
}

public enum ETextAtlasFormat : byte
{
    /// One 8-bit coverage/distance channel.
    L8,
    /// Four 8-bit color/distance channels.
    RGBA8,
}

public enum ETextAtlasAllocationAlgorithm : byte
{
    /// Append rectangles along shelves with minimal bookkeeping.
    ShelfLinear,
    /// Choose the shelf with the least fitting waste.
    ShelfBestFit,
    /// Split reusable free rectangles along one axis.
    GuillotineSplit,
}

public enum ETextPixelMode : byte
{
    /// Premultiplied color glyph pixels.
    Colored,
    /// Single-channel grayscale coverage.
    Gray,
    /// Horizontal LCD grayscale coverage.
    GrayLcd,
    /// Single-channel signed distance field.
    Sdf,
    /// LCD-oriented signed distance field.
    SdfLcd,
    /// Multi-channel signed distance field.
    Msdf,
    /// LCD-oriented multi-channel signed distance field.
    MsdfLcd,
}

public enum ETextRasterMode : byte
{
    /// Rasterize coverage at logical size multiplied by the paint pixel ratio.
    Bitmap,
    /// Rasterize one fixed-PPEM signed distance field and scale it at replay.
    Sdf,
}

public enum ETextHinting : byte
{
    /// Preserve the unhinted outline in both axes.
    None,
    /// Align vertical features without horizontally grid-fitting the outline.
    Light,
    /// Align both axes for maximum sharpness at the cost of spacing changes.
    Normal,
}

public enum ETextSubpixelPositioning : byte
{
    /// Round horizontal shaping metrics and use one whole-pixel bitmap variant.
    Disabled,
    /// Preserve fractional shaping metrics through 20 PPEM, then select
    /// quarter-, half-, or whole-pixel bitmap variants from the effective
    /// raster PPEM.
    Auto,
    /// Cache and select two horizontal bitmap phases.
    Half,
    /// Cache and select four horizontal bitmap phases.
    Quarter,
}

[Flags]
public enum ETextLineBreakFlag : uint
{
    None = 0,
    /// Honor mandatory source line separators.
    Mandatory = 1 << 0,
    /// Prefer Unicode word boundaries.
    WordBound = 1 << 1,
    /// Permit Unicode grapheme boundaries.
    GraphemeBound = 1 << 2,
    /// Fall back from word to grapheme boundaries when needed.
    Adaptive = 1 << 3,
    /// Measure initial indentation and reserve that width on wrapped lines.
    TrimIndent = 1 << 5,
    /// Exclude leading whitespace from ranges created after a line break.
    TrimStartEdgeSpaces = 1 << 6,
    /// Exclude trailing whitespace from wrapped ranges before the final range.
    TrimEndEdgeSpaces = 1 << 7,
    /// Convenience mask of indentation and edge-whitespace policies.
    TrimMask = (1 << 5) | (1 << 6) | (1 << 7),
}

[Flags]
public enum ETextJustificationFlag : uint
{
    None = 0,
    /// Reserved Kashida expansion policy; current layout does not consume it.
    Kashida = 1 << 0,
    /// Expand eligible word boundaries.
    WordBound = 1 << 1,
    /// Reserved edge-space policy; current layout does not consume it.
    TrimEdgeSpaces = 1 << 2,
    /// Reserved post-tab policy; current layout does not consume it.
    AfterLastTab = 1 << 3,
    /// Reserved ellipsis policy; current layout does not consume it.
    ConstrainEllipsis = 1 << 4,
    /// Do not justify the final line.
    SkipLastLine = 1 << 5,
    /// Skip the final line only when it still contains visible characters.
    SkipLastLineWithVisibleChars = 1 << 6,
    /// Permit justification of a one-line buffer despite last-line rules.
    DoNotSkipSingleLine = 1 << 7,
}

public enum ETextOverrunBehavior : byte
{
    /// Preserve the complete line.
    NoTrimming,
    /// Trim at a grapheme boundary.
    TrimChar,
    /// Trim at a word boundary.
    TrimWord,
    /// Trim at a grapheme boundary and append ellipsis.
    TrimEllipsis,
    /// Trim at a word boundary and append ellipsis.
    TrimWordEllipsis,
    /// Always reserve/emit ellipsis while trimming by grapheme.
    TrimEllipsisForce,
    /// Always reserve/emit ellipsis while trimming by word.
    TrimWordEllipsisForce,
}

[Flags]
public enum ETextOverrunFlag : uint
{
    None = 0,
    /// Remove content outside the target width.
    Trim = 1 << 0,
    /// Permit only word-boundary trim points.
    TrimWordOnly = 1 << 1,
    /// Append the configured ellipsis after trimming.
    AddEllipsis = 1 << 2,
    /// Emit ellipsis even when no ordinary trim point fits.
    EnforceEllipsis = 1 << 3,
    /// Reserved justification-aware trimming policy; currently not consumed.
    JustificationAware = 1 << 4,
    /// Allow ellipsis to replace very short content.
    ShortStringEllipsis = 1 << 5,
}

[Flags]
public enum ETextGraphemeFlag : uint
{
    None = 0,
    /// Cluster head containing a valid source grapheme.
    Valid = 1 << 0,
    /// Glyph belongs to a right-to-left run.
    Rtl = 1 << 1,
    /// Layout-only glyph without an ordinary visible source scalar.
    Virtual = 1 << 2,
    /// Whitespace cluster.
    Space = 1 << 3,
    /// Mandatory line-break boundary.
    BreakHard = 1 << 4,
    /// Optional line-break boundary.
    BreakSoft = 1 << 5,
    /// Tab cluster requiring tab-stop resolution.
    Tab = 1 << 6,
    /// Elongation candidate used by script-aware justification.
    Elongation = 1 << 7,
    /// Punctuation cluster.
    Punctuation = 1 << 8,
    /// Underscore cluster, kept distinct for word breaking.
    Underscore = 1 << 9,
    /// Cluster has a shaping dependency on a neighbor and cannot be broken
    /// safely in isolation, such as a ligature, mark, or cursive connection.
    Connected = 1 << 10,
    /// Boundary where Tatweel may be inserted safely.
    SafeToInsertTatweel = 1 << 11,
    /// Embedded object cluster.
    EmbeddedObject = 1 << 12,
    /// Soft-hyphen cluster whose visibility depends on breaking.
    SoftHyphen = 1 << 13,
}

public enum ETextInlineAlignment : byte
{
    /// Select the object's top edge as the source anchor.
    TopTo = 0b0000,
    /// Select the object's center as the source anchor.
    CenterTo = 0b0001,
    /// Select the object's explicit baseline as the source anchor.
    BaselineTo = 0b0011,
    /// Select the object's bottom edge as the source anchor.
    BottomTo = 0b0010,
    /// Select the line top as the destination anchor.
    ToTop = 0b0000,
    /// Select the line center as the destination anchor.
    ToCenter = 0b0100,
    /// Select the line baseline as the destination anchor.
    ToBaseline = 0b1000,
    /// Select the line bottom as the destination anchor.
    ToBottom = 0b1100,
    /// Align object top to line top.
    Top = 0b0000,
    /// Align object center to line center.
    Center = 0b0101,
    /// Align object bottom to line bottom.
    Bottom = 0b1110,
}

public enum ETextGlyphKind : byte
{
    /// Glyph supplied by a loaded face.
    Glyph,
    /// Missing-glyph fallback box. The historical name does not imply that a
    /// hexadecimal label is drawn.
    HexBox,
    /// Embedded object placeholder; the caller paints the object separately.
    Object,
}
