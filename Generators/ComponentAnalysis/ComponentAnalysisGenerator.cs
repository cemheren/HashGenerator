namespace Generators.ComponentAnalysis
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using Generators.Extensions;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using RoslynHelpers;

    [Generator]
    public class ComponentAnalysisGenerator : ISourceGenerator
    {
        private int count;

        private string version = "0.0.7";

        public Dictionary<string, ITypeSymbol> CompiledDependencies { get; private set; }
  
        public Dictionary<string, SyntaxNode> AllClasses { get; private set; }
        
        public List<ClassDeclarationSyntax> ComponentClasses { get; private set; }
        internal Dictionary<string, Component> ComponentCache { get; private set; }

        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            //if (!Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}
#endif 

            // Register a factory that can create our custom syntax receiver
            context.RegisterForSyntaxNotifications(() => new MySyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // the generator infrastructure will create a receiver and populate it
            // we can retrieve the populated instance via the context
            MySyntaxReceiver syntaxReceiver = (MySyntaxReceiver)context.SyntaxReceiver;

            // Get all my compiled dependencies, they might also have analyzed components in them.
            var types = context.Compilation.GetAllCompilationTypes();

            var properties = types.Where(t => (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Interface) && t.DeclaredAccessibility <= Accessibility.Public).Select(t => new
            {
                TypeSymbol = t,
                Properties = t.GetMembers()
            }).ToList();

            this.CompiledDependencies = properties.Select(p => p.TypeSymbol).ToDictionarySafe(t => t.Name);

            //var f = properties.First().Properties.First();

            this.AllClasses = syntaxReceiver.AllClasses;
            this.ComponentClasses = syntaxReceiver.ClassesToAugment;

            var dir = Assembly.GetExecutingAssembly().Location;
            var compilation = context.Compilation;

            this.ComponentCache = new Dictionary<string, Component>();
            var componentList = new List<Component>();

            this.count = 0;

            foreach (var syntaxNode in this.AllClasses.Values)
            {
                if (syntaxNode is ClassDeclarationSyntax classdeclerationSyntax)
                {
                    var topLevelComponent = new Component
                    {
                        Identifier = classdeclerationSyntax.Identifier.ToString(),
                        HashCode = 1
                    };

                    componentList.Add(topLevelComponent);

                    topLevelComponent.Children = this.GetDescendantNodesRecursive(compilation, syntaxNode, 1);
                    topLevelComponent.HashCode = topLevelComponent.Children.Select(c => c.HashCode).XOr() * syntaxNode.ToString().GetHashCode();
                }
            }

            var componentHashcodes = PrintHashCodes(componentList);
            var componentDependencies = PrintD2Dependencies(componentList, new HashSet<Component>(), new StringBuilder());

            SourceText sourceText = SourceText.From($@"
namespace ComponentAnalysisGenerated {{
    using System.Collections.Generic;
    using System.Linq;

    public partial class ComponentHashcodes
    {{
        private string version => ""{version}"";
        
        {componentHashcodes}
    }}

    public partial class ComponentDependencies
    {{
        private HashSet<string> visited = new HashSet<string>();

        {componentDependencies}
    }}

}}", Encoding.UTF8);

            context.AddSource("ComponentAnalysisGenerated.cs", sourceText);


            //File.WriteAllText(Path.Combine(Path.GetDirectoryName(dir), "Analysis.txt"), string.Join("\n", componentList));
        }

        private List<Component> GetDescendantNodesRecursive(Compilation compilation, SyntaxNode syntaxNode, int level)
        {
            var semantic = compilation.GetSemanticModel(syntaxNode.SyntaxTree);
            var spaces = ""; //// Enumerable.Range(0, level).Select(x => "__").StringJoin();

            var descendants = new List<Component>();
            var processed = new HashSet<SyntaxNode>();

            foreach (var node in AllDescendantNodesRecursive(syntaxNode, processed))
            {
                if (descendants.Count > 2000 || level > 15)
                {
                    break;
                }

                ////var descendantType = semantic.GetTypeInfo(node).Type;

                // todo: filter based on assembly here to reduce noise.
                if (node is IdentifierNameSyntax identifierNameSyntax)
                {
                    var identifierName = identifierNameSyntax.Identifier.ToString();

                    // Don't recompute known components
                    if (ComponentCache.TryGetValue(identifierName, out var discoveredComponent))
                    {
                        descendants.Add(discoveredComponent);
                        continue;
                    }

                    if (this.AllClasses.TryGetValue(identifierName, out var @class))
                    {
                        var current = new Component()
                        {
                            Identifier = $"{spaces}{identifierName}",
                            HashCode = @class.ToFullString().GetHashCode()
                        };

                        current.Children = this.GetDescendantNodesRecursive(compilation, @class, ++level);
                        current.HashCode = current.Children.Select(c => c.HashCode).XOr() * identifierName.GetHashCode();
                    
                        descendants.Add(current);
                        ComponentCache[identifierName] = current;
                    }

                    if (this.CompiledDependencies.TryGetValue(identifierName, out var typeSymbol))
                    {
                        var current = new Component()
                        {
                            Identifier = $"{spaces}{identifierName}",
                            HashCode = typeSymbol.MetadataToken != 0 ? typeSymbol.MetadataToken : typeSymbol.GetHashCode()
                        };

                        descendants.Add(current);
                        ComponentCache[identifierName] = current;
                    }
                }
            }   

            return descendants.ToHashSet().ToList();
        }

        private IEnumerable<SyntaxNode> AllDescendantNodesRecursive(SyntaxNode syntaxNode, HashSet<SyntaxNode> processed)
        {
            if (processed.Contains(syntaxNode))
            {
                yield break;
            }

            var immediateDescendants = syntaxNode.DescendantNodes();

            foreach (var desc in immediateDescendants)
            {
                processed.Add(desc);
                yield return desc;

                foreach (var nextlevel in AllDescendantNodesRecursive(desc, processed))
                { 
                    processed.Add(nextlevel);
                    yield return nextlevel;
                };
            }
        }

        private string PrintHashCodes(List<Component> topLevelComponents)
        {
            return topLevelComponents.Distinct().Select(c => $@"public int {c.Identifier} => {c.HashCode};").StringJoin("\n");
        }

        private string PrintD2Dependencies(List<Component> topLevelComponents, HashSet<Component> cache, StringBuilder sb)
        {
            var queue = new Queue<Component>(topLevelComponents);

            if (queue.Count > 0)
            {
                sb.AppendLine($@"public string d2() {{ ");
            }
            
            var builder = new StringBuilder();

            while (queue.Count > 0)
            {
                var component = queue.Dequeue();
                if (cache.Contains(component) == false)
                {
                    
                    builder.Append($"{component.Identifier}\\n");

                    if (component.Children.Count > 0)
                    {
                        builder.Append($"{component.Identifier} -> {component.Children[0].Identifier}\\n");
                        queue.Enqueue(component.Children[0]);

                        foreach (var child in component.Children.Skip(1))
                        {
                            builder.Append($"{component.Identifier} -> {child.Identifier}\\n");
                            queue.Enqueue(child);
                        }
                    }

                    cache.Add(component);
                }
            }
            sb.AppendLine($@"    return ""{builder.ToString()}""; }}");

            return sb.ToString();
        }

        private string PrintDependencies(List<Component> topLevelComponents, HashSet<Component> cache, StringBuilder sb)
        {
            var queue = new Queue<Component>(topLevelComponents);
            while(queue.Count > 0)
            {
                var component = queue.Dequeue();
                if (cache.Contains(component) == false)
                {
                    sb.AppendLine($@"public List<string> {component.Identifier}() {{ ");

                        sb.AppendLine(@"    var ls = new List<string>() { """ + component.Identifier + @"""};");
                    
                        sb.AppendLine($@"   if(this.visited.Contains(""" + component.Identifier + @""")) return ls;");

                        sb.AppendLine($@"   this.visited.Add(""" + component.Identifier + @""");");

                        if (component.Children.Count > 0)
                        {

                            sb.AppendLine($@"   ls.AddRange({component.Children[0].Identifier}());");
                            queue.Enqueue(component.Children[0]);    

                            foreach (var child in component.Children.Skip(1))
                            {
                                sb.AppendLine($@"   ls.AddRange({child.Identifier}());");
                                queue.Enqueue(child);
                            }
                        }

                    sb.AppendLine("    return ls; }");
                    cache.Add(component);
                }
            }

            return sb.ToString();
        }

        class MySyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> ClassesToAugment = new List<ClassDeclarationSyntax>();
            
            public Dictionary<string, SyntaxNode> AllClasses = new Dictionary<string, SyntaxNode>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Business logic to decide what we're interested in goes here
                if (syntaxNode is ClassDeclarationSyntax cds)
                {
                    if (cds.AttributeLists.Any(a => a is AttributeListSyntax als && als.ToString() == "[ComponentAnalysis]"))
                    {
                        ClassesToAugment.Add(cds);
                    }

                    AllClasses[cds.Identifier.ToString()] = cds;
                }

                if (syntaxNode is InterfaceDeclarationSyntax ids)
                {
                    AllClasses[ids.Identifier.ToString()] = ids;
                }
            }
        }
    }
}
