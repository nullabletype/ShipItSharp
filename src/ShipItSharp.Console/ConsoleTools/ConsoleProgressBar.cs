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


using ShipItSharp.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShipItSharp.Console.ConsoleTools {
    internal class ConsoleProgressBar : IProgressBar {

        private int status;
        private int currentItem;
        private int totalItems;
        private string[] clocks = {"\\", "|", "/", "-"};
        private string currentMessage = string.Empty;
        private bool spinning;
        private CancellationTokenSource cancelToken;

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
            if (cancelToken != null && spinning)
            {
                cancelToken.Cancel();
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
            if (cancelToken != null && !cancelToken.IsCancellationRequested)
            {
                cancelToken.Cancel();
            }
        }
        
        public void WriteProgress(int current, int total, string message)
        {
            currentMessage = message;
            totalItems = total;
            currentItem = current;

            if (spinning)
            {
                return;
            }
            
            spinning = true;
            cancelToken = new CancellationTokenSource();
            _ = Task.Run(() => StartStatusThread(cancelToken.Token)).ConfigureAwait(false);
        }

        private async Task StartStatusThread(CancellationToken token)
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    var builder = new StringBuilder("\r[");
                    for (var i = 1; i <= currentItem; i++)
                    {
                        builder.Append("█");
                    }

                    for (var i = currentItem + 1; i <= totalItems; i++)
                    {
                        builder.Append("·");
                    }

                    builder.Append("]");
                    if (!string.IsNullOrEmpty(currentMessage))
                    {
                        builder.Append($" {clocks[status]} {currentMessage}");
                    }

                    while (builder.Length < System.Console.BufferWidth)
                    {
                        builder.Append(" ");
                    }

                    if (token.IsCancellationRequested)
                    {
                        token.ThrowIfCancellationRequested();
                    }
                    else
                    {
                        System.Console.SetCursorPosition(0, System.Console.CursorTop);
                        System.Console.Write(builder.ToString());
                        System.Console.SetCursorPosition(0, System.Console.CursorTop);
                    }

                    status++;
                    
                    if (status > clocks.Length - 1)
                    {
                        status = 0;
                    }

                    Thread.Sleep(500);
                }
            }, token);
        }
    }
}
