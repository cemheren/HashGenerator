﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneratorDependencies;

namespace Program.ComponentAnalysis
{
    [ComponentAnalysis]
    public class TopLevelClassOne
    {
        private DependencyLevelOne dependencyLevelOne;

        private HybridDictionary hybridDictionary;
    }
}
