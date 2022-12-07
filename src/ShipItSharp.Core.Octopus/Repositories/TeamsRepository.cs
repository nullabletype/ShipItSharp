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


using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class TeamsRepository : ITeamsRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public TeamsRepository(OctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task RemoveEnvironmentsFromTeams(string envId)
        {
            var teams = await _octopusHelper.Client.Repository.Teams.FindAll(CancellationToken.None);
            foreach (var team in teams)
            {
                var scopes = await _octopusHelper.Client.Repository.Teams.GetScopedUserRoles(team);

                foreach (var scope in scopes.Where(s => s.EnvironmentIds.Contains(envId)))
                {
                    scope.EnvironmentIds.Remove(envId);
                    await _octopusHelper.Client.Repository.ScopedUserRoles.Modify(scope, CancellationToken.None);
                }

            }
        }

        public async Task AddEnvironmentToTeam(string envId, string teamId)
        {
            var team = await _octopusHelper.Client.Repository.Teams.Get(teamId, CancellationToken.None);
            var environment = await _octopusHelper.Client.Repository.Environments.Get(envId, CancellationToken.None);
            if (team == null || environment == null)
            {
                return;
            }
            var scopes = await _octopusHelper.Client.Repository.Teams.GetScopedUserRoles(team);
            foreach (var scope in scopes)
            {
                if (!scope.EnvironmentIds.Contains(envId))
                {
                    scope.EnvironmentIds.Add(envId);
                    await _octopusHelper.Client.Repository.ScopedUserRoles.Modify(scope, CancellationToken.None);
                }
            }
        }
    }
}