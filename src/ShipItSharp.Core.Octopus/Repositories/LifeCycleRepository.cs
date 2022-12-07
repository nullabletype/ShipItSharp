using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories;

public class LifeCycleRepository : ILifeCycleRepository
{
    private OctopusHelper octopusHelper;

    public LifeCycleRepository(OctopusHelper octopusHelper)
    {
        this.octopusHelper = octopusHelper;
    }

    public async Task<LifeCycle> GetLifeCycle(string idOrHref) 
    {
        return ConvertLifeCycle(await octopusHelper.client.Repository.Lifecycles.Get(idOrHref, CancellationToken.None));
    }

    public async Task RemoveEnvironmentsFromLifecycles(string envId) 
    {
        await octopusHelper.client.Repository.Environments.Get(envId, CancellationToken.None);
        var lifecycles = await octopusHelper.client.Repository.Lifecycles.FindMany(lifecycle => 
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
            await octopusHelper.client.Repository.Lifecycles.Modify(lifecycle, CancellationToken.None);
        }
    }

    public async Task<(bool Success, LifecycleErrorType ErrorType, string Error)> AddEnvironmentToLifecyclePhase(string envId, string lcId, int phaseId, bool automatic) {
        LifecycleResource lifecycle;
        try 
        {
            lifecycle = await octopusHelper.client.Repository.Lifecycles.Get(lcId, CancellationToken.None);
        } 
        catch (Exception e) 
        {
            return (false, LifecycleErrorType.UnexpectedError, e.Message);
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
            await octopusHelper.client.Repository.Lifecycles.Modify(lifecycle, CancellationToken.None);
        } 
        catch (Exception e) 
        {
            return (false, LifecycleErrorType.UnexpectedError, e.Message);
        }
        return (false, LifecycleErrorType.None, string.Empty);
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

                if (newPhase.AutomaticDeploymentTargetEnvironmentIds.Any() ||
                    newPhase.OptionalDeploymentTargetEnvironmentIds.Any())
                {
                    lc.Phases.Add(newPhase);
                }
            }
        }

        return lc;
    }
}

public enum LifecycleErrorType 
{
    LifeCycleNotFound,
    EnvironmentNotFound,
    PhaseInLifeCycleNotFound,
    UnexpectedError,
    None
}