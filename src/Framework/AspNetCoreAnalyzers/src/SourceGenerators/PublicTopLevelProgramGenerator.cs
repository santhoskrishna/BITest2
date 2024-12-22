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
            .Select(static (compilation, cancellationToken) =>
            {
                var entryPoint = compilation.GetEntryPoint(cancellationToken);
                return entryPoint is
                    {
                        // Get the entry point associated with the compilation, this maps to the Main method definition
                        // Get the containing symbol of the entry point, this maps to the Program class
                        // If the discovered `Program` type is not a class then its not
                        // generated and has been defined in source, so we can skip it
                        // If the program class is already public, we don't need to generate anything.
                        ContainingSymbol: { DeclaredAccessibility: Accessibility.Public, TypeKind: TypeKind.Class },
                        // If there are multiple partial declarations, then do nothing since we don't want
                        // to trample on visibility explicitly set by the user
                        DeclaringSyntaxReferences: { Length: 1 } declaringSyntaxReferences
                    } &&
                    // If the `Program` class is already declared in user code, we don't need to generate anything.
                    declaringSyntaxReferences.Single() is not ClassDeclarationSyntax;
            });

        context.RegisterSourceOutput(internalGeneratedProgramClass, (context, result) =>
        {
            if (result)
            {
                context.AddSource("PublicTopLevelProgram.Generated.g.cs", PublicPartialProgramClassSource);
            }
        });
    }
}
