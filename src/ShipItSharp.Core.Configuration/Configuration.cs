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
using System.Linq;
using Newtonsoft.Json;
using ShipItSharp.Core.Configuration.Interfaces;

namespace ShipItSharp.Core.Configuration
{
    internal class Configuration : IConfiguration
    {
        public string ProjectGroupFilterString { get; set; }
        public string ApiKey { get; set; }
        public string OctopusUrl { get; set; }
        public ChangeLogProviderConfiguration ChangeProviderConfiguration { get; set; }
        public bool EnableTrace { get; set; }
        public int CacheTimeoutInSeconds { get; set; }
        public string DefaultChannel { get; set; }
    }
}