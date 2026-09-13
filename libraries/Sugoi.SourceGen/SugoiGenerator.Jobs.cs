using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Sugoi.SourceGen;

public sealed partial class SugoiGenerator
{
    private static Candidate BuildJob(GeneratorAttributeSyntaxContext context)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var name = FullName(type);
        var location = SourceLocation.Of(type);
        var diagnostics = new List<DiagnosticModel>();
        void Fail(string message) => diagnostics.Add(new DiagnosticModel(InvalidJob, location, message));
        void OwnershipFailure(string message) => diagnostics.Add(new DiagnosticModel(InvalidJobOwnership, location, message));
        bool messageJob = NameOf(context.Attributes[0]) == MessageJobAttribute;
        bool creationJob = NameOf(context.Attributes[0]) == CreationJobAttribute;
        var debugName = StringArgument(context.Attributes[0], "Name");
        if (debugName != null && string.IsNullOrWhiteSpace(debugName)) Fail($"'{name}' declares an empty job debug name.");
        if (debugName != null && type.AllInterfaces.Any(i => FullName(i) == "global::Sugoi.Tasks.IJobDebugInfo"))
            Fail($"'{name}' supplies both a generated Name and IJobDebugInfo; keep one debug name definition.");
        ITypeSymbol? messageType = null;
        if (messageJob && context.Attributes[0].ConstructorArguments.Length == 1)
            messageType = context.Attributes[0].ConstructorArguments[0].Value as ITypeSymbol;
        if (messageJob && (messageType == null || !messageType.IsUnmanagedType))
            Fail($"Message job '{name}' requires one unmanaged payload type in [MessageJob(typeof(T))].");
        if (type.GetAttributes().Count(a => NameOf(a) == JobAttribute || NameOf(a) == MessageJobAttribute || NameOf(a) == CreationJobAttribute) > 1)
            Fail($"'{name}' must select one of QueryJob, MessageJob, or CreationJob.");
        if (!AllPartial(type) || type.IsRefLikeType || type.IsStatic || type.IsAbstract)
            Fail($"Job '{name}' must be a concrete, non-ref partial struct or class with partial enclosing types.");

        var methods = type.GetMembers().OfType<IMethodSymbol>().ToArray();
        var asyncJob = methods.Any(m => m.Name == "ExecuteAsync" || m.Name.EndsWith(".ExecuteAsync", StringComparison.Ordinal));
        if (creationJob && asyncJob) Fail($"Creation job '{name}' is immediate and synchronous; ExecuteAsync is not a creation entry.");
        var payloadName = messageType == null ? "global::System.Byte" : FullName(messageType);
        var synchronousContract = creationJob ? "global::Sugoi.Tasks.ICreationJob" : messageJob ? "global::Sugoi.Tasks.IMessageJob<" + payloadName + ">" : "global::Sugoi.Tasks.IQueryJob";
        var builderType = creationJob ? "global::Sugoi.Tasks.CreationBuilder" : "global::Sugoi.Tasks.JobAccessBuilder";
        var contract = asyncJob ? (messageJob ? "global::Sugoi.Tasks.IAsyncMessageJob<" + payloadName + ">" : "global::Sugoi.Tasks.IAsyncQueryJob") : synchronousContract;
        var contextType = asyncJob
            ? (messageJob ? "global::Sugoi.Tasks.MessageTaskBatch<" + payloadName + ">" : "global::Sugoi.Tasks.AsyncJobContext")
            : (messageJob ? "global::Sugoi.Tasks.MessageJobContext<" + payloadName + ">" : "global::Sugoi.Tasks.JobContext");
        var executeName = asyncJob ? "ExecuteAsync" : "Execute";
        var returnType = asyncJob ? "global::System.Threading.Tasks.ValueTask" : "void";
        bool IsVisibleContract(IMethodSymbol method) => method.ExplicitInterfaceImplementations.Length != 0 || method.DeclaredAccessibility == Accessibility.Public;
        bool ExactExecute(IMethodSymbol method) => (method.Name == executeName || method.Name.EndsWith("." + executeName, StringComparison.Ordinal)) &&
            method.Parameters.Length == 1 && FullName(method.Parameters[0].Type) == contextType &&
            method.Parameters[0].RefKind == (asyncJob ? RefKind.None : RefKind.In) && !method.IsStatic &&
            (asyncJob ? FullName(method.ReturnType) == returnType : method.ReturnsVoid);
        var manualExecute = methods.Any(m => ExactExecute(m) && IsVisibleContract(m));
        var manualBuild = methods.Any(m => (m.Name == "Build" || m.Name.EndsWith(".Build", StringComparison.Ordinal)) &&
            m.Parameters.Length == 1 && FullName(m.Parameters[0].Type) == builderType &&
            m.Parameters[0].RefKind == RefKind.None && m.ReturnsVoid && !m.IsStatic && IsVisibleContract(m));
        var executeCandidates = methods.Where(m => m.Name == executeName).ToArray();
        IMethodSymbol? execute = null;
        if (!manualExecute)
        {
            if (executeCandidates.Length != 1) Fail($"'{name}' needs exactly one typed {executeName} overload or an exact public/explicit {contract} implementation.");
            else
            {
                execute = executeCandidates[0];
                bool returnsTask = FullName(execute.ReturnType) == "global::System.Threading.Tasks.Task";
                if (execute.IsStatic || execute.IsGenericMethod || execute.ReturnsByRef || execute.ReturnsByRefReadonly ||
                    (!asyncJob && (execute.IsAsync || !execute.ReturnsVoid)) ||
                    (asyncJob && FullName(execute.ReturnType) != returnType && !returnsTask))
                    Fail($"'{name}.{executeName}' must be an instance method returning {(asyncJob ? "Task or ValueTask" : "void synchronously")}.");
            }
        }

