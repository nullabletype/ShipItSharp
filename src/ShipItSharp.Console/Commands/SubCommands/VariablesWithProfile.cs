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
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.Deployment.Models.Variables;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Utilities;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class VariablesWithProfile : BaseCommand
    {
        //todo convert to runner
        public VariablesWithProfile(IOctopusHelper octopusHelper, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider) { }
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profile";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(VariablesWithProfileOptionNames.File, command.Option("-f|--filepath", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ProfileFile"), CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var file = GetStringFromUser(VariablesWithProfileOptionNames.File, string.Empty);

            var config = StandardSerialiser.DeserializeFromJsonNet<VariableSetCollection>(File.ReadAllText(file));

            if (config != null)
            {
                foreach (var varSet in config.VariableSets)
                {
                    System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "UpdatingVariableSet"), varSet.Id, varSet.Variables.Count);
                    try
                    {
                        await OctoHelper.Variables.UpdateVariableSet(varSet);
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "FailedUpdatingVariableSet"), e.Message);
                        return -1;
                    }
                }
            }
            else
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "FailedParsingVariableFile"));
            }

            System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "Done"), string.Empty);

            return 0;
        }

        private struct VariablesWithProfileOptionNames
        {
            public const string File = "file";
        }
    }
}