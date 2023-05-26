
using Program.ComponentAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace TestProgram
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var topLevelClassOne = new TopLevelClassOne();
            var topLevelClassTwo = new TopLevelClassTwo();

            System.Console.WriteLine(new ComponentAnalysisGenerated.ComponentHashcodes().Program);
            System.Console.WriteLine(string.Join(",", new ComponentAnalysisGenerated.ComponentDependencies().d2()));
        }
    }
}

