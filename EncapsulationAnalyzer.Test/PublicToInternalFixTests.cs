using System.Linq;
using System.Threading.Tasks;
using EncapsulationAnalyzer.Core;
using EncapsulationAnalyzer.Core.Fixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EncapsulationAnalyzer.Test
{
    [TestFixture]
    public class PublicToInternalFixTests
    {
        [Test]
        public async Task PublicToIntenalFix_ShouldMakeClassInternal()
        {
            var services = new ServiceCollection();
            services
                .AddLogging()
                .AddSingleton<PublicToInternalFix>();
            var provider = services.BuildServiceProvider();

            var fixer = provider.GetRequiredService<PublicToInternalFix>();
            
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
            var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

            var sourceText = SourceText.From(@"
                using System;
                namespace Lib 
                {
                    public class Foo 
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar 
                    {
                       public int Prop { get; set; }
                    }
                }
            ");
            var doc = workspace.AddDocument(libProject.Id, "Lib.cs", sourceText);

            var compilation = await workspace.CurrentSolution.GetProject(libProject.Id).GetCompilationAsync();
            var symbols = compilation.GetSymbolsWithName(_ => true);

            var newSolution = await fixer.MakePublicTypesInternal(workspace.CurrentSolution, symbols.OfType<INamedTypeSymbol>());
            Assert.IsTrue(workspace.TryApplyChanges(newSolution));

            var text = await workspace.CurrentSolution.GetDocument(doc.Id).GetTextAsync();
            Assert.IsTrue(text.ToString().Contains("internal class Foo"));
            Assert.IsTrue(text.ToString().Contains("internal class Bar"));
        }
    }
}