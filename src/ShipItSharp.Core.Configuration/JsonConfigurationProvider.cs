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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Logging;

namespace ShipItSharp.Core.Configuration
{
    public class JsonConfigurationProvider : ConfigurationImplementation
    {

        private string _configurationFileName;
        private readonly bool _hasExplicitConfigurationFileName;
        private readonly IConfigurationConnectivityValidator _connectivityValidator;

        public JsonConfigurationProvider(ILanguageProvider languageProvider)
            : this(languageProvider, null, null) { }

        public JsonConfigurationProvider(ILanguageProvider languageProvider, IConfigurationConnectivityValidator connectivityValidator, string configurationFileName = null)
            : base(languageProvider)
        {
            _connectivityValidator = connectivityValidator ?? new OctopusConfigurationConnectivityValidator();
            _hasExplicitConfigurationFileName = configurationFileName != null;
            _configurationFileName = configurationFileName ?? Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "config.json");
        }

        public override async Task<ConfigurationLoadResult> LoadConfiguration()
        {
            var loadResult = new ConfigurationLoadResult();
            if (!_hasExplicitConfigurationFileName && !File.Exists(_configurationFileName))
            {
                _configurationFileName = "config.json";
            }

            if (!File.Exists(_configurationFileName))
            {
                var sampleConfig = GetSampleConfig();
                try
                {
                    await File.WriteAllTextAsync(_configurationFileName,
                        JsonConvert.SerializeObject(sampleConfig, Formatting.Indented));
                    loadResult.Errors.Add(LanguageProvider.GetString(LanguageSection.ConfigurationStrings, "LoadNoFileFound"));
                    return loadResult;
                }
                catch (Exception e)
                {
                    LoggingProvider.GetLogger<JsonConfigurationProvider>().Error("Failed to save sample config", e);
                }
                loadResult.Errors.Add(LanguageProvider.GetString(LanguageSection.ConfigurationStrings, "LoadNoFileFoundCantCreate"));
                return loadResult;
            }

            var stringContent = await File.ReadAllTextAsync(_configurationFileName);
            try
            {
                var config = JsonConvert.DeserializeObject<Configuration>(stringContent);
                if (config == null)
                {
                    loadResult.Errors.Add(LanguageProvider.GetString(LanguageSection.ConfigurationStrings, "LoadCouldntParseFile"));
                    return loadResult;
                }
                await ValidateConfiguration(config, loadResult);
                if (loadResult.Success)
                {
                    loadResult.Configuration = config;
                    return loadResult;
                }
                return loadResult;
            }
            catch (Exception e)
            {
                LoggingProvider.GetLogger<JsonConfigurationProvider>().Error("Failed to parse config", e);
                loadResult.Errors.Add(LanguageProvider.GetString(LanguageSection.ConfigurationStrings, "LoadCouldntParseFile"));
                return loadResult;
            }
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
