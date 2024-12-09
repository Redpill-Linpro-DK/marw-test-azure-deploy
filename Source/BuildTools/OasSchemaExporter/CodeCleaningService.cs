using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OasSchemaExporter
{
    internal static class CodeCleaningService
    {

        /// <summary>
        /// Will remove any occurence of _additionalProperties and AdditionalProperties from any class in the provided C#
        /// </summary>
        /// <param name="code">Code to clean</param>
        /// <returns>C# without any _additionalProperties or AdditionalProperties members</returns>
        internal static string CleanAdditionalProperties(string code)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var newRoot = root.ReplaceNodes(
                root.DescendantNodes().OfType<ClassDeclarationSyntax>(),
                (node, _) => RemoveAdditionalProperties(node));

            return newRoot.ToFullString();
        }

        internal static string MakeNullPropertiesSerializable(string code)
        {
            return code.Replace("NullValueHandling.Ignore", "NullValueHandling.Include");
        }

        static ClassDeclarationSyntax RemoveAdditionalProperties(ClassDeclarationSyntax classDeclaration)
        {
            var membersToRemove = classDeclaration.Members
                .Where(m => m is FieldDeclarationSyntax fds &&
                            fds.Declaration.Variables.Any(v => v.Identifier.Text == "_additionalProperties") ||
                            m is PropertyDeclarationSyntax pds &&
                            pds.Identifier.Text == "AdditionalProperties");

            return classDeclaration.RemoveNodes(membersToRemove, SyntaxRemoveOptions.KeepNoTrivia) ?? throw new NullReferenceException();
        }
    }
}


