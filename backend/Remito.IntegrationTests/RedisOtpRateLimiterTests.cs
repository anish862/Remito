using Remito.Infrastructure.Auth;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Remito.IntegrationTests;

public sealed class RedisOtpRateLimiterTests
{
    [Fact]
    public async Task Limits_are_enforced_per_destination_and_per_ip()
    {
        await using var redis = new RedisBuilder().Build();
        await redis.StartAsync();
        using var connection = await ConnectionMultiplexer.ConnectAsync(redis.GetConnectionString());
        var limiter = new RedisOtpRateLimiter(connection);
        var window = TimeSpan.FromMinutes(1);

        var firstSubjectAttempt = await limiter.CheckLimitAsync(
            "subject-test", "subject-hash", "192.0.2.1", 2, 100, window, default);
        var secondSubjectAttempt = await limiter.CheckLimitAsync(
            "subject-test", "subject-hash", "192.0.2.2", 2, 100, window, default);
        var thirdSubjectAttempt = await limiter.CheckLimitAsync(
            "subject-test", "subject-hash", "192.0.2.3", 2, 100, window, default);

        Assert.True(firstSubjectAttempt.IsAllowed);
        Assert.True(secondSubjectAttempt.IsAllowed);
        Assert.False(thirdSubjectAttempt.IsAllowed);

        var firstIpAttempt = await limiter.CheckLimitAsync(
            "ip-test", "subject-one", "192.0.2.10", 100, 2, window, default);
        var secondIpAttempt = await limiter.CheckLimitAsync(
            "ip-test", "subject-two", "192.0.2.10", 100, 2, window, default);
        var thirdIpAttempt = await limiter.CheckLimitAsync(
            "ip-test", "subject-three", "192.0.2.10", 100, 2, window, default);

        Assert.True(firstIpAttempt.IsAllowed);
        Assert.True(secondIpAttempt.IsAllowed);
        Assert.False(thirdIpAttempt.IsAllowed);
        Assert.True(thirdIpAttempt.RetryAfter > TimeSpan.Zero);
    }
}
