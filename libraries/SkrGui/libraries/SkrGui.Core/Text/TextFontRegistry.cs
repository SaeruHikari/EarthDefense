namespace SkrGui;

// Source: text/font/text_font_registry.cpp @ 611561f8.
internal readonly record struct TextFontQueryCacheKey(Utf8StringView Family, EFontWeight Weight, EFontStyle Style, EFontStretch Stretch);
internal readonly record struct TextFontQueryCacheValue(FontFaceQuery Query, bool Found);
internal sealed class TextSystemFallbackCacheKey : IEquatable<TextSystemFallbackCacheKey>
{
    public Utf8StringView BaseFamily, Language;
    public EFontWeight Weight; public EFontStyle Style; public EFontStretch Stretch;
    public uint[] Codepoints = [];
    public bool Equals(TextSystemFallbackCacheKey? other) => other is not null && BaseFamily == other.BaseFamily && Language == other.Language && Weight == other.Weight && Style == other.Style && Stretch == other.Stretch && Codepoints.AsSpan().SequenceEqual(other.Codepoints);
    public override bool Equals(object? obj) => obj is TextSystemFallbackCacheKey other && Equals(other);
    public override int GetHashCode() { var hash = new HashCode(); hash.Add(BaseFamily); hash.Add(Language); hash.Add(Weight); hash.Add(Style); hash.Add(Stretch); foreach (var cp in Codepoints) hash.Add(cp); return hash.ToHashCode(); }
}
internal readonly record struct TextSystemFallbackCacheValue(TextFontFaceId Font, bool Found);
internal sealed partial class TextServicesImpl
{
    internal readonly Dictionary<TextFontQueryCacheKey, TextFontQueryCacheValue> FontQueryCache = [];
    internal readonly Dictionary<TextSystemFallbackCacheKey, TextSystemFallbackCacheValue> SystemFallbackCache = [];
    public override void AddFontProvider(FontProvider provider)
    {
        if (provider is null) return;
        FontProviders.Add(provider); FontQueryCache.Clear(); SystemFallbackCache.Clear(); FontTopologyRevisionValue++; FontStateRevisionValue++;
    }
    public override void ClearFontProviders()
    {
        if (FontProviders.Count == 0) return;
        FontProviders.Clear(); FontQueryCache.Clear(); SystemFallbackCache.Clear(); FontTopologyRevisionValue++; FontStateRevisionValue++;
    }
    public override ulong FontProviderCount() => (ulong)FontProviders.Count;
    public override FontProvider? FontProviderAt(ulong index) => index < (ulong)FontProviders.Count ? FontProviders[(int)index] : null;
    public override bool QueryFontFaces(TextStyle style, List<FontFaceQuery> out_queries)
    {
        out_queries.Clear(); if (!IsValid()) return false;
        foreach (var token in TextAlgorithms.FontFamilyTokens(style.FontFamilies))
        {
            Utf8StringView family = token; if (family.IsEmpty()) continue;
            if (QueryFontFace(family, style.FontWeight, style.FontStyle, style.FontStretch, out var query) && !HasQuery(out_queries, query)) out_queries.Add(query);
        }
        return out_queries.Count != 0;
    }
    public override FontFace? PreloadFontFace(FontFaceQuery query) => LoadFontFace(query);
    public override bool PreloadFontFaces(TextStyle style, List<FontFace> out_faces)
    {
        out_faces.Clear(); foreach (var id in style.FontFaces) if (FontFace(id) is { } face) out_faces.Add(face);
        List<FontFaceQuery> queries = []; QueryFontFaces(style, queries);
        foreach (var query in queries)
        {
            if (PreloadFontFace(query) is not { } face) continue;
            bool duplicate = false; foreach (var candidate in out_faces) duplicate |= ReferenceEquals(candidate, face);
            if (!duplicate) out_faces.Add(face);
        }
        return out_faces.Count != 0;
    }
    public override ulong FontFaceCount() => (ulong)FontFaces.Count;
    public override FontFace? FontFaceAt(ulong index) => index < (ulong)FontFaces.Count ? FontFaces[(int)index] : null;
    public override FontFace? FontFace(TextFontFaceId id) => FindFontFace(id);
    public override bool UnloadFontFace(TextFontFaceId id)
    {
        for (int i = 0; i < FontFaces.Count; i++)
        {
            if (FontFaces[i].IdValue != id) continue;
            FontFacesById.Remove(id.Value); FontFaces[i].Dispose(); FontFaces.RemoveAt(i); SystemFallbackCache.Clear();
            FontTopologyRevisionValue++; FontStateRevisionValue++; ClearAtlases(); return true;
        }
        return false;
    }
    public override void UnloadAllFontFaces()
    {
        if (FontFaces.Count == 0 && FontAssets.Count == 0 && FontData.Count == 0 && SystemFallbackCache.Count == 0) return;
        FontFacesById.Clear(); foreach (var face in FontFaces) face.Dispose(); FontFaces.Clear(); FontAssets.Clear();
        foreach (var data in FontData) data.Dispose(); FontData.Clear(); SystemFallbackCache.Clear();
        FontTopologyRevisionValue++; FontStateRevisionValue++; ClearAtlases();
    }
    internal TextFontFaceImpl? FindFontFace(TextFontFaceId font) => FontFacesById.TryGetValue(font.Value, out var result) ? result : null;
    internal bool QueryFontFace(Utf8StringView family, EFontWeight weight, EFontStyle style, EFontStretch stretch, out FontFaceQuery out_query)
    {
        out_query = new(); var key = new TextFontQueryCacheKey(new Utf8StringView(family.Bytes.ToArray()), weight, style, stretch);
        if (FontQueryCache.TryGetValue(key, out var cached)) { out_query = cached.Query; return cached.Found; }
        foreach (var provider in FontProviders)
        {
            if (!provider.QueryFace(family, weight, style, stretch, out var source)) continue;
            out_query = new(provider, source, source.Weight != weight || source.Style != style || source.Stretch != stretch);
            bool found = out_query.IsValid(); FontQueryCache.TryAdd(key, new(out_query, found)); return found;
        }
        FontQueryCache.TryAdd(key, new()); return false;
    }
    internal bool ResolveTextStyle(TextStyle style, TextResolvedSpan output)
    {
        output.Fonts.Clear(); if (!IsValid() || !IsFinitePositive(style.FontSize)) return false;
        foreach (var id in style.FontFaces) if (FindFontFace(id) is not null) output.Fonts.Add(new() { Font = id, Resolved = true });
        foreach (var token in TextAlgorithms.FontFamilyTokens(style.FontFamilies))
        {
            Utf8StringView family = token; if (family.IsEmpty()) continue;
            bool duplicate = false; foreach (var candidate in output.Fonts) if (!candidate.Family.IsEmpty() && candidate.Family == family) { duplicate = true; break; }
            if (!duplicate) output.Fonts.Add(new() { Family = new Utf8StringView(family.Bytes.ToArray()) });
        }
        return output.Fonts.Count != 0;
    }
    internal ulong FontCandidateCount(TextResolvedSpan span) => (ulong)span.Fonts.Count;
    internal TextFontFaceId ResolveFontCandidate(TextStyle style, TextResolvedSpan span, ulong index)
    {
        if (index >= (ulong)span.Fonts.Count) return new(); var candidate = span.Fonts[(int)index];
        if (candidate.Resolved) return candidate.Font; candidate.Resolved = true;
        if (!QueryFontFace(candidate.Family, style.FontWeight, style.FontStyle, style.FontStretch, out var query)) return new();
        if (LoadFontFace(query) is { } face) candidate.Font = face.IdValue;
        return candidate.Font;
    }
    internal bool FontDependenciesValid(IReadOnlyList<TextFontDependency> dependencies)
    { foreach (var dep in dependencies) { var face = FindFontFace(dep.Font); if (face is null || face.Revision != dep.Revision) return false; } return true; }
    internal void AppendFontDependency(TextShapedData shaped, TextFontFaceId font)
    {
        if (!font.IsValid()) return; foreach (var dep in shaped.FontDependencies) if (dep.Font == font) return;
        if (FindFontFace(font) is { } face) shaped.FontDependencies.Add(new(font, face.Revision));
    }
    internal void CollectFontDependencies(TextShapedData shaped)
    {
        shaped.FontDependencies.Clear(); foreach (var span in shaped.ResolvedSpans) foreach (var candidate in span.Fonts) AppendFontDependency(shaped, candidate.Font);
        foreach (var glyph in shaped.Glyphs) AppendFontDependency(shaped, glyph.Glyph.Font);
        foreach (var glyph in shaped.Trim.EllipsisGlyphs) AppendFontDependency(shaped, glyph.Glyph.Font);
    }
    internal TextFontFaceId ResolveSystemFallback(TextStyle style, ReadOnlySpan<uint> codepoints, Utf8StringView language)
    {
        if (codepoints.IsEmpty) return new(); Utf8StringView baseFamily = default;
        foreach (var token in TextAlgorithms.FontFamilyTokens(style.FontFamilies)) if (baseFamily.IsEmpty()) baseFamily = new Utf8StringView(token.Bytes.ToArray());
        var key = new TextSystemFallbackCacheKey { BaseFamily = baseFamily, Language = new Utf8StringView(language.Bytes.ToArray()), Weight = style.FontWeight, Style = style.FontStyle, Stretch = style.FontStretch, Codepoints = codepoints.ToArray() };
        if (SystemFallbackCache.TryGetValue(key, out var cached)) return cached.Found ? cached.Font : new();
        foreach (var provider in FontProviders)
        {
            if (!provider.IsSystemFontSource()) continue;
            FontProviderFaceSource source;
            if (!TryQuerySystemFallback(provider, baseFamily, codepoints, language, style.FontWeight, style.FontStyle, style.FontStretch, out source)) continue;
            if (LoadFontFace(new(provider, source, true)) is not { } face) continue;
            SystemFallbackCache.TryAdd(key, new(face.IdValue, true)); return face.IdValue;
        }
        SystemFallbackCache.TryAdd(key, new()); return new();
    }
    internal unsafe TextFontFaceImpl? LoadFontFace(FontFaceQuery query)
    {
        if (!query.IsValid() || !IsValid()) return null;
        foreach (var face in FontFaces) if (ReferenceEquals(face.ProviderRef, query.Provider) && face.QueryIdentity.IsValid() && face.QueryIdentity == query.Source) return face;
        var asset = LoadFontAsset(query); if (asset?.Data?.Bytes is not { } bytes) return null;
        if (asset.FaceLoadAttempted && !asset.FaceLoadable) return null;
        if (TextNative.FT_New_Memory_Face(Library, (byte*)asset.Data.BytesPointer, bytes.Length, (int)asset.FaceIndex, out var nativeFace) != 0)
        { asset.FaceLoadAttempted = true; asset.FaceLoadable = false; return null; }
        asset.FaceLoadAttempted = true; asset.FaceLoadable = true;
        var result = new TextFontFaceImpl(this, new(NextFontFaceId++), query.Provider!, query.Source, asset, nativeFace);
        result.PreloadMetadata(); result.ApplyVariationCoordinates(); FontFaces.Add(result); FontFacesById.TryAdd(result.IdValue.Value, result); return result;
    }
    internal TextFontData LoadFontData(FontFaceQuery query)
    {
        foreach (var data in FontData) if (ReferenceEquals(data.Provider, query.Provider) && data.SourceKey == query.Source.SourceKey) return data;
        var result = new TextFontData { Provider = query.Provider, SourceKey = new Utf8StringView(query.Source.SourceKey.Bytes.ToArray()) }; List<byte> bytes = [];
        if (query.Provider!.LoadFaceData(query.Source, bytes) && bytes.Count != 0) result.SetBytes(bytes.ToArray());
        FontData.Add(result); return result;
    }
    internal TextFontAsset? LoadFontAsset(FontFaceQuery query)
    {
        var data = LoadFontData(query); if (data.Bytes is null) return null;
        foreach (var asset in FontAssets) if (ReferenceEquals(asset.Data, data) && asset.FaceIndex == query.Source.FaceIndex) return asset;
        var result = new TextFontAsset { Data = data, FaceIndex = query.Source.FaceIndex }; FontAssets.Add(result); return result;
    }
    internal static bool HasQuery(IReadOnlyList<FontFaceQuery> queries, FontFaceQuery query)
    { foreach (var candidate in queries) if (ReferenceEquals(candidate.Provider, query.Provider) && candidate.Source == query.Source) return true; return false; }
}
internal static partial class TextAlgorithms
{
    // Source StringView split(',') followed by its default space/tab trim.
    internal static IEnumerable<Utf8StringView> FontFamilyTokens(Utf8StringView families)
    {
        int start=0;
        for(int end=0;end<=families.Memory.Length;end++)
        {
            if(end<families.Memory.Length&&families.Bytes[end]!=(byte)',')continue;
            int first=start,last=end;
            while(first<last&&(families.Bytes[first]==(byte)' '||families.Bytes[first]==(byte)'\t'))first++;
            while(last>first&&(families.Bytes[last-1]==(byte)' '||families.Bytes[last-1]==(byte)'\t'))last--;
            yield return families.Slice(first,last-first);start=end+1;
        }
    }
}
