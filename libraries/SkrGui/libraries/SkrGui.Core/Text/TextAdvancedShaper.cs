using System.Runtime.InteropServices;
namespace SkrGui;

internal static unsafe partial class TextNative
{
    [StructLayout(LayoutKind.Sequential)] internal struct hb_feature_t { public uint Tag, Value, Start, End; }
    [StructLayout(LayoutKind.Sequential)] internal struct hb_glyph_info_t { public uint Codepoint, Mask, Cluster, Var1, Var2; }
    [StructLayout(LayoutKind.Sequential)] internal struct hb_glyph_position_t { public int XAdvance, YAdvance, XOffset, YOffset; public uint Var; }
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_buffer_create();
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_buffer_clear_contents(nint buffer);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_buffer_set_cluster_level(nint buffer,int level);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_buffer_set_direction(nint buffer,int direction);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_buffer_set_flags(nint buffer,uint flags);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_buffer_set_script(nint buffer,uint script);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_buffer_set_language(nint buffer,nint language);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_buffer_add_utf32(nint buffer,uint* text,int length,uint itemOffset,int itemLength);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_shape(nint font,nint buffer,hb_feature_t* features,uint count);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern hb_glyph_info_t* hb_buffer_get_glyph_infos(nint buffer,out uint count);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern hb_glyph_position_t* hb_buffer_get_glyph_positions(nint buffer,out uint count);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint hb_glyph_info_get_glyph_flags(hb_glyph_info_t* info);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint hb_icu_script_to_script(int script);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="ubidi_getBaseDirection_72")] internal static extern int ubidi_getBaseDirection(ushort* text,int length);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="u_isblank_72")] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool u_isblank(int cp);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="u_isgraph_72")] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool u_isgraph(int cp);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="u_getBidiPairedBracket_72")] internal static extern int u_getBidiPairedBracket(int codepoint);
}

