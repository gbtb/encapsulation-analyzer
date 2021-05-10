namespace EncapsulationAnalyzer.Core
{
    public record FindInternalClassesProgress
    {
        public FindInternalClassesProgress(FindInternalTypesStep step, int? currentValue, int? maxValue = null)
        {
            Step = step;
            CurrentValue = currentValue;
            MaxValue = maxValue;
        }

        public FindInternalTypesStep Step { get; }
        
        public int? CurrentValue { get; }
        
        public int? MaxValue { get; }
    }
}

namespace System.Runtime.CompilerServices
{
    //record bugfix lol
    internal static class IsExternalInit {}
}