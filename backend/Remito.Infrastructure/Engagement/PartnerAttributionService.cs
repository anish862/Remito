using System.Data;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Remito.Application.Engagement;
using Remito.Domain.Entities;
using Remito.Infrastructure.Persistence;

namespace Remito.Infrastructure.Engagement;

public sealed class PartnerAttributionService(
    RemitoDbContext dbContext,
    IConfiguration configuration,
    TimeProvider timeProvider) : IPartnerAttributionService
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    public async Task<PartnerPostbackResponse> ProcessPostbackAsync(
        Guid exchangeId,
        string timestampHeader,
        string signatureHeader,
        string rawBody,
        PartnerPostbackRequest request,
        CancellationToken cancellationToken)
    {
        VerifySignature(exchangeId, timestampHeader, signatureHeader, rawBody);
        if (request.Amount <= 0
            || request.Amount > 1_000_000_000_000m
            || string.IsNullOrWhiteSpace(request.ExternalRef)
            || request.ExternalRef.Length > 200
            || string.IsNullOrWhiteSpace(request.ClickId)
            || request.ClickId.Length > 100
            || request.ConvertedAt == default
            || request.Status is not (ConversionStatus.Confirmed or ConversionStatus.Rejected))
        {
            throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Postback payload is invalid.");
        }

        var exchange = await dbContext.Exchanges.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exchangeId && item.Status == ExchangeStatus.Active,
                cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Active partner not found.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var prior = await dbContext.ReferralConversions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ExchangeId == exchangeId
                && item.ExternalRef == request.ExternalRef, cancellationToken);
        if (prior is not null)
        {
            if (prior.ClickId != request.ClickId
                || prior.Amount != request.Amount
                || prior.Status != request.Status)
            {
                throw new EngagementOperationException(
                    (int)HttpStatusCode.Conflict,
                    "External reference was already recorded with different conversion data.");
            }

            return new PartnerPostbackResponse(true, true, prior.Id);
        }

        var redirect = await dbContext.RedirectEvents
            .SingleOrDefaultAsync(item => item.ClickId == request.ClickId
                && item.ExchangeId == exchangeId, cancellationToken)
            ?? throw new EngagementOperationException(
                (int)HttpStatusCode.BadRequest,
                "Click ID does not belong to this partner.");
        if (await dbContext.ReferralConversions.AnyAsync(
                item => item.ClickId == request.ClickId,
                cancellationToken))
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.Conflict,
                "Click ID already has an attributed conversion.");
        }

        var currencyCode = exchange.CommissionCurrencyCode;
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            currencyCode = await dbContext.Corridors.AsNoTracking()
                .Where(item => item.Id == redirect.CorridorId)
                .Select(item => item.ToCurrency)
                .SingleAsync(cancellationToken);
        }

        if (!await dbContext.Currencies.AsNoTracking()
                .AnyAsync(currency => currency.Code == currencyCode, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Commission currency '{currencyCode}' is not configured for exchange {exchangeId}.");
        }

        var conversion = new ReferralConversion
        {
            Id = Guid.NewGuid(),
            ExchangeId = exchangeId,
            ClickId = request.ClickId,
            ExternalRef = request.ExternalRef,
            ConvertedAt = request.ConvertedAt.ToUniversalTime(),
            Amount = request.Amount,
            Status = request.Status
        };
        dbContext.ReferralConversions.Add(conversion);
        if (request.Status == ConversionStatus.Confirmed)
        {
            dbContext.CommissionLedgers.Add(new CommissionLedger
            {
                Id = Guid.NewGuid(),
                ConversionId = conversion.Id,
                Amount = exchange.CommissionFlatAmount,
                CurrencyCode = currencyCode,
                Status = LedgerStatus.Pending
            });
        }

        redirect.Status = RedirectStatus.Redirected;
        await dbContext.SaveChangesAsync(cancellationToken);
        var missingPostbackIssue = await dbContext.PartnerReconciliationIssues
            .SingleOrDefaultAsync(issue => issue.ExchangeId == exchangeId
                && issue.ClickId == request.ClickId
                && issue.IssueType == "MissingPostback",
                cancellationToken);
        if (missingPostbackIssue is not null)
        {
            missingPostbackIssue.IsResolved = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new PartnerPostbackResponse(true, false, conversion.Id);
    }

    public async Task<ExchangeMonthlyReport> GetMonthlyReportAsync(
        Guid exchangeId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
        {
            throw new EngagementOperationException((int)HttpStatusCode.BadRequest, "Year or month is invalid.");
        }

        var exchange = await dbContext.Exchanges.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exchangeId, cancellationToken)
            ?? throw new EngagementOperationException((int)HttpStatusCode.NotFound, "Exchange not found.");
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);
        var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        long redirects;
        List<ReferralConversion> conversions;
        if (isSqlite)
        {
            redirects = (await dbContext.RedirectEvents.AsNoTracking()
                    .Where(item => item.ExchangeId == exchangeId)
                    .ToListAsync(cancellationToken))
                .LongCount(item => item.CreatedAt >= start && item.CreatedAt < end);
            conversions = (await dbContext.ReferralConversions.AsNoTracking()
                    .Where(item => item.ExchangeId == exchangeId && item.Status == ConversionStatus.Confirmed)
                    .ToListAsync(cancellationToken))
                .Where(item => item.ConvertedAt >= start && item.ConvertedAt < end)
                .ToList();
        }
        else
        {
            redirects = await dbContext.RedirectEvents.AsNoTracking()
                .LongCountAsync(item => item.ExchangeId == exchangeId
                    && item.CreatedAt >= start && item.CreatedAt < end, cancellationToken);
            conversions = await dbContext.ReferralConversions.AsNoTracking()
                .Where(item => item.ExchangeId == exchangeId
                    && item.ConvertedAt >= start && item.ConvertedAt < end
                    && item.Status == ConversionStatus.Confirmed)
                .ToListAsync(cancellationToken);
        }
        var conversionIds = conversions.Select(item => item.Id).ToArray();
        var ledger = await dbContext.CommissionLedgers.AsNoTracking()
            .Where(item => conversionIds.Contains(item.ConversionId)
                && item.Status != LedgerStatus.Reversed)
            .ToListAsync(cancellationToken);
        var currency = ledger.Select(item => item.CurrencyCode).Distinct(StringComparer.Ordinal).SingleOrDefault()
            ?? exchange.CommissionCurrencyCode
            ?? await dbContext.Corridors.AsNoTracking()
                .Where(item => item.FromCurrency == "OMR" && item.ToCurrency == "INR")
                .Select(item => item.ToCurrency)
                .SingleAsync(cancellationToken);
        var amount = ledger.Where(item => item.CurrencyCode == currency).Sum(item => item.Amount);
        var reportRows = await dbContext.PartnerReportRows.AsNoTracking()
            .Where(item => item.ExchangeId == exchangeId)
            .ToListAsync(cancellationToken);
        reportRows = reportRows
            .Where(item => item.ConvertedAt >= start && item.ConvertedAt < end)
            .ToList();
        var reportedCommission = reportRows
            .Where(item => item.CurrencyCode == currency)
            .Sum(item => item.Commission);
        var unmatchedRows = await dbContext.PartnerReconciliationIssues.AsNoTracking()
            .CountAsync(item => item.ExchangeId == exchangeId
                && !item.IsResolved
                && item.IssueType == "UnmatchedReport", cancellationToken);
        var missingPostbacks = await dbContext.PartnerReconciliationIssues.AsNoTracking()
            .CountAsync(item => item.ExchangeId == exchangeId
                && !item.IsResolved
                && item.IssueType == "MissingPostback", cancellationToken);

        return new ExchangeMonthlyReport(
            exchangeId,
            exchange.Name,
            new DateOnly(year, month, 1),
            redirects,
            conversions.Count,
            redirects == 0 ? 0m : decimal.Round(conversions.Count * 100m / redirects, 2),
            amount,
            currency,
            reportedCommission,
            amount - reportedCommission,
            unmatchedRows,
            missingPostbacks);
    }

    private void VerifySignature(Guid exchangeId, string timestampHeader, string signatureHeader, string rawBody)
    {
        var secret = configuration[$"PartnerPostback:Secrets:{exchangeId:D}"];
        if (string.IsNullOrWhiteSpace(secret)
            && !string.IsNullOrWhiteSpace(configuration["PartnerPostback:SecretsJson"]))
        {
            using var secrets = JsonDocument.Parse(configuration["PartnerPostback:SecretsJson"]!);
            if (secrets.RootElement.TryGetProperty(exchangeId.ToString("D"), out var configuredSecret)
                && configuredSecret.ValueKind == JsonValueKind.String)
            {
                secret = configuredSecret.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new EngagementOperationException(
                (int)HttpStatusCode.Unauthorized,
                "Partner signature is invalid.");
        }

        if (!long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
        {
            throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Partner signature is invalid.");
        }

        var nowSeconds = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var toleranceSeconds = (long)TimestampTolerance.TotalSeconds;
        if (timestamp < nowSeconds - toleranceSeconds || timestamp > nowSeconds + toleranceSeconds)
        {
            throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Partner signature is expired.");
        }

        var suppliedHex = signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signatureHeader["sha256=".Length..]
            : signatureHeader;
        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(suppliedHex);
        }
        catch (FormatException)
        {
            throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Partner signature is invalid.");
        }

        var message = Encoding.UTF8.GetBytes($"{timestampHeader}.{rawBody}");
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message);
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            throw new EngagementOperationException((int)HttpStatusCode.Unauthorized, "Partner signature is invalid.");
        }
    }
}
