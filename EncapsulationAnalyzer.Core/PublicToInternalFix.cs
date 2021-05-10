using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace EncapsulationAnalyzer.Core
{
    public class PublicToInternalFix: IPublicToInternalFixPort
    {
        private readonly ILogger<PublicToInternalFix> _logger;

        public PublicToInternalFix(ILogger<PublicToInternalFix> logger)
        {
            _logger = logger;
        }
        
        public async Task<Solution> MakePublicTypesInternal(Solution solution,
            IEnumerable<INamedTypeSymbol> publicTypes)
        {
            //group types by doc because multiple types can be declared in same file, and we need to do all changes in one pass
            var groupByDoc = publicTypes
                .SelectMany(p => 
                    p.Locations.Zip(Enumerable.Repeat(p, 100), (location, symbol) => (location, symbol))
                    ).GroupBy(t => solution.GetDocument(t.location.SourceTree));

            foreach (var group in groupByDoc)
            {
                var doc = group.Key;
                if (doc == null)
                {
                    _logger.LogError("Failed to find docs for some symbols location: {Symbols}", "TODO");
                    continue;
                }
                
                var root = await doc.GetSyntaxRootAsync();
                if (root == null)
                {
                    _logger.LogError("Failed to get syntax root of document: {Doc}", doc.Name);
                    continue;
                }
                
                var declarationNodes = group.Select(g => root.FindNode(g.location.SourceSpan));
                var rewriter = new AccesibilityRewriter(declarationNodes);
                var newRoot = rewriter.Visit(root);

                solution = doc.WithSyntaxRoot(newRoot).Project.Solution;
            }

            return solution;
        }
    }
}