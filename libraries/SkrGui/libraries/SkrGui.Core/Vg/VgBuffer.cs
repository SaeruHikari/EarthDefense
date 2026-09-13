using System.Collections;

namespace SkrGui;

// Retained VG algorithms rely on references into contiguous vector storage.
// This adapter preserves those reference and index semantics without a native SkrGui container.
internal sealed class VgBuffer<T> : IReadOnlyList<T>
{
    private T[] _data = [];
    public int Count { get; private set; }
    public ref T this[int index] => ref _data[index];
    public ref T this[uint index] => ref _data[checked((int)index)];
    public ref T this[ulong index] => ref _data[checked((int)index)];
    T IReadOnlyList<T>.this[int index] => _data[index];
    public ulong Size() => (ulong)Count;
    public bool IsEmpty() => Count == 0;
    public Span<T> AsSpan() => _data.AsSpan(0, Count);
    public void Reserve(ulong capacity)
    {
        if (capacity <= (ulong)_data.Length) return;
        Array.Resize(ref _data, checked((int)capacity));
    }
    public void Clear() { Array.Clear(_data, 0, Count); Count = 0; }
    public void Release(ulong capacity = 0) { _data = new T[checked((int)capacity)]; Count = 0; }
    public ref T AddDefault()
    {
        if (Count == _data.Length) Array.Resize(ref _data, System.Math.Max(4, checked(Count * 2)));
        _data[Count] = default!; return ref _data[Count++];
    }
    public void PushBack(T item) => AddDefault() = item;
    public ref T AtLast() => ref _data[Count - 1];
    public void RemoveAt(ulong index, ulong count = 1)
    {
        int start = checked((int)index), length = checked((int)count);
        Array.Copy(_data, start + length, _data, start, Count - start - length);
        Array.Clear(_data, Count - length, length); Count -= length;
    }
    public void StackPopUnsafe(ulong count) { int length = checked((int)count); Array.Clear(_data, Count - length, length); Count -= length; }
    public void ResizeUnsafe(ulong count) { Reserve(count); if (count < (ulong)Count) Array.Clear(_data, (int)count, Count - (int)count); Count = checked((int)count); }
    public IEnumerator<T> GetEnumerator() { for (int i = 0; i < Count; ++i) yield return _data[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
