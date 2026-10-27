using GovUK.Dfe.FlexForms.Application.Diagnostics;

namespace GovUK.Dfe.FlexForms.Application.Tests.Diagnostics;

public class RequestTimingsTests
{
    [Fact]
    public void Measure_ShouldDoNothing_OutsideATimedRequest()
    {
        RequestTimings.Stop();

        using (RequestTimings.Measure("step"))
        {
        }

        RequestTimings.Count("calls");
        Assert.Null(RequestTimings.Current);
    }

    [Fact]
    public async Task Measure_ShouldAddUpRepeatedSteps_AcrossAwaits()
    {
        var timings = RequestTimings.Start();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                using (RequestTimings.Measure("parse template"))
                    await Task.Delay(5);
            }

            await Task.Run(() => RequestTimings.Count("save body KB", 40));
        }
        finally
        {
            RequestTimings.Stop();
        }

        var parse = timings.Steps["parse template"];
        Assert.Equal(2, parse.Count);
        Assert.True(parse.Elapsed >= TimeSpan.FromMilliseconds(8));
        Assert.Equal(new RequestTimings.Step(TimeSpan.Zero, 40, false), timings.Steps["save body KB"]);
        Assert.Matches(@"^parse template=\d+ ms x2, save body KB=40$", timings.Describe());
    }

    [Fact]
    public void Describe_ShouldListTheSlowestStepFirst()
    {
        var timings = RequestTimings.Start();
        try
        {
            using (RequestTimings.Measure("quick"))
            {
            }

            using (RequestTimings.Measure("slow"))
                Thread.Sleep(10);
        }
        finally
        {
            RequestTimings.Stop();
        }

        Assert.StartsWith("slow=", timings.Describe());
    }
}
