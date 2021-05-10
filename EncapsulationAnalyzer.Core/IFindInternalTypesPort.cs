using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace EncapsulationAnalyzer.Core
{
    public interface IFindInternalTypesPort
    {
        Task<IEnumerable<INamedTypeSymbol>> FindProjClassesWhichCanBeInternalAsync(Solution solution, ProjectId projectId, 
            IProgress<FindInternalClassesProgress> progressSubscriber, CancellationToken token);
    }
}