using Remito.Application.Auth;
using StackExchange.Redis;

namespace Remito.Infrastructure.Auth;

public sealed class RedisOtpRateLimiter(IConnectionMultiplexer connection) : IOtpRateLimiter
{
    private const string IncrementScript = """
        local subjectCount = redis.call('INCR', KEYS[1])
        if subjectCount == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
        local ipCount = redis.call('INCR', KEYS[2])
        if ipCount == 1 then redis.call('PEXPIRE', KEYS[2], ARGV[1]) end
        local subjectTtl = redis.call('PTTL', KEYS[1])
        local ipTtl = redis.call('PTTL', KEYS[2])
        local retryAfter = math.max(subjectTtl, ipTtl)
        return {
          subjectCount <= tonumber(ARGV[2]) and ipCount <= tonumber(ARGV[3]) and 1 or 0,
          math.min(tonumber(ARGV[2]) - subjectCount, tonumber(ARGV[3]) - ipCount),
          retryAfter
        }
        """;

    public async Task<OtpRateLimitResult> CheckLimitAsync(
        string scope,
        string subjectHash,
        string ipAddress,
        int subjectLimit,
        int ipLimit,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var database = connection.GetDatabase();
        var ipHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(ipAddress)));
        var keys = new RedisKey[]
        {
            $"otp-limit:{scope}:subject:{subjectHash}",
            $"otp-limit:{scope}:ip:{ipHash}"
        };
        var arguments = new RedisValue[]
        {
            checked((long)window.TotalMilliseconds),
            subjectLimit,
            ipLimit
        };
        var result = (RedisResult[]?)await database.ScriptEvaluateAsync(IncrementScript, keys, arguments)
            .WaitAsync(cancellationToken);
        if (result is null || result.Length != 3)
        {
            throw new InvalidOperationException("Redis returned an invalid OTP rate-limit response.");
        }

        var isAllowed = (bool)result[0];
        var remaining = Math.Max(0, (int)result[1]);
        var retryAfterMilliseconds = Math.Max(0, (long)result[2]);
        return new OtpRateLimitResult(
            isAllowed,
            remaining,
            TimeSpan.FromMilliseconds(retryAfterMilliseconds));
    }
}
