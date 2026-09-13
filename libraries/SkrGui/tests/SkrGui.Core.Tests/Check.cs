namespace SkrGui.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class GuiTestAttribute(string source) : Attribute { public string Source { get; } = source; }

public static class Check
{
    public static void That(bool value, string message = "Expected true") { if (!value) throw new InvalidOperationException(message); }
    public static void False(bool value, string message = "Expected false") => That(!value, message);
    public static void Equal<T>(T expected, T actual, string? message = null) => That(EqualityComparer<T>.Default.Equals(expected, actual), message ?? $"Expected {expected}; got {actual}");
    public static void Same(object? expected, object? actual) => That(ReferenceEquals(expected, actual), "Object identity differs");
    public static void NotNull(object? value) => That(value != null, "Expected a non-null value");
    public static void Null(object? value) => That(value == null, "Expected a null value");
    public static void Near(double expected, double actual, double tolerance = 1e-6, string? message = null) => That(expected.Equals(actual) || Math.Abs(expected - actual) <= tolerance, message ?? $"Expected {expected} +/- {tolerance}; got {actual}");
    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual) => That(expected.SequenceEqual(actual), "Sequence differs");
    public static T Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T error) { return error; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }
}
