using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sugoi.SourceGen;

var checks = 0;
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path)).ToList();
foreach (var assembly in new[] { typeof(Sugoi.Data.TypeRegistry).Assembly, typeof(Sugoi.Tasks.IQueryJob).Assembly, typeof(SugoiFixtures.Position).Assembly })
    if (references.All(reference => reference.FilePath != assembly.Location)) references.Add(MetadataReference.CreateFromFile(assembly.Location));
var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

(GeneratorDriver Driver, Compilation Compilation, GeneratorDriverRunResult Run) Generate(string source, string assemblyName = "GeneratorConsumer")
{
    var compilation = CSharpCompilation.Create(assemblyName,
        new[] { CSharpSyntaxTree.ParseText(source, parseOptions, "Input.cs") }, references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new SugoiGenerator().AsSourceGenerator() }, parseOptions: parseOptions,
        driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
    return (driver, updated, driver.GetRunResult());
}

void ExpectDiagnostic(string source, string id)
{
    var result = Generate(source);
    Check(result.Run.Diagnostics.Any(d => d.Id == id), $"Expected {id}: {string.Join(Environment.NewLine, result.Run.Diagnostics)}");
    Check(result.Run.Results.All(r => r.Exception == null), "Generator threw while reporting a user diagnostic.");
}

var positive = Generate("""
using System;
using Sugoi.Data;
using Sugoi.Tasks;
namespace Example;
public struct Nested { public Entity Target; }
[Component("19ab448d-e2dc-4294-975d-3cfe2ba3ea31")]
public partial struct Data { private Entity _parent; public Nested Relation; public float Number; }
[QueryJob]
public partial struct Job {
    private void Execute(Span<Data> values, in JobContext context) { for (int i=0;i<values.Length;i++) values[i].Number++; }
}
""");
Check(!positive.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
    "Positive fixture failed: " + string.Join(Environment.NewLine, positive.Compilation.GetDiagnostics()));
var positiveSources = string.Join("\n", positive.Run.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
Check(positiveSources.Contains("value._parent = remapper(value._parent)"), "Private root Entity field was not remapped.");
Check(positiveSources.Contains("value.Relation.Target = remapper(value.Relation.Target)"), "Nested Entity field was not remapped.");
Check(positiveSources.Contains("context.View.WriteOwned<global::Example.Data>()"), "Typed job binding did not use the public view contract.");
Check(positiveSources.Contains("SugoiFixturesModule.RegisterWithDependencies(registry)"), "Referenced assembly manifest was not statically composed.");
Check(!positiveSources.Contains("Reflection") && !positiveSources.Contains("Serialize"), "Generated source includes an out-of-scope mechanism.");

ExpectDiagnostic("""
using Sugoi.Data;
[Component("ff888fbb-7712-4999-827a-d2a5d4a67262")] public struct NotPartial { public int V; }
""", "SG001");
ExpectDiagnostic("""
using Sugoi.Data;
[Component("not-a-guid")] public partial struct BadGuid { public int V; }
""", "SG002");
ExpectDiagnostic("""
using Sugoi.Data;
[Component("83d70af8-7fb5-4729-a291-9f25ac82c194")] public partial struct First { }
[Component("83d70af8-7fb5-4729-a291-9f25ac82c194")] public partial struct Second { }
""", "SG002");
ExpectDiagnostic("""
using Sugoi.Data;
[Component("edbe6db2-45c2-4986-9a93-636355a2d101")] public partial struct ExternalConflict { }
""", "SG002");
ExpectDiagnostic("""
using Sugoi.Data;
[Component("8e14a898-78b5-4b01-aa5b-dc8bf79d397e")] public partial struct Managed { public string Text; }
""", "SG003");
ExpectDiagnostic("""
using Sugoi.Data;
[Component("89a3c4b7-cefe-426a-bd6b-80eaf11e7bbf")] public partial struct BrokenOwner {
    [ComponentDestroy] private static void Drop(in ComponentContext c, ref BrokenOwner value) { }
}
""", "SG004");
ExpectDiagnostic("""
using Sugoi.Data;
[Component("4bbf461a-27f5-4a9c-bcd8-d4b9da5be41e")] public readonly partial struct Frozen { public readonly Entity Target; }
""", "SG005");
ExpectDiagnostic("""
using System;
using System.Threading.Tasks;
using Sugoi.Tasks;
[QueryJob] public partial struct AsyncJob { private async Task Execute() { await Task.Yield(); } }
""", "SG006");
ExpectDiagnostic("""
using System;
using Sugoi.Tasks;
[QueryJob, Without(typeof(int))] public partial struct ExcludedJob { private void Execute(Span<int> a) { } }
""", "SG006");
ExpectDiagnostic("""
using System;
using Sugoi.Data;
using Sugoi.Tasks;
[Component("c10b3b28-c7b7-42e9-925b-16c99744d93f", Kind=ComponentKind.Tag)] public partial struct Marker { }
[QueryJob] public partial struct TagSpanJob { private void Execute(ReadOnlySpan<Marker> values) { } }
""", "SG006");
ExpectDiagnostic("""
using System;
using Sugoi.Data;
using Sugoi.Tasks;
[QueryJob] public partial struct IdentityWriter { private void Execute(Span<Entity> values) { } }
""", "SG006");

var custom = Generate("""
using Sugoi.Data;
[Component("a493c917-991b-42ea-a14e-8e5eb21a22bb")]
public readonly partial struct Custom {
    private readonly Entity _entity;
    public Custom(Entity entity) => _entity=entity;
    [ComponentRemap] private static void Map(ref Custom value, EntityRemapper remapper) => value=new Custom(remapper(value._entity));
}
""");
Check(!custom.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), "Custom remapper could not handle a readonly representation.");

// Run the same semantic input through the same driver: models must cache without retaining Compilation/ISymbol.
var originalInput = positive.Compilation.RemoveSyntaxTrees(positive.Compilation.SyntaxTrees.Where(t => t.FilePath != "Input.cs"));
var cachedDriver = positive.Driver.RunGenerators(originalInput);
var tracked = cachedDriver.GetRunResult().Results.SelectMany(r => r.TrackedSteps)
    .Where(step => step.Key == "SugoiComponents" || step.Key == "SugoiJobs")
    .SelectMany(step => step.Value).SelectMany(step => step.Outputs).ToArray();
Check(tracked.Length > 0, "Incremental generator steps were not tracked.");
Check(tracked.All(output => output.Reason == IncrementalStepRunReason.Cached || output.Reason == IncrementalStepRunReason.Unchanged), "Unchanged component/job models did not cache.");

var unrelated = originalInput.AddSyntaxTrees(CSharpSyntaxTree.ParseText("internal sealed class Unrelated { public int Value; }", parseOptions, "Unrelated.cs"));
var changedDriver = cachedDriver.RunGenerators(unrelated);
var before = cachedDriver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).ToDictionary(s => s.HintName, s => s.SourceText.ToString());
var after = changedDriver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).ToDictionary(s => s.HintName, s => s.SourceText.ToString());
Check(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var source) && pair.Value == source), "An unrelated edit changed generated component/module output.");

Console.WriteLine($"Sugoi generator checks passed: {checks}");
AdvancedGeneratorChecks.Run(references, parseOptions);