        var accesses = new List<string>();
        var arguments = new List<string>();
        var required = new HashSet<string>(StringComparer.Ordinal);
        if (execute != null)
        {
            foreach (var parameter in execute.Parameters)
            {
                var parameterType = FullName(parameter.Type);
                if (parameterType == contextType && (parameter.RefKind == RefKind.None || !asyncJob && parameter.RefKind == RefKind.In))
                {
                    arguments.Add(parameter.RefKind == RefKind.In ? "in context" : "context");
                    continue;
                }
                if (parameter.RefKind != RefKind.None)
                {
                    Fail($"'{name}.{executeName}' binding '{parameter.Name}' must be passed by value; only the synchronous context may use in.");
                    continue;
                }
                var generic = parameter.Type as INamedTypeSymbol;
                if (generic == null || generic.TypeArguments.Length != 1 || !generic.TypeArguments[0].IsUnmanagedType)
                {
                    Fail($"'{name}.{executeName}' has unsupported binding '{parameter.Name}'. Use typed component wrappers, the declared message consumer/sender, or {contextType}.");
                    continue;
                }
                var element = generic.TypeArguments[0];
                var component = FullName(element);
                var definition = generic.OriginalDefinition.ToDisplayString();
                bool ownedRead = definition == "System.ReadOnlySpan<T>";
                bool ownedWrite = definition == "System.Span<T>";
                if (ownedRead || ownedWrite)
                {
                    if (asyncJob)
                    {
                        Fail($"Async job '{name}' cannot bind Span/ReadOnlySpan across await. Bind {contextType} and borrow views only inside synchronous helpers.");
                        continue;
                    }
                    if (component == "global::Sugoi.Data.Entity")
                    {
                        if (ownedWrite) Fail($"'{name}' cannot write the Entity identity column; use ReadOnlySpan<Entity>.");
                        arguments.Add("context.Entities");
                        continue;
                    }
                    if (HasSpecialColumn(element)) Fail($"'{name}' must bind buffer/chunk/tag '{component}' using the corresponding wrapper, not an owned scalar span.");
                    required.Add(component);
                    accesses.Add("builder." + (ownedWrite ? "Write" : "Read") + "<" + component + ">();");
                    arguments.Add("context.View." + (ownedWrite ? "WriteOwned" : "ReadOwned") + "<" + component + ">()");
                    continue;
                }
                string? access = null;
                string? binding = null;
                bool requires = false;
                bool borrowed = true;
                switch (definition)
                {
                    case "Sugoi.Tasks.OptionalRead<T>": access = "OptionalRead"; break;
                    case "Sugoi.Tasks.OptionalWrite<T>": access = "OptionalWrite"; break;
                    case "Sugoi.Tasks.SharedRead<T>": access = "ReadShared"; requires = true; break;
                    case "Sugoi.Tasks.ChunkRead<T>": access = "ReadChunk"; requires = true; break;
                    case "Sugoi.Tasks.ChunkWrite<T>": access = "WriteChunk"; requires = true; break;
                    case "Sugoi.Tasks.BufferRead<T>": access = "ReadBuffer"; requires = true; break;
                    case "Sugoi.Tasks.BufferWrite<T>": access = "WriteBuffer"; requires = true; break;
                    case "Sugoi.Tasks.RandomReader<T>": access = "RandomRead"; borrowed = false; binding = "new " + parameterType + "(context.World)"; break;
                    case "Sugoi.Tasks.RandomWriter<T>": access = "RandomWrite"; borrowed = false; binding = "new " + parameterType + "(context.World)"; break;
                    case "Sugoi.Tasks.RandomReadWrite<T>": access = "RandomReadWrite"; borrowed = false; binding = "new " + parameterType + "(context.World)"; break;
                    case "Sugoi.Tasks.MessageConsumer<T>":
                        if (!messageJob || !SymbolEqualityComparer.Default.Equals(element, messageType))
                            Fail($"Message consumer '{component}' does not match this task's MessageJob payload.");
                        binding = "context.Consumer";
                        break;
                    case "Sugoi.Tasks.MessageSender<T>":
                        borrowed = false;
                        binding = "new " + parameterType + "(context.MessagesBus)";
                        break;
                    default:
                        Fail($"'{name}.{executeName}' has unsupported binding '{parameterType}'.");
                        continue;
                }
                if (asyncJob && borrowed)
                {
                    Fail($"Async job '{name}' cannot retain borrowed binding '{parameterType}'. Use {contextType} and bind only in a synchronous scope between awaits.");
                    continue;
                }
                if (requires) required.Add(component);
                if (access != null) accesses.Add("builder." + access + "<" + component + ">();");
                arguments.Add(binding ?? "new " + parameterType + "(context.View)");
            }
        }
        AppendAccessAttributes(type, accesses, required, Fail);
        if (creationJob) AppendCreationAttributes(type, accesses, Fail);

