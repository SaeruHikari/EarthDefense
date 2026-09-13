using System.Diagnostics;

var timer = Stopwatch.StartNew();
try
{
    Console.WriteLine($"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}; dynamic-code={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled}; mask={Sugoi.Data.MaskScanner.ActiveMode}");
    PrimitivesChecks.Run();
    Console.WriteLine("PASS primitives / pools / SIMD / buffer lifecycle");
    QueryParsingChecks.Run();
    Console.WriteLine("PASS query parsing / parameter and phase semantics");
    WorldChecks.Run();
    Console.WriteLine("PASS world / query / structure / Entity64");
    TransferChecks.Run();
    Console.WriteLine("PASS import / generation / references / transfer ownership");
    PackingChecks.Run();
    Console.WriteLine("PASS identity packing / generation history / failure boundaries");
    StagingChecks.Run();
    await JobParityChecks.RunAsync();
    await MessagesParityChecks.RunAsync();
    await WeakSignalChecks.RunAsync();
    await TasksChecks.RunAsync();
    Console.WriteLine("PASS online scheduling / waits / messages / ownership");
    await GeneratedChecks.RunAsync();
    await GeneratedAdvancedChecks.RunAsync();
    Console.WriteLine("PASS cross-assembly generated components and jobs");
    if (args.Contains("--stress")) await TasksStressChecks.RunAsync();
    Console.WriteLine($"SUGOI CHECKS PASSED in {timer.Elapsed.TotalSeconds:F3}s");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
