namespace ShipItSharp.Core.VersionChecking
{
    public interface IAsset
    {
        string DownloadUrl { get; set; }
        string Name { get; set; }
    }
}