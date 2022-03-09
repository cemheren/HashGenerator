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

        public override bool Equals(object obj)
        {
            return this.Identifier == ((Component)obj).Identifier;
        }

        public override int GetHashCode()
        {
            return this.Identifier.GetHashCode();
        }
    }
}
