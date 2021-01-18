using System;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using GenericMediaTools.MediaDurationCalculator.Infrastructure.Constants;
using GenericMediaTools.MediaDurationCalculator.Infrastructure.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GenericMediaTools.MediaDurationCalculator.Infrastructure.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class ServiceProviderExtensions
    {
        public static string GetDirectoryFromConfig(
            this IServiceProvider serviceProvider)
        {
            var directory = serviceProvider
                .GetService<IConfiguration>().GetSection("FileDirectory");

            if (directory == null)
            {
                throw new ArgumentNullException("FileDirectory is required.");
            }

            return directory.Value;
        }
    }
}