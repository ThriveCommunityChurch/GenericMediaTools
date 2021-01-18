using System.Diagnostics.CodeAnalysis;
using GenericMediaTools.MediaDurationCalculator.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GenericMediaTools.MediaDurationCalculator.Infrastructure.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    public static class MyRegistrationExtensions
    {
        public static IServiceCollection RegisterMyDependencies(this IServiceCollection services)
        {
            services.AddTransient<IIOService, IOService>();

            // services.AddAssemblyTypes(typeof(ClassInAssemblyToRegister));

            // services.AddAssemblyTypes(typeof(ClassInAssemblyToRegister), lifetime: ServiceLifetime.Scoped);

            return services;
        }
    }
}