        var cloneHook = FindJobHook(type, "JobClone", diagnostics, location);
        var disposeHook = FindJobHook(type, "JobDispose", diagnostics, location);
        if (cloneHook != null && (cloneHook.IsStatic || cloneHook.IsAsync || cloneHook.IsGenericMethod || cloneHook.Parameters.Length != 0 || !SymbolEqualityComparer.Default.Equals(cloneHook.ReturnType, type)))
            OwnershipFailure($"'{name}.{cloneHook.Name}' must be an instance, synchronous, parameterless method returning {name}.");
        if (disposeHook != null && (disposeHook.IsStatic || disposeHook.IsAsync || disposeHook.IsGenericMethod || disposeHook.Parameters.Length != 0 || !disposeHook.ReturnsVoid))
            OwnershipFailure($"'{name}.{disposeHook.Name}' must be an instance, synchronous, parameterless void method.");
        var typedCloneContract = "global::Sugoi.Tasks.IJobCloneable<" + name + ">";
        var oldCloneContract = messageJob ? "global::Sugoi.Tasks.ICloneableMessageJob<" + payloadName + ">" : "global::Sugoi.Tasks.ICloneableQueryJob";
        bool typedClone = type.AllInterfaces.Any(i => FullName(i) == typedCloneContract);
        bool oldClone = type.AllInterfaces.Any(i => FullName(i) == oldCloneContract);
        bool disposable = type.AllInterfaces.Any(i => FullName(i) == "global::System.IDisposable");
        bool declaredOwnership = type.GetAttributes().Any(a => NameOf(a) == "Sugoi.Tasks.JobOwnershipAttribute");
        var publicClone = methods.FirstOrDefault(m => m.Name == "CloneForBatch" && m.DeclaredAccessibility == Accessibility.Public &&
            !m.IsStatic && !m.IsAsync && m.Parameters.Length == 0 && SymbolEqualityComparer.Default.Equals(m.ReturnType, type));
        bool ownsResources = disposable || disposeHook != null || declaredOwnership;
        if (asyncJob && ownsResources && type.TypeKind == TypeKind.Struct)
            OwnershipFailure($"Owning async job '{name}' must be a class with explicit clone/cleanup. An async struct copies this into its state machine, so scheduler cleanup cannot observe later ownership changes.");
        if (!creationJob && ownsResources && !typedClone && !oldClone && cloneHook == null && publicClone == null)
            OwnershipFailure($"Owning job '{name}' requires an explicit IJobCloneable<{name}>/CloneForBatch or [JobClone] copy; shallow copying its resources is unsafe.");
        if (declaredOwnership && !disposable && disposeHook == null)
            OwnershipFailure($"Owning job '{name}' requires IDisposable or a [JobDispose] hook.");
        if (cloneHook != null && (typedClone || oldClone)) OwnershipFailure($"'{name}' supplies both [JobClone] and an existing clone interface; keep one ownership policy.");
        if (disposeHook != null && disposable) OwnershipFailure($"'{name}' supplies both [JobDispose] and IDisposable; keep one cleanup policy.");

