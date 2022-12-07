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


using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface ILifeCycleRepository
    {
        Task<LifeCycle> GetLifeCycle(string idOrHref);
        Task RemoveEnvironmentsFromLifecycles(string envId);
        Task<(bool Success, LifecycleErrorType ErrorType, string Error)> AddEnvironmentToLifecyclePhase(string envId, string lcId, int phaseId, bool automatic);
    }
}