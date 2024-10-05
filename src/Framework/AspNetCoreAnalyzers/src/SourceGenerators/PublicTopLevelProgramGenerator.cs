// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.AspNetCore.SourceGenerators;

[Generator]
public class PublicProgramSourceGenerator : IIncrementalGenerator
{
    private const string PublicPartialProgramClassSource = """
// <auto-generated />
public partial class Program { }
""";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var internalGeneratedProgramClass = context.CompilationProvider
            // Get the entry point associated with the compilation, this maps to the Main method definition
            .Select((compilation, cancellationToken) => compilation.GetEntryPoint(cancellationToken))
            // Get the containing symbol of the entry point, this maps to the Program class
            .Select((symbol, _) => symbol?.ContainingSymbol)
            // If the program class is already public, we don't need to generate anything.
            .Select((symbol, _) => symbol?.DeclaredAccessibility == Accessibility.Public ? null : symbol)
            // If the discovered `Program` type is not a class then its not
            // generated and has been defined in source, so we can skip it
            .Select((symbol, _) => symbol is INamedTypeSymbol { TypeKind: TypeKind.Class } ? symbol : null)
            // If there are multiple partial declarations, then do nothing since we don't want
            // to trample on visibility explicitly set by the user
            .Select((symbol, _) => symbol is { DeclaringSyntaxReferences: { Length: 1 } declaringSyntaxReferences } ? declaringSyntaxReferences.Single() : null)
            // If the `Program` class is already declared in user code, we don't need to generate anything.
            .Select((declaringSyntaxReference, cancellationToken) => declaringSyntaxReference?.GetSyntax(cancellationToken) is ClassDeclarationSyntax ? null : declaringSyntaxReference);

        context.RegisterSourceOutput(internalGeneratedProgramClass, (context, result) =>
        {
            if (result is not null)
            {
                context.AddSource("PublicTopLevelProgram.Generated.g.cs", PublicPartialProgramClassSource);
            }
        });
    }
}
