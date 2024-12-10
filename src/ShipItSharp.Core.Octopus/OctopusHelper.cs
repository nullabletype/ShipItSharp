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
using Microsoft.Extensions.Logging;
using Octopus.Client;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus
{
    public class OctopusHelper : IOctopusHelper
    {
        public static IOctopusHelper Default;
        private readonly ChannelRepository _channelsInternal;
        private readonly EnvironmentRepository _environmentsInternal;
        private readonly LifeCycleRepository _lifeCyclesInternal;
        private readonly TeamsRepository _teamsInternal;
        internal readonly IOctopusAsyncClient Client;
        internal readonly DeploymentRepository DeploymentsInternal;
        internal readonly PackageRepository PackagesInternal;
        internal readonly ProjectRepository ProjectsInternal;
        internal readonly ReleaseRepository ReleasesInternal;
        internal readonly VariableRepository VariablesInternal;
        internal ICacheObjects CacheProvider;

        public OctopusHelper(string url, string apiKey, ICacheObjects cacheProvider, ILoggerFactory factory)
        {
            _teamsInternal = new TeamsRepository(this);
            _lifeCyclesInternal = new LifeCycleRepository(this);
            DeploymentsInternal = new DeploymentRepository(this);
            ReleasesInternal = new ReleaseRepository(this);
            _environmentsInternal = new EnvironmentRepository(this);
            _channelsInternal = new ChannelRepository(this);
            VariablesInternal = new VariableRepository(this);
            PackagesInternal = new PackageRepository(this);
            ProjectsInternal = new ProjectRepository(this, factory.CreateLogger<ProjectRepository>());

            CacheProvider = cacheProvider;
            Client = InitClient(url, apiKey);
        }

        public OctopusHelper(IOctopusAsyncClient client, ILoggerFactory factory, ICacheObjects memoryCache = null)
        {
            _teamsInternal = new TeamsRepository(this);
            _lifeCyclesInternal = new LifeCycleRepository(this);
            DeploymentsInternal = new DeploymentRepository(this);
            ReleasesInternal = new ReleaseRepository(this);
            _environmentsInternal = new EnvironmentRepository(this);
            _channelsInternal = new ChannelRepository(this);
            VariablesInternal = new VariableRepository(this);
            PackagesInternal = new PackageRepository(this);
            ProjectsInternal = new ProjectRepository(this, factory.CreateLogger<ProjectRepository>());
            SetCacheImplementationInternal(memoryCache);
            Client = client;
        }

        public IPackageRepository Packages => PackagesInternal;
        public IProjectRepository Projects => ProjectsInternal;
        public IVariableRepository Variables => VariablesInternal;
        public IChannelRepository Channels => _channelsInternal;
        public IEnvironmentRepository Environments => _environmentsInternal;
        public IReleaseRepository Releases => ReleasesInternal;
        public IDeploymentRepository Deployments => DeploymentsInternal;
        public ILifeCycleRepository LifeCycles => _lifeCyclesInternal;
        public ITeamsRepository Teams => _teamsInternal;

        public void SetCacheImplementation(ICacheObjects memoryCacheImp, int cacheTimeoutToSet)
        {
            SetCacheImplementationInternal(CacheProvider);
            CacheProvider.SetCacheTimeout(cacheTimeoutToSet);
        }

        public static IOctopusHelper Init(string url, string apikey, ILoggerFactory factory, ICacheObjects memoryCache = null)
        {
            var client = InitClient(url, apikey);
            Default = new OctopusHelper(client, factory, memoryCache);
            Default.SetCacheImplementation(memoryCache, 1);
            return Default;
        }

        private static IOctopusAsyncClient InitClient(string url, string apikey)
        {
            var endpoint = new OctopusServerEndpoint(url, apikey);
            IOctopusAsyncClient client = null;
            Task.Run(async () => { client = await OctopusAsyncClient.Create(endpoint); }).Wait();
            return client;
        }

        private void SetCacheImplementationInternal(ICacheObjects memoryCache)
        {
            CacheProvider = memoryCache ?? new NoCache();
        }
    }
}