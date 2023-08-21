using System;
using System.Collections.Generic;
using System.Text;

namespace GeneratorDependencies
{
    [System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class ComponentAttribute : Attribute
    {
        readonly string componentName;

        // This is a positional argument
        public ComponentAttribute(string componentName)
        {
            this.componentName = componentName;
        }

        public ComponentAttribute()
        {
            this.componentName = null;
        }

        public string ComponentName
        {
            get { return componentName; }
        }

        // This is a named argument
        public bool External { get; set; }
    }
}
