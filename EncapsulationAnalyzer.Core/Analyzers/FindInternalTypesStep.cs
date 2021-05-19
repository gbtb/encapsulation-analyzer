namespace EncapsulationAnalyzer.Core.Analyzers
{
    public enum FindInternalTypesStep
    {
        LoadSolution,
        GetPublicSymbols,
        GetDocsToSearch,
        LookForReferencesInOtherProjects
    }
}