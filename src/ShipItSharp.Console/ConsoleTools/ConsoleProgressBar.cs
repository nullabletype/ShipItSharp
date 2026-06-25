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


using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShipItSharp.Core.Interfaces;

namespace ShipItSharp.Console.ConsoleTools
{
    internal class ConsoleProgressBar : IProgressBar
    {
        private const int MaxProgressWidth = 28;
        private readonly string[] _statusFrames = { "|", "/", "-", "\\" };
        private CancellationTokenSource _cancelToken;
        private int _currentItem;
        private string _currentMessage = string.Empty;

        private int _status;
        private int _totalItems;

        public void WriteStatusLine(string status)
        {
            WriteSingleLine(new[]
            {
                new ConsoleSegment(status, null)
            });
        }

        public void CleanCurrentLine()
        {
            if (_cancelToken != null && !_cancelToken.IsCancellationRequested)
            {
                _cancelToken.Cancel();
            }

            ClearLine();
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
            if (total < 1)
            {
                total = 1;
            }

            _currentMessage = message;
            _totalItems = total;
            _currentItem = Math.Clamp(current, 0, total);

            if (_cancelToken is { IsCancellationRequested: false })
            {
                return;
            }
            
            _cancelToken = new CancellationTokenSource();
            _ = Task.Run(() => StartStatusThread(_cancelToken.Token)).ConfigureAwait(false);
        }

        private async Task StartStatusThread(CancellationToken token)
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        token.ThrowIfCancellationRequested();
                        return;
                    }

                    WriteSingleLine(BuildProgressSegments());

                    _status++;

                    if (_status > _statusFrames.Length - 1)
                    {
                        _status = 0;
                    }

                    Thread.Sleep(500);
                }
            }, token);
        }

        private ConsoleSegment[] BuildProgressSegments()
        {
            var progressWidth = Math.Min(_totalItems, MaxProgressWidth);
            var completedWidth = (int)Math.Round((double)_currentItem / _totalItems * progressWidth);
            completedWidth = Math.Clamp(completedWidth, 0, progressWidth);

            var builder = new StringBuilder();
            builder.Append(']');

            if (!string.IsNullOrEmpty(_currentMessage))
            {
                builder.Append(' ');
                builder.Append(_statusFrames[_status]);
                builder.Append(' ');
                builder.Append(_currentMessage);
            }

            return new[]
            {
                new ConsoleSegment("[", null),
                new ConsoleSegment(new string('█', completedWidth), ConsoleColor.Green),
                new ConsoleSegment(new string('~', progressWidth - completedWidth), ConsoleColor.Blue),
                new ConsoleSegment(builder.ToString(), null)
            };
        }

        private static void WriteSingleLine(ConsoleSegment[] segments)
        {
            var textLength = 0;
            foreach (var segment in segments)
            {
                textLength += segment.Text.Length;
            }

            if (!System.Console.IsOutputRedirected)
            {
                System.Console.SetCursorPosition(0, System.Console.CursorTop);
            }

            var originalColor = System.Console.ForegroundColor;
            foreach (var segment in segments)
            {
                if (segment.Color.HasValue && !System.Console.IsOutputRedirected)
                {
                    System.Console.ForegroundColor = segment.Color.Value;
                }
                else
                {
                    System.Console.ForegroundColor = originalColor;
                }

                System.Console.Write(segment.Text);
            }
            System.Console.ForegroundColor = originalColor;

            WritePadding(textLength);

            if (!System.Console.IsOutputRedirected)
            {
                System.Console.SetCursorPosition(0, System.Console.CursorTop);
            }
            else
            {
                System.Console.WriteLine();
            }
        }

        private static void ClearLine()
        {
            if (System.Console.IsOutputRedirected)
            {
                return;
            }

            var builder = new StringBuilder("\r");
            while (builder.Length < GetBufferWidth())
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(builder.ToString());
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
        }

        private static void WritePadding(int textLength)
        {
            var padding = GetBufferWidth() - textLength;
            if (padding <= 0)
            {
                return;
            }

            System.Console.Write(new string(' ', padding));
        }

        private static int GetBufferWidth()
        {
            return System.Console.IsOutputRedirected ? 80 : System.Console.BufferWidth;
        }

        private readonly struct ConsoleSegment
        {
            public ConsoleSegment(string text, ConsoleColor? color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }
            public ConsoleColor? Color { get; }
        }
    }
}
