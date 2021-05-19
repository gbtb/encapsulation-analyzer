using System;
using System.Collections.Concurrent;
using EncapsulationAnalyzer.Core;
using EncapsulationAnalyzer.Core.Analyzers;
using Microsoft.CodeAnalysis.MSBuild;
using Spectre.Console;

namespace EncapsulationAnalyzer.CLI
{
    /// <summary>
    /// Subscribes to progress of search operation and project load progress, and reports it to Spectre.Console
    /// </summary>
    internal class AnsiConsoleProgressSubscriber : IProgress<FindInternalClassesProgress>, IProgress<ProjectLoadProgress>
    {
        private readonly ProgressContext _progressContext;
        private readonly ConcurrentDictionary<FindInternalTypesStep, ProgressTask> _tasks;

        public AnsiConsoleProgressSubscriber(ProgressContext progressContext)
        {
            _progressContext = progressContext;
            _tasks = new ConcurrentDictionary<FindInternalTypesStep, ProgressTask>();
        }
        
        public void Report(FindInternalClassesProgress value)
        {
            var task = _tasks.GetOrAdd(value.Step, _ =>
            {
                var t = _progressContext.AddTask(value.Step.ToString());
                t.StartTask();
                return t;
            });
            task.Value = value.CurrentValue ?? 0;
            task.MaxValue = value.MaxValue ?? task.MaxValue;
            if (Math.Abs(task.Value - task.MaxValue) < 1e-2)
                task.StopTask();
        }

        public void Report(ProjectLoadProgress value)
        {
            var task = _tasks.GetOrAdd(FindInternalTypesStep.LoadSolution, _ =>
            {
                var t = _progressContext.AddTask(FindInternalTypesStep.LoadSolution.ToString());
                t.StartTask();
                return t;
            });
            
            task.Increment(1);//we dont know in advance amount of steps, so just incrementing counter. Hope it wont exceed 100
        }
    }
}