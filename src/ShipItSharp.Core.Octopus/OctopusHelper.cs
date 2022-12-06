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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Octopus.Repositories;
using Environment = ShipItSharp.Core.Models.Environment;

namespace ShipItSharp.Core.Octopus
{
    public class OctopusHelper : IOctopusHelper 
    {
        internal IOctopusAsyncClient client;
        public static IOctopusHelper Default;
        internal ICacheObjects memoryCache;
        internal readonly ProjectRepository ProjectsInternal;
        internal readonly PackageRepository PackagesInternal;
        internal readonly VariableRepository VariablesInternal;
        
        public IPackageRepository Packages => PackagesInternal;
        public IProjectRepository Projects => ProjectsInternal;
        public IVariableRepository Variables => VariablesInternal;

        public OctopusHelper(string url, string apiKey, ICacheObjects memoryCache) 
        {
            VariablesInternal = new VariableRepository(this);
            PackagesInternal = new PackageRepository(this);
            ProjectsInternal = new ProjectRepository(this);
            
            this.memoryCache = memoryCache;
            this.client = InitClient(url, apiKey);
        }

        public OctopusHelper(IOctopusAsyncClient client, ICacheObjects memoryCache = null)
        {
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
            SetCacheImplementationInternal(memoryCache);
            memoryCache.SetCacheTimeout(cacheTimeoutToSet);
        }
        
        private void SetCacheImplementationInternal(ICacheObjects memoryCache)
        {
            this.memoryCache = memoryCache ?? new NoCache();
        }

        public async Task<(Release Release, Deployment Deployment)> GetReleasedVersion(string projectId, string envId) 
        {
            var deployment =
                (await client.Repository.Deployments.FindOne(search:resource => Search(resource, projectId, envId), pathParameters: new { take = 1, projects = projectId, environments = envId }, cancellationToken:CancellationToken.None, path:null));
            if (deployment != null) 
            {
                var release = await GetReleaseInternal(deployment.ReleaseId);
                if (release != null)
                {
                    return (Release: await this.ConvertRelease(release), Deployment: this.ConvertDeployment(deployment));
                }
            }
            return (new Release { Id = "", Version = "None" }, new Deployment());
        }

        public async Task<bool> UpdateReleaseVariables(string releaseId)
        {
            var release = await this.client.Repository.Releases.Get(releaseId, CancellationToken.None);
            if(release == null)
            {
                return false;
            }
            await this.client.Repository.Releases.SnapshotVariables(release, CancellationToken.None);
            return true;
        }

        public async Task<List<Environment>> GetEnvironments() 
        {
            var envs = await client.Repository.Environments.GetAll(CancellationToken.None);
            return envs.Select(ConvertEnvironment).ToList();
        }

