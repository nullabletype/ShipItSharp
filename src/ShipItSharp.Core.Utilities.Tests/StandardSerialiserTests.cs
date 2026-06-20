using System.Collections.Generic;
using NUnit.Framework;
using ShipItSharp.Core.Utilities;

namespace ShipItSharp.Core.Utilities.Tests;

[TestFixture]
public class StandardSerialiserTests
{
    [Test]
    public void SerializeAndDeserialize_RoundTripsSimpleDto()
    {
        var original = new SampleDto { Name = "Payments", Count = 3 };

        var json = StandardSerialiser.SerializeToJsonNet(original);
        var result = StandardSerialiser.DeserializeFromJsonNet<SampleDto>(json);

        Assert.That(result.Name, Is.EqualTo("Payments"));
        Assert.That(result.Count, Is.EqualTo(3));
    }

    [Test]
    public void SerializeAndDeserialize_WithDerivedTypes_RoundTripsRuntimeType()
    {
        SampleBase original = new DerivedSample { Name = "Payments", Extra = "Blue" };

        var json = StandardSerialiser.SerializeToJsonNet(original, handleDerivedTypes: true);
        var result = StandardSerialiser.DeserializeFromJsonNet<SampleBase>(json, handleDerivedTypes: true);

        Assert.That(result, Is.TypeOf<DerivedSample>());
        Assert.That(((DerivedSample)result).Extra, Is.EqualTo("Blue"));
    }

    [Test]
    public void SerializeToJsonNet_ReturnsEmptyString_ForNull()
    {
        Assert.That(StandardSerialiser.SerializeToJsonNet<SampleDto>(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void DeserializeFromJsonNet_ReturnsDefault_ForEmptyInput()
    {
        Assert.That(StandardSerialiser.DeserializeFromJsonNet<SampleDto>(""), Is.Null);
    }

    [Test]
    public void DeserializeFromJsonNet_ReturnsSingleStringList_WhenInvalidJsonTargetsStringList()
    {
        var result = StandardSerialiser.DeserializeFromJsonNet<List<string>>("raw-value");

        Assert.That(result, Is.EqualTo(new[] { "raw-value" }));
    }

    private class SampleDto
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    private class SampleBase
    {
        public string Name { get; set; }
    }

    private class DerivedSample : SampleBase
    {
        public string Extra { get; set; }
    }
}
