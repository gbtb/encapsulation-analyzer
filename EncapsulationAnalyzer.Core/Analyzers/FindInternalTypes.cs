using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EncapsulationAnalyzer.Core.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis.CSharp.Syntax;


[assembly: InternalsVisibleTo("EncapsulationAnalyzer.Test")]
namespace EncapsulationAnalyzer.Core
{
    internal class FindInternalTypes: IFindInternalTypesPort
    {
        private readonly ILogger<FindInternalTypes> _logger;

        public FindInternalTypes(ILogger<FindInternalTypes> logger)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<INamedTypeSymbol>> FindProjClassesWhichCanBeInternalAsync(Solution solution, ProjectId projectId,  IProgress<FindInternalClassesProgress> progressSubscriber,  CancellationToken token)
        {
            progressSubscriber.Report(new FindInternalClassesProgress(FindInternalTypesStep.GetPublicSymbols, 0));

            var proj = solution.GetProject(projectId);
            if (proj == null)
            {
                _logger.LogError("Project {ProjectId} not found in solution", projectId);
                return Enumerable.Empty<INamedTypeSymbol>();
            }
            
            var compilation = await proj.WithCompilationOptions(
                proj.CompilationOptions
                    .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
                    .WithStrongNameProvider(new DesktopStrongNameProvider()))
                .GetCompilationAsync(token);
            
            if (compilation == null)
            {
                _logger.LogError("Project compilation returned null. Maybe Roslyn has failed to compile?");
                return Enumerable.Empty<INamedTypeSymbol>();
            }

            var publicSymbols = GetNamedTypeSymbols(compilation, compilation.Assembly,
                s => s.DeclaredAccessibility == Accessibility.Public).ToList();
            progressSubscriber.Report(new FindInternalClassesProgress(FindInternalTypesStep.GetPublicSymbols, 100));
            progressSubscriber.Report(new FindInternalClassesProgress(FindInternalTypesStep.GetDocsToSearch, 0));
            
            var docsToSearchIn = GetDocsToSearchIn(solution, proj, compilation);
            progressSubscriber.Report(new FindInternalClassesProgress(FindInternalTypesStep.GetDocsToSearch, 100));

            progressSubscriber.Report(
                new FindInternalClassesProgress(FindInternalTypesStep.LookForReferencesInOtherProjects, 0,
                    publicSymbols.Count));
            var i = 0;

            var resultList = new List<INamedTypeSymbol>();
            var thisProjectDocs = proj.Documents.ToImmutableHashSet();
            foreach (var publicSymbol in publicSymbols)
            {
                if (token.IsCancellationRequested)
                    return Enumerable.Empty<INamedTypeSymbol>();

                progressSubscriber.Report(new FindInternalClassesProgress(
                    FindInternalTypesStep.LookForReferencesInOtherProjects, ++i, publicSymbols.Count));
                
                var hasExternalReference = await FindExternalReferenceAsync(solution, token, proj, publicSymbol, docsToSearchIn);
                if (hasExternalReference)
                    continue;

                if (publicSymbol.MightContainExtensionMethods)
                {
                    hasExternalReference =
                        await FindExtensionMethodsReferencesAsync(solution, token, proj, publicSymbol, docsToSearchIn);
                   
                    if (hasExternalReference)
                        continue;
                }
                
                var refs = await SymbolFinder.FindReferencesAsync(publicSymbol, solution, null, thisProjectDocs,
                    CancellationToken.None);
                foreach (var referencedSymbol in refs.Where(r => r.Definition is INamedTypeSymbol && r.Locations.Any()))
                {
                    foreach (var referenceLocation in referencedSymbol.Locations.Where(l => !l.IsCandidateLocation))
                    {
                        var tree = referenceLocation.Location.SourceTree;
                        if (tree == null)
                            continue;

                        var walker = new ClimbSyntaxTreeWalker();
                        walker.Visit(tree.GetRoot().FindNode(referenceLocation.Location.SourceSpan));
                        if (walker.Result)
                        {
                            goto A;
                        }
                    }
                }
                
                resultList.Add(publicSymbol);
                
                A: continue;
            }

            return resultList;
        }

