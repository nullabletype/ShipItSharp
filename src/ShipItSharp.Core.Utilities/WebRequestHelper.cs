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


using System.Net;
using System.Net.Http;
using System.Xml.Serialization;

namespace ShipItSharp.Core.Utilities
{
    public interface IWebRequestHelper
    {
        T GetXmlWebRequestWithBasicAuth<T>(string url, string username, string password);
    }

    public class WebRequestHelper : IWebRequestHelper
    {

        public T GetXmlWebRequestWithBasicAuth<T>(string url, string username, string password)
        {
            var credentials = new NetworkCredential(username, password);
            var handler = new HttpClientHandler { Credentials = credentials, PreAuthenticate = true };
            var client = new HttpClient(handler);

            using (var stream = client.GetStreamAsync(url).GetAwaiter().GetResult())
            {
                var serializer = new XmlSerializer(typeof(T));
                if (stream.CanRead)
                {
                    return (T) serializer.Deserialize(stream);
                }
            }
            return default(T);
        }
    }
}