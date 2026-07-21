using System;
using System.Collections.Generic;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.Deployment.Interfaces;

namespace ShipItSharp.Console.ConsoleTools
{
    internal class ConsoleDeploymentQueueInteraction : IDeploymentQueueInteraction
    {
        private readonly Func<string, bool> _confirm;
        private readonly Func<IEnumerable<char>> _readKeys;
        private bool _qPressed;

        public ConsoleDeploymentQueueInteraction()
            : this(ReadAvailableConsoleKeys, prompt => Prompt.GetYesNo(prompt, defaultAnswer: false))
        {
        }

        internal ConsoleDeploymentQueueInteraction(Func<IEnumerable<char>> readKeys, Func<string, bool> confirm)
        {
            _readKeys = readKeys;
            _confirm = confirm;
        }

        public void Reset()
        {
            _qPressed = false;
        }

        public bool QueueJumpRequested()
        {
            try
            {
                var requested = false;
                foreach (var key in _readKeys())
                {
                    requested |= ProcessKey(key);
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
            return _confirm(prompt);
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

        private static IEnumerable<char> ReadAvailableConsoleKeys()
        {
            if (System.Console.IsInputRedirected)
            {
                yield break;
            }

            while (System.Console.KeyAvailable)
            {
                yield return System.Console.ReadKey(intercept: true).KeyChar;
            }
        }
    }
}