        var source = "";
        if (diagnostics.Count == 0)
        {
            bool addClone = !creationJob && !typedClone && !oldClone && (type.TypeKind == TypeKind.Class || cloneHook != null || publicClone != null);
            bool addDispose = disposeHook != null && !disposable;
            var interfaces = contract + (addClone ? ", " + typedCloneContract : "") + (addDispose ? ", global::System.IDisposable" : "") +
                (debugName != null ? ", global::Sugoi.Tasks.IJobDebugInfo" : "");
            var code = new StringBuilder(Header());
            OpenType(code, type, interfaces);
            if (debugName != null) code.Append("    string global::Sugoi.Tasks.IJobDebugInfo.DebugName => ").Append(Literal(debugName)).AppendLine(";");
            if (!manualBuild)
            {
                code.Append("    void ").Append(synchronousContract).Append(".Build(").Append(builderType).AppendLine(" builder)");
                code.AppendLine("    {");
                foreach (var access in accesses.Distinct()) code.Append("        ").AppendLine(access);
                code.AppendLine("    }");
            }
            if (!manualExecute)
            {
                code.Append("    ").Append(returnType).Append(' ').Append(contract).Append('.').Append(executeName).Append('(')
                    .Append(asyncJob ? "" : "in ").Append(contextType).AppendLine(" context)");
                code.AppendLine("    {");
                var call = executeName + "(" + string.Join(", ", arguments) + ")";
                if (asyncJob && execute != null && FullName(execute.ReturnType) == "global::System.Threading.Tasks.Task")
                    code.Append("        return new global::System.Threading.Tasks.ValueTask(").Append(call).AppendLine(");");
                else code.Append("        ").Append(asyncJob ? "return " : "").Append(call).AppendLine(";");
                code.AppendLine("    }");
            }
            if (addClone)
            {
                string clone = cloneHook != null ? Escape(cloneHook.Name) + "()" : publicClone != null ? "CloneForBatch()" : "(" + name + ")MemberwiseClone()";
                code.Append("    ").Append(name).Append(' ').Append(typedCloneContract).Append(".CloneForBatch() => ").Append(clone).AppendLine(";");
            }
            if (addDispose) code.Append("    void global::System.IDisposable.Dispose() => ").Append(Escape(disposeHook!.Name)).AppendLine("();");
            CloseType(code, type);
            source = code.ToString();
        }
        return new Candidate(name, "", Hint(name, creationJob ? "CreationJob" : messageJob ? "MessageJob" : "QueryJob"), source, location, diagnostics.ToImmutableArray());
    }

    private static bool HasSpecialColumn(ITypeSymbol type) => type.GetAttributes().Any(a => NameOf(a) == BufferAttribute ||
        (NameOf(a) == ComponentAttribute && (UIntArgument(a, "Kind", 0) & 0xC0000000u) != 0));

    private static IMethodSymbol? FindJobHook(INamedTypeSymbol type, string attributeName, List<DiagnosticModel> diagnostics, SourceLocation location)
    {
        var methods = type.GetMembers().OfType<IMethodSymbol>().Where(m => m.GetAttributes().Any(a => NameOf(a) == "Sugoi.Tasks." + attributeName + "Attribute")).ToArray();
        if (methods.Length > 1) diagnostics.Add(new DiagnosticModel(InvalidJobOwnership, location, $"'{FullName(type)}' declares multiple [{attributeName}] hooks."));
        return methods.FirstOrDefault();
    }

    private static void AppendAccessAttributes(INamedTypeSymbol type, List<string> accesses, HashSet<string> required, Action<string> fail)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attributeName = NameOf(attribute);
            if (attributeName != "Sugoi.Tasks.ReadAttribute" && attributeName != "Sugoi.Tasks.WriteAttribute" && attributeName != "Sugoi.Tasks.WithoutAttribute") continue;
            if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not ITypeSymbol component || !component.IsUnmanagedType)
            { fail($"'{FullName(type)}' declares an invalid component access type."); continue; }
            var method = attributeName.EndsWith("WithoutAttribute", StringComparison.Ordinal) ? "Without" :
                (BoolArgument(attribute, "Random", false) ? "Random" : "") + (attributeName.EndsWith("ReadAttribute", StringComparison.Ordinal) ? "Read" : "Write");
            if (method == "Without" && required.Contains(FullName(component))) fail($"'{FullName(type)}' both requires and excludes '{FullName(component)}'.");
            accesses.Add("builder." + method + "<" + FullName(component) + ">();");
        }
    }

    private static void AppendCreationAttributes(INamedTypeSymbol type, List<string> accesses, Action<string> fail)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attributeName = NameOf(attribute);
            if (attributeName == "Sugoi.Tasks.CreationComponentAttribute" || attributeName == "Sugoi.Tasks.CreationBufferAttribute")
            {
                if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is ITypeSymbol component && component.IsUnmanagedType)
                    accesses.Add("builder." + (attributeName.EndsWith("CreationBufferAttribute", StringComparison.Ordinal) ? "AddBuffer" : "Add") + "<" + FullName(component) + ">();");
                else fail($"'{FullName(type)}' declares an invalid creation component.");
            }
            if (attributeName != "Sugoi.Tasks.CreationMetaAttribute") continue;
            var memberName = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
            var member = memberName == null ? null : type.GetMembers(memberName).FirstOrDefault();
            ITypeSymbol? memberType = member is IFieldSymbol field && !field.IsStatic ? field.Type :
                member is IPropertySymbol property && !property.IsStatic && !property.IsIndexer && property.GetMethod != null ? property.Type : null;
            if (memberType == null) { fail($"Creation meta '{memberName}' must name an instance Entity or Entity collection member."); continue; }
            string expression = "this." + Escape(memberName!);
            bool valid = FullName(memberType) == "global::Sugoi.Data.Entity" || memberType is IArrayTypeSymbol array && FullName(array.ElementType) == "global::Sugoi.Data.Entity";
            if (memberType is INamedTypeSymbol generic && generic.TypeArguments.Length == 1 && FullName(generic.TypeArguments[0]) == "global::Sugoi.Data.Entity")
            {
                var definition = generic.OriginalDefinition.ToDisplayString();
                if (definition == "System.ReadOnlyMemory<T>" || definition == "System.Memory<T>") { expression += ".Span"; valid = true; }
                else if (definition == "System.ReadOnlySpan<T>" || definition == "System.Span<T>" ||
                    generic.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")) valid = true;
            }
            if (!valid) fail($"Creation meta '{memberName}' must contain Entity values.");
            else accesses.Add("builder.WithMeta(" + expression + ");");
        }
    }
}
