using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Sugoi.SourceGen;

public sealed partial class SugoiGenerator
{
    private static Candidate BuildMessage(GeneratorAttributeSyntaxContext context)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var name = FullName(type);
        var location = SourceLocation.Of(type);
        var diagnostics = new List<DiagnosticModel>();
        void Fail(string message) => diagnostics.Add(new DiagnosticModel(InvalidMessage, location, message));
        if (type.TypeKind != TypeKind.Struct || type.IsRefLikeType || !type.IsUnmanagedType || !AllPartial(type) || HasGenericContainer(type) || HasPrivateContainer(type))
            Fail($"Message '{name}' must be an accessible, non-generic, unmanaged partial struct; enclosing types must be partial.");
        if (type.GetMembers("RegisterMessage").Length != 0) Fail($"'{name}' reserves RegisterMessage for its generated static registration.");
        var attribute = context.Attributes[0];
        var rawId = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
        string id = Guid.TryParse(rawId, out var guid) && guid != Guid.Empty ? guid.ToString("D") : "";
        if (id.Length == 0) Fail($"Message '{name}' requires a valid non-empty stable GUID.");
        var alignment = IntArgument(attribute, "Alignment", 0);
        if (alignment < 0 || alignment != 0 && (alignment & (alignment - 1)) != 0)
            Fail($"Message '{name}' alignment must be zero (natural) or a positive power of two.");
        var copyable = BoolArgument(attribute, "IsCopyable", true);
        var hooks = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var hook in new[] { "Copy", "Move", "Destroy" })
        {
            var methods = type.GetMembers().OfType<IMethodSymbol>().Where(m => m.GetAttributes().Any(a => NameOf(a) == "Sugoi.Tasks.Message" + hook + "Attribute")).ToArray();
            if (methods.Length > 1) Fail($"Message '{name}' declares more than one {hook} hook.");
            if (methods.Length == 0) continue;
            var method = methods[0];
            var parameters = method.Parameters;
            bool Value(int index, RefKind refKind) => parameters[index].RefKind == refKind && SymbolEqualityComparer.Default.Equals(parameters[index].Type, type);
            bool validParameters = hook == "Destroy" ? parameters.Length == 1 && Value(0, RefKind.Ref) :
                parameters.Length == 2 && Value(0, hook == "Copy" ? RefKind.In : RefKind.Ref) && Value(1, RefKind.Ref);
            if (!method.IsStatic || method.IsAsync || method.IsGenericMethod || !method.ReturnsVoid || !validParameters)
                Fail($"'{name}.{method.Name}' must exactly match Message{hook}<{name}> as a synchronous static void hook.");
            else hooks.Add(hook, Escape(method.Name));
        }
        if (!copyable && hooks.ContainsKey("Copy")) Fail($"Message '{name}' is marked non-copyable but supplies a Copy hook.");
        if (hooks.ContainsKey("Destroy") && (!hooks.ContainsKey("Move") || copyable && !hooks.ContainsKey("Copy")))
            Fail($"Owning message '{name}' requires Move and, when copyable, Copy hooks alongside Destroy.");
        var source = "";
        if (diagnostics.Count == 0)
        {
            var code = new StringBuilder(Header());
            OpenType(code, type, null);
            code.AppendLine("    /// <summary>Registers a statically reachable message identity and lifetime table; payload construction remains the sender's responsibility.</summary>");
            code.AppendLine("    public static global::Sugoi.Tasks.MessageDescriptor RegisterMessage(global::Sugoi.Tasks.MessageRegistry registry)");
            code.AppendLine("    {");
            code.Append("        return registry.Register<").Append(name).Append(">(new global::Sugoi.Tasks.MessageRegistration<").Append(name).AppendLine(">");
            code.AppendLine("        {");
            code.Append("            Id = new global::System.Guid(").Append(Literal(id)).AppendLine("),");
            code.Append("            Name = ").Append(Literal(StringArgument(attribute, "Name") ?? type.ToDisplayString())).AppendLine(",");
            code.Append("            Alignment = ").Append(alignment).AppendLine(",");
            code.Append("            IsCopyable = ").Append(copyable ? "true" : "false").AppendLine(",");
            foreach (var hook in hooks.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                code.Append("            ").Append(hook.Key).Append(" = ").Append(hook.Value).AppendLine(",");
            code.AppendLine("        });");
            code.AppendLine("    }");
            CloseType(code, type);
            source = code.ToString();
        }
        return new Candidate(name, id, Hint(name, "Message"), source, location, diagnostics.ToImmutableArray());
    }

    private static Dependencies ReadMessageDependencies(Compilation compilation)
    {
        var modules = new SortedSet<string>(StringComparer.Ordinal);
        var identities = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            foreach (var attribute in assembly.GetAttributes())
            {
                if (NameOf(attribute) == "Sugoi.Tasks.GeneratedMessageModuleAttribute" && attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is ITypeSymbol type)
                    modules.Add(FullName(type));
                if (NameOf(attribute) == "Sugoi.Tasks.GeneratedMessageIdentityAttribute" && attribute.ConstructorArguments.Length == 2)
                    identities.Add(attribute.ConstructorArguments[0].Value + "|" + attribute.ConstructorArguments[1].Value);
            }
        return new Dependencies(Sanitize(compilation.AssemblyName ?? "Assembly"), string.Join("\n", modules), string.Join("\n", identities));
    }

    private static void EmitMessageModule(SourceProductionContext output, ImmutableArray<Candidate> messages, Dependencies dependencies)
    {
        var registrations = messages.Where(c => c.Source.Length != 0 && c.Id.Length != 0).OrderBy(c => c.Id, StringComparer.Ordinal).ThenBy(c => c.Name, StringComparer.Ordinal).ToArray();
        if (registrations.Length == 0 && dependencies.Modules.Length == 0) return;
        var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in Lines(dependencies.Identities))
        {
            int separator = identity.IndexOf('|');
            if (separator < 0) continue;
            var id = identity.Substring(0, separator);
            var name = identity.Substring(separator + 1);
            if (identities.TryGetValue(id, out var previous) && previous != name)
                output.ReportDiagnostic(Diagnostic.Create(InvalidMessage, Location.None, $"Referenced message GUID '{id}' is used by both '{previous}' and '{name}'."));
            else identities[id] = name;
        }
        foreach (var message in registrations)
        {
            if (identities.TryGetValue(message.Id, out var previous) && previous != message.Name)
                output.ReportDiagnostic(new DiagnosticModel(InvalidMessage, message.Location, $"Message GUID '{message.Id}' is used by both '{previous}' and '{message.Name}'.").Create());
            else identities[message.Id] = message.Name;
        }
        var moduleName = dependencies.Assembly + "Messages";
        var code = new StringBuilder(Header());
        code.Append("[assembly: global::Sugoi.Tasks.GeneratedMessageModuleAttribute(typeof(global::Sugoi.Generated.").Append(moduleName).AppendLine("))]");
        foreach (var message in registrations)
            code.Append("[assembly: global::Sugoi.Tasks.GeneratedMessageIdentityAttribute(").Append(Literal(message.Id)).Append(", ").Append(Literal(message.Name)).AppendLine(")]");
        code.AppendLine("namespace Sugoi.Generated {");
        code.Append("    public static class ").Append(moduleName).AppendLine(" {");
        code.AppendLine("        public static void Register(global::Sugoi.Tasks.MessageRegistry registry) {");
        foreach (var message in registrations) code.Append("            ").Append(message.Name).AppendLine(".RegisterMessage(registry);");
        code.AppendLine("        }");
        code.AppendLine("        public static void RegisterDependencies(global::Sugoi.Tasks.MessageRegistry registry) {");
        foreach (var module in Lines(dependencies.Modules)) code.Append("            ").Append(module).AppendLine(".RegisterWithDependencies(registry);");
        code.AppendLine("        }");
        code.AppendLine("        public static void RegisterWithDependencies(global::Sugoi.Tasks.MessageRegistry registry) {");
        code.AppendLine("            RegisterDependencies(registry);");
        code.AppendLine("            Register(registry);");
        code.AppendLine("        }");
        code.AppendLine("    }");
        code.AppendLine("}");
        output.AddSource(moduleName + ".Messages.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
    }
}
