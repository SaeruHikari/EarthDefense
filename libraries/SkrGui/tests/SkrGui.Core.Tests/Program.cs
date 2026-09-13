using System.Reflection;
using System.Text.Json;
using SkrGui.Tests;

var tests = typeof(Check).Assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
    .Where(m => m.GetCustomAttribute<GuiTestAttribute>() != null)
    .OrderBy(m => m.GetCustomAttribute<GuiTestAttribute>()!.Source, StringComparer.Ordinal).ToArray();
string filter = args.FirstOrDefault(a => a.StartsWith("--filter="))?[9..] ?? "";
int passed = 0, failed = 0;
var results = new List<object>();
foreach (var test in tests)
{
    string source = test.GetCustomAttribute<GuiTestAttribute>()!.Source;
    if (filter.Length > 0 && !source.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
    try
    {
        var value = test.Invoke(null, null);
        if (value is Task task) await task;
        results.Add(new { source, result = "passed" }); passed++;
    }
    catch (Exception error)
    {
        var cause = error is TargetInvocationException invocation ? invocation.InnerException! : error;
        Console.Error.WriteLine($"FAIL {source}: {cause}");
        results.Add(new { source, result = "failed", error = cause.ToString() }); failed++;
    }
}
string? report = args.FirstOrDefault(a => a.StartsWith("--report="))?[9..];
if (report != null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
    File.WriteAllText(report, JsonSerializer.Serialize(new { passed, failed, tests = results }, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"SKRGUI_SOURCE_CHECKS: {passed} passed / {failed} failed / {tests.Length} registered");
return failed == 0 && passed > 0 ? 0 : 1;
