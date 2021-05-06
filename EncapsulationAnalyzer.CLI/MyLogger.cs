using System;
using Microsoft.Build.Framework;

namespace EncapsulationAnalyzer.CLI
{
    internal class MyLogger : ILogger
    {
        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += LogIt;
        }

        private void LogIt(object sender, BuildEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void Shutdown()
        {
        }

        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Detailed;
        public string Parameters { get; set; }
    }
}