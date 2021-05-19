using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;

namespace EncapsulationAnalyzer.Core.Analyzers
{
    internal class FindReferencesProgressSubscriber : IFindReferencesProgress
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _source;
        private readonly Project _project;

        internal FindReferencesProgressSubscriber(ILogger logger, CancellationTokenSource cancellationTokenSource, Project project)
        {
            _logger = logger;
            _source = cancellationTokenSource;
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
            if (location.Document.Project.Id != _project.Id)
            {
               _logger.LogTrace("Symbol {ReferencedSymbol} is used by other project: {Location}",
                   symbol.ToDisplayString(),
                   location.Location.GetLineSpan()
               ); 
                _source.Cancel();
            }
        }

        public void ReportProgress(int current, int maximum)
        {
        }
    }
}