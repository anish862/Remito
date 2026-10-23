using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Remito.Application.Engagement;

namespace Remito.Infrastructure.Engagement;

public sealed class FirebaseAdminPushSender(FirebaseApp firebaseApp) : IPushSender
{
    public async Task<PushBatchResult> SendAsync(
        IReadOnlyList<string> tokens,
        PushNotice notice,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0)
        {
            return new PushBatchResult(new HashSet<string>(StringComparer.Ordinal));
        }

        var invalidTokens = new HashSet<string>(StringComparer.Ordinal);
        var messaging = FirebaseMessaging.GetMessaging(firebaseApp);
        foreach (var batch in tokens.Chunk(500))
        {
            var response = await messaging.SendEachForMulticastAsync(
                new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new Notification { Title = notice.Title, Body = notice.Body },
                    Data = new Dictionary<string, string>(notice.Data, StringComparer.Ordinal)
                },
                cancellationToken);
            for (var index = 0; index < response.Responses.Count; index++)
            {
                var item = response.Responses[index];
                if (!item.IsSuccess
                    && item.Exception is FirebaseMessagingException { MessagingErrorCode: MessagingErrorCode.Unregistered })
                {
                    invalidTokens.Add(batch[index]);
                }
            }
        }

        return new PushBatchResult(invalidTokens);
    }
}

public sealed class UnconfiguredPushSender : IPushSender
{
    public Task<PushBatchResult> SendAsync(
        IReadOnlyList<string> tokens,
        PushNotice notice,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Firebase:ServiceAccountJson must be configured before push notifications can be delivered.");
}

public sealed class ConsolePushSender(ILogger<ConsolePushSender> logger) : IPushSender
{
    public Task<PushBatchResult> SendAsync(
        IReadOnlyList<string> tokens,
        PushNotice notice,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Development push simulated. Category={Category} DeviceCount={DeviceCount}",
            notice.Category,
            tokens.Count);
        return Task.FromResult(new PushBatchResult(new HashSet<string>(StringComparer.Ordinal)));
    }
}
