using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading;
using EncapsulationAnalyzer.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        static async Task<int> Main(params string[] args)
        {
            
            var root = new RootCommand
            {
                Description = "CLI interface for finding public .Net types which can be made internal"
            };

            var logOption = new Option<LogLevel>("--logLevel", description: "Set logging level, default level: info", getDefaultValue: () => LogLevel.Information);
            root.AddOption(logOption);
            var res = root.Parse(args);
            var logLevel = res.ValueForOption(logOption);

            var analyzeCommand = new Command("analyze",
                "Analyze project from a solution and find all types in this project which can be made internal")
            {
                Handler = CommandHandler.Create<IHost, FileInfo, string>(AnalyzeCommand)
            };
            
            analyzeCommand.AddArgument(new Argument<FileInfo>("solutionPath"));
            analyzeCommand.AddArgument(new Argument<string>("projectName"));
            var parser = new CommandLineBuilder(root)
                .UseHelp()
                .UseVersionOption()
                .AddCommand(analyzeCommand)
                .UseHost(h => h.ConfigureServices((context, services) =>
                {
                    services
                        .AddLogging(b => b.AddSpectreConsole(configuration => configuration.LogLevel = logLevel))
                        .AddSingleton<IFindInternalClassesPort, FindInternalClasses>();
                })).Build();

            return await parser.InvokeAsync(args);
        }

        private static async Task<int> AnalyzeCommand(IHost host, FileInfo solutionPath, string projectName)
        {
            try
            {
                return await AnsiConsole.Progress().AutoClear(false)
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn()).
                    StartAsync(async progressContext =>
                {
                    var progressSubscriber = new AnsiConsoleProgressSubscriber(progressContext);
                    progressSubscriber.Report(new FindInternalClassesProgress
                    {
                        Step = FindInternalClassesStep.LoadSolution,
                        CurrentValue = 0
                    });
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();
                    var workspace = MSBuildWorkspace.Create();
                    workspace.WorkspaceFailed += HandleWorkspaceFailure(logger);
                
                    var solution = await workspace.OpenSolutionAsync(solutionPath.ToString(), progressSubscriber);
                    var proj = solution.Projects.FirstOrDefault(p => p.Name == projectName);
                    if (proj == null)
                    {
                        AnsiConsole.Write($"Project not found: {projectName}");
                        return -1;
                    }
                    
                    progressSubscriber.Report(new FindInternalClassesProgress
                    {
                        Step = FindInternalClassesStep.LoadSolution,
                        CurrentValue = 100
                    });

                    var port = host.Services.GetRequiredService<IFindInternalClassesPort>();
                    var progress = AnsiConsole.Progress().AutoClear(false);
                    var internalSymbols = await port.FindProjClassesWhichCanBeInternalAsync(solution, proj.Id,
                        progressSubscriber,
                        CancellationToken.None);
                    AnsiConsole.WriteLine($"Found {internalSymbols.Count()} public types which can be made internal");

                    var table = new Table();
                    table.AddColumn("Type").AddColumn("Location");

                    foreach (var symbol in internalSymbols)
                    {
                        table.AddRow($"{symbol.Kind} {symbol.Name}", symbol.Locations.FirstOrDefault()?.GetLineSpan().ToString() ?? "");
                    }
                
                    AnsiConsole.Render(table);
                
                    return 0;
                });
                
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return -1;
            }
        }

        private static EventHandler<WorkspaceDiagnosticEventArgs> HandleWorkspaceFailure(ILogger logger)
        {
            return (object sender, WorkspaceDiagnosticEventArgs e) => logger.LogTrace("Error/Warning while opening solution: {Message}",e.Diagnostic.ToString());
        }
    }
}