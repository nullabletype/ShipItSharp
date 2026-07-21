using System;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.Deployment.Interfaces;

namespace ShipItSharp.Console.ConsoleTools
{
    internal class ConsoleDeploymentQueueInteraction : IDeploymentQueueInteraction
    {
        private bool _qPressed;

        public void Reset()
        {
            _qPressed = false;
        }

        public bool QueueJumpRequested()
        {
            if (System.Console.IsInputRedirected)
            {
                return false;
            }

            try
            {
                var requested = false;
                while (System.Console.KeyAvailable)
                {
                    requested |= ProcessKey(System.Console.ReadKey(intercept: true).KeyChar);
                }

                return requested;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public bool ConfirmQueueJump(string prompt)
        {
            return Prompt.GetYesNo(prompt, defaultAnswer: false);
        }

        internal bool ProcessKey(char key)
        {
            key = char.ToLowerInvariant(key);
            if (_qPressed && key == 'j')
            {
                _qPressed = false;
                return true;
            }

            _qPressed = key == 'q';
            return false;
        }
    }
}
