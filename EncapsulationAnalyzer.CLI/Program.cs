using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace EncapsulationAnalyzer.CLI
{
    class Program
    {
        static Program()
        {
            MSBuildLocator.RegisterDefaults();
            //var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            //MSBuildLocator.RegisterInstance(instances.First());
            //MSBuildLocator.RegisterMSBuildPath(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin");
            //MSBuildLocator.RegisterMSBuildPath(@"C:\Program Files\dotnet\sdk\5.0.101\");
            //MSBuildLocator.RegisterMSBuildPath(@"C:\Program Files\dotnet\sdk\2.1.202");
        }
        
        static async Task Main(string[] args)
        {
            var slnPath = @"C:\projects\websales-git-ssd\SalesWebSite.sln";
            var projPath = @"C:\projects\websales-git-ssd\Dns.Sales\Dns.Sales.csproj";

            var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += HandleWorkspaceFailure;
            
            var solution = await workspace.OpenSolutionAsync(slnPath);
            var proj = solution.Projects.FirstOrDefault(p => p.FilePath == projPath);
            if (proj == null)
            {
                await Console.Error.WriteLineAsync($"Project not found: {projPath}");
                return;
            }

            var otherDocs = GetDocsToSearchIn(solution, proj);

            var compilation = await proj.GetCompilationAsync();

            var publicSymbols = GetNamedTypeSymbols(compilation, compilation.Assembly, s => s.DeclaredAccessibility == Accessibility.Public).ToList();
            foreach (var publicSymbol in publicSymbols)
            {
                var source = new CancellationTokenSource();
                var progressMonitor = new FindRefProgress(source, proj);
                var refs = await SymbolFinder.FindReferencesAsync(publicSymbol, solution, progressMonitor, otherDocs, CancellationToken.None);
                var referencedSymbol = refs?.FirstOrDefault(r => r.Locations.Any());
                if (referencedSymbol != null)
                {
                    Console.WriteLine($"Symbol {referencedSymbol.Definition.ToDisplayString()} is used by other project: {referencedSymbol.Locations.FirstOrDefault()}");
                    continue;
                }
                
                Console.WriteLine($"Public symbol {publicSymbol.ToDisplayString()} can be made internal");
            }
        }

        private static void HandleWorkspaceFailure(object sender, WorkspaceDiagnosticEventArgs e)
        {
            Console.Error.WriteLine(e.Diagnostic.ToString());
        }

        private static ImmutableHashSet<Document> GetDocsToSearchIn(Solution solution, Project proj)
        {
            var graph = solution.GetProjectDependencyGraph();
            var otherProjects = graph.GetProjectsThatDirectlyDependOnThisProject(proj.Id);
            return otherProjects.SelectMany(id => solution.GetProject(id).Documents).ToImmutableHashSet();
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
    }

    internal class FindRefProgress : IFindReferencesProgress
    {
        private readonly CancellationTokenSource _source;
        private readonly Project _project;

        internal FindRefProgress(CancellationTokenSource source, Project project)
        {
            _source = source;
            _project = project;
        }
        
        public void OnStarted()
        {
        }

        public void OnCompleted()
        {
        }

        public void OnFindInDocumentStarted(Document document)
        {
        }

        public void OnFindInDocumentCompleted(Document document)
        {
        }

        public void OnDefinitionFound(ISymbol symbol)
        {
        }

        public void OnReferenceFound(ISymbol symbol, ReferenceLocation location)
        {
            if (location.Document.Project != _project)
                _source.Cancel();
        }

        public void ReportProgress(int current, int maximum)
        {
        }
    }
}