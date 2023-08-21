using System;
using System.Collections.Generic;
using System.Text;

namespace Generators.ComponentAnalysis

    internal class Component
    {
        public string Identifier { get; set; }

        public string CodeName { get; set; }

        public bool External { get; set; }

        public int HashCode { get; set; }

        public List<Component> Children = new List<Component>();

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
