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
using System.Reflection;
using System.Threading.Tasks;

namespace ShipItSharp.Core.VersionChecking
{
    public class VersionChecker : IVersionChecker
    {
        private readonly IVersionCheckingProvider _provider;

        public VersionChecker(IVersionCheckingProvider provider)
        {
            _provider = provider;
        }

        public async Task<VersionCheckResult> GetLatestVersion()
        {
            var latestVersion = await _provider.GetLatestRelease();
            if (latestVersion == null)
            {
                return new VersionCheckResult();
            }
            var currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            latestVersion.CurrentVersion = currentVersion.ToString().TrimEnd(".0".ToCharArray());
            var latestTagVersion = new Version(latestVersion.TagName.Split('-')[0]);
            if (currentVersion.CompareTo(latestTagVersion) < 0)
            {
                return new VersionCheckResult
                {
                    NewVersion = true,
                    Release = latestVersion
                };
            }
            return new VersionCheckResult();
        }
    }

    public class VersionCheckResult
    {
        public IRelease Release { get; set; }
        public bool NewVersion { get; set; }
    }
}