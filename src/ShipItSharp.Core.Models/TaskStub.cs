﻿#region copyright
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

namespace ShipItSharp.Core.Deployment.Models
{
    public class TaskStub
    {
        public TaskStatus State { get; set; }
        public string TaskId { get; set; }
        public bool IsComplete { get; set; }
        public bool FinishedSuccessfully { get; set; }
        public bool HasWarningsOrErrors { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Links { get; set; }
        public string DeploymentId { get; set; }
    }
}