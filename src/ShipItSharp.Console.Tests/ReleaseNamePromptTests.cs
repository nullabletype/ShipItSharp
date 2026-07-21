#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ShipItSharp.Console.Commands;

namespace ShipItSharp.Console.Tests;

[TestFixture]
public class ReleaseNamePromptTests
{
    [Test]
    public void PromptForStringWithTimeout_ReturnsInputReceivedBeforeTimeout()
    {
        using var input = new StringReader("1.2.3\n");
        using var output = new StringWriter();

        var result = BaseCommand.PromptForStringWithTimeout("Release version", TimeSpan.FromSeconds(1), input, output);

        Assert.That(result, Is.EqualTo("1.2.3"));
        Assert.That(output.ToString(), Is.EqualTo("Release version ( 1s): "));
    }

    [Test]
    public void PromptForStringWithTimeout_ReturnsEmptyWhenTimeoutExpires()
    {
        using var input = new NeverCompletingReader();
        using var output = new StringWriter();

        var result = BaseCommand.PromptForStringWithTimeout("Release version", TimeSpan.FromMilliseconds(20), input, output);

        Assert.That(result, Is.Empty);
        Assert.That(output.ToString(), Is.EqualTo($"Release version ( 1s): {System.Environment.NewLine}"));
    }

    [Test]
    public void PromptForStringWithTimeout_DoesNotLetSynchronousAsyncReaderHidePromptOrTimeout()
    {
        using var input = new SynchronouslyBlockingReader();
        using var output = new StringWriter();
        var timer = Stopwatch.StartNew();

        var result = BaseCommand.PromptForStringWithTimeout("Release version", TimeSpan.FromMilliseconds(20), input, output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(output.ToString(), Does.StartWith("Release version"));
            Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(150)));
        });
    }

    private sealed class NeverCompletingReader : TextReader
    {
        public override ValueTask<string?> ReadLineAsync(System.Threading.CancellationToken cancellationToken)
        {
            return new ValueTask<string?>(Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith<string?>(
                _ => null,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default));
        }
    }

    private sealed class SynchronouslyBlockingReader : TextReader
    {
        public override ValueTask<string?> ReadLineAsync(System.Threading.CancellationToken cancellationToken)
        {
            Thread.Sleep(200);
            return ValueTask.FromResult<string?>("late input");
        }
    }
}
