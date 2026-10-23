using Remito.Api.Controllers;

namespace Remito.UnitTests;

public sealed class AnalyticsEventTaxonomyTests
{
    [Theory]
    [InlineData("screen_view")]
    [InlineData("compare_run")]
    [InlineData("exchange_selected")]
    [InlineData("redirect_started")]
    [InlineData("redirect_returned")]
    [InlineData("alert_created")]
    [InlineData("notification_opened")]
    [InlineData("ad_impression")]
    [InlineData("ad_click")]
    public void Taxonomy_accepts_only_declared_event_names(string eventName) =>
        Assert.True(AnalyticsEventTaxonomy.IsSupportedEvent(eventName));

    [Fact]
    public void Installation_id_must_be_random_uuid_v4_and_only_its_hash_is_returned()
    {
        var installationId = Guid.NewGuid().ToString("D");

        Assert.True(AnalyticsEventTaxonomy.TryHashInstallationId(installationId, out var hash));
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(installationId, hash, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-an-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Invalid_or_nonrandom_installation_ids_are_rejected(string installationId) =>
        Assert.False(AnalyticsEventTaxonomy.TryHashInstallationId(installationId, out _));
}
