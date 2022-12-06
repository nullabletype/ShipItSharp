using System.Collections.Generic;
using System.Threading.Tasks;
using ShipItSharp.Core.Models;

namespace ShipItSharp.Core.Octopus.Interfaces;

public interface IPackageRepository
{
    Task<IList<PackageStep>> GetPackages(string projectIdOrHref, string versionRange, string tag, int take = 5);
    Task<PackageFull> GetFullPackage(PackageStub stub);
}