        public async Task<List<Environment>> GetMatchingEnvironments(string keyword, bool extactMatch = false)
        {
            var environments = await GetEnvironments();
            var matchingEnvironments = environments.Where(env => env.Name.Equals(keyword, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            if (matchingEnvironments.Length == 0 && !extactMatch)
            {
                matchingEnvironments = environments.Where(env => env.Name.ToLower().Contains(keyword.ToLower())).ToArray();
            }
            return matchingEnvironments.ToList();
        }

        public async Task<Environment> CreateEnvironment(string name, string description) 
        {
            var env = new EnvironmentResource {
                Name = name,
                Description = description
            };
            env = await client.Repository.Environments.Create(env, CancellationToken.None);
            
            return ConvertEnvironment(env);
        }

        public async Task<Environment> GetEnvironment(string idOrName) 
        {
            return ConvertEnvironment(await client.Repository.Environments.Get(idOrName, CancellationToken.None));
        }

        public async Task<IEnumerable<Environment>> GetEnvironments(string[] idOrNames)
        {
            var environments = new List<Environment>();
            foreach (var envId in idOrNames.Distinct())
            {
                var env = await client.Repository.Environments.Get(envId, CancellationToken.None);
                environments.Add(ConvertEnvironment(env));
            }
            return environments;
        }

        public async Task DeleteEnvironment(string idOrhref) 
        {
            var env = await client.Repository.Environments.Get(idOrhref, CancellationToken.None);
            await client.Repository.Environments.Delete(env, CancellationToken.None);
        }

        public async Task<List<ProjectGroup>> GetFilteredProjectGroups(string filter) 
        {
            var groups = await client.Repository.ProjectGroups.GetAll(CancellationToken.None);
            return groups.Where(g => g.Name.ToLower().Contains(filter.ToLower())).Select(ConvertProjectGroup).ToList();
        }

        public async Task<Channel> GetChannelByProjectNameAndChannelName(string name, string channelName) 
        {
            var project = await client.Repository.Projects.FindOne(resource => resource.Name == name, CancellationToken.None);
            return ConvertChannel(await client.Repository.Channels.FindByName(project, channelName));
        }

        public async Task<Channel> GetChannelByName(string projectIdOrName, string channelName) 
        {
            var project = await ProjectsInternal.GetProject(projectIdOrName);
            return ConvertChannel(await client.Repository.Channels.FindByName(project, channelName));
        }

        public async Task<List<Channel>> GetChannelsForProject(string projectIdOrHref, int take = 30) 
        {
            var project = await ProjectsInternal.GetProject(projectIdOrHref);
            var channels = await client.List<ChannelResource>(project.Link("Channels"), new { take = 9999 }, CancellationToken.None);
            return channels.Items.Select(ConvertChannel).ToList();
        }

        public async Task<(bool Success, IEnumerable<Release> Releases)> RemoveChannel(string channelId)
        {
            var channel = await client.Repository.Channels.Get(channelId, CancellationToken.None);
            var allReleases = await client.Repository.Channels.GetAllReleases(channel);
            if (!allReleases.Any())
            {
                await client.Repository.Channels.Delete(channel, CancellationToken.None);
                return (true, null);
            }

            return (false, allReleases.Select(async r => await ConvertRelease(r)).Select(t => t.Result));
        }

        public async Task<Release> GetRelease(string releaseIdOrHref) 
        {
            return await ConvertRelease(await GetReleaseInternal(releaseIdOrHref));
        }

        public async Task<Release> GetRelease(string name, Project project)
        {
            var projectRes = await client.Repository.Projects.Get(project.ProjectId, CancellationToken.None);
            ReleaseResource release;
            try
            {
                release = await client.Repository.Projects.GetReleaseByVersion(projectRes, name, CancellationToken.None);
            }
            catch
            {
                return null;
            }
            if (release == null)
            {
                return null;
            }
            return await ConvertRelease(release);
        }

        public async Task<Release> GetLatestRelease(Project project, string channelName)
        {
            ReleaseResource release;
            try
            {
                var projectRes = await client.Repository.Projects.Get(project.ProjectId, CancellationToken.None);
                var channelRes = await client.Repository.Channels.FindByName(projectRes, channelName);
                if (channelRes == null)
                {
                    return null;
                }
                release = (await client.Repository.Channels.GetReleases(channelRes, 0, 1)).Items.FirstOrDefault();
            }
            catch
            {
                return null;
            }
            if (release == null)
            {
                return null;
            }
            return await ConvertRelease(release);
        }

        public async Task<LifeCycle> GetLifeCycle(string idOrHref) 
        {
            return ConvertLifeCycle(await client.Repository.Lifecycles.Get(idOrHref, CancellationToken.None));
        }

        public async Task<Release> CreateRelease(ProjectDeployment project, bool ignoreChannelRules = false) 
        {
            var user = await client.Repository.Users.GetCurrent();
            var split = project.Packages.First().PackageName.Split('.');
            var releaseName = project.ReleaseVersion ?? split[0] + "." + split[1] + ".i";
            if (project.ReleaseVersion == null && !string.IsNullOrEmpty(project.ChannelName)) 
            {
                releaseName += "-" + project.ChannelName;
            }
            var release = new ReleaseResource
            {
                Assembled = DateTimeOffset.UtcNow,
                ChannelId = project.ChannelId,
                LastModifiedBy = user.Username,
                LastModifiedOn = DateTimeOffset.UtcNow,
                ProjectId = project.ProjectId,
                ReleaseNotes = project.ReleaseMessage ?? string.Empty,
                Version = releaseName,
            };
            foreach (var package in project.Packages)
            {
                release.SelectedPackages.Add(new SelectedPackage { Version = package.PackageName, ActionName = package.StepName });
            }
            var result =
                    await
                        client.Repository.Releases.Create(release, ignoreChannelRules: ignoreChannelRules, CancellationToken.None);
            return new Release {
                Version = result.Version,
                Id = result.Id,
                ReleaseNotes = result.ReleaseNotes
            };
        }

        public async Task<Deployment> CreateDeploymentTask(ProjectDeployment project, string environmentId, string releaseId) 
        {
            var user = await client.Repository.Users.GetCurrent();
            var deployment = new DeploymentResource
            {
                ChannelId = project.ChannelId,
                Comments = "Initiated by ShipItSharp",
                Created = DateTimeOffset.UtcNow,
                EnvironmentId = environmentId,
                LastModifiedBy = user.Username,
                LastModifiedOn = DateTimeOffset.UtcNow,
                Name = project.ProjectName + ":" + project.Packages?.First().PackageName,
                ProjectId = project.ProjectId,
                ReleaseId = releaseId,
            };
            if (project.RequiredVariables != null)
            {
                foreach (var variable in project.RequiredVariables)
                {
                    deployment.FormValues.Add(variable.Id, variable.Value);
                }
            }
            var deployResult = await client.Repository.Deployments.Create(deployment, CancellationToken.None);
            return new Deployment {
                TaskId = deployResult.TaskId
            };
        }

        public async Task<TaskDetails> GetTaskDetails(string taskId) 
        {
            var task = await client.Repository.Tasks.Get(taskId, CancellationToken.None);
            var taskDeets = await client.Repository.Tasks.GetDetails(task);

            return new TaskDetails 
            {
                PercentageComplete = taskDeets.Progress.ProgressPercentage,
                TimeLeft = taskDeets.Progress.EstimatedTimeRemaining,
                State = taskDeets.Task.State == TaskState.Success ? Models.TaskStatus.Done :
                    taskDeets.Task.State == TaskState.Executing ? Models.TaskStatus.InProgress :
                    taskDeets.Task.State == TaskState.Queued ? Models.TaskStatus.Queued : Models.TaskStatus.Failed,
                TaskId = taskId,
                Links = taskDeets.Links.ToDictionary(l => l.Key, l => l.Value.ToString())
            };
        }

        public async Task<IEnumerable<TaskStub>> GetDeploymentTasks(int skip, int take) 
        {
            //var taskDeets = await client.Repository.Tasks.FindAll(pathParameters: new { skip, take, name = "Deploy" });

            var taskDeets = await client.Get<ResourceCollection<TaskResource>>((await client.Repository.LoadRootDocument(CancellationToken.None)).Links["Tasks"], new { skip, take, name = "Deploy" }, CancellationToken.None);

            var tasks = new List<TaskStub>();

            foreach (var currentTask in taskDeets.Items) 
            {
                String deploymentId = null;
                if(currentTask.Arguments != null && currentTask.Arguments.ContainsKey("DeploymentId"))
                {
                    deploymentId = currentTask.Arguments["DeploymentId"].ToString();
                }
                tasks.Add(new TaskStub 
                {
                    State = currentTask.State == TaskState.Success ? Models.TaskStatus.Done :
                        currentTask.State == TaskState.Executing ? Models.TaskStatus.InProgress :
                        currentTask.State == TaskState.Queued ? Models.TaskStatus.Queued : Models.TaskStatus.Failed,
                    ErrorMessage = currentTask.ErrorMessage,
                    FinishedSuccessfully = currentTask.FinishedSuccessfully,
                    HasWarningsOrErrors = currentTask.HasWarningsOrErrors,
                    IsComplete = currentTask.IsCompleted,
                    TaskId = currentTask.Id,
                    Links = currentTask.Links.ToDictionary(l => l.Key, l => l.Value.ToString()),
                    DeploymentId = deploymentId
                });
            }

            return tasks;
        }

        public async Task<IEnumerable<Deployment>> GetDeployments(string[] deploymentIds)
        {
            var deployments = new List<Deployment>();
            foreach (var deploymentId in deploymentIds.Distinct())
            {
                var deployment = await client.Repository.Deployments.Get(deploymentId, CancellationToken.None);
                deployments.Add(ConvertDeployment(deployment));
            }

            return deployments;
        }

        public async Task RemoveEnvironmentsFromTeams(string envId) 
        {
            var teams = await client.Repository.Teams.FindAll(CancellationToken.None);
            foreach (var team in teams) 
            {
                var scopes = await client.Repository.Teams.GetScopedUserRoles(team);
                
                foreach (var scope in scopes.Where(s => s.EnvironmentIds.Contains(envId))) 
                {
                    scope.EnvironmentIds.Remove(envId);
                    await client.Repository.ScopedUserRoles.Modify(scope, CancellationToken.None);
                }
                
            }
        }


        public async Task RemoveEnvironmentsFromLifecycles(string envId) 
        {
            await client.Repository.Environments.Get(envId, CancellationToken.None);
            var lifecycles = await client.Repository.Lifecycles.FindMany(lifecycle => 
                { 
                    return lifecycle.Phases.Any(phase => 
                        {
                            if (phase.AutomaticDeploymentTargets != null && phase.AutomaticDeploymentTargets.Contains(envId))
                            {
                                return true;
                            }
                            if (phase.OptionalDeploymentTargets != null && phase.OptionalDeploymentTargets.Contains(envId))
                            {
                                return true;
                            }
                            return false;
                        }
                    ); 
                }, CancellationToken.None);
            foreach(var lifecycle in lifecycles) 
            {
                foreach (var phase in lifecycle.Phases) 
                {
                    if (phase.AutomaticDeploymentTargets != null)
                    {
                        phase.AutomaticDeploymentTargets.RemoveWhere(phaseEnvId => phaseEnvId.Equals(envId));
                    }
                    if (phase.OptionalDeploymentTargets != null)
                    {
                        phase.OptionalDeploymentTargets.RemoveWhere(phaseEnvId => phaseEnvId.Equals(envId));
                    }
                }
                await client.Repository.Lifecycles.Modify(lifecycle, CancellationToken.None);
            }
        }

        public async Task AddEnvironmentToTeam(string envId, string teamId) 
        {
            var team = await client.Repository.Teams.Get(teamId, CancellationToken.None);
            var environment = await client.Repository.Environments.Get(envId, CancellationToken.None);
            if (team == null || environment == null) 
            {
                return;
            }
            var scopes = await client.Repository.Teams.GetScopedUserRoles(team);
            foreach (var scope in scopes) 
            {
                if (!scope.EnvironmentIds.Contains(envId))
                {
                    scope.EnvironmentIds.Add(envId);
                    await client.Repository.ScopedUserRoles.Modify(scope, CancellationToken.None);
                }
            }
        }


        public async Task<(bool Success, LifecycleErrorType ErrorType, string Error)> AddEnvironmentToLifecyclePhase(string envId, string lcId, int phaseId, bool automatic) {
            LifecycleResource lifecycle;
            try 
            {
                lifecycle = await client.Repository.Lifecycles.Get(lcId, CancellationToken.None);
            } 
            catch (Exception e) 
            {
                return (false,LifecycleErrorType.UnexpectedError, e.Message);
            }
            if (lifecycle.Phases.Count < phaseId) 
            {
                return (false, LifecycleErrorType.PhaseInLifeCycleNotFound, string.Empty);
            }
            if (automatic) 
            {
                if (!lifecycle.Phases[phaseId].AutomaticDeploymentTargets.Contains(envId)) 
                {
                    lifecycle.Phases[phaseId].AutomaticDeploymentTargets.Add(envId);
                }
            } 
            else 
            {
                if (!lifecycle.Phases[phaseId].OptionalDeploymentTargets.Contains(envId)) 
                {
                    lifecycle.Phases[phaseId].OptionalDeploymentTargets.Add(envId);
                }
            }
            try 
            {
                await client.Repository.Lifecycles.Modify(lifecycle, CancellationToken.None);
            } 
            catch (Exception e) 
            {
                return (false, LifecycleErrorType.UnexpectedError, e.Message);
            }
            return (false, LifecycleErrorType.None, string.Empty);
        }

        public async Task<string> GetTaskRawLog(string taskId) 
        {
            var task = await client.Repository.Tasks.Get(taskId, CancellationToken.None);
            return await client.Repository.Tasks.GetRawOutputLog(task);
        }

        public async Task<IEnumerable<Deployment>> GetDeployments(string releaseId)
        {
            if(string.IsNullOrEmpty(releaseId))
            {
                return Array.Empty<Deployment>();
            }
            var deployments = await client.Repository.Releases.GetDeployments(await GetReleaseInternal(releaseId), 0, 100, CancellationToken.None);
            return deployments.Items.ToList().Select(ConvertDeployment);
        }

        public bool Search(DeploymentResource deploymentResource, string projectId, string envId)
        {
            return deploymentResource.ProjectId == projectId && deploymentResource.EnvironmentId == envId;
        }

        public async Task<(string error, bool success)> RenameRelease(string releaseId, string newReleaseVersion)
        {
            try
            {
                var release = await GetReleaseInternal(releaseId);
                release.Version = newReleaseVersion;
                await client.Repository.Releases.Modify(release, CancellationToken.None);
            }
            catch (Exception e)
            {
                return (e.Message, false);
            }

            return (string.Empty, true);
        }
        
        private Environment ConvertEnvironment(EnvironmentResource env)
        {
            return new Environment {Id = env.Id, Name = env.Name};
        }

        private Deployment ConvertDeployment(DeploymentResource dep)
        {
            return new Deployment
            {
                EnvironmentId = dep.EnvironmentId,
                ReleaseId = dep.ReleaseId,
                TaskId = dep.TaskId,
                LastModifiedBy = dep.LastModifiedBy,
                LastModifiedOn = dep.LastModifiedOn,
                Created = dep.Created
            };
        }

        private LifeCycle ConvertLifeCycle(LifecycleResource lifeCycle)
        {
            var lc = new LifeCycle
            {
                Name = lifeCycle.Name,
                Id = lifeCycle.Id,
                Description = lifeCycle.Description
            };
            if (lifeCycle.Phases != null)
            {
                foreach (var phase in lifeCycle.Phases)
                {
                    
                    var newPhase = new Phase
                    {
                        Name = phase.Name,
                        Id = phase.Id,
                        MinimumEnvironmentsBeforePromotion = phase.MinimumEnvironmentsBeforePromotion,
                        Optional = phase.IsOptionalPhase
                    };
                    if (phase.OptionalDeploymentTargets != null)
                    {
                        newPhase.OptionalDeploymentTargetEnvironmentIds = phase.OptionalDeploymentTargets.ToList();
                    }
                    if (phase.AutomaticDeploymentTargets != null)
                    {
                        newPhase.AutomaticDeploymentTargetEnvironmentIds = phase.AutomaticDeploymentTargets.ToList();
                    }
                    if (newPhase.AutomaticDeploymentTargetEnvironmentIds.Any() || newPhase.OptionalDeploymentTargetEnvironmentIds.Any())
                    {
                        lc.Phases.Add(newPhase);
                    }
                }
            }
            return lc;
        }

        private Channel ConvertChannel(ChannelResource channel)
        {
            if (channel == null)
            {
                return null;
            }
            var versionRange = String.Empty;
            var versionTag = String.Empty;
            if (channel.Rules.Any())
            {
                versionRange = channel.Rules[0].VersionRange;
                versionTag = channel.Rules[0].Tag;
            }
            return new Channel {Id = channel.Id, Name = channel.Name, VersionRange = versionRange, VersionTag = versionTag};
        }

        private ProjectGroup ConvertProjectGroup(ProjectGroupResource projectGroup)
        {
            return new ProjectGroup() {Id = projectGroup.Id, Name = projectGroup.Name};
        }

        private async Task<Release> ConvertRelease(ReleaseResource release)
        {
            var project = await ProjectsInternal.GetProject(release.ProjectId);
            var packages =
                release.SelectedPackages.Select(
                    async p => await PackagesInternal.ConvertPackage(project, p)).Select(t => t.Result).ToList();
            return new Release
            {
                Id = release.Id,
                ProjectId = release.ProjectId,
                Version = release.Version,
                SelectedPackages = packages,
                DisplayPackageVersion = packages.Any() ? packages.First().Version : string.Empty,
                LastModifiedBy = release.LastModifiedBy,
                LastModifiedOn = release.LastModifiedOn,
                ReleaseNotes = release.ReleaseNotes
            };
        }

        internal async Task<DeploymentProcessResource> GetDeploymentProcess(string deploymentProcessId)
        {
            var cached = memoryCache.GetCachedObject<DeploymentProcessResource>(deploymentProcessId);
            if (cached == default(DeploymentProcessResource))
            {
                var deployment = await client.Repository.DeploymentProcesses.Get(deploymentProcessId, CancellationToken.None);
                memoryCache.CacheObject(deployment.Id, deployment);
                return deployment;
            }
            return cached;
        }

        private async Task<ReleaseResource> GetReleaseInternal(string releaseId)
        {
            var cached = memoryCache.GetCachedObject<ReleaseResource>(releaseId);
            if (cached == default(ReleaseResource))
            {
                var release = await client.Repository.Releases.Get(releaseId, CancellationToken.None);
                memoryCache.CacheObject(release.Id, release);
                return release;
            }
            return cached;
        }

        public enum LifecycleErrorType 
        {
            LifeCycleNotFound,
            EnvironmentNotFound,
            PhaseInLifeCycleNotFound,
            UnexpectedError,
            None
        }
    }
}