using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace GenericMediaTools.MediaDurationCalculator.Infrastructure.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAssemblyTypes(
            this IServiceCollection serviceCollection,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            params Assembly[] assemblies)
        {
            serviceCollection.Scan(scan =>
            {
                scan.FromAssemblies(assemblies)
                    .AddClasses()
                    .AsImplementedInterfaces()
                    .WithLifetime(lifetime);
            });

            return serviceCollection;
        }

        public static IServiceCollection AddAssemblyTypes(
            this IServiceCollection serviceCollection,
            Type typeContainedByAssembly,
            ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            return serviceCollection.AddAssemblyTypes(lifetime, typeContainedByAssembly.GetTypeInfo().Assembly);
        }
    }
}