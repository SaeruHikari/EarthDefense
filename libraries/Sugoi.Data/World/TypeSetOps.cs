namespace Sugoi.Data;

internal static class TypeSetOps
{
    internal static T[] Union<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b) where T : IComparable<T>, IEquatable<T>
    {
        var result = new T[a.Length + b.Length];
        int i = 0, j = 0, count = 0;
        while (i < a.Length && j < b.Length)
        {
            int compare = a[i].CompareTo(b[j]);
            if (compare < 0) result[count++] = a[i++];
            else if (compare > 0) result[count++] = b[j++];
            else { result[count++] = a[i++]; j++; }
        }
        while (i < a.Length) result[count++] = a[i++];
        while (j < b.Length) result[count++] = b[j++];
        if (count != result.Length) Array.Resize(ref result, count);
        return result;
    }

    internal static T[] Subtract<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b) where T : IComparable<T>, IEquatable<T>
    {
        var result = new T[a.Length];
        int j = 0, count = 0;
        foreach (var value in a)
        {
            while (j < b.Length && b[j].CompareTo(value) < 0) j++;
            if (j == b.Length || !b[j].Equals(value)) result[count++] = value;
        }
        if (count != result.Length) Array.Resize(ref result, count);
        return result;
    }

    internal static bool All<T>(ReadOnlySpan<T> sorted, ReadOnlySpan<T> subset) where T : IComparable<T>
    {
        int i = 0;
        foreach (var value in subset)
        {
            while (i < sorted.Length && sorted[i].CompareTo(value) < 0) i++;
            if (i == sorted.Length || sorted[i].CompareTo(value) != 0) return false;
        }
        return true;
    }
}
