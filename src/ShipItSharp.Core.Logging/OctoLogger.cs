﻿#region copyright
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
using Microsoft.Extensions.Logging;
using ILogger = ShipItSharp.Core.Logging.Interfaces.ILogger;

namespace ShipItSharp.Core.Logging
{
    public class OctoLogger<T> : ILogger where T : class
    {

        private readonly Microsoft.Extensions.Logging.ILogger _log;

        public OctoLogger()
        {
            _log = LoggingProvider.LoggerFactory.CreateLogger<T>();
        }

        public void Info(string message)
        {
            _log.LogInformation(message);
        }

        public void Error(string message, Exception e = null)
        {
            _log.LogError(message, e);
        }
    }
}