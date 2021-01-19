using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediaDurationCalculator.Infrastructure.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    public static class ConfigurationOptionsRegistrationExtensions
    {
        public static IServiceCollection RegisterConfigurationOptions(
            this ServiceCollection serviceCollection,
            IConfigurationRoot configuration)
        {
            serviceCollection.AddSingleton<IConfiguration>(x => configuration);

            return serviceCollection;
        }
    }
}