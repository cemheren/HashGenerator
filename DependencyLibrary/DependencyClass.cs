using Newtonsoft.Json;
using System;

namespace DependencyLibrary
{
    public static class StaticStuff
    {
        public static int a = 4;
    }

    public class DependencyClass
    {
        public int k = StaticStuff.a;

        public string SomeFunction(string abc)
        {
            return abc;
        }

        [JsonProperty]
        public int SomeProperty { get; set; }
    }
}
