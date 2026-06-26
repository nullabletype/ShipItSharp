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
using System.Threading.Tasks;
using Octopus.Client;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus
{
    public class OctopusHelper : IOctopusHelper
    {
        public static IOctopusHelper Default { get; private set; }
        internal static Func<OctopusServerEndpoint, Task<IOctopusAsyncClient>> ClientCreator { get; set; } = endpoint => OctopusAsyncClient.Create(endpoint);
        private readonly ChannelRepository _channelsInternal;
        private readonly EnvironmentRepository _environmentsInternal;
        private readonly LifeCycleRepository _lifeCyclesInternal;
        private readonly MachineRepository _machinesInternal;
        private readonly TeamsRepository _teamsInternal;
        internal readonly IOctopusAsyncClient Client;
        internal readonly DeploymentRepository DeploymentsInternal;
        internal readonly PackageRepository PackagesInternal;
        internal readonly ProjectRepository ProjectsInternal;
        internal readonly ReleaseRepository ReleasesInternal;
        internal readonly VariableRepository VariablesInternal;
        internal ICacheObjects CacheProvider;

        public OctopusHelper(string url, string apiKey, ICacheObjects cacheProvider)
            : this(CreateClient(url, apiKey).GetAwaiter().GetResult(), cacheProvider) { }

        public OctopusHelper(IOctopusAsyncClient client, ICacheObjects memoryCache = null)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _teamsInternal = new TeamsRepository(this);
            _lifeCyclesInternal = new LifeCycleRepository(this);
            _machinesInternal = new MachineRepository(this);
            DeploymentsInternal = new DeploymentRepository(this);
            ReleasesInternal = new ReleaseRepository(this);
            _environmentsInternal = new EnvironmentRepository(this);
            _channelsInternal = new ChannelRepository(this);
            VariablesInternal = new VariableRepository(this);
            PackagesInternal = new PackageRepository(this);
            ProjectsInternal = new ProjectRepository(this);
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
        public IMachineRepository Machines => _machinesInternal;

        public void SetCacheImplementation(ICacheObjects memoryCacheImp, int cacheTimeoutToSet)
        {
            SetCacheImplementationInternal(memoryCacheImp);
            CacheProvider.SetCacheTimeout(cacheTimeoutToSet);
        }

        public static async Task<IOctopusHelper> CreateAsync(string url, string apikey, ICacheObjects memoryCache = null, int cacheTimeoutSeconds = 1)
        {
            var client = await CreateClient(url, apikey);
            var helper = new OctopusHelper(client, memoryCache);
            helper.SetCacheImplementation(memoryCache, cacheTimeoutSeconds);
            return helper;
        }

        public static async Task<IOctopusHelper> InitAsync(string url, string apikey, ICacheObjects memoryCache = null, int cacheTimeoutSeconds = 1)
        {
            var helper = await CreateAsync(url, apikey, memoryCache, cacheTimeoutSeconds);
            Default = helper;
            return helper;
        }

        public static IOctopusHelper Init(string url, string apikey, ICacheObjects memoryCache = null)
        {
            return InitAsync(url, apikey, memoryCache, 1).GetAwaiter().GetResult();
        }

        private static async Task<IOctopusAsyncClient> CreateClient(string url, string apikey)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Octopus URL must be provided.", nameof(url));
            }
            if (string.IsNullOrWhiteSpace(apikey))
            {
                throw new ArgumentException("Octopus API key must be provided.", nameof(apikey));
            }

            var endpoint = new OctopusServerEndpoint(url, apikey);
            return await ClientCreator(endpoint);
        }

        private void SetCacheImplementationInternal(ICacheObjects memoryCache)
        {
            CacheProvider = memoryCache ?? new NoCache();
        }
    }
}
