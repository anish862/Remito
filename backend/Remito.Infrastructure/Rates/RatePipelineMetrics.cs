using Microsoft.ApplicationInsights;

namespace Remito.Infrastructure.Rates;

public sealed class RatePipelineMetrics(TelemetryClient telemetryClient)
{
    public void RecordFetch(string source, TimeSpan duration, bool succeeded)
    {
        telemetryClient.TrackMetric(
            "RateFetchDurationMilliseconds",
            duration.TotalMilliseconds,
            new Dictionary<string, string> { ["source"] = source });
        telemetryClient.TrackMetric(
            "RateSourceFetchSucceeded",
            succeeded ? 1 : 0,
            new Dictionary<string, string> { ["source"] = source });
    }

    public void RecordFreshness(string source, TimeSpan age) =>
        telemetryClient.TrackMetric(
            "RateFreshnessSeconds",
            Math.Max(0, age.TotalSeconds),
            new Dictionary<string, string> { ["source"] = source });

    public void RecordQuarantine(string source, string pair) =>
        telemetryClient.TrackMetric(
            "RateQuarantineCount",
            1,
            new Dictionary<string, string>
            {
                ["source"] = source,
                ["pair"] = pair
            });
}
