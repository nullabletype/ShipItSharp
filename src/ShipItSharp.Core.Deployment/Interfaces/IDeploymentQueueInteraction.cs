namespace ShipItSharp.Core.Deployment.Interfaces
{
    public interface IDeploymentQueueInteraction
    {
        void Reset();
        bool QueueJumpRequested();
        bool ConfirmQueueJump(string prompt);
    }
}
