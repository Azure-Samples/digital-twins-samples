using System;
using Microsoft.Extensions.Configuration;

namespace SampleFunctionsApp
{
    internal static class ConfigurationHelper
    {
        public static AdtConfiguration GetAdtConfiguration()
        {
            var config = new AdtConfiguration();
            var Configuration = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", true)
                .AddEnvironmentVariables()
                .Build();
            Configuration.GetSection("AdtConfiguration").Bind(config);

            return config;
        }
    }
}