        private async Task<bool> FindExtensionMethodsReferencesAsync(Solution solution, CancellationToken token, Project proj, INamedTypeSymbol publicSymbol, ImmutableHashSet<Document> docsToSearchIn)
        {
            var extensionMethods = publicSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.IsExtensionMethod && m.DeclaredAccessibility == Accessibility.Public);

            foreach (var methodSymbol in extensionMethods)
            {
                var hasExternalReference =
                    await FindExternalReferenceAsync(solution, token, proj, methodSymbol, docsToSearchIn);
                if (hasExternalReference)
                    return true;
            }

            return false;
        }

        private async Task<bool> FindExternalReferenceAsync(Solution solution, CancellationToken token, Project proj,
            ISymbol publicSymbol, ImmutableHashSet<Document> docsToSearchIn)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(token);
            var searchController = new FindReferencesProgressSubscriber(_logger, source, proj);
            try
            {
                var refs = await SymbolFinder.FindReferencesAsync(publicSymbol, solution, searchController, docsToSearchIn,
                    source.Token);
                var referencedSymbol = refs?.FirstOrDefault(r => r.Locations.Any());

                if (referencedSymbol != null)
                {
                    //due to cancelling token early in searchController, this block is most likely unreachable 
                    _logger.LogTrace("Symbol {ReferencedSymbol} is used by other project: {Location}",
                        referencedSymbol.Definition.ToDisplayString(),
                        referencedSymbol.Locations.FirstOrDefault().Location.GetLineSpan()
                    );
                    return true;
                }

                _logger.LogTrace("Public symbol {PublicSymbol} can be made internal", publicSymbol.ToDisplayString());
                return false;
            }
            catch (OperationCanceledException e)
            {
                if (e.CancellationToken == source.Token)
                    return true;

                throw;
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetNamedTypeSymbols(Compilation compilation,
            IAssemblySymbol compilationAssembly, Predicate<INamedTypeSymbol> searchPredicate)
        {
            var stack = new Stack<INamespaceSymbol>();
            stack.Push(compilation.GlobalNamespace);

            while (stack.Count > 0)
            {
                var @namespace = stack.Pop();

                foreach (var member in @namespace.GetMembers())
                {
                    if (member.ContainingAssembly?.Equals(compilationAssembly, SymbolEqualityComparer.IncludeNullability) == false)
                        continue;
                    
                    if (member is INamespaceSymbol memberAsNamespace)
                    {
                        stack.Push(memberAsNamespace);
                    }
                    else if (member is INamedTypeSymbol memberAsNamedTypeSymbol)
                    {
                        if (searchPredicate(memberAsNamedTypeSymbol))
                            yield return memberAsNamedTypeSymbol;
                    }
                }
            }
        }
        
        private static ImmutableHashSet<Document> GetDocsToSearchIn(Solution solution, Project proj, Compilation compilation)
        {
            var graph = solution.GetProjectDependencyGraph();
            var otherProjects = graph.GetProjectsThatDirectlyDependOnThisProject(proj.Id);

            var friendlyAssemblyNames = compilation.Assembly.GetAttributes()
                .Where(a => a.AttributeClass.Name == "InternalsVisibleTo" || a.AttributeClass.Name == "InternalsVisibleToAttribute")
                .Select(a =>
                {
                    if (a.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax s)
                        return s.ArgumentList?.Arguments.Single().Expression.ToString().Trim('\"');

                    return null;
                })
                .Where(v => v != null)
                .ToImmutableHashSet();
            
            var builder = ImmutableHashSet.CreateBuilder<Document>();
            foreach (var otherProjectId in otherProjects)
            {
                var otherProject = solution.GetProject(otherProjectId);
                if (friendlyAssemblyNames.Contains(otherProject.AssemblyName))
                    continue;

                foreach (var projectDocument in otherProject.Documents)
                {
                    builder.Add(projectDocument);
                }
            }

            
            return builder.ToImmutable();
        }
    }
}