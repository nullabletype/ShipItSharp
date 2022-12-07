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


using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Deployment.Models.Variables;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class VariableRepository : IVariableRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public VariableRepository(OctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task UpdateVariableSet(VariableSet varSet)
        {
            var id = varSet.Id;
            if (varSet.IdType == VariableSet.VariableIdTypes.Library)
            {
                id = (await _octopusHelper.Client.Repository.LibraryVariableSets.Get(id, CancellationToken.None)).VariableSetId;
            }
            var set = await _octopusHelper.Client.Repository.VariableSets.Get(id, CancellationToken.None);
            foreach (var variable in varSet.Variables)
            {
                var scope = new ScopeSpecification();

                if (variable.EnvironmentIds.Any())
                {
                    scope.Add(ScopeField.Environment, new ScopeValue(variable.EnvironmentIds));
                }
                if (variable.TargetIds.Any())
                {
                    scope.Add(ScopeField.Machine, new ScopeValue(variable.TargetIds));
                }
                if (variable.RoleIds.Any())
                {
                    scope.Add(ScopeField.Role, new ScopeValue(variable.RoleIds));
                }

                set.AddOrUpdateVariableValue(variable.Key, variable.Value, scope);
            }

            await _octopusHelper.Client.Repository.VariableSets.Modify(set, CancellationToken.None);
        }

        internal async Task<List<RequiredVariable>> GetVariables(string variableSetId)
        {
            var variables = await _octopusHelper.Client.Repository.VariableSets.Get(variableSetId, CancellationToken.None);
            var requiredVariables = new List<RequiredVariable>();
            foreach (var variable in variables.Variables)
            {
                if ((variable.Prompt != null) && variable.Prompt.Required)
                {
                    var requiredVariable = new RequiredVariable { Name = variable.Name, Type = variable.Type.ToString(), Id = variable.Id };
                    if (variable.Prompt.DisplaySettings.ContainsKey("Octopus.SelectOptions"))
                    {
                        requiredVariable.ExtraOptions = string.Join(", ", variable.Prompt.DisplaySettings["Octopus.SelectOptions"].Split('\n').Select(s => s.Split('|')[0]));
                    }
                    requiredVariables.Add(requiredVariable);
                }
            }

            return requiredVariables;
        }
    }
}