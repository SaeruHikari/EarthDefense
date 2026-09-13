using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sugoi.SourceGen;

internal static class AdvancedGeneratorChecks
{
    internal static void Run(IEnumerable<MetadataReference> references, CSharpParseOptions parseOptions)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Advanced generator: " + message);
            checks++;
        }
        (Compilation Compilation, GeneratorDriverRunResult Run) Generate(string source)
        {
            var compilation = CSharpCompilation.Create("AdvancedConsumer",
                [CSharpSyntaxTree.ParseText(source, parseOptions, "Advanced.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new SugoiGenerator().AsSourceGenerator()], parseOptions: parseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            return (updated, driver.GetRunResult());
        }
        var positive = Generate("""
            using System;
            using System.Threading.Tasks;
            using Sugoi.Data;
            using Sugoi.Tasks;
            namespace Advanced;
            [Component("144a7520-576a-49cd-adba-893793cc3401")] public partial struct Value { public int Number; }
            [Component("144a7520-576a-49cd-adba-893793cc3402",Kind=ComponentKind.Chunk)] public partial struct Header { public int Number; }
            [BufferComponent("144a7520-576a-49cd-adba-893793cc3403",4)] public partial struct Element { public int Number; }
            [Message("144a7520-576a-49cd-adba-893793cc3404")] public partial struct Ping { public int Number; }
            [Message("144a7520-576a-49cd-adba-893793cc3405")] public partial struct Pong { public int Number; }
            [QueryJob] public partial struct Mixed {
                private void Execute(Span<Value> owned, OptionalRead<Value> optionalRead, OptionalWrite<Value> optionalWrite,
                    SharedRead<Value> shared, ChunkRead<Header> header, ChunkWrite<Header> update,
                    BufferRead<Element> readBuffer, BufferWrite<Element> writeBuffer,
                    RandomReader<Value> reader, RandomWriter<Value> writer, RandomReadWrite<Value> both,
                    MessageSender<Ping> sender, ReadOnlySpan<Entity> ids) { }
            }
            [MessageJob(typeof(Ping))] public partial struct Receiver {
                private void Execute(MessageConsumer<Ping> messages, Span<Value> values, MessageSender<Pong> sender) { }
            }
            [QueryJob, Read(typeof(Value))] public partial struct AsyncQuery {
                private ValueTask ExecuteAsync(AsyncJobContext batch, RandomReader<Value> random, MessageSender<Pong> sender) => default;
            }
            [MessageJob(typeof(Ping)), Write(typeof(Value))] public partial struct AsyncMessage {
                private async Task ExecuteAsync(MessageTaskBatch<Ping> batch) { await Task.Yield(); }
            }
            [QueryJob, JobOwnership] public partial struct Owner {
                [JobClone] private Owner Copy() => this;
                [JobDispose] private void Release() { }
                private void Execute(Span<Value> values) { }
            }
            [CreationJob(Name="Create fixture"),CreationMeta(nameof(Meta)),CreationBuffer(typeof(Element))]
            public partial struct CreateValues {
                public Entity Meta;
                private void Execute(Span<Value> values, BufferWrite<Element> elements, RandomReader<Value> references) { }
            }
            """);
        Check(!positive.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "Typed component/message/async/lifetime fixture did not compile: " + string.Join(Environment.NewLine, positive.Compilation.GetDiagnostics()));
        Check(positive.Run.Results.All(result => result.Exception is null), "Generator threw on advanced binding input.");
        var source = string.Join("\n", positive.Run.Results.SelectMany(result => result.GeneratedSources).Select(generated => generated.SourceText.ToString()));
        foreach (var expected in new[] { "builder.OptionalRead<", "builder.OptionalWrite<", "builder.ReadShared<", "builder.ReadChunk<", "builder.WriteChunk<", "builder.ReadBuffer<", "builder.WriteBuffer<", "builder.RandomRead<", "builder.RandomWrite<", "builder.RandomReadWrite<", "context.Consumer", "context.MessagesBus", "IAsyncQueryJob.ExecuteAsync", "IAsyncMessageJob<", "IJobCloneable<", "System.IDisposable.Dispose", "RegisterMessage", "SugoiFixturesMessages.RegisterWithDependencies", "ICreationJob.Build", "builder.WithMeta(this.Meta)", "IJobDebugInfo.DebugName" })
            Check(source.Contains(expected, StringComparison.Ordinal), "Missing generated binding/registration: " + expected);

        void Invalid(string input, string diagnostic)
        {
            var generated = Generate(input);
            Check(generated.Run.Diagnostics.Any(value => value.Id == diagnostic), "Expected diagnostic " + diagnostic);
            Check(generated.Run.Results.All(result => result.Exception is null), "Generator crashed while diagnosing invalid input.");
        }
        Invalid("""
            using System;
            using Sugoi.Tasks;
            [QueryJob] public partial class Leaky : IDisposable {
                public void Dispose() { }
                private void Execute(Span<int> values) { }
            }
            """, "SG008");
        Invalid("""
            using Sugoi.Tasks;
            [QueryJob,JobOwnership] public partial struct Leaky {
                [JobClone] private Leaky Copy() => this;
                private void Execute() { }
            }
            """, "SG008");
        Invalid("""
            using System;
            using System.Threading.Tasks;
            using Sugoi.Tasks;
            [QueryJob] public partial struct Escaping {
                private ValueTask ExecuteAsync(OptionalWrite<int> values) => default;
            }
            """, "SG006");
        Invalid("""
            using Sugoi.Tasks;
            [Message("not-guid")] public partial struct Bad { }
            """, "SG007");
        Invalid("""
            using Sugoi.Tasks;
            [Message("a2b1f3ed-97a8-430c-88af-07ba36f0b401")] public partial struct ConflictingDependency { }
            """, "SG007");
        Invalid("""
            using Sugoi.Tasks;
            [Message("999cc08e-904d-480b-8d62-7b61b9b21391")] public partial struct Owner {
                [MessageDestroy] private static void Release(ref Owner value) { }
            }
            """, "SG007");
        Invalid("""
            using Sugoi.Tasks;
            [MessageJob(typeof(int))] public partial struct WrongConsumer {
                private void Execute(MessageConsumer<long> messages) { }
            }
            """, "SG006");
        Invalid("""
            using System.Threading.Tasks;
            using Sugoi.Tasks;
            [QueryJob,JobOwnership] public partial struct AsyncOwner {
                [JobClone] private AsyncOwner Copy() => this;
                [JobDispose] private void Release() { }
                private ValueTask ExecuteAsync(AsyncJobContext batch) => default;
            }
            """, "SG008");
        Invalid("""
            using Sugoi.Data;
            [Component("64e8c8cb-dbe1-43c1-b7c5-cd1cc76cb4e5")] public partial struct WrongResource {
                [ResourceField] public int Asset;
            }
            """, "SG009");
        Invalid("""
            using System.Threading.Tasks;
            using Sugoi.Tasks;
            [CreationJob] public partial struct AsyncCreation {
                private ValueTask ExecuteAsync(AsyncJobContext context) => default;
            }
            """, "SG006");
        Console.WriteLine($"Advanced generator checks passed: {checks}");
    }
}
