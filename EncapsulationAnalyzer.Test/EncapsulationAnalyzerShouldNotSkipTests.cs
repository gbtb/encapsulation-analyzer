using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EncapsulationAnalyzer.Core.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EncapsulationAnalyzer.Test
{
    [TestFixture]
    public class EncapsulationAnalyzerShouldNotSkipTests
    {
        private ServiceProvider _provider;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var services = new ServiceCollection();
            services.AddLogging()
                .AddSingleton<IFindInternalTypesPort, FindInternalTypes>();
            _provider = services.BuildServiceProvider();
        }
        
        [Test]
        public async Task ShouldNotSkipIfTypeUsedInsidePublicMethod()
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
                        public abstract void Foo()
                        {
                            var b = Bar.One;
                        }
                    }

                    public enum Bar { One, Two }
                }
            ");
            workspace.AddDocument(libProject.Id, "Lib.cs", sourceText);

            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(2, internalSymbols.Count());
        }
        
        [Test]
        public async Task ShouldNotSkipIfTypeIsUsedInsideOwnDeclaration()
        {
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
            var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

            var sourceText = SourceText.From(@"
                using System;
                
                namespace Lib 
                {
                    public interface ILogger<T>{}

                    public class Foo 
                    {
                        public static object Create()
                        {
                            return new Foo();
                        }
                    }

                    public class Bar
                    {
                        public Bar(ILogger<Bar> logger){}
                    }
                }
            ");
            workspace.AddDocument(libProject.Id, "Lib.cs", sourceText);

            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(2, internalSymbols.Count()); //2 instead of 3 because we don't do cycle detection
        }
        
        [Test]
        public async Task ShouldNotSkipIfTypeIsUsedInsideGetterExpression()
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
                        public int Prop => Method<Bar>();
                    
                        private int Method<T> 
                        {
                            return 10;
                        }
                    }

                    public class Bar
                    {
                    }
                }
            ");
            workspace.AddDocument(libProject.Id, "Lib.cs", sourceText);

            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(2, internalSymbols.Count()); 
        }
    }
}