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
            var types = compilation.SourceModule.ReferencedAssemblySymbols.SelectMany(assemblySymbol =>
            {
                try
                {
                    // Skip some test assemblies for easier unit testing
                    if (assemblySymbol.ToString().Contains("VisualStudio") || assemblySymbol.ToString().Contains("System."))
                    {
                        return Enumerable.Empty<ITypeSymbol>();
                    }

                    //a.TypeNames contains every type including the generated ones, we just need to traverse those.

                    var main = assemblySymbol.Identity.Name.Split('.').Aggregate(assemblySymbol.GlobalNamespace, 
                        (symbol, c) => 
                        {
                            var members = symbol.GetNamespaceMembers().ToArray();

                            if (members.Length == 0)
                            {
                                return null;
                            }

                            if (members.Length == 1)
                            {
                                return members[0];
                            }

                            var anyNamespaceMemberIntheIdentityName = symbol.GetNamespaceMembers().SingleOrDefault(m => m.Name.Equals(c));

                            return anyNamespaceMemberIntheIdentityName;
                        }
                        );

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
            if (root != null)
            {
                foreach (var namespaceOrTypeSymbol in root.GetMembers())
                {
                    if (namespaceOrTypeSymbol is INamespaceSymbol @namespace) foreach (var nested in GetAllTypes(@namespace)) yield return nested;

                    else if (namespaceOrTypeSymbol is ITypeSymbol type) yield return type;
                }
            }
        }
    }
}
