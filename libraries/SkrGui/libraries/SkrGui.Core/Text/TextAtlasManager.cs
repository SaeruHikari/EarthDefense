using System.Diagnostics;
using System.Numerics;
namespace SkrGui;

// Source: text/render/text_atlas_private.hpp + text_atlas_manager.cpp @ 611561f8.
internal enum ETextAtlasBitmapFormat : byte { Gray, Mono, BGRA, LCD }
internal record struct TextAtlasAllocation
{
    public uint AtlasIndex = uint.MaxValue, X, Y, Width, Height;
    public TextAtlasAllocation() { }
}
internal sealed class TextAtlasSlot
{
    public uint X, Y, Width, Height, Prev = uint.MaxValue, Next = uint.MaxValue;
}
internal sealed class TextAtlasShelf { public uint X, Y, Width, Height; }
internal sealed class TextAtlasPage
{
    public uint AtlasIndex = uint.MaxValue;
    public ETextAtlasFormat Format;
    public byte[] Pixels = [];
    public uint Width = 1024, Height = 1024;
    public readonly List<TextAtlasSlot> Slots = [];
    public readonly List<uint> FreeSlotHeads = [];
    public uint UsedSlotHead = uint.MaxValue;
    public readonly List<TextAtlasShelf> Shelves = [];
    public uint ShelfX, ShelfY, ShelfHeight;
    public ulong Generation = 1;
    public bool IsDirty;
}
internal sealed class TextAtlasDesc
{
    public Sizei L8PageSize = new(1024, 1024), Rgba8PageSize = new(512, 512);
    public ETextAtlasAllocationAlgorithm AllocationAlgorithm = ETextAtlasAllocationAlgorithm.GuillotineSplit;
    public uint GlyphPadding = 1;
    public Sizei PageSize(ETextAtlasFormat format) => format == ETextAtlasFormat.RGBA8 ? Rgba8PageSize : L8PageSize;
    public void SetPageSize(ETextAtlasFormat format, Sizei size) { if (format == ETextAtlasFormat.RGBA8) Rgba8PageSize = size; else L8PageSize = size; }
}
internal sealed class TextAtlasManager
{
    private const uint InvalidSlot = uint.MaxValue, MinSlotDim = 2;
    private readonly TextAtlasDesc _desc = new();
    private readonly List<TextAtlasPage> _pages = [];
    private bool _isUpdateActive;
    private static uint NextPowerOfTwo(uint value)
    { if (value <= 1) return 1; value--; value |= value >> 1; value |= value >> 2; value |= value >> 4; value |= value >> 8; value |= value >> 16; return value + 1; }
    private static int SanitizeDimension(int value) => (int)Math.Min(NextPowerOfTwo((uint)Math.Clamp(value, 128, 2048)), 2048u);
    private static Sizei SanitizeSize(Sizei value) => new(SanitizeDimension(value.Width), SanitizeDimension(value.Height));
    public TextAtlasManager() { _desc.L8PageSize = SanitizeSize(_desc.L8PageSize); _desc.Rgba8PageSize = SanitizeSize(_desc.Rgba8PageSize); }
    public Sizei AtlasPageSize(ETextAtlasFormat format) => _desc.PageSize(format);
    public bool SetAtlasPageSize(ETextAtlasFormat format, Sizei size)
    { size = SanitizeSize(size); if (_desc.PageSize(format) == size) return false; _desc.SetPageSize(format, size); return true; }
    public ETextAtlasAllocationAlgorithm AtlasAllocationAlgorithm() => _desc.AllocationAlgorithm;
    public bool SetAtlasAllocationAlgorithm(ETextAtlasAllocationAlgorithm algorithm)
    {
        if (algorithm != ETextAtlasAllocationAlgorithm.ShelfLinear && algorithm != ETextAtlasAllocationAlgorithm.ShelfBestFit && algorithm != ETextAtlasAllocationAlgorithm.GuillotineSplit)
        { Debug.Assert(false, "invalid text atlas allocation algorithm"); return false; }
        if (_desc.AllocationAlgorithm == algorithm || _pages.Count != 0) return false;
        _desc.AllocationAlgorithm = algorithm; return true;
    }
    public uint AtlasGlyphPadding() => _desc.GlyphPadding;
    public bool SetAtlasGlyphPadding(uint padding) { if (_desc.GlyphPadding == padding) return false; _desc.GlyphPadding = padding; return true; }
    public uint AtlasCount() => (uint)_pages.Count;
    public TextAtlasView Atlas(uint atlasIndex)
    {
        if (atlasIndex >= _pages.Count) return new();
        var page = _pages[(int)atlasIndex];
        return new() { AtlasIndex = page.AtlasIndex, Width = page.Width, Height = page.Height, Stride = page.Width * FormatChannels(page.Format), Pixels = page.Pixels, Generation = page.Generation, Format = page.Format };
    }
    public void Clear() => _pages.Clear();
    public bool UpdateActive() => _isUpdateActive;
    public void BeginUpdate() { Debug.Assert(!_isUpdateActive, "TextAtlasManager update batch is already active"); _isUpdateActive = true; }
    public void EndUpdate()
    {
        Debug.Assert(_isUpdateActive, "TextAtlasManager update batch is not active");
        foreach (var page in _pages) { if (!page.IsDirty) continue; page.Generation++; page.IsDirty = false; }
        _isUpdateActive = false;
    }
    public bool Allocate(ETextAtlasFormat format, uint width, uint height, out TextAtlasAllocation output)
    {
        output = new();
        Debug.Assert(_isUpdateActive, "TextAtlasManager::allocate requires an active update batch");
        if (!_isUpdateActive || width == 0 || height == 0) return false;
        ulong padding = SlotPadding(), pw = width + padding * 2, ph = height + padding * 2;
        var size = _desc.PageSize(format);
        if (pw > (uint)size.Width || ph > (uint)size.Height) return false;
        foreach (var page in _pages)
            if (page.Format == format && TryAllocate(page, width, height, (uint)pw, (uint)ph, out output)) return true;
        return TryAllocate(CreatePage(format), width, height, (uint)pw, (uint)ph, out output);
    }
    public unsafe bool WriteBitmap(in TextAtlasAllocation allocation, byte* pixels, int pitch, ETextAtlasBitmapFormat bitmapFormat)
    {
        var page = FindPage(allocation.AtlasIndex);
        if (page is null || pixels == null || allocation.Width == 0 || allocation.Height == 0) return false;
        if (allocation.X + allocation.Width > page.Width || allocation.Y + allocation.Height > page.Height) return false;
        if (bitmapFormat == ETextAtlasBitmapFormat.LCD && page.Format != ETextAtlasFormat.RGBA8) return false;
        uint channels = FormatChannels(page.Format);
        fixed (byte* targetBase = page.Pixels)
        for (uint row = 0; row < allocation.Height; row++)
        {
            byte* source = pitch >= 0 ? pixels + (ulong)row * (uint)pitch : pixels + (ulong)(allocation.Height - row - 1) * (uint)(-pitch);
            byte* target = targetBase + ((ulong)(allocation.Y + row) * page.Width + allocation.X) * channels;
            if (channels == 4)
            {
                for (uint col = 0; col < allocation.Width; col++)
                {
                    byte* rgba = target + (ulong)col * 4;
                    if (bitmapFormat == ETextAtlasBitmapFormat.BGRA)
                    { byte* bgra = source + (ulong)col * 4; rgba[0] = bgra[2]; rgba[1] = bgra[1]; rgba[2] = bgra[0]; rgba[3] = bgra[3]; }
                    else if (bitmapFormat == ETextAtlasBitmapFormat.LCD)
                    { byte* lcd = source + (ulong)col * 3; rgba[0] = lcd[0]; rgba[1] = lcd[1]; rgba[2] = lcd[2]; rgba[3] = 255; }
                    else if (bitmapFormat == ETextAtlasBitmapFormat.Mono)
                    { byte mask = (byte)(0x80u >> (int)(col & 7u)); rgba[0] = rgba[1] = rgba[2] = 255; rgba[3] = (source[col >> 3] & mask) != 0 ? (byte)255 : (byte)0; }
                    else { rgba[0] = rgba[1] = rgba[2] = 255; rgba[3] = source[col]; }
                }
            }
            else if (bitmapFormat == ETextAtlasBitmapFormat.BGRA)
            { for (uint col = 0; col < allocation.Width; col++) target[col] = source[(ulong)col * 4 + 3]; }
            else if (bitmapFormat == ETextAtlasBitmapFormat.Mono)
            { for (uint col = 0; col < allocation.Width; col++) { byte mask = (byte)(0x80u >> (int)(col & 7)); target[col] = (source[col >> 3] & mask) != 0 ? (byte)255 : (byte)0; } }
            else { for (uint col = 0; col < allocation.Width; col++) target[col] = source[col]; }
        }
        WriteBitmapPadding(page, allocation, bitmapFormat);
        page.IsDirty = true; return true;
    }
    private static uint FormatChannels(ETextAtlasFormat format) => format == ETextAtlasFormat.RGBA8 ? 4u : 1u;
    private TextAtlasPage? FindPage(uint index) => index < _pages.Count && _pages[(int)index].AtlasIndex == index ? _pages[(int)index] : null;
    private TextAtlasPage CreatePage(ETextAtlasFormat format)
    {
        var size = _desc.PageSize(format);
        var page = new TextAtlasPage { AtlasIndex = (uint)_pages.Count, Format = format, Width = (uint)size.Width, Height = (uint)size.Height, Generation = 1 };
        page.Pixels = new byte[(ulong)page.Width * page.Height * FormatChannels(format)];
        InitializePageAllocator(page); _pages.Add(page); return page;
    }
    private bool TryAllocate(TextAtlasPage page, uint width, uint height, uint pw, uint ph, out TextAtlasAllocation output)
    {
        switch (_desc.AllocationAlgorithm)
        {
            case ETextAtlasAllocationAlgorithm.ShelfLinear: return TryAllocateShelfLinear(page, width, height, pw, ph, out output);
            case ETextAtlasAllocationAlgorithm.ShelfBestFit: return TryAllocateShelfBestFit(page, width, height, pw, ph, out output);
            case ETextAtlasAllocationAlgorithm.GuillotineSplit: return TryAllocateGuillotineSplit(page, width, height, pw, ph, out output);
            default: Debug.Assert(false, "invalid text atlas allocation algorithm"); output = new(); return false;
        }
    }
    private bool TryAllocateShelfLinear(TextAtlasPage page, uint width, uint height, uint pw, uint ph, out TextAtlasAllocation output)
    {
        output = new();
        if (pw > page.Width || ph > page.Height) return false;
        if (page.ShelfHeight == 0) page.ShelfHeight = ph;
        else if (page.ShelfX + pw > page.Width) { page.ShelfX = 0; page.ShelfY += page.ShelfHeight; page.ShelfHeight = ph; }
        if (page.ShelfY + ph > page.Height) return false;
        uint padding = (uint)SlotPadding(), x = page.ShelfX, y = page.ShelfY;
        page.ShelfX += pw; page.ShelfHeight = Math.Max(page.ShelfHeight, ph);
        output = new() { AtlasIndex = page.AtlasIndex, X = x + padding, Y = y + padding, Width = width, Height = height }; return true;
    }
    private bool TryAllocateShelfBestFit(TextAtlasPage page, uint width, uint height, uint pw, uint ph, out TextAtlasAllocation output)
    {
        output = new(); if (pw > page.Width || ph > page.Height) return false;
        uint nextY = 0, best = uint.MaxValue; ulong waste = ulong.MaxValue;
        for (int i = 0; i < page.Shelves.Count; i++)
        {
            var shelf = page.Shelves[i]; nextY = Math.Max(nextY, shelf.Y + shelf.Height);
            if (pw > shelf.Width || ph > shelf.Height) continue;
            if (ph == shelf.Height) return AllocateFromShelf(page, shelf, width, height, pw, out output);
            ulong candidate = (ulong)(shelf.Height - ph) * pw;
            if (candidate < waste) { waste = candidate; best = (uint)i; }
        }
        if (best != uint.MaxValue) return AllocateFromShelf(page, page.Shelves[(int)best], width, height, pw, out output);
        if (nextY + ph > page.Height) return false;
        var added = new TextAtlasShelf { X = 0, Y = nextY, Width = page.Width, Height = ph };
        page.Shelves.Add(added); return AllocateFromShelf(page, added, width, height, pw, out output);
    }
    private bool TryAllocateGuillotineSplit(TextAtlasPage page, uint width, uint height, uint pw, uint ph, out TextAtlasAllocation output)
    {
        output = new(); uint padding = (uint)SlotPadding(); if (pw > page.Width || ph > page.Height) return false;
        for (uint bucket = SlotSearchIndex(pw); bucket < page.FreeSlotHeads.Count; bucket++)
        {
            uint index = page.FreeSlotHeads[(int)bucket];
            while (index != InvalidSlot)
            {
                var slot = page.Slots[(int)index];
                if (pw <= slot.Width && ph <= slot.Height)
                {
                    uint x = slot.X, y = slot.Y, sw = slot.Width, sh = slot.Height, rw = sw - pw, rh = sh - ph;
                    UnlinkFreeSlot(page, index);
                    if (rh >= MinSlotDim || rw >= MinSlotDim)
                    {
                        if (rh <= rw) { AddFreeSlot(page, x + pw, y, rw, sh); if (rh >= MinSlotDim) AddFreeSlot(page, x, y + ph, pw, rh); }
                        else { AddFreeSlot(page, x, y + ph, sw, rh); if (rw >= MinSlotDim) AddFreeSlot(page, x + pw, y, rw, ph); }
                    }
                    var used = page.Slots[(int)index]; used.X = x; used.Y = y; used.Width = pw; used.Height = ph; LinkUsedSlot(page, index);
                    output = new() { AtlasIndex = page.AtlasIndex, X = x + padding, Y = y + padding, Width = width, Height = height }; return true;
                }
                index = slot.Next;
            }
        }
        return false;
    }
    private bool AllocateFromShelf(TextAtlasPage page, TextAtlasShelf shelf, uint width, uint height, uint pw, out TextAtlasAllocation output)
    {
        output = new(); uint padding = (uint)SlotPadding(); if (pw > shelf.Width) return false;
        uint x = shelf.X, y = shelf.Y; shelf.X += pw; shelf.Width -= pw;
        output = new() { AtlasIndex = page.AtlasIndex, X = x + padding, Y = y + padding, Width = width, Height = height }; return true;
    }
    private void InitializePageAllocator(TextAtlasPage page)
    {
        page.Slots.Clear(); page.FreeSlotHeads.Clear(); page.UsedSlotHead = InvalidSlot; page.Shelves.Clear(); page.ShelfX = page.ShelfY = page.ShelfHeight = 0;
        if (_desc.AllocationAlgorithm == ETextAtlasAllocationAlgorithm.GuillotineSplit) InitializePageSlots(page);
    }
    private void InitializePageSlots(TextAtlasPage page)
    {
        page.Slots.Clear(); page.FreeSlotHeads.Clear(); page.UsedSlotHead = InvalidSlot;
        uint count = SlotSearchIndex(page.Width) + 1;
        for (uint i = 0; i < count; i++) page.FreeSlotHeads.Add(InvalidSlot);
        AddFreeSlot(page, 0, 0, page.Width, page.Height);
    }
    private uint AddFreeSlot(TextAtlasPage page, uint x, uint y, uint width, uint height)
    {
        if (width == 0 || height == 0) return InvalidSlot;
        uint index = (uint)page.Slots.Count, bucket = SlotSearchIndex(width);
        while (bucket >= page.FreeSlotHeads.Count) page.FreeSlotHeads.Add(InvalidSlot);
        var slot = new TextAtlasSlot { X = x, Y = y, Width = width, Height = height, Prev = InvalidSlot, Next = page.FreeSlotHeads[(int)bucket] };
        page.Slots.Add(slot); if (slot.Next != InvalidSlot) page.Slots[(int)slot.Next].Prev = index; page.FreeSlotHeads[(int)bucket] = index; return index;
    }
    private void UnlinkFreeSlot(TextAtlasPage page, uint index)
    {
        var slot = page.Slots[(int)index]; uint bucket = SlotSearchIndex(slot.Width); Debug.Assert(bucket < page.FreeSlotHeads.Count);
        if (slot.Prev != InvalidSlot) page.Slots[(int)slot.Prev].Next = slot.Next; else page.FreeSlotHeads[(int)bucket] = slot.Next;
        if (slot.Next != InvalidSlot) page.Slots[(int)slot.Next].Prev = slot.Prev;
        slot.Prev = slot.Next = InvalidSlot;
    }
    private void LinkUsedSlot(TextAtlasPage page, uint index)
    {
        var slot = page.Slots[(int)index]; slot.Prev = InvalidSlot; slot.Next = page.UsedSlotHead;
        if (page.UsedSlotHead != InvalidSlot) page.Slots[(int)page.UsedSlotHead].Prev = index;
        page.UsedSlotHead = index;
    }
    private ulong SlotPadding() => (ulong)_desc.GlyphPadding * 2;
    private static uint SlotSearchIndex(uint width) => width <= 1 ? 0u : 32u - (uint)BitOperations.LeadingZeroCount(width - 1);
    private void WriteBitmapPadding(TextAtlasPage page, in TextAtlasAllocation allocation, ETextAtlasBitmapFormat bitmapFormat)
    {
        uint padding = _desc.GlyphPadding;
        if (padding == 0 || allocation.Width == 0 || allocation.Height == 0) return;
        uint channels = FormatChannels(page.Format), left = allocation.X, top = allocation.Y, right = left + allocation.Width, bottom = top + allocation.Height;
        uint pl = left >= padding ? left - padding : 0, pt = top >= padding ? top - padding : 0;
        uint pr = Math.Min(page.Width, right + padding), pb = Math.Min(page.Height, bottom + padding);
        void Write(uint dx, uint dy, uint sx, uint sy)
        {
            ulong d = ((ulong)dy * page.Width + dx) * channels, s = ((ulong)sy * page.Width + sx) * channels;
            if (channels == 4)
            {
                if (bitmapFormat == ETextAtlasBitmapFormat.LCD) { page.Pixels[d] = page.Pixels[d + 1] = page.Pixels[d + 2] = 0; page.Pixels[d + 3] = 255; }
                else if (bitmapFormat == ETextAtlasBitmapFormat.BGRA) { page.Pixels[d] = page.Pixels[d + 1] = page.Pixels[d + 2] = page.Pixels[d + 3] = 0; }
                else { page.Pixels[d] = page.Pixels[s]; page.Pixels[d + 1] = page.Pixels[s + 1]; page.Pixels[d + 2] = page.Pixels[s + 2]; page.Pixels[d + 3] = 0; }
            }
            else page.Pixels[d] = 0;
        }
        for (uint y = top; y < bottom; y++) { for (uint x = pl; x < left; x++) Write(x, y, left, y); for (uint x = right; x < pr; x++) Write(x, y, right - 1, y); }
        for (uint y = pt; y < top; y++) for (uint x = pl; x < pr; x++) Write(x, y, Math.Clamp(x, left, right - 1), top);
        for (uint y = bottom; y < pb; y++) for (uint x = pl; x < pr; x++) Write(x, y, Math.Clamp(x, left, right - 1), bottom - 1);
    }
}
