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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ShipItSharp.Core.Logging;
using ShipItSharp.Core.Logging.Interfaces;

namespace ShipItSharp.Core.VersionChecking.GitLab
{
    public class GitLabVersionChecker : IVersionCheckingProvider
    {
        private readonly ILogger _log;

        public GitLabVersionChecker()
        {
            _log = LoggingProvider.GetLogger<GitLabVersionChecker>();
        }

        public async Task<IRelease> GetLatestRelease()
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("user-agent", "ShipItSharp");
                var response = await client.GetStringAsync("https://gitlab.com/api/v4/projects/10756071/releases");
                var release = JsonConvert.DeserializeObject<List<Release>>(response).First();
                return release;
            }
            catch (Exception e)
            {
                _log.Error("Couldn't load the latest version information from github", e);
            }
            return null;
        }
    }
}