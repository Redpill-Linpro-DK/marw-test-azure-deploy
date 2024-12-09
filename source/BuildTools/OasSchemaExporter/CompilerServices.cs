using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace OasSchemaExporter;

internal class CompilerServices
{
    internal static Type Compile(string sourceCode, string typeName)
    {
        // Create a syntax tree from the source code
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var references = AssemblyLoadContext.Default.Assemblies.Where(a => !a.IsDynamic).Select(a => MetadataReference.CreateFromFile(a.Location)).ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "MyDynamicAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Emit the compiled assembly to memory
        using var ms = new MemoryStream();
        EmitResult result = compilation.Emit(ms);

        if (!result.Success)
        {
            var failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

            foreach (var failure in failures)
            {
                Console.Error.WriteLine($"{failure.Id}: {failure.GetMessage()}");
            }

            throw new ArgumentException("Failed to compile provided code");
        }

        // Load the assembly into the current AppDomain
        Assembly compiledAssembly = Assembly.Load(ms.ToArray());

        // Get the specified type by name
        return compiledAssembly.GetType(typeName) ?? throw new ArgumentException($"Could not find type {typeName} in provided code");
    }
}