
using Program.ComponentAnalysis;

namespace TestProgram
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var topLevelClassOne = new TopLevelClassOne();
            var topLevelClassTwo = new TopLevelClassTwo();

            System.Console.WriteLine(new ComponentAnalysisGenerated.AnalyzedComponents123().TopLevelClassOne);
        }
    }
}
