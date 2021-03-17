using System;
using System.Diagnostics.CodeAnalysis;
using MediaDurationCalculator.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediaDurationCalculator
{
    [ExcludeFromCodeCoverage]
    public static class ConsoleStartup
    {
        public static IServiceProvider SetupDependencyInjection(IConfigurationRoot configuration)
        {
            return new ServiceCollection() // Invoke static configuration methods containing your registrations here
                .RegisterConfigurationOptions(configuration)
                .RegisterMyDependencies()
                .BuildServiceProvider(false);
        }

        public static IConfigurationRoot SetupConfiguration()
        {
            string environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            IConfigurationBuilder configBuilder = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();

            return configBuilder.Build();
        }
    }
}