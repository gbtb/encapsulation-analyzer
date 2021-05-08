using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.CommandLine;
using System.CommandLine.Invocation;
using Spectre.Console;

namespace EncapsulationAnalyzer.CLI
{
    class Program
    {
        static Program()
        {
            MSBuildLocator.RegisterDefaults();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action">Action which should be performed</param>
        /// <param name="solutionPath">Path to a solution file</param>
        /// <param name="projectName">Name of a project for analysis</param>
        static async Task Main(params string[] args)
        {
            var root = new RootCommand
            {
                Description = "CLI interface for finding public .Net types which can be made internal"
            };

            var analyzeCommand = new Command("analyze",
                "Analyze project from a solution and find all types in this project which can be made internal")
            {
                Handler = CommandHandler.Create<FileInfo, string>(AnalyzeCommand)
            };
            analyzeCommand.AddArgument(new Argument<FileInfo>("solutionPath"));
            analyzeCommand.AddArgument(new Argument<string>("projectName"));
            root.AddCommand(analyzeCommand);
            //var slnPath = @"C:\projects\websales-git-ssd\SalesWebSite.sln";
            //var projPath = @"C:\projects\websales-git-ssd\Dns.Sales\Dns.Sales.csproj";


            await root.InvokeAsync(args);
        }

        private static async Task<int> AnalyzeCommand(FileInfo solutionPath, string projectName)
        {
            try
            {
                var workspace = MSBuildWorkspace.Create();
                workspace.WorkspaceFailed += HandleWorkspaceFailure;
                
                var solution = await workspace.OpenSolutionAsync(solutionPath.ToString());
                var proj = solution.Projects.FirstOrDefault(p => p.Name == projectName);
                if (proj == null)
                {
                    AnsiConsole.Write($"Project not found: {projectName}");
                    return -1;
                }

                return 0;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return -1;
            }
        }

        private static void HandleWorkspaceFailure(object sender, WorkspaceDiagnosticEventArgs e)
        {
            AnsiConsole.WriteLine(e.Diagnostic.ToString());
        }
    }

    internal enum CommandVerb
    {
        Analyze,
        Fix
    }
}