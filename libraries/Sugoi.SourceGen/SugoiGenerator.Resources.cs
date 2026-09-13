using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Sugoi.SourceGen;

public sealed partial class SugoiGenerator
{
    private static string? FindResourceHook(INamedTypeSymbol type, List<DiagnosticModel> diagnostics, SourceLocation location)
    {
        var hooks = type.GetMembers().OfType<IMethodSymbol>().Where(method => method.GetAttributes().Any(attribute => NameOf(attribute) == "Sugoi.Data.ComponentResourceScanAttribute")).ToArray();
        if (hooks.Length > 1) diagnostics.Add(new DiagnosticModel(InvalidResource, location, $"'{FullName(type)}' declares multiple resource scan hooks."));
        if (hooks.Length == 0) return null;
        var method = hooks[0];
        if (!method.IsStatic || method.IsAsync || method.IsGenericMethod || !method.ReturnsVoid || method.Parameters.Length != 2 ||
            method.Parameters[0].RefKind != RefKind.In || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type) ||
            method.Parameters[1].RefKind != RefKind.None || FullName(method.Parameters[1].Type) != "global::Sugoi.Data.ResourceVisitor")
            diagnostics.Add(new DiagnosticModel(InvalidResource, location, $"'{FullName(type)}.{method.Name}' must be static void(in {FullName(type)}, ResourceVisitor)."));
        return Escape(method.Name);
    }

    private static bool IsResourceField(ISymbol member) => member.GetAttributes().Any(attribute => NameOf(attribute) == "Sugoi.Data.ResourceFieldAttribute");

    private static bool ContainsResources(ITypeSymbol type, HashSet<ITypeSymbol> path)
    {
        if (FullName(type) == "global::Sugoi.Data.ResourceReference") return true;
        if (type is not INamedTypeSymbol named || type.SpecialType != SpecialType.None || type.TypeKind != TypeKind.Struct || !path.Add(type)) return false;
        bool found = named.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic && (IsResourceField(field) || ContainsResources(field.Type, path))) ||
            named.GetMembers().OfType<IPropertySymbol>().Any(property => !property.IsStatic && IsResourceField(property));
        path.Remove(type);
        return found;
    }

    private static void CollectResources(ITypeSymbol type, INamedTypeSymbol root, string expression, HashSet<ITypeSymbol> path,
        List<string> code, List<DiagnosticModel> diagnostics, SourceLocation location, int depth)
    {
        if (FullName(type) == "global::Sugoi.Data.ResourceReference")
        {
            code.Add("if (" + expression + ".Id != global::System.Guid.Empty) visitor(" + expression + ".Id);");
            return;
        }
        if (type is not INamedTypeSymbol named || type.SpecialType != SpecialType.None || type.TypeKind != TypeKind.Struct || !path.Add(type)) return;
        var inline = named.GetAttributes().FirstOrDefault(attribute => NameOf(attribute) == "System.Runtime.CompilerServices.InlineArrayAttribute");
        if (inline != null && inline.ConstructorArguments.Length == 1)
        {
            var field = named.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(member => !member.IsStatic);
            if (field != null && ContainsResources(field.Type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
            {
                string index = "__resource" + depth;
                var body = new List<string>();
                CollectResources(field.Type, root, expression + "[" + index + "]", path, body, diagnostics, location, depth + 1);
                code.Add("for (int " + index + " = 0; " + index + " < " + inline.ConstructorArguments[0].Value + "; ++" + index + ")");
                code.Add("{");
                code.AddRange(body.Select(line => "    " + line));
                code.Add("}");
            }
        }
        else
        {
            var fields = named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic).ToArray();
            foreach (var field in fields.Where(field => !field.IsImplicitlyDeclared).OrderBy(field => field.Name, StringComparer.Ordinal))
                VisitMember(field, field.Type, field.DeclaredAccessibility);
            foreach (var property in named.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic && !property.IsIndexer && property.GetMethod != null).OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                bool automatic = fields.Any(field => field.IsImplicitlyDeclared && SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
                if (automatic || IsResourceField(property)) VisitMember(property, property.Type, property.GetMethod!.DeclaredAccessibility);
            }

            void VisitMember(ISymbol member, ITypeSymbol memberType, Accessibility accessibility)
            {
                bool explicitGuid = IsResourceField(member);
                if (!explicitGuid && !ContainsResources(memberType, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default))) return;
                bool accessible = SymbolEqualityComparer.Default.Equals(named, root) || accessibility == Accessibility.Public ||
                    ((accessibility == Accessibility.Internal || accessibility == Accessibility.ProtectedOrInternal) && SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, root.ContainingAssembly));
                string value = expression + "." + Escape(member.Name);
                if (!accessible)
                {
                    diagnostics.Add(new DiagnosticModel(InvalidResource, location, $"Resource path '{value}' is inaccessible. Supply a [ComponentResourceScan] hook or expose a readable member."));
                    return;
                }
                if (explicitGuid)
                {
                    if (FullName(memberType) == "global::System.Guid") code.Add("if (" + value + " != global::System.Guid.Empty) visitor(" + value + ");");
                    else diagnostics.Add(new DiagnosticModel(InvalidResource, location, $"[ResourceField] member '{value}' must be a Guid. ResourceReference fields are scanned automatically."));
                }
                else CollectResources(memberType, root, value, path, code, diagnostics, location, depth + 1);
            }
        }
        path.Remove(type);
    }
}
