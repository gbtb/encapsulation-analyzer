namespace EncapsulationAnalyzer.Core
{
    public record FindInternalClassesProgress
    {
        public FindInternalClassesStep Step { get; init; }
        
        public int? CurrentValue { get; init; }
        
        public int? MaxValue { get; init; }
    }
}

namespace System.Runtime.CompilerServices
{
    //record bugfix lol
    internal static class IsExternalInit {}
}