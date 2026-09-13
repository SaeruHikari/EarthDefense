using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Sugoi.SourceGen;

[Generator(LanguageNames.CSharp)]
public sealed partial class SugoiGenerator : IIncrementalGenerator
{
    private const string ComponentAttribute = "Sugoi.Data.ComponentAttribute";
    private const string BufferAttribute = "Sugoi.Data.BufferComponentAttribute";
    private const string JobAttribute = "Sugoi.Tasks.QueryJobAttribute";
    private const string MessageJobAttribute = "Sugoi.Tasks.MessageJobAttribute";
    private const string MessageAttribute = "Sugoi.Tasks.MessageAttribute";
    private const string CreationJobAttribute = "Sugoi.Tasks.CreationJobAttribute";
    private static readonly DiagnosticDescriptor InvalidComponent = Error("SG001", "Invalid component declaration");
    private static readonly DiagnosticDescriptor InvalidGuid = Error("SG002", "Invalid or conflicting component identity");
    private static readonly DiagnosticDescriptor InvalidRepresentation = Error("SG003", "Component cannot use native storage");
    private static readonly DiagnosticDescriptor InvalidHook = Error("SG004", "Invalid component lifecycle hook");
    private static readonly DiagnosticDescriptor InvalidReference = Error("SG005", "Entity reference cannot be generated safely");
    private static readonly DiagnosticDescriptor InvalidJob = Error("SG006", "Invalid query job binding");
    private static readonly DiagnosticDescriptor InvalidMessage = Error("SG007", "Invalid message declaration");
    private static readonly DiagnosticDescriptor InvalidJobOwnership = Error("SG008", "Job ownership requires an explicit clone");
    private static readonly DiagnosticDescriptor InvalidResource = Error("SG009", "Invalid resource reference scanner");

    private static DiagnosticDescriptor Error(string id, string title) =>
        new DiagnosticDescriptor(id, title, "{0}", "Sugoi", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider.ForAttributeWithMetadataName(ComponentAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => BuildComponent(ctx, false)).WithTrackingName("SugoiComponents");
        var buffers = context.SyntaxProvider.ForAttributeWithMetadataName(BufferAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => BuildComponent(ctx, true)).WithTrackingName("SugoiBuffers");
        var jobs = context.SyntaxProvider.ForAttributeWithMetadataName(JobAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => BuildJob(ctx)).WithTrackingName("SugoiJobs");
        var messageJobs = context.SyntaxProvider.ForAttributeWithMetadataName(MessageJobAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => BuildJob(ctx)).WithTrackingName("SugoiMessageJobs");
        var messages = context.SyntaxProvider.ForAttributeWithMetadataName(MessageAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => BuildMessage(ctx)).WithTrackingName("SugoiMessages");
        var creationJobs = context.SyntaxProvider.ForAttributeWithMetadataName(CreationJobAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => BuildJob(ctx)).WithTrackingName("SugoiCreationJobs");
        context.RegisterSourceOutput(components, Emit);
        context.RegisterSourceOutput(buffers, Emit);
        context.RegisterSourceOutput(jobs, Emit);
        context.RegisterSourceOutput(messageJobs, Emit);
        context.RegisterSourceOutput(messages, Emit);
        context.RegisterSourceOutput(creationJobs, Emit);
        var dependencies = context.CompilationProvider.Select(static (compilation, _) => ReadDependencies(compilation))
            .WithTrackingName("SugoiModuleDependencies");
        context.RegisterSourceOutput(components.Collect().Combine(buffers.Collect()).Combine(dependencies),
            static (output, input) => EmitModule(output, input.Left.Left.AddRange(input.Left.Right), input.Right));
        var messageDependencies = context.CompilationProvider.Select(static (compilation, _) => ReadMessageDependencies(compilation))
            .WithTrackingName("SugoiMessageModuleDependencies");
        context.RegisterSourceOutput(messages.Collect().Combine(messageDependencies),
            static (output, input) => EmitMessageModule(output, input.Left, input.Right));
    }

    private static void Emit(SourceProductionContext output, Candidate candidate)
    {
        foreach (var diagnostic in candidate.Diagnostics) output.ReportDiagnostic(diagnostic.Create());
        if (candidate.Source.Length != 0)
            output.AddSource(candidate.Hint, SourceText.From(candidate.Source, Encoding.UTF8));
    }

