using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Octopus.Interfaces;
using Environment = ShipItSharp.Core.Deployment.Models.Environment;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class EnvironmentRepository : IEnvironmentRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public EnvironmentRepository(OctopusHelper octopusHelper)
        {
            this._octopusHelper = octopusHelper;
        }

        public async Task<List<Environment>> GetEnvironments()
        {
            var envs = await _octopusHelper.Client.Repository.Environments.GetAll(CancellationToken.None);
            return envs.Select(ConvertEnvironment).ToList();
        }

        public async Task<List<Environment>> GetMatchingEnvironments(string keyword, bool extactMatch = false)
        {
            var environments = await GetEnvironments();
            var matchingEnvironments = environments.Where(env => env.Name.Equals(keyword, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            if ((matchingEnvironments.Length == 0) && !extactMatch)
            {
                matchingEnvironments = environments.Where(env => env.Name.ToLower().Contains(keyword.ToLower())).ToArray();
            }
            return matchingEnvironments.ToList();
        }

        public async Task<Environment> CreateEnvironment(string name, string description)
        {
            var env = new EnvironmentResource
            {
                Name = name,
                Description = description
            };
            env = await _octopusHelper.Client.Repository.Environments.Create(env, CancellationToken.None);

            return ConvertEnvironment(env);
        }

        public async Task<Environment> GetEnvironment(string idOrName)
        {
            return ConvertEnvironment(await _octopusHelper.Client.Repository.Environments.Get(idOrName, CancellationToken.None));
        }

        public async Task<IEnumerable<Environment>> GetEnvironments(string[] idOrNames)
        {
            var environments = new List<Environment>();
            foreach (var envId in idOrNames.Distinct())
            {
                var env = await _octopusHelper.Client.Repository.Environments.Get(envId, CancellationToken.None);
                environments.Add(ConvertEnvironment(env));
            }
            return environments;
        }

        public async Task DeleteEnvironment(string idOrhref)
        {
            var env = await _octopusHelper.Client.Repository.Environments.Get(idOrhref, CancellationToken.None);
            await _octopusHelper.Client.Repository.Environments.Delete(env, CancellationToken.None);
        }

        private Environment ConvertEnvironment(EnvironmentResource env)
        {
            return new Environment { Id = env.Id, Name = env.Name };
        }
    }
}