namespace SkrGui;
// Source: text/layout/text_shaped_pipeline.cpp:1033-1435 @ 611561f8.
internal static partial class TextAlgorithms
{
    private static float SourceMin(float a,float b) => b < a ? b : a;
    private static ulong BreakGlyphStart(TextPlacedGlyph glyph) => glyph.SourceBegin;
    private static ulong BreakGlyphEnd(TextPlacedGlyph glyph) => glyph.SourceEnd;
    private static int BreakGlyphCount(TextPlacedGlyph glyph) => (int)glyph.ClusterCount;
    private static ETextGraphemeFlag BreakGlyphFlags(TextPlacedGlyph glyph) => glyph.Flags;
    private static bool HasLineFlag(ETextLineBreakFlag flags,ETextLineBreakFlag bit) => (flags & bit) == bit;
    private static bool HasAny(ETextLineBreakFlag flags,ETextLineBreakFlag bit) => (flags & bit) != 0;
    private static bool HasAny(ETextGraphemeFlag flags,ETextGraphemeFlag bit) => (flags & bit) != 0;
internal static bool BreakGlyphRanges(IReadOnlyList<TextPlacedGlyph> glyphs, TextRange source_range, float target_width, ulong start, ETextLineBreakFlag flags, Func<TextPlacedGlyph,float> soft_hyphen_advance, List<TextRange> out_ranges)
{
    out_ranges.Clear();

    float width = 0.0f;
    ulong line_start = Math.Max(start, source_range.Start);
    ulong last_end = line_start;
    int previous_safe_break = 0;
    int last_safe_break = -1;
    ulong word_count = 0u;
    bool trim_next = false;

    ulong indent_end = 0u;
    float indent = 0.0f;
    if (HasLineFlag(flags, ETextLineBreakFlag.TrimIndent))
    {
        for (int i = 0; i < glyphs.Count; ++i)
        {
            ETextGraphemeFlag glyph_flags =
                BreakGlyphFlags(glyphs[i]);
            if (!HasAny(
                    glyph_flags,
                    ETextGraphemeFlag.Tab |
                        ETextGraphemeFlag.Space
                ))
            {
                break;
            }
            float advance = GlyphAdvance(glyphs[i]);
            if (indent + advance > target_width)
            {
                indent = 0.0f;
            }
            indent += advance;
            indent_end = BreakGlyphEnd(glyphs[i]);
        }
        indent = SourceMin(indent, 0.6f * target_width);
    }

    float line_width = target_width;
    int glyph_count = glyphs.Count;
    for (int i = 0; i < glyph_count; ++i)
    {
        var glyph = glyphs[(int)(i)];
        if (BreakGlyphStart(glyph) < start)
        {
            previous_safe_break = i + 1;
            continue;
        }
        if (BreakGlyphCount(glyph) > 0u)
        {
            float cluster_advance = 0.0f;
            for (int j = i;
                 j < glyph_count &&
                 BreakGlyphStart(glyph) ==
                     BreakGlyphStart(glyphs[(int)(j)]) &&
                 BreakGlyphEnd(glyph) ==
                     BreakGlyphEnd(glyphs[(int)(j)]);
                 ++j)
            {
                cluster_advance += GlyphAdvance(
                    glyphs[(int)(j)]
                );
            }

            if (line_width > 0.0f &&
                width + cluster_advance > line_width &&
                last_safe_break >= 0)
            {
                int current_safe_break = last_safe_break;
                if (HasAny(
                        flags,
                        ETextLineBreakFlag.TrimStartEdgeSpaces |
                            ETextLineBreakFlag.TrimEndEdgeSpaces
                    ))
                {
                    int start_position = previous_safe_break;
                    int end_position = last_safe_break;
                    while (HasLineFlag(
                               flags,
                               ETextLineBreakFlag.TrimStartEdgeSpaces
                           ) &&
                           trim_next && start_position < end_position &&
                           !HasAny(
                               BreakGlyphFlags(
                                   glyphs[(int)(start_position)]
                               ),
                               ETextGraphemeFlag.SoftHyphen
                           ) &&
                           HasAny(
                               BreakGlyphFlags(
                                   glyphs[(int)(start_position)]
                               ),
                               ETextGraphemeFlag.Space |
                                   ETextGraphemeFlag.BreakHard |
                                   ETextGraphemeFlag.BreakSoft
                           ))
                    {
                        start_position += BreakGlyphCount(
                            glyphs[(int)(start_position)]
                        );
                    }
                    while (HasLineFlag(
                               flags,
                               ETextLineBreakFlag.TrimEndEdgeSpaces
                           ) &&
                           start_position <= end_position &&
                           end_position > 0 &&
                           !HasAny(
                               BreakGlyphFlags(
                                   glyphs[(int)(end_position)]
                               ),
                               ETextGraphemeFlag.SoftHyphen
                           ) &&
                           HasAny(
                               BreakGlyphFlags(
                                   glyphs[(int)(end_position)]
                               ),
                               ETextGraphemeFlag.Space |
                                   ETextGraphemeFlag.BreakHard |
                                   ETextGraphemeFlag.BreakSoft
                           ))
                    {
                        end_position -= BreakGlyphCount(
                            glyphs[(int)(end_position)]
                        );
                    }
                    if (start_position >= 0 &&
                        end_position >= start_position &&
                        last_end <= BreakGlyphStart(
                                        glyphs[(int)(start_position)]
                                    ) &&
                        BreakGlyphStart(
                            glyphs[(int)(start_position)]
                        ) != BreakGlyphEnd(glyphs[(int)(end_position)]))
                    {
                        out_ranges.Add(new(
                            BreakGlyphStart(glyphs[(int)(start_position)]),
                            BreakGlyphEnd(glyphs[(int)(end_position)])));
                        if (target_width > indent && i > (int)(indent_end))
                        {
                            line_width = target_width - indent;
                        }
                        current_safe_break = last_safe_break;
                        last_end = BreakGlyphEnd(
                            glyphs[(int)(end_position)]
                        );
                    }
                    trim_next = true;
                }
                else if (last_end <= line_start)
                {
                    out_ranges.Add(new(
                        line_start,
                        BreakGlyphEnd(glyphs[(int)(last_safe_break)])));
                    if (target_width > indent &&
                        i > (int)(indent_end))
                    {
                        line_width = target_width - indent;
                    }
                    last_end = BreakGlyphEnd(
                        glyphs[(int)(last_safe_break)]
                    );
                }
                line_start = BreakGlyphEnd(
                    glyphs[(int)(current_safe_break)]
                );
                previous_safe_break = current_safe_break + 1;
                while (previous_safe_break < glyph_count &&
                       BreakGlyphEnd(
                           glyphs[(int)(previous_safe_break)]
                       ) == line_start)
                {
                    ++previous_safe_break;
                }
                i = current_safe_break;
                last_safe_break = -1;
                width = 0.0f;
                word_count = 0u;
                continue;
            }

            if (HasLineFlag(flags, ETextLineBreakFlag.Mandatory) &&
                HasAny(
                    BreakGlyphFlags(glyph),
                    ETextGraphemeFlag.BreakHard
                ))
            {
                int current_safe_break = i;
                if (HasAny(
                        flags,
                        ETextLineBreakFlag.TrimStartEdgeSpaces |
                            ETextLineBreakFlag.TrimEndEdgeSpaces
                    ))
                {
                    int start_position = previous_safe_break;
                    int end_position = i;
                    while (HasLineFlag(
                               flags,
                               ETextLineBreakFlag.TrimStartEdgeSpaces
                           ) &&
                           trim_next && start_position < end_position &&
                           HasAny(
                               BreakGlyphFlags(
                                   glyphs[(int)(start_position)]
                               ),
                               ETextGraphemeFlag.Space |
                                   ETextGraphemeFlag.BreakHard |
                                   ETextGraphemeFlag.BreakSoft
                           ))
                    {
                        start_position += BreakGlyphCount(
                            glyphs[(int)(start_position)]
                        );
                    }
                    while (HasLineFlag(
                               flags,
                               ETextLineBreakFlag.TrimEndEdgeSpaces
                           ) &&
                           start_position <= end_position &&
                           end_position > 0 &&
                           HasAny(
                               BreakGlyphFlags(
                                   glyphs[(int)(end_position)]
                               ),
                               ETextGraphemeFlag.Space |
                                   ETextGraphemeFlag.BreakHard |
                                   ETextGraphemeFlag.BreakSoft
                           ))
                    {
                        end_position -= BreakGlyphCount(
                            glyphs[(int)(end_position)]
                        );
                    }
                    trim_next = true;
                    if (start_position >= 0 &&
                        end_position >= start_position &&
                        last_end <= BreakGlyphStart(
                                        glyphs[(int)(start_position)]
                                    ) &&
                        BreakGlyphStart(
                            glyphs[(int)(start_position)]
                        ) != BreakGlyphEnd(glyphs[(int)(end_position)]))
                    {
                        out_ranges.Add(new(
                            BreakGlyphStart(glyphs[(int)(start_position)]),
                            BreakGlyphEnd(glyphs[(int)(end_position)])));
                        if (target_width > indent &&
                            i > (int)(indent_end))
                        {
                            line_width = target_width - indent;
                        }
                        last_end = BreakGlyphEnd(glyph);
                        current_safe_break = i;
                    }
                }
                else if (last_end <= line_start)
                {
                    out_ranges.Add(new( line_start, BreakGlyphEnd(glyph)));
                    if (target_width > indent &&
                        i > (int)(indent_end))
                    {
                        line_width = target_width - indent;
                    }
                    last_end = BreakGlyphEnd(glyph);
                }
                line_start = BreakGlyphEnd(
                    glyphs[(int)(current_safe_break)]
                );
                previous_safe_break = current_safe_break + 1;
                while (previous_safe_break < glyph_count &&
                       BreakGlyphEnd(
                           glyphs[(int)(previous_safe_break)]
                       ) == line_start)
                {
                    ++previous_safe_break;
                }
                last_safe_break = -1;
                width = 0.0f;
                continue;
            }

            if (HasLineFlag(flags, ETextLineBreakFlag.WordBound))
            {
                if (HasAny(
                        BreakGlyphFlags(glyph),
                        ETextGraphemeFlag.BreakSoft
                    ))
                {
                    if (HasAny(
                            BreakGlyphFlags(glyph),
                            ETextGraphemeFlag.SoftHyphen
                        ))
                    {
                        float hyphen_width = soft_hyphen_advance(glyph);
                        if (width + cluster_advance + hyphen_width <=
                                target_width ||
                            hyphen_width >= target_width)
                        {
                            last_safe_break = i;
                            ++word_count;
                        }
                    }
                    else if (i >= (int)(indent_end))
                    {
                        last_safe_break = i;
                        ++word_count;
                    }
                }
                if (HasLineFlag(flags, ETextLineBreakFlag.Adaptive) &&
                    word_count == 0u)
                {
                    last_safe_break = i;
                }
            }
            if (HasLineFlag(flags, ETextLineBreakFlag.GraphemeBound))
            {
                last_safe_break = i;
            }
        }
        width += GlyphAdvance(glyph);
    }

    if (glyph_count > 0)
    {
        if (out_ranges.Count == 0 ||
            (out_ranges[^1].End < source_range.End &&
             previous_safe_break < glyph_count))
        {
            ulong range_start = Math.Max(last_end, line_start);
            if (HasLineFlag(
                    flags,
                    ETextLineBreakFlag.TrimStartEdgeSpaces
                ))
            {
                int start_position = previous_safe_break < glyph_count ?
                    previous_safe_break :
                    glyph_count - 1;
                if (last_end <= BreakGlyphStart(
                                    glyphs[(int)(start_position)]
                                ))
                {
                    int end_position = glyph_count - 1;
                    while (trim_next && start_position < end_position &&
                           HasAny(
                               BreakGlyphFlags(
                                   glyphs[(int)(start_position)]
                               ),
                               ETextGraphemeFlag.Space |
                                   ETextGraphemeFlag.BreakHard |
                                   ETextGraphemeFlag.BreakSoft
                           ))
                    {
                        start_position += BreakGlyphCount(
                            glyphs[(int)(start_position)]
                        );
                    }
                    range_start = BreakGlyphStart(
                        glyphs[(int)(start_position)]
                    );
                }
                else
                {
                    range_start = last_end;
                }
            }
            out_ranges.Add(new( range_start, source_range.End));
        }
    }
    else
    {
        out_ranges.Add(new( 0u, 0u));
    }
    return true;
}
}
