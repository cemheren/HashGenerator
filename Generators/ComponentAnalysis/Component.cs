using System;
using System.Collections.Generic;
using System.Text;

namespace Generators.ComponentAnalysis
{
    internal class Component
    {
        public string Identifier { get; set; }

        public int HashCode { get; set; }

        public override string ToString()
        {
            return $"{Identifier} - {HashCode}";
        }
    }
}
