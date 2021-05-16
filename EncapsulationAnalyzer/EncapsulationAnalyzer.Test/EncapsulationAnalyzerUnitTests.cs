using System;
using System.Linq;
using System.Threading;
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
    [Parallelizable(ParallelScope.All)]
    public class EncapsulationAnalyzerUnitTest
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
        public async Task TwoSimpleProjectsTwoSimpleClassesTest()
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


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(1, internalSymbols.Count());
            Assert.AreEqual("Bar", internalSymbols.Single().Name);
        }
        
        [Test]
        public async Task InternalsVisibleToTest()
        {
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
            var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

            var sourceText = SourceText.From(@"
                using System;
                using System.Runtime.CompilerServices;
                
                [assembly: InternalsVisibleTo(""UI"")]
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


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(2, internalSymbols.Count());
            
            
            Assert.AreEqual("Bar", internalSymbols.OrderBy(s => s.Name).First().Name);
            Assert.AreEqual("Foo", internalSymbols.OrderBy(s => s.Name).Last().Name);
        }
        
        [Test]
        public async Task ShouldCheckForPublicPropertiesInOtherTypes()
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
                        public Bar Prop { get; set; }
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
                        }
                    }
                }
            ");
            workspace.AddDocument(uiProject.Id, "UI.cs", sourceText);


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(0, internalSymbols.Count());
        }
        
        [Test]
        public async Task ShouldCheckForInterfaceMembers()
        {
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
            var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

            var sourceText = SourceText.From(@"
                using System;
                
                namespace Lib 
                {
                    public interface Foo
                    {
                        Bar DoStuff();
                    }

                    internal class Baz 
                    {
                        public Fizz { get; set; }
                    }

                    public class Fizz {}

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
                            Foo foo = null;
                        }
                    }
                }
            ");
            workspace.AddDocument(uiProject.Id, "UI.cs", sourceText);


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(1, internalSymbols.Count());
        }
        
        [Test]
        public async Task ShouldCheckExtensionMethodsReferences()
        {
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
            var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

            var sourceText = SourceText.From(@"
                using System;
                
                namespace Lib 
                {
                    public static class BarExtensions 
                    {
                        public static void Ext(this Bar bar)
                        {
                        }
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
                            var bar = new Bar();
                            bar.Ext();
                        }
                    }
                }
            ");
            workspace.AddDocument(uiProject.Id, "UI.cs", sourceText);


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(0, internalSymbols.Count());
        }
        
        [Test]
        public async Task ShouldCheckForStaticProperties()
        {
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
            var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

            var sourceText = SourceText.From(@"
                using System;
                
                namespace Lib 
                {
                    public static class Foo 
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
                            var bar = new Bar();
                            bar.Prop = Foo.Prop;
                        }
                    }
                }
            ");
            workspace.AddDocument(uiProject.Id, "UI.cs", sourceText);


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(0, internalSymbols.Count());
        }
        
        
        [Test]
        public async Task ShouldSkipClassIfItIsConstructorArgument()
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
                        public Foo(Bar bar)
                        {
                        }
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
                            var foo = new Foo(null);
                        }
                    }
                }
            ");
            workspace.AddDocument(uiProject.Id, "UI.cs", sourceText);


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(0, internalSymbols.Count());
        }
        
        [Test]
        public async Task ShouldSkipEnumIfItIsMethodArgument()
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
                        public abstract void Foo(Bar bar){}
                    }

                    public enum Bar { One, Two }
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
                        }
                    }
                }
            ");
            workspace.AddDocument(uiProject.Id, "UI.cs", sourceText);


            var service = _provider.GetRequiredService<IFindInternalTypesPort>();
            var source = new CancellationTokenSource();
            var internalSymbols = await service.FindProjClassesWhichCanBeInternalAsync(workspace.CurrentSolution, libProject.Id, new Progress<FindInternalClassesProgress>(), source.Token);
            Assert.AreEqual(0, internalSymbols.Count());
        }
    }
}
