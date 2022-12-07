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


using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShipItSharp.Core.Interfaces;

namespace ShipItSharp.Console.ConsoleTools
{
    internal class ConsoleProgressBar : IProgressBar
    {
        private readonly string[] _clocks = { "\\", "|", "/", "-" };
        private CancellationTokenSource _cancelToken;
        private int _currentItem;
        private string _currentMessage = string.Empty;
        private bool _spinning;

        private int _status;
        private int _totalItems;

        public void WriteStatusLine(string status)
        {
            var builder = new StringBuilder(status);
            while (builder.Length < System.Console.BufferWidth)
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(status);
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
        }

        public void CleanCurrentLine()
        {
            if ((_cancelToken != null) && _spinning)
            {
                _cancelToken.Cancel();
            }

            var builder = new StringBuilder("\r");
            while (builder.Length < System.Console.BufferWidth)
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(builder.ToString());
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
        }

        public void StopAnimation()
        {
            if ((_cancelToken != null) && !_cancelToken.IsCancellationRequested)
            {
                _cancelToken.Cancel();
            }
        }

        public void WriteProgress(int current, int total, string message)
        {
            _currentMessage = message;
            _totalItems = total;
            _currentItem = current;

            if (_spinning)
            {
                return;
            }

            _spinning = true;
            _cancelToken = new CancellationTokenSource();
            _ = Task.Run(() => StartStatusThread(_cancelToken.Token)).ConfigureAwait(false);
        }

        private async Task StartStatusThread(CancellationToken token)
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    var builder = new StringBuilder("\r[");
                    for (var i = 1; i <= _currentItem; i++)
                    {
                        builder.Append("█");
                    }

                    for (var i = _currentItem + 1; i <= _totalItems; i++)
                    {
                        builder.Append("·");
                    }

                    builder.Append("]");
                    if (!string.IsNullOrEmpty(_currentMessage))
                    {
                        builder.Append($" {_clocks[_status]} {_currentMessage}");
                    }

                    while (builder.Length < System.Console.BufferWidth)
                    {
                        builder.Append(" ");
                    }

                    if (token.IsCancellationRequested)
                    {
                        token.ThrowIfCancellationRequested();
                        return;
                    }

                    System.Console.SetCursorPosition(0, System.Console.CursorTop);
                    System.Console.Write(builder.ToString());
                    System.Console.SetCursorPosition(0, System.Console.CursorTop);

                    _status++;

                    if (_status > _clocks.Length - 1)
                    {
                        _status = 0;
                    }

                    Thread.Sleep(500);
                }
            }, token);
        }
    }
}