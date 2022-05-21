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

        public Dictionary<string, ITypeSymbol> CompiledDependencies { get; private set; }
  
        public Dictionary<string, ClassDeclarationSyntax> AllClasses { get; private set; }
        
        public List<ClassDeclarationSyntax> ComponentClasses { get; private set; }

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

            var properties = types.Where(t => t.TypeKind == TypeKind.Class && t.DeclaredAccessibility <= Accessibility.Public).Select(t => new
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

            var componentList = new List<Component>();

            this.count = 0;

            foreach (var classDeclarationSyntax in this.AllClasses.Values)
            {
                var topLevelComponent = new Component
                {
                    Identifier = classDeclarationSyntax.Identifier.ToString(),
                    HashCode = 1
                };

                componentList.Add(topLevelComponent);

                topLevelComponent.Children = this.GetDescendantNodesRecursive(compilation, classDeclarationSyntax, 1);
                topLevelComponent.HashCode = topLevelComponent.Children.Select(c => c.HashCode).Multiply() * classDeclarationSyntax.ToString().GetHashCode();
            }

            var componentHashcodes = PrintHashCodes(componentList);
            var componentDependencies = PrintDependencies(componentList, new HashSet<Component>(), new StringBuilder());

            SourceText sourceText = SourceText.From($@"
namespace ComponentAnalysisGenerated {{
    using System.Collections.Generic;
    using System.Linq;

    public partial class ComponentHashcodes
    {{
        {componentHashcodes}
    }}

    public partial class ComponentDependencies
    {{
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
                if (descendants.Count > 200 || level > 10)
                {
                    break;
                }

                ////var descendantType = semantic.GetTypeInfo(node).Type;

                // todo: filter based on assembly here to reduce noise.
                if (node is IdentifierNameSyntax identifierNameSyntax)
                {
                    var identifierName = identifierNameSyntax.Identifier.ToString();
                    if (this.AllClasses.TryGetValue(identifierName, out var @class))
                    {
                        var current = new Component()
                        {
                            Identifier = $"{spaces}{identifierName}",
                            HashCode = @class.ToFullString().GetHashCode()
                        };

                        descendants.Add(current);
                        current.Children = this.GetDescendantNodesRecursive(compilation, @class, ++level);
                        current.HashCode = current.Children.Select(c => c.HashCode).Multiply() * identifierName.GetHashCode();
                    }

                    if (this.CompiledDependencies.TryGetValue(identifierName, out var typeSymbol))
                    {
                        var current = new Component()
                        {
                            Identifier = $"{spaces}{identifierName}",
                            HashCode = typeSymbol.MetadataToken != 0 ? typeSymbol.MetadataToken : typeSymbol.GetHashCode()
                        };

                        descendants.Add(current);
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

        private string PrintDependencies(List<Component> topLevelComponents, HashSet<Component> cache, StringBuilder sb)
        {
            var queue = new Queue<Component>(topLevelComponents);
            while(queue.Count > 0)
            {
                var component = queue.Dequeue();
                if (cache.Contains(component) == false)
                {
                    sb.Append($@"public List<string> {component.Identifier} => ");

                    if (component.Children.Count > 0)
                    {
                        sb.Append(@"new List<string>() { """ + component.Identifier + @"""}.Concat(");

                        sb.Append("this.");
                        sb.Append(component.Children[0].Identifier);
                        queue.Enqueue(component.Children[0]);    

                        foreach (var child in component.Children.Skip(1))
                        {
                            sb.Append(".Concat(");
                            sb.Append(child.Identifier);
                            sb.Append(")");
                            
                            queue.Enqueue(child);
                        }

                        sb.Append(")");
                        sb.Append(".ToList();");
                    }
                    else
                    {
                        sb.Append(@"new List<string>() { """ + component.Identifier + @""" };");
                    }

                    sb.AppendLine();
                    cache.Add(component);
                }
            }

            return sb.ToString();
        }

        class MySyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> ClassesToAugment = new List<ClassDeclarationSyntax>();
            
            public Dictionary<string, ClassDeclarationSyntax> AllClasses = new Dictionary<string, ClassDeclarationSyntax>();

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
            }
        }
    }
}
