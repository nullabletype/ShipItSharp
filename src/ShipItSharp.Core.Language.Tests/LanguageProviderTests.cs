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
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace ShipItSharp.Core.Language.Tests
{
    [TestFixture]
    public class LanguageProviderTests
    {
        [Test]
        public void ExceptionIsThrownOnNullKey()
        {
            Assert.Throws<ArgumentNullException>(delegate { new LanguageProvider().GetString(LanguageSection.UiStrings, null); });
        }

        [Test]
        public void CanFetchStringInEnglish()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-GB");
            Assert.That(new LanguageProvider().GetString(LanguageSection.UiStrings, "OnSource").Equals("On source", StringComparison.CurrentCultureIgnoreCase));
        }

        [Test]
        public void CanFetchStringInWelsh()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("cy-GB");
            Assert.That(new LanguageProvider().GetString(LanguageSection.UiStrings, "OnSource").Equals("Ar Ffynhonnell", StringComparison.CurrentCultureIgnoreCase));
        }
    }
}