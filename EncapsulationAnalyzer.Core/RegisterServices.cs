using EncapsulationAnalyzer.Core.Analyzers;
using EncapsulationAnalyzer.Core.Fixes;
using Microsoft.Extensions.DependencyInjection;

namespace EncapsulationAnalyzer.Core
{
    public static class RegisterServices
    {
        public static IServiceCollection RegisterCoreServices(this IServiceCollection services)
        {
            return services
                .AddSingleton<IFindInternalTypesPort, FindInternalTypes>()
                .AddSingleton<IPublicToInternalFixPort, PublicToInternalFix>()
                ;
        } 
    }
}