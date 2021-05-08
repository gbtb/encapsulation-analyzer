﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace EncapsulationAnalyzer.Core
{
    public interface IFindInternalClassesPort
    {
        Task<IEnumerable<INamedTypeSymbol>> FindProjClassesWhichCanBeInternalAsync(Solution solution, ProjectId projectId, CancellationToken token);
    }
}