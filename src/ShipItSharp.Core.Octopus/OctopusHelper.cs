#region copyright
/*
    ShipItSharp Deployment Coordinator. Provides extra tooling to help 
    deploy software through Octopus Deploy.

    Copyright (C) 2018  Steven Davies

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion


using System.Threading.Tasks;
using Octopus.Client;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus
{
    public class OctopusHelper : IOctopusHelper 
    {
        internal IOctopusAsyncClient client;
        public static IOctopusHelper Default;
        internal ICacheObjects cacheProvider;
        internal readonly ProjectRepository ProjectsInternal;
        internal readonly PackageRepository PackagesInternal;
        internal readonly VariableRepository VariablesInternal;
        internal readonly ReleaseRepository ReleasesInternal;
        internal readonly DeploymentRepository DeploymentsInternal;
        private readonly LifeCycleRepository LifeCyclesInternal;
        private readonly TeamsRepository TeamsInternal;
        private readonly ChannelRepository ChannelsInternal;
        private readonly EnvironmentRepository EnvironmentsInternal;

        public IPackageRepository Packages => PackagesInternal;
        public IProjectRepository Projects => ProjectsInternal;
        public IVariableRepository Variables => VariablesInternal;
        public IChannelRepository Channels => ChannelsInternal;
        public IEnvironmentRepository Environments => EnvironmentsInternal;
        public IReleaseRepository Releases => ReleasesInternal;
        public IDeploymentRepository Deployments => DeploymentsInternal;
        public ILifeCycleRepository LifeCycles => LifeCyclesInternal;
        public ITeamsRepository Teams => TeamsInternal;
        
        public OctopusHelper(string url, string apiKey, ICacheObjects cacheProvider) 
        {
            TeamsInternal = new TeamsRepository(this);
            LifeCyclesInternal = new LifeCycleRepository(this);
            DeploymentsInternal = new DeploymentRepository(this);
            ReleasesInternal = new ReleaseRepository(this);
            EnvironmentsInternal = new EnvironmentRepository(this);
            ChannelsInternal = new ChannelRepository(this);
            VariablesInternal = new VariableRepository(this);
            PackagesInternal = new PackageRepository(this);
            ProjectsInternal = new ProjectRepository(this);
            
            this.cacheProvider = cacheProvider;
            this.client = InitClient(url, apiKey);
        }

        public OctopusHelper(IOctopusAsyncClient client, ICacheObjects memoryCache = null)
        {
            TeamsInternal = new TeamsRepository(this);
            LifeCyclesInternal = new LifeCycleRepository(this);
            DeploymentsInternal = new DeploymentRepository(this);
            ReleasesInternal = new ReleaseRepository(this);
            EnvironmentsInternal = new EnvironmentRepository(this);
            ChannelsInternal = new ChannelRepository(this);
            VariablesInternal = new VariableRepository(this);
            PackagesInternal = new PackageRepository(this);
            ProjectsInternal = new ProjectRepository(this);
            SetCacheImplementationInternal(memoryCache);
            this.client = client;
        }

        public static IOctopusHelper Init(string url, string apikey, ICacheObjects memoryCache = null) {
            var client = InitClient(url, apikey);
            Default = new OctopusHelper(client, memoryCache);
            Default.SetCacheImplementation(memoryCache, 1);
            return Default;
        }

        private static IOctopusAsyncClient InitClient(string url, string apikey) {
            var endpoint = new OctopusServerEndpoint(url, apikey);
            IOctopusAsyncClient client = null;
            Task.Run(async () => { client = await OctopusAsyncClient.Create(endpoint); }).Wait();
            return client;
        }

        public void SetCacheImplementation(ICacheObjects memoryCacheImp, int cacheTimeoutToSet)
        {
            SetCacheImplementationInternal(cacheProvider);
            cacheProvider.SetCacheTimeout(cacheTimeoutToSet);
        }
        
        private void SetCacheImplementationInternal(ICacheObjects memoryCache)
        {
            this.cacheProvider = memoryCache ?? new NoCache();
        }
    }
}