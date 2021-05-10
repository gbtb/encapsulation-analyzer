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
using System.Text.RegularExpressions;
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

            var refactorCommand =
                new Command("refactor", "Analyze project from a solution then make all found types internal")
                {
                    Handler = CommandHandler.Create<IHost, FileInfo, string>(RefactorCommand)
                };
            
            refactorCommand.AddArgument(new Argument<FileInfo>("solutionPath"));
            refactorCommand.AddArgument(new Argument<string>("projectName"));
            
            analyzeCommand.AddArgument(new Argument<FileInfo>("solutionPath"));
            analyzeCommand.AddArgument(new Argument<string>("projectName"));
            var parser = new CommandLineBuilder(root)
                .UseHelp()
                .UseVersionOption()
                .AddCommand(analyzeCommand)
                .AddCommand(refactorCommand)
                .UseHost(h => h.ConfigureServices((context, services) =>
                {
                    services
                        .AddLogging(b => b
                            .AddFilter("Microsoft", LogLevel.Warning)
                            .AddFilter(l => l >= logLevel)
                            .AddSpectreConsole(configuration =>
                            {
                                configuration.LogLevel = logLevel;
                            }))
                        .RegisterCoreServices();
                })).Build();

            return await parser.InvokeAsync(args);
        }

        private static async Task<int> RefactorCommand(IHost host, FileInfo solutionPath, string projectName)
        {
            return await AnsiConsole.Progress().AutoClear(false)
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(),
                    new ElapsedTimeColumn()).StartAsync(async progressContext =>
                {
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();
                    var progressSubscriber = new AnsiConsoleProgressSubscriber(progressContext);
                    
                    var workspace = CreateWorkspace(progressSubscriber, logger);

                    var (solution, proj) = await OpenSolutionAndProject(solutionPath, projectName, workspace, progressSubscriber, logger);
                    if (proj == null)
                        return -1;
                    
                    progressSubscriber.Report(
                        new FindInternalClassesProgress(FindInternalTypesStep.LoadSolution, 100));

                    var port = host.Services.GetRequiredService<IFindInternalTypesPort>();
                    var internalSymbols = await port.FindProjClassesWhichCanBeInternalAsync(solution, proj.Id,
                        progressSubscriber,
                        CancellationToken.None);
                    AnsiConsole.WriteLine($"Found {internalSymbols.Count()} public types which can be made internal");

                    var fix = host.Services.GetRequiredService<IPublicToInternalFixPort>();
                    var newSolution = await fix.MakePublicTypesInternal(solution, internalSymbols);
                    return workspace.TryApplyChanges(newSolution) ? 0 : -1;
                });
        }

        private static async Task<int> AnalyzeCommand(IHost host, FileInfo solutionPath, string projectName)
        {
            try
            {
                return await AnsiConsole.Progress().AutoClear(false)
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn()).
                    StartAsync(async progressContext =>
                {
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();
                    var progressSubscriber = new AnsiConsoleProgressSubscriber(progressContext);
                    
                    var workspace = CreateWorkspace(progressSubscriber, logger);

                    var (solution, proj) = await OpenSolutionAndProject(solutionPath, projectName, workspace, progressSubscriber, logger);
                    if (proj == null)
                        return -1;

                    progressSubscriber.Report(
                        new FindInternalClassesProgress(FindInternalTypesStep.LoadSolution, 100));

                    var port = host.Services.GetRequiredService<IFindInternalTypesPort>();
                    var internalSymbols = await port.FindProjClassesWhichCanBeInternalAsync(solution, proj.Id,
                        progressSubscriber,
                        CancellationToken.None);
                    AnsiConsole.WriteLine($"Found {internalSymbols.Count()} public types which can be made internal");

                    var table = new Table();
                    table.AddColumn("№").AddColumn("Type").AddColumn("Location");

                    var i = 0;
                    foreach (var symbol in internalSymbols)
                    {
                        table.AddRow($"{++i}", $"{symbol.TypeKind} {symbol.Name}", symbol.Locations.FirstOrDefault()?.GetLineSpan().ToString() ?? string.Empty);
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

        private static async Task<(Solution solution, Project? proj)> OpenSolutionAndProject(FileInfo solutionPath, string projectName, MSBuildWorkspace workspace,
            AnsiConsoleProgressSubscriber progressSubscriber, ILogger<Program> logger)
        {
            var solution = await workspace.OpenSolutionAsync(solutionPath.ToString(), progressSubscriber);
            var proj = solution.Projects.FirstOrDefault(p => p.Name == projectName);
            if (proj == null)
            {
                logger.LogError($"Project not found: {projectName}");
                return (solution, proj);
            }

            return (solution, proj);
        }

        private static MSBuildWorkspace CreateWorkspace(AnsiConsoleProgressSubscriber progressSubscriber, ILogger<Program> logger)
        {
            progressSubscriber.Report(new FindInternalClassesProgress(FindInternalTypesStep.LoadSolution, 0));
            var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += HandleWorkspaceFailure(logger);
            return workspace;
        }

        //regex to replace chars which can be interpreted as Spectre.Console markup
        static Regex replaceRegex = new Regex(@"[\[\]]", RegexOptions.Compiled);
        
        private static EventHandler<WorkspaceDiagnosticEventArgs> HandleWorkspaceFailure(ILogger logger)
        {
            return (object sender, WorkspaceDiagnosticEventArgs e) => logger.LogTrace("Error/Warning while opening solution: {Message}", replaceRegex.Replace(e.Diagnostic.ToString(), ""));
        }
    }
}