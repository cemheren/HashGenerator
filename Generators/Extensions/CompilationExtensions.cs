using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Generators.Extensions
{
    internal static class CompilationExtensions
    {
        public static ITypeSymbol[] GetAllCompilationTypes(this Compilation compilation)
        {
            var types = compilation.SourceModule.ReferencedAssemblySymbols.SelectMany(a =>
            {
                try
                {
                    // Skip some test assemblies for easier unit testing
                    if (a.ToString().Contains("VisualStudio") || a.ToString().Contains("System."))
                    {
                        return Enumerable.Empty<ITypeSymbol>();
                    }

                    //a.TypeNames contains every type including the generated ones, we just need to traverse those.

                    var main = a.Identity.Name.Split('.').Aggregate(a.GlobalNamespace, (s, c) => s.GetNamespaceMembers().Single(m => m.Name.Equals(c)));

                    return GetAllTypes(main);
                }
                catch
                {
                    return Enumerable.Empty<ITypeSymbol>();
                }
            }).ToArray();

            return types;
        }

        private static IEnumerable<ITypeSymbol> GetAllTypes(INamespaceSymbol root)
        {
            foreach (var namespaceOrTypeSymbol in root.GetMembers())
            {
                if (namespaceOrTypeSymbol is INamespaceSymbol @namespace) foreach (var nested in GetAllTypes(@namespace)) yield return nested;

                else if (namespaceOrTypeSymbol is ITypeSymbol type) yield return type;
            }
        }
    }
}
