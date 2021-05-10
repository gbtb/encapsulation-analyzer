using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace EncapsulationAnalyzer.Core
{
    public interface IPublicToInternalFixPort
    {
        Task<Solution> MakePublicTypesInternal(Solution solution, IEnumerable<INamedTypeSymbol> publicType);
    }
}