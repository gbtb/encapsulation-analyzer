﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;

namespace EncapsulationAnalyzer.Core
{
    public class FindInternalClasses: IFindInternalClassesPort
    {
        private readonly ILogger<FindInternalClasses> _logger;

        public FindInternalClasses(ILogger<FindInternalClasses> logger)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<INamedTypeSymbol>> FindProjClassesWhichCanBeInternalAsync(Solution solution, ProjectId projectId, CancellationToken token)
        {
            var proj = solution.GetProject(projectId);
            if (proj == null)
            {
                _logger.LogError("Project {ProjectId} not found in solution", projectId);
                return Enumerable.Empty<INamedTypeSymbol>();
            }
            var compilation = await proj.GetCompilationAsync(token);
            if (compilation == null)
            {
                _logger.LogError("Project compilation returned null. Maybe Roslyn has failed to compile?");
                return Enumerable.Empty<INamedTypeSymbol>();
            }

            var publicSymbols = GetNamedTypeSymbols(compilation, compilation.Assembly,
                s => s.DeclaredAccessibility == Accessibility.Public);
            var docsToSearchIn = GetDocsToSearchIn(solution, proj);

            var resultList = new List<INamedTypeSymbol>();
            foreach (var publicSymbol in publicSymbols)
            {
                if (token.IsCancellationRequested)
                    return Enumerable.Empty<INamedTypeSymbol>();
                
                var source = CancellationTokenSource.CreateLinkedTokenSource(token);
                var searchController = new FindReferencesSearchController(_logger, source, proj);
                try
                {
                    var refs = await SymbolFinder.FindReferencesAsync(publicSymbol, solution, searchController, docsToSearchIn, source.Token);
                    var referencedSymbol = refs?.FirstOrDefault(r => r.Locations.Any());
                
                    if (referencedSymbol != null)
                    {
                        //due to cancelling token early in searchController, this block is most likely unreachable 
                        _logger.LogTrace("Symbol {ReferencedSymbol} is used by other project: {Location}",
                            referencedSymbol.Definition.ToDisplayString(),
                            referencedSymbol.Locations.FirstOrDefault()
                        );
                        continue;
                    }
                
                    _logger.LogTrace("Public symbol {PublicSymbol} can be made internal", publicSymbol.ToDisplayString());
                    resultList.Add(publicSymbol);
                }
                catch (OperationCanceledException e)
                {
                    if (e.CancellationToken == source.Token)
                        continue;

                    throw;
                }
                
            }

            return resultList;
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
                    if (member.ContainingAssembly?.Equals(compilationAssembly) == false)
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
        
        private static ImmutableHashSet<Document> GetDocsToSearchIn(Solution solution, Project proj)
        {
            var graph = solution.GetProjectDependencyGraph();
            var otherProjects = graph.GetProjectsThatDirectlyDependOnThisProject(proj.Id);
            return otherProjects.SelectMany(id => solution.GetProject(id)?.Documents).ToImmutableHashSet();
        }
    }
}