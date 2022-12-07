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
            this._octopusHelper = octopusHelper;
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