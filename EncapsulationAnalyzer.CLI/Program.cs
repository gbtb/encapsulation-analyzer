using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    }
}