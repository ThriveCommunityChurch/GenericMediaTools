using System;
using System.Diagnostics.CodeAnalysis;

namespace GenericMediaTools.MediaDurationCalculator.Infrastructure.Exceptions
{
    [ExcludeFromCodeCoverage]
    public class ConnectionNotFoundException : Exception
    {
        public ConnectionNotFoundException(string connectionName)
            : base($"Could not find a connection string whose name matches \"{connectionName}\"")
        {
        }
    }
}