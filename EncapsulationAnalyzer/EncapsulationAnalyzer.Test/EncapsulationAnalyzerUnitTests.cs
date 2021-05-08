using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using EncapsulationAnalyzer.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace EncapsulationAnalyzer.Test
{
    [TestFixture]
    public class EncapsulationAnalyzerUnitTest
    {
        private ServiceProvider _provider;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var services = new ServiceCollection();
            services.AddLogging()
                .AddSingleton<IFindInternalClassesPort, FindInternalClasses>();
            _provider = services.BuildServiceProvider();
        }
        
        [Test]
        public async Task TestMethod1()
        {
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
            workspace.AddDocument(libProject.Id, "Lib.cs", sourceText);
            var uiProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "UI", "UI", LanguageNames.CSharp, 
                    projectReferences: new [] {new ProjectReference(libProject.Id)}));
            sourceText = SourceText.From(@"
                using System;
                using Lib;

                namespace UI
                {
                    public static class Program 
                    {
                        public static void Main(string[] args)
                        {
                            var foo = new Foo();
                            foo.Prop = 1;
                        }
                    }
                }
            ");
            workspace.AddDocument(uiProject.Id, "UI.cs", sourceText);


            var service = _provider.GetRequiredService<IFindInternalClassesPort>();
            var source = new CancellationTokenSource();
            //source.CancelAfter(TimeSpan.FromSeconds(10));
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, source.Token);
            Assert.AreEqual(1, internalSymbols.Count());
            Assert.AreEqual("Bar", internalSymbols.Single().Name);
        }
        
    }
}