    private static Candidate BuildComponent(GeneratorAttributeSyntaxContext context, bool buffer)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var diagnostics = new List<DiagnosticModel>();
        var location = SourceLocation.Of(type);
        var name = FullName(type);
        void Fail(DiagnosticDescriptor descriptor, string message) => diagnostics.Add(new DiagnosticModel(descriptor, location, message));

        if (type.TypeKind != TypeKind.Struct || type.IsRefLikeType || !AllPartial(type) || HasGenericContainer(type))
            Fail(InvalidComponent, $"'{name}' must be a non-generic, non-ref partial struct; enclosing types must be partial and non-generic.");
        if (HasPrivateContainer(type))
            Fail(InvalidComponent, $"'{name}' must be accessible from its assembly module (internal or public).");
        if (!type.IsUnmanagedType)
            Fail(InvalidRepresentation, $"'{name}' contains managed references. Use an unmanaged handle with explicit ownership callbacks; native spans cannot store CLR references.");
        if (type.GetAttributes().Any(a => NameOf(a) == (buffer ? ComponentAttribute : BufferAttribute)))
            Fail(InvalidComponent, $"'{name}' cannot declare both Component and BufferComponent.");
        if (type.GetMembers("Register").Length != 0 || type.GetMembers("__SugoiRemap").Length != 0 || type.GetMembers("__SugoiScanResources").Length != 0)
            Fail(InvalidComponent, $"'{name}' reserves Register and __SugoiRemap for generated component members.");

