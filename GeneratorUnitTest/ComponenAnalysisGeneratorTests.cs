using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using DependencyLibrary;
using GeneratorDependencies;
using Generators.ComponentAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeneratorUnitTest
{
    [TestClass]
    public class ComponenAnalysisGeneratorTests
    {
        private static string Filler =>
@"
    public class Program
    {
        public static void Main(string[] args)
        {
            var topLevelClassOne = new TopLevelClassOne();
            //var topLevelClassTwo = new TopLevelClassTwo();
        }
    }
"
;

        [TestMethod]
        public void RecursionGenerationTest()
        {
            string userSource = @$"
namespace Program.Test
{{
    using System;
    using GeneratorDependencies;
    using DependencyLibrary;

    {Filler}

    public class DependencyLevelOne
    {{ 
        private DependencyLevelOne dependencyLevelOne;    
    }}

    [ComponentAnalysis]
    public class TopLevelClassOne
    {{
        private DependencyLevelOne dependencyLevelOne;

        private TopLevelClassOne topLevelClass;
    }}
}}
";
            Compilation comp = CreateCompilation(userSource);
            var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            var newComp = RunGenerators(comp, out var generatorDiags, new ComponentAnalysisGenerator());
            var newFile = newComp.SyntaxTrees.Single(x => Path.GetFileName(x.FilePath).EndsWith("ComponentAnalysisGenerated.cs"));

            Assert.IsNotNull(newFile);
            var generatedfile = newFile.GetText().ToString();

            Assert.IsTrue(generatedfile.Contains("DependencyLevelOne"), message: "DependencyLevelOne");

            Assert.AreEqual(0, generatorDiags.Length);
            errors = newComp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            Assert.AreEqual(0, errors.Count, message: string.Join("\n ", errors));
        }


        [TestMethod]
        public void SimpleGeneratorTest()
        {
            string userSource = @$"
namespace Program.Test
{{
    using System;
    using GeneratorDependencies;
    using DependencyLibrary;

    {Filler}

    public class DependencyLevelTwo    
    {{
        private ITopLevelInterface topLevelInterface;
    }}
    
    public class DependencyLevelOne
    {{ 
        private DependencyLevelTwo dependencyLevelTwo;    

        private Interleaver interleaver;

        private DependencyClass dependencyClass;

        private readonly IDependencyInterface dependencyInterface;
    }}

    public interface ITopLevelInterface
    {{
    }}

    [ComponentAnalysis]
    public class TopLevelClassOne
    {{
        private DependencyLevelOne dependencyLevelOne;
    }}

    [ComponentAnalysis]
    public class TopLevelClassTwo
    {{
        private DependencyLevelOne dependencyLevelOne;
    }}
}}
";
            Compilation comp = CreateCompilation(userSource);
            var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            var newComp = RunGenerators(comp, out var generatorDiags, new ComponentAnalysisGenerator());
            var newFile = newComp.SyntaxTrees.Single(x => Path.GetFileName(x.FilePath).EndsWith("ComponentAnalysisGenerated.cs"));

            Assert.IsNotNull(newFile);
            var generatedfile = newFile.GetText().ToString();

            Assert.IsTrue(generatedfile.Contains("DependencyLevelOne"), message: "DependencyLevelOne");
            Assert.IsTrue(generatedfile.Contains("DependencyLevelTwo"), message: "DependencyLevelTwo");
            Assert.IsTrue(generatedfile.Contains("IDependencyInterface"), message: "IDependencyInterface");

            Assert.AreEqual(0, generatorDiags.Length);
            errors = newComp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            Assert.AreEqual(0, errors.Count, message: string.Join("\n ", errors));
        }

        private static Compilation CreateCompilation(string source)
        {
            var dd = typeof(Enumerable).GetTypeInfo().Assembly.Location;
            var coreDir = Directory.GetParent(dd);

            // need to manually add .netstandard since the GeneratorDependencies is on .netstandard2.0 
            var references = new List<PortableExecutableReference>{
                    MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(DependencyClass).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(IGeneratorCapable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "netstandard.dll")
                };

            Assembly.GetEntryAssembly().GetReferencedAssemblies()
                .ToList()
                .ForEach(a => references.Add(MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

            return CSharpCompilation.Create("compilation",
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions()) },
                references,
                new CSharpCompilationOptions(OutputKind.WindowsApplication));
        }
        
        private static Compilation RunGenerators(Compilation c, out ImmutableArray<Diagnostic> diagnostics, params ISourceGenerator[] generators)
        {
            CSharpGeneratorDriver.Create(generators).RunGeneratorsAndUpdateCompilation(c, out var outputCompilation, out diagnostics);
            return outputCompilation;
        }
    }
 }

