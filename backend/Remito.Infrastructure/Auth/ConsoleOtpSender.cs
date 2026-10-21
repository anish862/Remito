using Remito.Application.Auth;
using Remito.Domain.Entities;

namespace Remito.Infrastructure.Auth;

public sealed class ConsoleOtpSender : IOtpSender
{
    public Task SendAsync(
        OtpChannel channel,
        string destination,
        string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.Out.WriteLine($"[DEV OTP] Delivery requested via {channel}.");
        return Task.CompletedTask;
    }
}

public sealed class UnconfiguredOtpSender : IOtpSender
{
    public Task SendAsync(
        OtpChannel channel,
        string destination,
        string code,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Configure a production OTP sender before enabling OTP authentication.");
}

public sealed class UnconfiguredOtpRateLimiter : IOtpRateLimiter
{
    public Task<OtpRateLimitResult> CheckLimitAsync(
        string scope,
        string subjectHash,
        string ipAddress,
        int subjectLimit,
        int ipLimit,
        TimeSpan window,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Configure Redis before enabling OTP authentication.");
}