        var attribute = context.Attributes[0];
        var rawId = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
        var id = Guid.TryParse(rawId, out var parsed) && parsed != Guid.Empty ? parsed.ToString("D") : "";
        if (id.Length == 0) Fail(InvalidGuid, $"'{name}' requires a non-empty, valid stable GUID.");
        var alignment = IntArgument(attribute, "Alignment", 0);
        if (alignment < 0 || (alignment != 0 && (alignment & (alignment - 1)) != 0))
            Fail(InvalidRepresentation, $"'{name}' Alignment must be zero (natural) or a positive power of two.");
        var kind = UIntArgument(attribute, "Kind", 0);
        const uint legalKinds = 0xF0000000;
        if ((kind & ~legalKinds) != 0 || (!buffer && (kind & 0x20000000) != 0) || (buffer && (kind & 0x80000000) != 0) ||
            ((kind & 0x80000000) != 0 && (kind & 0x40000000) != 0) || (kind & 0x50000000u) == 0x50000000u)
            Fail(InvalidRepresentation, $"'{name}' has an invalid component kind. Buffer storage is declared with BufferComponent, and tags cannot be buffers.");
        if ((kind & 0x80000000) != 0 && type.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic))
            Fail(InvalidRepresentation, $"Tag '{name}' cannot contain instance data; a tag has no component column.");
        var inlineCapacity = buffer && attribute.ConstructorArguments.Length > 1 ? (int)(attribute.ConstructorArguments[1].Value ?? 0) : 0;
        if (buffer && inlineCapacity < 0) Fail(InvalidRepresentation, $"'{name}' buffer inline capacity cannot be negative.");

        var hooks = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var hook in new[] { "Construct", "Copy", "Move", "Destroy", "Remap" })
        {
            var methods = type.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.GetAttributes().Any(a => NameOf(a) == "Sugoi.Data.Component" + hook + "Attribute")).ToArray();
            if (methods.Length > 1) Fail(InvalidHook, $"'{name}' declares more than one {hook} hook.");
            if (methods.Length == 0) continue;
            if (!ValidHook(methods[0], hook, type))
                Fail(InvalidHook, $"'{name}.{methods[0].Name}' must exactly match Component{hook}<{name}> and be a synchronous static method.");
            else hooks.Add(hook, Escape(methods[0].Name));
        }
        if (type.GetAttributes().Any(a => NameOf(a) == "Sugoi.Data.ComponentOwnershipAttribute") &&
            (!hooks.ContainsKey("Copy") || !hooks.ContainsKey("Move") || !hooks.ContainsKey("Destroy")))
            Fail(InvalidHook, $"Owned component '{name}' requires explicit Copy, Move, and Destroy hooks.");
        // A destructor owns something: accepting a shallow copy or default move here would double-free it.
        if (hooks.ContainsKey("Destroy") && (!hooks.ContainsKey("Copy") || !hooks.ContainsKey("Move")))
            Fail(InvalidHook, $"'{name}' defines Destroy and must also define Copy and Move to preserve ownership.");

        var referenceLines = new List<string>();
        if (!hooks.ContainsKey("Remap") && BoolArgument(attribute, "GenerateEntityRemap", true) && type.IsUnmanagedType)
            CollectReferences(type, type, "value", new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), referenceLines, diagnostics, location, 0);
        var resourceLines = new List<string>();
        string? resourceHook = FindResourceHook(type, diagnostics, location);
        if (resourceHook == null && type.IsUnmanagedType)
            CollectResources(type, type, "value", new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), resourceLines, diagnostics, location, 0);

        var source = "";
        if (diagnostics.Count == 0)
        {
            var code = new StringBuilder(Header());
            OpenType(code, type, null);
            code.AppendLine("    /// <summary>Registers this component explicitly. Re-registering validates the existing layout and updates compatible callbacks.</summary>");
            code.AppendLine("    public static global::Sugoi.Data.ComponentType Register(global::Sugoi.Data.TypeRegistry registry)");
            code.AppendLine("    {");
            code.Append("        return registry.").Append(buffer ? "RegisterBuffer" : "Register").Append('<').Append(name).AppendLine(">(new global::Sugoi.Data.ComponentRegistration<" + name + ">");
            code.AppendLine("        {");
            code.Append("            Id = new global::System.Guid(").Append(Literal(id)).AppendLine("),");
            code.Append("            Name = ").Append(Literal(StringArgument(attribute, "Name") ?? type.ToDisplayString())).AppendLine(",");
            code.Append("            Alignment = ").Append(alignment).AppendLine(",");
            code.Append("            Kind = (global::Sugoi.Data.ComponentKind)").Append(kind).AppendLine("u,");
            foreach (var hook in hooks.OrderBy(p => p.Key, StringComparer.Ordinal))
                code.Append("            ").Append(hook.Key).Append(" = ").Append(hook.Value).AppendLine(",");
            if (referenceLines.Count != 0) code.AppendLine("            Remap = __SugoiRemap,");
            if (resourceHook != null) code.Append("            ScanResources = ").Append(resourceHook).AppendLine(",");
            else if (resourceLines.Count != 0) code.AppendLine("            ScanResources = __SugoiScanResources,");
            code.Append("        }");
            if (buffer) code.Append(", ").Append(inlineCapacity);
            code.AppendLine(");");
            code.AppendLine("    }");
            if (referenceLines.Count != 0)
            {
                code.Append("    private static void __SugoiRemap(ref ").Append(name).AppendLine(" value, global::Sugoi.Data.EntityRemapper remapper)");
                code.AppendLine("    {");
                foreach (var line in referenceLines) code.Append("        ").AppendLine(line);
                code.AppendLine("    }");
            }
            if (resourceHook == null && resourceLines.Count != 0)
            {
                code.Append("    private static void __SugoiScanResources(in ").Append(name).AppendLine(" value, global::Sugoi.Data.ResourceVisitor visitor)");
                code.AppendLine("    {");
                foreach (var line in resourceLines) code.Append("        ").AppendLine(line);
                code.AppendLine("    }");
            }
            CloseType(code, type);
            source = code.ToString();
        }
        return new Candidate(name, id, Hint(name, buffer ? "Buffer" : "Component"), source, location, diagnostics.ToImmutableArray());
    }

    private static bool ValidHook(IMethodSymbol method, string hook, INamedTypeSymbol component)
    {
        if (!method.IsStatic || method.IsAsync || !method.ReturnsVoid || method.IsGenericMethod) return false;
        var p = method.Parameters;
        bool Context(int i) => p[i].RefKind == RefKind.In && FullName(p[i].Type) == "global::Sugoi.Data.ComponentContext";
        bool Value(int i, RefKind kind) => p[i].RefKind == kind && SymbolEqualityComparer.Default.Equals(p[i].Type, component);
        switch (hook)
        {
            case "Construct": case "Destroy": return p.Length == 2 && Context(0) && Value(1, RefKind.Ref);
            case "Copy": return p.Length == 4 && Context(0) && Value(1, RefKind.In) && Context(2) && Value(3, RefKind.Ref);
            case "Move": return p.Length == 4 && Context(0) && Value(1, RefKind.Ref) && Context(2) && Value(3, RefKind.Ref);
            case "Remap": return p.Length == 2 && Value(0, RefKind.Ref) && p[1].RefKind == RefKind.None && FullName(p[1].Type) == "global::Sugoi.Data.EntityRemapper";
            default: return false;
        }
    }

    private static void CollectReferences(ITypeSymbol type, INamedTypeSymbol root, string expression, HashSet<ITypeSymbol> path,
        List<string> code, List<DiagnosticModel> diagnostics, SourceLocation location, int depth)
    {
        if (FullName(type) == "global::Sugoi.Data.Entity")
        {
            code.Add(expression + " = remapper(" + expression + ");");
            return;
        }
        if (type is not INamedTypeSymbol named || type.SpecialType != SpecialType.None || type.TypeKind != TypeKind.Struct || !path.Add(type)) return;
        var inline = named.GetAttributes().FirstOrDefault(a => NameOf(a) == "System.Runtime.CompilerServices.InlineArrayAttribute");
        if (inline != null && inline.ConstructorArguments.Length == 1)
        {
            var field = named.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => !f.IsStatic);
            if (field != null && ContainsEntity(field.Type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
            {
                var body = new List<string>();
                var variable = "__i" + depth;
                CollectReferences(field.Type, root, expression + "[" + variable + "]", path, body, diagnostics, location, depth + 1);
                code.Add("for (int " + variable + " = 0; " + variable + " < " + inline.ConstructorArguments[0].Value + "; ++" + variable + ")");
                code.Add("{");
                code.AddRange(body.Select(line => "    " + line));
                code.Add("}");
            }
        }
        else
        {
            foreach (var field in named.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (!ContainsEntity(field.Type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default))) continue;
                var accessible = SymbolEqualityComparer.Default.Equals(named, root) || field.DeclaredAccessibility == Accessibility.Public ||
                    ((field.DeclaredAccessibility == Accessibility.Internal || field.DeclaredAccessibility == Accessibility.ProtectedOrInternal) && SymbolEqualityComparer.Default.Equals(field.ContainingAssembly, root.ContainingAssembly));
                if (field.IsReadOnly || field.IsImplicitlyDeclared || !accessible)
                {
                    diagnostics.Add(new DiagnosticModel(InvalidReference, location,
                        $"Entity path '{expression}.{field.Name}' is readonly, auto-backed, or inaccessible. Supply an explicit [ComponentRemap] hook that rebuilds the value, or expose a writable field."));
                    continue;
                }
                CollectReferences(field.Type, root, expression + "." + Escape(field.Name), path, code, diagnostics, location, depth + 1);
            }
        }
        path.Remove(type);
    }

    private static bool ContainsEntity(ITypeSymbol type, HashSet<ITypeSymbol> path)
    {
        if (FullName(type) == "global::Sugoi.Data.Entity") return true;
        if (type is not INamedTypeSymbol named || type.SpecialType != SpecialType.None || type.TypeKind != TypeKind.Struct || !path.Add(type)) return false;
        var found = named.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic && ContainsEntity(f.Type, path));
        path.Remove(type);
        return found;
    }

    private static Dependencies ReadDependencies(Compilation compilation)
    {
        var modules = new SortedSet<string>(StringComparer.Ordinal);
        var identities = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            foreach (var attribute in assembly.GetAttributes())
            {
                if (NameOf(attribute) == "Sugoi.Data.GeneratedComponentModuleAttribute" && attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is ITypeSymbol type)
                    modules.Add(FullName(type));
                if (NameOf(attribute) == "Sugoi.Data.GeneratedComponentIdentityAttribute" && attribute.ConstructorArguments.Length == 2)
                    identities.Add(attribute.ConstructorArguments[0].Value + "|" + attribute.ConstructorArguments[1].Value);
            }
        return new Dependencies(Sanitize(compilation.AssemblyName ?? "Assembly"), string.Join("\n", modules), string.Join("\n", identities));
    }

    private static void EmitModule(SourceProductionContext output, ImmutableArray<Candidate> components, Dependencies dependencies)
    {
        var registrations = components.Where(c => c.Source.Length != 0 && c.Id.Length != 0).OrderBy(c => c.Id, StringComparer.Ordinal).ThenBy(c => c.Name, StringComparer.Ordinal).ToArray();
        // Do not generate a Data dependency into unrelated assemblies that have no components or referenced manifests.
        if (registrations.Length == 0 && dependencies.Modules.Length == 0) return;
        var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in Lines(dependencies.Identities))
        {
            var separator = identity.IndexOf('|');
            if (separator > 0)
            {
                var id = identity.Substring(0, separator);
                var name = identity.Substring(separator + 1);
                if (identities.TryGetValue(id, out var previous) && previous != name)
                    output.ReportDiagnostic(Diagnostic.Create(InvalidGuid, Location.None,
                        $"Referenced component GUID '{id}' is used by both '{previous}' and '{name}'."));
                else identities[id] = name;
            }
        }
        foreach (var component in registrations)
        {
            if (identities.TryGetValue(component.Id, out var previous) && previous != component.Name)
                output.ReportDiagnostic(new DiagnosticModel(InvalidGuid, component.Location,
                    $"Component GUID '{component.Id}' is used by both '{previous}' and '{component.Name}'.").Create());
            else identities[component.Id] = component.Name;
        }
        var moduleName = dependencies.Assembly + "Module";
        var code = new StringBuilder(Header());
        code.Append("[assembly: global::Sugoi.Data.GeneratedComponentModuleAttribute(typeof(global::Sugoi.Generated.").Append(moduleName).AppendLine("))]");
        foreach (var component in registrations)
            code.Append("[assembly: global::Sugoi.Data.GeneratedComponentIdentityAttribute(").Append(Literal(component.Id)).Append(", ").Append(Literal(component.Name)).AppendLine(")]");
        code.AppendLine("namespace Sugoi.Generated");
        code.AppendLine("{");
        code.Append("    public static class ").AppendLine(moduleName);
        code.AppendLine("    {");
        code.AppendLine("        /// <summary>Registers only this assembly. No runtime type discovery or reflection is performed.</summary>");
        code.AppendLine("        public static void Register(global::Sugoi.Data.TypeRegistry registry)");
        code.AppendLine("        {");
        foreach (var component in registrations) code.Append("            ").Append(component.Name).AppendLine(".Register(registry);");
        code.AppendLine("        }");
        code.AppendLine("        public static void RegisterDependencies(global::Sugoi.Data.TypeRegistry registry)");
        code.AppendLine("        {");
        foreach (var module in Lines(dependencies.Modules)) code.Append("            ").Append(module).AppendLine(".RegisterWithDependencies(registry);");
        code.AppendLine("        }");
        code.AppendLine("        public static void RegisterWithDependencies(global::Sugoi.Data.TypeRegistry registry)");
        code.AppendLine("        {");
        code.AppendLine("            RegisterDependencies(registry);");
        code.AppendLine("            Register(registry);");
        code.AppendLine("        }");
        code.AppendLine("    }");
        code.AppendLine("}");
        output.AddSource(moduleName + ".Module.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
    }

    private static string Header() => "// <auto-generated/>\n#nullable enable\n";
    private static string FullName(ITypeSymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static string NameOf(AttributeData attribute) => attribute.AttributeClass?.ToDisplayString() ?? "";
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
    private static string Escape(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None ? "@" + value : value;
    private static string Sanitize(string value)
    {
        var result = new string(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return result.Length == 0 || char.IsDigit(result[0]) ? "_" + result : result;
    }
    private static string Hint(string name, string suffix)
    {
        uint hash = 2166136261;
        foreach (var character in name) hash = unchecked((hash ^ character) * 16777619);
        return Sanitize(name) + "_" + hash.ToString("x8") + "." + suffix + ".g.cs";
    }
    private static IEnumerable<string> Lines(string value) => value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
    private static string? StringArgument(AttributeData attribute, string key) => attribute.NamedArguments.FirstOrDefault(p => p.Key == key).Value.Value as string;
    private static int IntArgument(AttributeData attribute, string key, int fallback) => attribute.NamedArguments.FirstOrDefault(p => p.Key == key).Value.Value is int value ? value : fallback;
    private static bool BoolArgument(AttributeData attribute, string key, bool fallback) => attribute.NamedArguments.FirstOrDefault(p => p.Key == key).Value.Value is bool value ? value : fallback;
    private static uint UIntArgument(AttributeData attribute, string key, uint fallback)
    {
        var value = attribute.NamedArguments.FirstOrDefault(p => p.Key == key).Value.Value;
        return value == null ? fallback : Convert.ToUInt32(value);
    }
    private static bool AllPartial(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.ContainingType)
            if (current.DeclaringSyntaxReferences.Length == 0 || current.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is not TypeDeclarationSyntax declaration || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))) return false;
        return true;
    }
    private static bool HasGenericContainer(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.ContainingType) if (current.Arity != 0) return true;
        return false;
    }
    private static bool HasPrivateContainer(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility == Accessibility.Private || current.DeclaredAccessibility == Accessibility.Protected || current.DeclaredAccessibility == Accessibility.ProtectedAndInternal) return true;
        return false;
    }
    private static void OpenType(StringBuilder code, INamedTypeSymbol type, string? interfaces)
    {
        if (!type.ContainingNamespace.IsGlobalNamespace) code.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).AppendLine(" {");
        var stack = new Stack<INamedTypeSymbol>();
        for (var current = type; current != null; current = current.ContainingType) stack.Push(current);
        while (stack.Count != 0)
        {
            var current = stack.Pop();
            if (current.IsStatic) code.Append("static ");
            if (current.IsReadOnly) code.Append("readonly ");
            code.Append("partial ");
            if (current.IsRecord) code.Append("record ");
            code.Append(current.TypeKind == TypeKind.Struct ? "struct " : "class ").Append(Escape(current.Name));
            if (current.TypeParameters.Length != 0) code.Append('<').Append(string.Join(", ", current.TypeParameters.Select(p => Escape(p.Name)))).Append('>');
            if (SymbolEqualityComparer.Default.Equals(current, type) && interfaces != null) code.Append(" : ").Append(interfaces);
            code.AppendLine(" {");
        }
    }
    private static void CloseType(StringBuilder code, INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.ContainingType) code.AppendLine("}");
        if (!type.ContainingNamespace.IsGlobalNamespace) code.AppendLine("}");
    }

    private readonly struct SourceLocation : IEquatable<SourceLocation>
    {
        public SourceLocation(string path, TextSpan span, LinePositionSpan lines) { Path = path; Span = span; Lines = lines; }
        public string Path { get; }
        public TextSpan Span { get; }
        public LinePositionSpan Lines { get; }
        public static SourceLocation Of(ISymbol symbol)
        {
            var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
            return location == null ? new SourceLocation("", default, default) : new SourceLocation(location.GetLineSpan().Path, location.SourceSpan, location.GetLineSpan().Span);
        }
        public Location Create() => Path.Length == 0 ? Location.None : Location.Create(Path, Span, Lines);
        public bool Equals(SourceLocation other) => Path == other.Path && Span.Equals(other.Span) && Lines.Equals(other.Lines);
        public override bool Equals(object? other) => other is SourceLocation value && Equals(value);
        public override int GetHashCode() => Path.GetHashCode() ^ Span.GetHashCode();
    }
    private readonly struct DiagnosticModel : IEquatable<DiagnosticModel>
    {
        public DiagnosticModel(DiagnosticDescriptor descriptor, SourceLocation location, string message) { Descriptor = descriptor; Location = location; Message = message; }
        public DiagnosticDescriptor Descriptor { get; }
        public SourceLocation Location { get; }
        public string Message { get; }
        public Diagnostic Create() => Diagnostic.Create(Descriptor, Location.Create(), Message);
        public bool Equals(DiagnosticModel other) => Descriptor.Id == other.Descriptor.Id && Location.Equals(other.Location) && Message == other.Message;
        public override bool Equals(object? other) => other is DiagnosticModel value && Equals(value);
        public override int GetHashCode() => Descriptor.Id.GetHashCode() ^ Message.GetHashCode();
    }
    private sealed class Candidate : IEquatable<Candidate>
    {
        public Candidate(string name, string id, string hint, string source, SourceLocation location, ImmutableArray<DiagnosticModel> diagnostics)
        { Name = name; Id = id; Hint = hint; Source = source; Location = location; Diagnostics = diagnostics; }
        public string Name { get; }
        public string Id { get; }
        public string Hint { get; }
        public string Source { get; }
        public SourceLocation Location { get; }
        public ImmutableArray<DiagnosticModel> Diagnostics { get; }
        public bool Equals(Candidate? other) => other != null && Name == other.Name && Id == other.Id && Hint == other.Hint && Source == other.Source && Location.Equals(other.Location) && Diagnostics.SequenceEqual(other.Diagnostics);
        public override bool Equals(object? other) => Equals(other as Candidate);
        public override int GetHashCode() => Name.GetHashCode() ^ Source.GetHashCode();
    }
    private sealed class Dependencies : IEquatable<Dependencies>
    {
        public Dependencies(string assembly, string modules, string identities) { Assembly = assembly; Modules = modules; Identities = identities; }
        public string Assembly { get; }
        public string Modules { get; }
        public string Identities { get; }
        public bool Equals(Dependencies? other) => other != null && Assembly == other.Assembly && Modules == other.Modules && Identities == other.Identities;
        public override bool Equals(object? other) => Equals(other as Dependencies);
        public override int GetHashCode() => Assembly.GetHashCode() ^ Modules.GetHashCode() ^ Identities.GetHashCode();
    }
}
