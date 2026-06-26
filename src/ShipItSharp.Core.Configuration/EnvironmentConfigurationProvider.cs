#region copyright
// /*
//     ShipItSharp Deployment Coordinator. Provides extra tooling to help
//     deploy software through Octopus Deploy.
//
//     Copyright (C) 2022  Steven Davies
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
// */
#endregion

using System;
using System.Linq;
using System.Threading.Tasks;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Language;

namespace ShipItSharp.Core.Configuration
{
    public class EnvironmentConfigurationProvider : ConfigurationImplementation
    {
        public const string OctopusUrlEnvironmentVariable = "SHIPITSHARP_OCTOPUS_URL";
        public const string ApiKeyEnvironmentVariable = "SHIPITSHARP_OCTOPUS_API_KEY";
        public const string DefaultChannelEnvironmentVariable = "SHIPITSHARP_DEFAULT_CHANNEL";
        public const string CacheTimeoutEnvironmentVariable = "SHIPITSHARP_CACHE_TIMEOUT_SECONDS";

        private readonly IConfigurationConnectivityValidator _connectivityValidator;

        public EnvironmentConfigurationProvider(ILanguageProvider languageProvider)
            : this(languageProvider, null) { }

        public EnvironmentConfigurationProvider(ILanguageProvider languageProvider, IConfigurationConnectivityValidator connectivityValidator)
            : base(languageProvider)
        {
            _connectivityValidator = connectivityValidator ?? new OctopusConfigurationConnectivityValidator();
        }

        public static bool HasEnvironmentConfiguration()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OctopusUrlEnvironmentVariable)) ||
                   !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable));
        }

        public override async Task<ConfigurationLoadResult> LoadConfiguration()
        {
            var loadResult = new ConfigurationLoadResult();
            var config = new Configuration
            {
                OctopusUrl = Environment.GetEnvironmentVariable(OctopusUrlEnvironmentVariable),
                ApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable),
                DefaultChannel = Environment.GetEnvironmentVariable(DefaultChannelEnvironmentVariable) ?? "Default",
                CacheTimeoutInSeconds = GetCacheTimeout()
            };

            await ValidateConfiguration(config, loadResult);
            if (loadResult.Success)
            {
                loadResult.Configuration = config;
            }

            return loadResult;
        }

        private static int GetCacheTimeout()
        {
            var configuredValue = Environment.GetEnvironmentVariable(CacheTimeoutEnvironmentVariable);
            if (int.TryParse(configuredValue, out var timeout))
            {
                return timeout;
            }

            return 1;
        }

        private async Task ValidateConfiguration(IConfiguration config, ConfigurationLoadResult validationResult)
        {
            if (string.IsNullOrEmpty(config.OctopusUrl))
            {
                validationResult.Errors.Add(LanguageProvider.GetString(LanguageSection.ConfigurationStrings, "ValidationOctopusUrl"));
            }

            if (string.IsNullOrEmpty(config.ApiKey))
            {
                validationResult.Errors.Add(LanguageProvider.GetString(LanguageSection.ConfigurationStrings, "ValidationOctopusApiKey"));
            }

            if (!validationResult.Errors.Any())
            {
                try
                {
                    await _connectivityValidator.ValidateConnectivity(config);
                }
                catch (Exception e)
                {
                    validationResult.Errors.Add(LanguageProvider.GetString(LanguageSection.ConfigurationStrings, "ValidationOctopusApiFailure") + ": " + e.Message);
                }
            }
        }
    }
}
