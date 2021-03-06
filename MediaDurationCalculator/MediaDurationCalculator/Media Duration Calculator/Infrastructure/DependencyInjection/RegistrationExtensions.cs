﻿using System.Diagnostics.CodeAnalysis;
using MediaDurationCalculator.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MediaDurationCalculator.Infrastructure.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    public static class RegistrationExtensions
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