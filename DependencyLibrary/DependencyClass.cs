using Newtonsoft.Json;
using System;

namespace DependencyLibrary
{
    public class DependencyClass
    {
        public string SomeFunction(string abc)
        {
            return abc;
        }

        [JsonProperty]
        public int SomeProperty { get; set; }
    }
}