// Source: text/layout/text_advanced_shaper.cpp @ 611561f8.
internal sealed class AdvancedTextMap
{
    internal TextAdvancedSourceCache Cache = null!;
    internal void Build(TextShapedData shaped)
    {
        Cache = shaped.Advanced; if (Cache.Utf16.Count != 0 || Cache.SourceToUtf16.Count != 0) return;
        foreach (uint cp in shaped.Codepoints)
        { Cache.SourceToUtf16.Add(Cache.Utf16.Count); if (cp <= 0xffff) Cache.Utf16.Add((ushort)cp); else { Cache.Utf16.Add((ushort)((cp >> 10) + 0xd7c0)); Cache.Utf16.Add((ushort)((cp & 0x3ff) | 0xdc00)); } }
        Cache.SourceToUtf16.Add(Cache.Utf16.Count);
    }
    internal ulong CodepointAtUtf16(int offset)
    { int first = 0, last = Cache.SourceToUtf16.Count; while (first < last) { int middle = first + (last - first) / 2; if (Cache.SourceToUtf16[middle] < offset) first = middle + 1; else last = middle; } return (ulong)first; }
}
internal sealed class AdvancedChunk
{
    internal ulong SourceBegin, SourceEnd, SpanIndex;
    internal TextFontFaceId Font;
    internal uint Script = 0x5a7a7a7a;
    internal TextShapedObjectData? Object;
}
internal sealed class AdvancedShapeContext(TextServicesImpl services, TextShapedData desc, nint buffer)
{
    internal readonly TextServicesImpl Services = services;
    internal readonly TextShapedData Desc = desc;
    internal readonly IReadOnlyList<TextResolvedSpan> ResolvedSpans = desc.ResolvedSpans;
    internal readonly IReadOnlyList<byte> GraphemeBoundaries = desc.Advanced.GraphemeBoundaries;
    internal readonly nint Buffer = buffer;
    internal readonly List<TextNative.hb_feature_t> Features = [];
    internal readonly List<uint> ClusterStarts = [];
}
internal static unsafe class TextAdvancedShaper
{
    private const uint ScriptEmoji = 0x5a737965, ScriptCommon = 0x5a797979;
    internal static ulong SpanIndexById(IReadOnlyList<TextLayoutSpan> spans, TextLayoutSpanId id)
        => id.IsValid() && id.Value < (ulong)spans.Count && spans[(int)id.Value].Id == id ? id.Value : ulong.MaxValue;
    private static bool Property(uint cp, IcuUProperty property) => TextNative.u_hasBinaryProperty((int)cp,(int)property);
    private static bool IsEmojiSequenceStart(uint cp,uint next)
    {
        if (next == 0xfe0e && (Property(cp,IcuUProperty.UCHAR_EMOJI) || Property(cp,IcuUProperty.UCHAR_EXTENDED_PICTOGRAPHIC))) return false;
        if (next == 0xfe0f && (Property(cp,IcuUProperty.UCHAR_EMOJI) || Property(cp,IcuUProperty.UCHAR_EXTENDED_PICTOGRAPHIC))) return true;
        return Property(cp,IcuUProperty.UCHAR_EMOJI_PRESENTATION) || Property(cp,IcuUProperty.UCHAR_EMOJI_MODIFIER) || Property(cp,IcuUProperty.UCHAR_REGIONAL_INDICATOR) || Property(cp,IcuUProperty.UCHAR_EMOJI) && Property(next,IcuUProperty.UCHAR_EMOJI_MODIFIER);
    }
    private sealed class ParenEntry { internal uint Open; internal int Script; }
    private sealed class EmojiRange { internal int Begin, End; }
    internal static void CollectScripts(IReadOnlyList<uint> codepoints,List<uint> output)
    {
        output.Clear(); for (int i=0;i<codepoints.Count;i++) output.Add(0); if (codepoints.Count==0) return;
        List<ParenEntry> parens=[]; List<EmojiRange> emojis=[]; int startStack=-1,scriptEnd=0;
        while (scriptEnd<codepoints.Count)
        {
            int script=0; emojis.Clear(); bool inEmoji=false; int scriptBegin=scriptEnd;
            for (;scriptEnd<codepoints.Count;scriptEnd++)
            {
                uint cp=codepoints[scriptEnd],next=scriptEnd+1<codepoints.Count?codepoints[scriptEnd+1]:0;
                if (IsEmojiSequenceStart(cp,next)) { if (!inEmoji) { inEmoji=true; emojis.Add(new(){Begin=scriptEnd,End=scriptEnd}); } }
                else if (inEmoji && cp!=0x200d && cp!=0xfe0f && cp!=0x20e3 && !(Property(cp,IcuUProperty.UCHAR_EXTENDED_PICTOGRAPHIC) && next!=0xfe0e))
                { inEmoji=false; emojis[^1].End=scriptEnd; }
                int status=0; int current=TextNative.uscript_getScript((int)cp,ref status); if (status>0) current=103;
                int bracket=TextNative.u_getIntPropertyValue((int)cp,(int)IcuUProperty.UCHAR_BIDI_PAIRED_BRACKET_TYPE);
                if (bracket==1) parens.Add(new(){Open=cp,Script=script});
                else if (bracket==2 && parens.Count!=0)
                {
                    uint paired=(uint)TextNative.u_getBidiPairedBracket((int)cp);
                    while (parens.Count!=0 && parens[^1].Open!=paired) parens.RemoveAt(parens.Count-1);
                    startStack=Math.Min(startStack,parens.Count-1); if (parens.Count!=0) current=parens[^1].Script;
                }
                bool same=script<=1 || current<=1 || script==current; if (!same) break;
                if (script<=1 && current>1) { script=current; while(startStack+1<parens.Count) { startStack++; parens[startStack].Script=script; } }
                if (bracket==2 && parens.Count!=0) { parens.RemoveAt(parens.Count-1); startStack=Math.Min(startStack,parens.Count-1); }
            }
            if (inEmoji) emojis[^1].End=scriptEnd;
            uint hbScript=TextNative.hb_icu_script_to_script(script); for(int i=scriptBegin;i<scriptEnd;i++) output[i]=hbScript;
            foreach(var emoji in emojis) for(int i=emoji.Begin;i<emoji.End;i++) output[i]=ScriptEmoji;
        }
    }
    internal static void CollectGraphemeBoundaries(TextShapedData desc,AdvancedTextMap map,List<byte> output)
    {
        output.Clear(); for(int i=0;i<=desc.Codepoints.Count;i++) output.Add(0); output[0]=output[desc.Codepoints.Count]=1;
        int spanIndex=0;
        while(spanIndex<desc.Spans.Count)
        {
            var span=desc.Spans[spanIndex]; if(span.Start==span.End) { output[(int)span.Start]=1;spanIndex++;continue; }
            Utf8StringView language=span.Language; ulong begin=span.Start,end=span.End;
            while(spanIndex+1<desc.Spans.Count) { var next=desc.Spans[spanIndex+1]; if(next.Start!=next.End && next.Language!=language) break;spanIndex++;end=next.End; }
            begin=Math.Min(begin,(ulong)desc.Codepoints.Count);end=Math.Clamp(end,begin,(ulong)desc.Codepoints.Count);
            int status=0; byte[] locale=TextAlgorithms.NullTerminated(language); nint iterator;
            fixed(byte* lang=locale) iterator=TextNative.ubrk_open(0,language.IsEmpty()?null:lang,(char*)map.Cache.PinnedUtf16()+map.Cache.SourceToUtf16[(int)begin],map.Cache.SourceToUtf16[(int)end]-map.Cache.SourceToUtf16[(int)begin],ref status);
            if(status<=0 && iterator!=0)
            { for(int boundary=TextNative.ubrk_first(iterator);boundary!=-1;boundary=TextNative.ubrk_next(iterator)) output[(int)map.CodepointAtUtf16(map.Cache.SourceToUtf16[(int)begin]+boundary)]=1; }
            else for(ulong source=begin;source<=end;source++) output[(int)source]=1;
            if(iterator!=0) TextNative.ubrk_close(iterator);spanIndex++;
        }
        foreach(var obj in desc.Objects) { output[(int)Math.Min(obj.Start,(ulong)desc.Codepoints.Count)]=1;output[(int)Math.Min(obj.End,(ulong)desc.Codepoints.Count)]=1; }
    }
    private static ulong NextGraphemeBoundary(IReadOnlyList<byte> boundaries,ulong source,ulong end)
    { ulong next=source+1;while(next<end && boundaries[(int)next]==0) next++;return Math.Min(next,end); }
    private static ETextGraphemeFlag BaseFlags(bool rtl)=>rtl?ETextGraphemeFlag.Rtl:ETextGraphemeFlag.None;
    private static void AppendRawRun(AdvancedShapeContext context,ulong spanIndex,ulong begin,ulong end,bool rtl,TextFontFaceId previousFont,List<TextPlacedGlyph> output)
    {
        var desc=context.Desc;var span=desc.Spans[(int)spanIndex];var service=context.Services;
        void AppendSource(ulong source)
        {
            uint cp=desc.Codepoints[(int)source];if(!desc.PreserveInvalid && !(desc.PreserveControl && TextAlgorithms.IsControl(cp))) return;
            bool zero=TextAlgorithms.IsZeroWidth(cp,desc.PreserveControl),found=false;TextGlyphMetrics glyph=new();
            foreach(var candidate in context.ResolvedSpans[(int)spanIndex].Fonts)
                if(service.GetExactGlyphMetrics(candidate.Font,cp,span.Style.FontSize,ref glyph)){found=true;break;}
            if(!found && previousFont.IsValid() && service.GetExactGlyphMetrics(previousFont,cp,span.Style.FontSize,ref glyph)) found=true;
            if(!found) glyph=TextAlgorithms.MissingGlyphMetrics(cp,span.Style.FontSize);
            if(zero) glyph=TextAlgorithms.ZeroWidthGlyphMetrics(glyph,cp,span.Style.FontSize);
            output.Add(new(){Glyph=glyph,SpanId=span.Id,YOff=glyph.Kind==ETextGlyphKind.Glyph && glyph.GlyphIndex!=0?service.FontBaselineShift(glyph.Font,glyph.FontSize):0,Advance=glyph.Advance.X,ClusterCount=1,Flags=BaseFlags(rtl),SourceBegin=source,SourceEnd=source+1});
        }
        if(rtl)for(ulong source=end;source>begin;source--)AppendSource(source-1);else for(ulong source=begin;source<end;source++)AppendSource(source);
    }
    private static void CollectFeatures(TextFontFaceImpl face,TextStyle style,List<TextNative.hb_feature_t> output)
    {
        output.Clear();foreach(var value in face.FeatureOverrides)output.Add(new(){Tag=value.Tag,Value=value.Value,Start=0,End=uint.MaxValue});
        foreach(var value in style.OpenTypeFeatures)
        { bool replaced=false;for(int i=0;i<output.Count;i++)if(output[i].Tag==value.Tag){var feature=output[i];feature.Value=value.Value;output[i]=feature;replaced=true;break;}if(!replaced)output.Add(new(){Tag=value.Tag,Value=value.Value,Start=0,End=uint.MaxValue}); }
    }
    private static bool MissingGlyphIsAllowed(TextShapedData desc,ulong source)
    {
        uint cp=desc.Codepoints[(int)source];if(TextAlgorithms.IsZeroWidth(cp,desc.PreserveControl)||cp=='\t')return true;
        if(TextNative.u_isblank((int)cp))return false;if(desc.PreserveControl)return TextAlgorithms.IsLinebreak(cp);return !TextNative.u_isgraph((int)cp);
    }
    private static bool ShapeCandidate(AdvancedShapeContext context,ulong sourceBegin,ulong sourceEnd,ulong spanIndex,uint script,bool rtl,TextFontFaceId fontId,List<TextPlacedGlyph> output)
    {
        output.Clear();var face=context.Services.FindFontFace(fontId);if(face is null || face.Face==0)return false;
        var span=context.Desc.Spans[(int)spanIndex];
        if(!context.Services.ResolveLayoutFontSize(face,span.Style.FontSize,out var resolved))return false;
        nint font=resolved.Cache!.ShapingFont;float shapingScale=resolved.ShapingScale();if(font==0)return false;
        var config=context.Services.EffectiveFontRasterConfig(face);bool subpixel=resolved.PreservesHorizontalSubpixel(config);
        float extra=face.EmboldenValue*resolved.Cache.Size26_6/4096f,baseline=context.Services.FontBaselineShift(fontId,span.Style.FontSize);
        TextNative.hb_buffer_clear_contents(context.Buffer);TextNative.hb_buffer_set_cluster_level(context.Buffer,0);TextNative.hb_buffer_set_direction(context.Buffer,rtl?5:4);
        uint flags=0x80;if(context.Desc.PreserveControl)flags|=4;if(sourceBegin==0)flags|=1;if(sourceEnd==(ulong)context.Desc.Codepoints.Count)flags|=2;
        TextNative.hb_buffer_set_flags(context.Buffer,flags);TextNative.hb_buffer_set_script(context.Buffer,script==ScriptEmoji?ScriptCommon:script);
        if(!span.Language.IsEmpty()){byte[] language=span.Language.Bytes.ToArray();fixed(byte* p=language)TextNative.hb_buffer_set_language(context.Buffer,TextNative.hb_language_from_string(p,language.Length));}
        fixed(uint* cp=CollectionsMarshal.AsSpan(context.Desc.Codepoints))TextNative.hb_buffer_add_utf32(context.Buffer,cp,context.Desc.Codepoints.Count,(uint)sourceBegin,(int)(sourceEnd-sourceBegin));
        context.Features.Clear();CollectFeatures(face,span.Style,context.Features);
        fixed(TextNative.hb_feature_t* features=CollectionsMarshal.AsSpan(context.Features))TextNative.hb_shape(font,context.Buffer,features,(uint)context.Features.Count);
        var infos=TextNative.hb_buffer_get_glyph_infos(context.Buffer,out uint count);var positions=TextNative.hb_buffer_get_glyph_positions(context.Buffer,out count);
        if(infos==null||positions==null)return false;if(count==0)return true;
        uint lastNonZero=count-1;bool final=sourceEnd==(ulong)context.Desc.Codepoints.Count;
        if(final)for(uint i=count;i>0;i--){lastNonZero=i-1;if(positions[lastNonZero].XAdvance!=0)break;}
        context.ClusterStarts.Clear();for(uint i=0;i<count;i++)context.ClusterStarts.Add(Math.Clamp(infos[i].Cluster,(uint)sourceBegin,(uint)sourceEnd-1));context.ClusterStarts.Add((uint)sourceEnd);context.ClusterStarts.Sort();
        int unique=0;for(int i=0;i<context.ClusterStarts.Count;i++)if(unique==0||context.ClusterStarts[i]!=context.ClusterStarts[unique-1])context.ClusterStarts[unique++]=context.ClusterStarts[i];context.ClusterStarts.RemoveRange(unique,context.ClusterStarts.Count-unique);
        float remainder=0;
        for(uint first=0;first<count;)
        {
            uint cluster=Math.Clamp(infos[first].Cluster,(uint)sourceBegin,(uint)sourceEnd-1),last=first+1;
            while(last<count&&Math.Clamp(infos[last].Cluster,(uint)sourceBegin,(uint)sourceEnd-1)==cluster)last++;
            int low=0,high=context.ClusterStarts.Count;while(low<high){int mid=low+(high-low)/2;if(context.ClusterStarts[mid]<=cluster)low=mid+1;else high=mid;}
            uint clusterEnd=low==context.ClusterStarts.Count?(uint)sourceEnd:context.ClusterStarts[low];bool valid=true;
            for(uint i=first;i<last;i++)
            {
                uint glyphIndex=TextAlgorithms.IsZeroWidth(context.Desc.Codepoints[(int)cluster],context.Desc.PreserveControl)?0:infos[i].Codepoint;
                if(glyphIndex==0&&!MissingGlyphIsAllowed(context.Desc,cluster))valid=false;
            }
            for(uint i=first;i<last;i++)
            {
                uint cp=context.Desc.Codepoints[(int)cluster],glyphIndex=TextAlgorithms.IsZeroWidth(cp,context.Desc.PreserveControl)?0:infos[i].Codepoint;
                TextGlyphMetrics glyph=new(){Font=fontId,Codepoint=cp,GlyphIndex=glyphIndex,FontSize=span.Style.FontSize};
                if(glyphIndex!=0&&!context.Services.GetGlyphMetrics(fontId,cp,glyphIndex,span.Style.FontSize,ref glyph))return false;
                float shift=glyphIndex!=0?baseline:0,xOff=0,yOff=shift,advance=0;
                if(cp=='\t'||TextNative.u_isblank((int)cp)||TextAlgorithms.IsLinebreak(cp))remainder=0;
                if(glyphIndex!=0)
                {
                    float rawX=positions[i].XOffset/64f*shapingScale,rawY=positions[i].YOffset/64f*shapingScale,rawAdvance=positions[i].XAdvance/64f*shapingScale+extra;
                    xOff=subpixel?rawX:MathF.Round(remainder+rawX,MidpointRounding.AwayFromZero);yOff=shift-MathF.Round(rawY,MidpointRounding.AwayFromZero);
                    if(subpixel)advance=MathF.Abs(rawAdvance);
                    else{float full=remainder+rawAdvance,rounded=MathF.Round(full,MidpointRounding.AwayFromZero);advance=MathF.Abs(rounded);if(config.KeepRoundingRemainders)remainder=full-rounded;}
                }
                if(advance!=0&&(!final||i<lastNonZero))
                {var spacing=TextAlgorithms.IsWhitespace(cp)?ETextSpacing.Space:ETextSpacing.Glyph;advance+=context.Desc.Spacing[(int)spacing]+face.SpacingValues[(int)spacing];}
                var glyphFlags=BaseFlags(rtl);uint hbFlags=TextNative.hb_glyph_info_get_glyph_flags(infos+i);if((hbFlags&1)!=0)glyphFlags|=ETextGraphemeFlag.Connected;if((hbFlags&4)!=0)glyphFlags|=ETextGraphemeFlag.SafeToInsertTatweel;if(i==first&&valid)glyphFlags|=ETextGraphemeFlag.Valid;
                output.Add(new(){Glyph=glyph,SpanId=span.Id,XOff=xOff,YOff=yOff,Advance=advance,ClusterCount=i==first?last-first:0,Flags=glyphFlags,SourceBegin=cluster,SourceEnd=clusterEnd});
            }
            first=last;
        }
        return true;
    }
    private static void ShapeRun(AdvancedShapeContext context,ulong sourceBegin,ulong sourceEnd,ulong spanIndex,uint script,bool rtl,ulong fallbackIndex,ulong previousBegin,ulong previousEnd,TextFontFaceId previousFont,List<TextPlacedGlyph> output)
    {
        if(sourceBegin>=sourceEnd||spanIndex>=(ulong)context.ResolvedSpans.Count)return;
        var resolved=context.ResolvedSpans[(int)spanIndex];var style=context.Desc.Spans[(int)spanIndex].Style;ulong candidateCount=context.Services.FontCandidateCount(resolved);TextFontFaceId font=new();
        if(fallbackIndex<candidateCount)
        {
            font=context.Services.ResolveFontCandidate(style,resolved,fallbackIndex);
            if(!font.IsValid()){ShapeRun(context,sourceBegin,sourceEnd,spanIndex,script,rtl,fallbackIndex+1,sourceBegin,sourceEnd,previousFont,output);return;}
        }
        else if(candidateCount>0&&(fallbackIndex==candidateCount||fallbackIndex>candidateCount&&sourceBegin!=previousBegin))
        {
            ulong next=NextGraphemeBoundary(context.GraphemeBoundaries,sourceBegin,sourceEnd);
            font=context.Services.ResolveSystemFallback(style,CollectionsMarshal.AsSpan(context.Desc.Codepoints).Slice((int)sourceBegin,(int)(next-sourceBegin)),context.Desc.Spans[(int)spanIndex].Language);
        }
        if(!font.IsValid()){AppendRawRun(context,spanIndex,sourceBegin,sourceEnd,rtl,previousFont,output);return;}
        if(script==ScriptEmoji&&candidateCount>0&&context.Services.HasFeature(ETextFeature.FontSystem))
        {
            var face=context.Services.FindFontFace(font);
            if(face is not null&&!face.HasColorGlyphs()){ShapeRun(context,sourceBegin,sourceEnd,spanIndex,script,rtl,fallbackIndex+1,sourceBegin,sourceEnd,font,output);return;}
        }
        List<TextPlacedGlyph> candidate=[];
        if(!ShapeCandidate(context,sourceBegin,sourceEnd,spanIndex,script,rtl,font,candidate))
        {ShapeRun(context,sourceBegin,sourceEnd,spanIndex,script,rtl,fallbackIndex+1,sourceBegin,sourceEnd,font,output);return;}
        if(candidate.Count==0)
        {
            if(fallbackIndex>=candidateCount)
            {var span=context.Desc.Spans[(int)spanIndex];output.Add(new(){Glyph=new(){Font=font,Codepoint=context.Desc.Codepoints[(int)sourceBegin],FontSize=span.Style.FontSize},SpanId=span.Id,ClusterCount=1,Flags=ETextGraphemeFlag.Valid,SourceBegin=sourceBegin,SourceEnd=sourceEnd});return;}
            ShapeRun(context,sourceBegin,sourceEnd,spanIndex,script,rtl,fallbackIndex+1,sourceBegin,sourceEnd,font,output);return;
        }
        TextFontFaceId recursivePrevious=fallbackIndex>=candidateCount?font:new();List<TextRange> failed=[];
        for(int index=0;index<candidate.Count;)
        {
            var head=candidate[index];System.Diagnostics.Debug.Assert(head.ClusterCount>0);
            if((head.Flags&ETextGraphemeFlag.Valid)==0)
            {ulong begin=head.SourceBegin;while(begin>sourceBegin&&context.GraphemeBoundaries[(int)begin]==0)begin--;ulong end=head.SourceEnd;while(end<sourceEnd&&context.GraphemeBoundaries[(int)end]==0)end++;failed.Add(new(begin,end));}
            index+=(int)head.ClusterCount;
        }
        if(failed.Count==0){foreach(var glyph in candidate)output.Add(glyph.Copy());return;}
        failed.Sort((a,b)=>a.Start.CompareTo(b.Start));int merged=0;
        for(int i=0;i<failed.Count;i++)
        {var range=failed[i];if(merged>0&&range.Start<=failed[merged-1].End)failed[merged-1]=new(failed[merged-1].Start,Math.Max(failed[merged-1].End,range.End));else failed[merged++]=range;}
        failed.RemoveRange(merged,failed.Count-merged);int lastEmitted=-1;
        for(int index=0;index<candidate.Count;)
        {
            var head=candidate[index];System.Diagnostics.Debug.Assert(head.ClusterCount>0);int first=0,last=failed.Count;
            while(first<last){int mid=first+(last-first)/2;if(failed[mid].End<=head.SourceBegin)first=mid+1;else last=mid;}
            int failedIndex=first<failed.Count&&failed[first].Start<head.SourceEnd?first:-1;
            if(failedIndex<0){for(int i=0;i<head.ClusterCount;i++)output.Add(candidate[index+i].Copy());}
            else if(failedIndex!=lastEmitted)
            {lastEmitted=failedIndex;ShapeRun(context,failed[failedIndex].Start,failed[failedIndex].End,spanIndex,script,rtl,fallbackIndex+1,sourceBegin,sourceEnd,recursivePrevious,output);}
            index+=(int)head.ClusterCount;
        }
        _=previousEnd;
    }
    private static int LowerBoundObject(IReadOnlyList<TextShapedObjectData> objects,ulong source)
    {int first=0,last=objects.Count;while(first<last){int mid=first+(last-first)/2;if(objects[mid].Start<source)first=mid+1;else last=mid;}return first;}
    private static bool HasObjectAt(IReadOnlyList<TextShapedObjectData> objects,ulong source)
    {int index=LowerBoundObject(objects,source);return index<objects.Count&&objects[index].Start==source;}
    internal static void AppendObject(TextShapedData desc,AdvancedChunk chunk,bool rtl,List<TextPlacedGlyph> output)
    {
        var obj=chunk.Object!;output.Add(new(){Glyph=new(){Kind=ETextGlyphKind.Object,Advance=new(obj.Size.Width,0),BitmapSize=obj.Size},SpanId=desc.Spans[(int)chunk.SpanIndex].Id,Advance=obj.Size.Width,ClusterCount=1,Flags=ETextGraphemeFlag.EmbeddedObject|ETextGraphemeFlag.Valid|BaseFlags(rtl),SourceBegin=chunk.SourceBegin,SourceEnd=chunk.SourceEnd,ObjectKey=obj.Key});
    }
    internal static bool ShapeBidiRange(TextShapedData desc,AdvancedTextMap map,IReadOnlyList<uint> scripts,AdvancedShapeContext context,ulong rangeBegin,ulong rangeEnd,byte paragraphLevel,List<ulong> emittedObjects,List<TextPlacedGlyph> output,out nint outBidi)
    {
        outBidi=0;int utf16Begin=map.Cache.SourceToUtf16[(int)rangeBegin],utf16End=map.Cache.SourceToUtf16[(int)rangeEnd],status=0;
        nint bidi=TextNative.ubidi_openSized(utf16End-utf16Begin,0,ref status);int runCount=1;
        if(status<=0&&bidi!=0)
        {TextNative.ubidi_setPara(bidi,(char*)map.Cache.PinnedUtf16()+utf16Begin,utf16End-utf16Begin,paragraphLevel,null,ref status);runCount=status<=0?TextNative.ubidi_countRuns(bidi,ref status):0;}
        if(status>0||runCount<=0){if(bidi!=0){TextNative.ubidi_close(bidi);bidi=0;}runCount=1;}
        List<AdvancedChunk> chunks=[];
        for(int visualRun=0;visualRun<runCount;visualRun++)
        {
            ulong runBegin=rangeBegin,runEnd=rangeEnd;bool rtl=paragraphLevel==1;
            if(bidi!=0)
            {rtl=TextNative.ubidi_getVisualRun(bidi,visualRun,out int logicalBegin,out int logicalLength)==1;logicalBegin+=utf16Begin;runBegin=map.CodepointAtUtf16(logicalBegin);runEnd=map.CodepointAtUtf16(logicalBegin+logicalLength);}
            chunks.Clear();ulong source=runBegin;
            while(source<runEnd)
            {
                int objIndex=LowerBoundObject(desc.Objects,source);
                while(objIndex<desc.Objects.Count&&desc.Objects[objIndex].Start==source&&desc.Objects[objIndex].End==source)
                {
                    var obj=desc.Objects[objIndex];
                    if(!emittedObjects.Contains(obj.Key))
                    {ulong span=SpanIndexById(desc.Spans,obj.SpanId);if(span!=ulong.MaxValue){chunks.Add(new(){SourceBegin=source,SourceEnd=source,SpanIndex=span,Object=obj});emittedObjects.Add(obj.Key);}}
                    objIndex++;
                }
                if(objIndex<desc.Objects.Count&&desc.Objects[objIndex].Start==source&&desc.Objects[objIndex].End>source)
                {
                    var obj=desc.Objects[objIndex];ulong span=SpanIndexById(desc.Spans,obj.SpanId);
                    if(span!=ulong.MaxValue){chunks.Add(new(){SourceBegin=source,SourceEnd=Math.Min(obj.End,runEnd),SpanIndex=span,Object=obj});emittedObjects.Add(obj.Key);}
                    source=Math.Min(obj.End,runEnd);continue;
                }
                ulong spanIndex=TextAlgorithms.SpanAtSource(desc.Spans,source);if(spanIndex==ulong.MaxValue){source++;continue;}
                uint script=scripts[(int)source];ulong end=Math.Min(runEnd,desc.Spans[(int)spanIndex].End);
                for(ulong next=source+1;next<end;next++)if(scripts[(int)next]!=script||HasObjectAt(desc.Objects,next)){end=next;break;}
                chunks.Add(new(){SourceBegin=source,SourceEnd=end,SpanIndex=spanIndex,Script=script});source=end;
            }
            int boundaryIndex=LowerBoundObject(desc.Objects,runEnd);
            while(boundaryIndex<desc.Objects.Count&&desc.Objects[boundaryIndex].Start==runEnd&&desc.Objects[boundaryIndex].End==runEnd)
            {
                var obj=desc.Objects[boundaryIndex];if(!emittedObjects.Contains(obj.Key))
                {ulong span=SpanIndexById(desc.Spans,obj.SpanId);if(span!=ulong.MaxValue){chunks.Add(new(){SourceBegin=runEnd,SourceEnd=runEnd,SpanIndex=span,Object=obj});emittedObjects.Add(obj.Key);}}
                boundaryIndex++;
            }
            void EmitChunk(AdvancedChunk chunk)
            {if(chunk.Object is not null)AppendObject(desc,chunk,rtl,output);else ShapeRun(context,chunk.SourceBegin,chunk.SourceEnd,chunk.SpanIndex,chunk.Script,rtl,0,0,0,new(),output);}
            if(rtl)for(int i=chunks.Count;i>0;i--)EmitChunk(chunks[i-1]);else foreach(var chunk in chunks)EmitChunk(chunk);
        }
        outBidi=bidi;return true;
    }
}
internal sealed partial class TextServicesImpl
{
    internal unsafe bool ShapeAdvanced(TextShapedData shaped)
    {
        var desc=shaped;var output=shaped.Glyphs;output.Clear();shaped.InferredDirection=desc.Direction==ETextDirectionMode.RTL?ETextDirection.RTL:ETextDirection.LTR;
        if(desc.Codepoints.Count==0)
        {
            foreach(var obj in desc.Objects)
            {ulong span=TextAdvancedShaper.SpanIndexById(desc.Spans,obj.SpanId);if(span==ulong.MaxValue)continue;TextAdvancedShaper.AppendObject(desc,new(){SourceBegin=obj.Start,SourceEnd=obj.End,SpanIndex=span,Object=obj},false,output);}
            return true;
        }
        AdvancedTextMap map=new();map.Build(desc);
        if(shaped.Advanced.GraphemeBoundaries.Count==0)TextAdvancedShaper.CollectGraphemeBoundaries(desc,map,shaped.Advanced.GraphemeBoundaries);
        if(!shaped.Advanced.ScriptsValid){TextAdvancedShaper.CollectScripts(desc.Codepoints,shaped.Advanced.Scripts);shaped.Advanced.ScriptsValid=true;}
        byte paragraphLevel=0;
        if(desc.Direction==ETextDirectionMode.LTR)paragraphLevel=0;
        else if(desc.Direction==ETextDirectionMode.RTL)paragraphLevel=1;
        else
        {
            int direction=TextNative.ubidi_getBaseDirection(map.Cache.PinnedUtf16(),map.Cache.Utf16.Count);
            if(direction==1)paragraphLevel=1;
            else if(direction==3)foreach(var span in desc.Spans)if(!span.Language.IsEmpty()){paragraphLevel=IsLocaleRightToLeft(span.Language)?(byte)1:(byte)0;break;}
        }
        shaped.InferredDirection=paragraphLevel==1?ETextDirection.RTL:ETextDirection.LTR;shaped.Advanced.ParagraphLevel=paragraphLevel;
        if(shaped.Advanced.HbBuffer==0)shaped.Advanced.HbBuffer=TextNative.hb_buffer_create();if(shaped.Advanced.HbBuffer==0)return false;
        AdvancedShapeContext context=new(this,desc,shaped.Advanced.HbBuffer);List<ulong> emitted=[];bool result=true;
        if(desc.BidiOverrides.Count==0)
        {result=TextAdvancedShaper.ShapeBidiRange(desc,map,shaped.Advanced.Scripts,context,0,(ulong)desc.Codepoints.Count,paragraphLevel,emitted,output,out nint bidi);shaped.Advanced.BidiIterators.Add(bidi);}
        else
        {
            foreach(var value in desc.BidiOverrides)
            {
                ulong begin=Math.Min(value.Range.Start,(ulong)desc.Codepoints.Count),end=Math.Clamp(value.Range.End,begin,(ulong)desc.Codepoints.Count);
                if(begin==end){shaped.Advanced.BidiIterators.Add(0);continue;}
                byte level=value.Direction==ETextDirection.RTL?(byte)1:(byte)0;
                if(!TextAdvancedShaper.ShapeBidiRange(desc,map,shaped.Advanced.Scripts,context,begin,end,level,emitted,output,out nint bidi))
                {if(bidi!=0)TextNative.ubidi_close(bidi);result=false;break;}
                shaped.Advanced.BidiIterators.Add(bidi);
            }
        }
        return result;
    }
}
