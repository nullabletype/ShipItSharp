using System.IO;
using NUnit.Framework;
using ShipItSharp.Console.ConsoleTools;

namespace ShipItSharp.Console.Tests;

[NonParallelizable]
public class ConsoleProgressBarTests
{
    [Test]
    public void WriteProgress_RendersCompletionBeforeAnimationStops()
    {
        var bar = new ConsoleProgressBar();
        var originalOutput = System.Console.Out;
        using var output = new StringWriter();
        try
        {
            System.Console.SetOut(output);
            bar.WriteProgress(0, 2, "0 of 2 done");
            bar.CleanCurrentLine();
            bar.WriteProgress(2, 2, "2 of 2 done");
            bar.StopAnimation();
            bar.CleanCurrentLine();

            Assert.That(output.ToString(), Does.Contain("[██]").And.Contains("2 of 2 done"));
        }
        finally
        {
            bar.StopAnimation();
            System.Console.SetOut(originalOutput);
        }
    }
}
