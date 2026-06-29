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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using ShipItSharp.Core.Octopus.Interfaces;
using Machine = ShipItSharp.Core.Deployment.Models.Machine;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class MachineRepository : IMachineRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public MachineRepository(OctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task<Machine> GetMachine(string idOrName, string environmentId)
        {
            if (string.IsNullOrWhiteSpace(idOrName))
            {
                return null;
            }

            if (IsMachineId(idOrName))
            {
                return new Machine { Id = idOrName, Name = idOrName };
            }

            var root = await _octopusHelper.Client.Repository.LoadRootDocument(CancellationToken.None);
            var machines = await _octopusHelper.Client.List<MachineResource>(
                root.Links["Machines"],
                new { environments = environmentId, partialName = idOrName, take = 100 },
                CancellationToken.None);

            var machine = machines.Items
                .FirstOrDefault(m => m.Name.Equals(idOrName, StringComparison.CurrentCultureIgnoreCase));

            return machine == null ? null : ConvertMachine(machine);
        }

        public async Task<bool> DisableMachine(string idOrName, string environmentId)
        {
            return await SetMachineDisabled(idOrName, environmentId, true);
        }

        public async Task<int> DisableMachines(string environmentId)
        {
            return await SetMachinesDisabled(environmentId, true);
        }

        public async Task<bool> EnableMachine(string idOrName, string environmentId)
        {
            return await SetMachineDisabled(idOrName, environmentId, false);
        }

        public async Task<int> EnableMachines(string environmentId)
        {
            return await SetMachinesDisabled(environmentId, false);
        }

        private async Task<bool> SetMachineDisabled(string idOrName, string environmentId, bool disabled)
        {
            var machine = await GetMachineResource(idOrName, environmentId);
            if (machine == null)
            {
                return false;
            }

            machine.IsDisabled = disabled;
            await _octopusHelper.Client.Repository.Machines.Modify(machine, CancellationToken.None);
            return true;
        }

        private async Task<int> SetMachinesDisabled(string environmentId, bool disabled)
        {
            var machines = await GetMachineResources(environmentId);
            var changed = 0;
            foreach (var machine in machines)
            {
                machine.IsDisabled = disabled;
                await _octopusHelper.Client.Repository.Machines.Modify(machine, CancellationToken.None);
                changed++;
            }

            return changed;
        }

        private async Task<MachineResource> GetMachineResource(string idOrName, string environmentId)
        {
            if (string.IsNullOrWhiteSpace(idOrName) || string.IsNullOrWhiteSpace(environmentId))
            {
                return null;
            }

            if (IsMachineId(idOrName))
            {
                try
                {
                    var machine = await _octopusHelper.Client.Repository.Machines.Get(idOrName, CancellationToken.None);
                    return machine.EnvironmentIds.Contains(environmentId) ? machine : null;
                }
                catch (OctopusResourceNotFoundException)
                {
                    return null;
                }
            }

            var machines = await GetMachineResources(environmentId, idOrName);

            return machines
                .FirstOrDefault(m => m.Name.Equals(idOrName, StringComparison.CurrentCultureIgnoreCase));
        }

        private async Task<MachineResource[]> GetMachineResources(string environmentId, string partialName = null)
        {
            const int pageSize = 100;
            var skip = 0;
            var results = new List<MachineResource>();
            var root = await _octopusHelper.Client.Repository.LoadRootDocument(CancellationToken.None);
            MachineResource[] page;
            do
            {
                var machines = await _octopusHelper.Client.List<MachineResource>(
                    root.Links["Machines"],
                    new { environments = environmentId, partialName, skip, take = pageSize },
                    CancellationToken.None);

                page = machines.Items.ToArray();
                results.AddRange(page);
                skip += pageSize;
            } while (page.Length == pageSize);

            return results.ToArray();
        }

        private static Machine ConvertMachine(MachineResource machine)
        {
            return new Machine { Id = machine.Id, Name = machine.Name };
        }

        private static bool IsMachineId(string idOrName)
        {
            return idOrName.StartsWith("Machines-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
