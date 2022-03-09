namespace Generators.ComponentAnalysis
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
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

            var properties = types.Where(t => t.TypeKind == TypeKind.Interface && t.DeclaredAccessibility == Accessibility.Public).Select(t => new
            {
                Interface = t,
                Properties = t.GetMembers()
            });

            //var f = properties.First().Properties.First();

            this.AllClasses = syntaxReceiver.AllClasses;
            this.ComponentClasses = syntaxReceiver.ClassesToAugment;

            var dir = Assembly.GetExecutingAssembly().Location;
            var compilation = context.Compilation;

            var componentList = new List<Component>();

            foreach (var classDeclarationSyntax in this.AllClasses.Values)
            {
                var topLevelComponent = new Component
                {
                    Identifier = classDeclarationSyntax.Identifier.ToString(),
                    HashCode = 1
                };

                var componentDependencyList = new List<Component>();
                componentDependencyList.Add(topLevelComponent);

                this.GetDescendantNodesRecursive(compilation, classDeclarationSyntax, 1, componentDependencyList);

                // todo this should only compute using descendants.
                topLevelComponent.HashCode = componentDependencyList.Select(c => c.HashCode).Multiply() * classDeclarationSyntax.ToString().GetHashCode();

                //var memberString = $@"public static int hashcode => {topLevelComponent.HashCode};";
                componentList.AddRange(componentDependencyList);
            }

            var componentFields = componentList.Distinct().Select(c => $@"public int {c.Identifier} => {c.HashCode};").StringJoin("\n");

            SourceText sourceText = SourceText.From($@"
namespace ComponentAnalysisGenerated {{

    public partial class AnalyzedComponents123
    {{
        {componentFields}
    }}
}}", Encoding.UTF8);

            context.AddSource("ComponentAnalysisGenerated.cs", sourceText);


            //File.WriteAllText(Path.Combine(Path.GetDirectoryName(dir), "Analysis.txt"), string.Join("\n", componentList));
        }

        private void GetDescendantNodesRecursive(Compilation compilation, SyntaxNode syntaxNode, int level, List<Component> classStrlist)
        {
            var semantic = compilation.GetSemanticModel(syntaxNode.SyntaxTree);
            var spaces = Enumerable.Range(0, level).Select(x => "__").StringJoin();

            foreach (var node in syntaxNode.DescendantNodes())
            {
                var descendantType = semantic.GetTypeInfo(node).Type;

                // todo: filter based on assembly here to reduce noise.
                if (descendantType != null)
                {
                    if (node is IdentifierNameSyntax identifierNameSyntax 
                        && this.AllClasses.TryGetValue(identifierNameSyntax.Identifier.Value.ToString(), out var @class))
                    {
                        classStrlist.Add(
                            new Component()
                            {
                                Identifier = $"{spaces}{descendantType.Name}",
                                HashCode = @class.ToFullString().GetHashCode()
                            });

                        this.GetDescendantNodesRecursive(compilation, @class, ++level, classStrlist);
                    }
                }
            }
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

                    AllClasses.Add(cds.Identifier.ToString(), cds);
                }
            }
        }
    }
}
