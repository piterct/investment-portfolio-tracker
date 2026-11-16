using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvestmentPortfolioTracker.Business.Dtos;
using InvestmentPortfolioTracker.Business.Models;
using InvestmentPortfolioTracker.Business.Repositories;

namespace InvestmentPortfolioTracker.Business.Services
{
    public sealed class QuoteRefreshService(
        HttpClient httpClient,
        ISettingsRepository settingsRepository,
        IAssetRepository assetRepository,
        QuoteRefreshOptions quoteRefreshOptions) : IQuoteRefreshService
    {
        private const int MaxAttempts = 3;
        private static readonly TimeSpan PlanCacheDuration = TimeSpan.FromHours(6);
        private static readonly TimeSpan FailedPlanCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly ConcurrentDictionary<string, CachedPlan> PlanCache = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> PlanLocks = new();

        public async Task<QuoteRefreshResponseDto> RefreshVisibleQuotesAsync(
            int pageNumber = PaginationLimits.DefaultPageNumber,
            int pageSize = PaginationLimits.DefaultPageSize,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            var settingsTask = settingsRepository.GetSettingsAsync();
            var assetPageTask = assetRepository.GetPortfolioPageAsync(pageNumber, pageSize, searchTerm);
            await Task.WhenAll(settingsTask, assetPageTask);

            var settings = await settingsTask;
            var assets = (await assetPageTask).Assets;
            if (assets.Count == 0)
            {
                return CreateResponse(pageNumber, pageSize, 0, 0, Array.Empty<QuoteRefreshFailureDto>());
            }

            var now = DateTimeOffset.UtcNow;
            var refreshBefore = now - quoteRefreshOptions.FreshnessInterval;
            var assetsToRefresh = assets
                .Where(asset => asset.LastQuoteAttemptAt is null || asset.LastQuoteAttemptAt <= refreshBefore)
                .ToList();
            var skipped = assets.Count - assetsToRefresh.Count;
            if (assetsToRefresh.Count == 0)
            {
                return CreateResponse(pageNumber, pageSize, 0, 0, Array.Empty<QuoteRefreshFailureDto>(), skipped);
            }

            var plan = await GetPlanLimitsAsync(settings.ApiToken, cancellationToken);
            var failures = new List<QuoteRefreshFailureDto>();
            var updated = 0;

            foreach (var batch in assetsToRefresh.Chunk(plan.MaxTickersPerRequest))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attemptedAt = DateTimeOffset.UtcNow;
                var batchResult = await GetQuoteBatchWithRetryAsync(batch, settings.ApiToken, cancellationToken);
                if (batchResult.Error is not null)
                {
                    await RecordQuoteAttemptsAsync(batch, attemptedAt);
                    failures.AddRange(batch.Select(asset => new QuoteRefreshFailureDto(asset.Ticker, batchResult.Error)));
                    continue;
                }

                foreach (var asset in batch)
                {
                    if (!batchResult.Prices!.TryGetValue(asset.Ticker, out var price))
                    {
                        await RecordQuoteAttemptsAsync([asset], attemptedAt);
                        failures.Add(new QuoteRefreshFailureDto(asset.Ticker, "A API não retornou cotação para este ticker."));
                        continue;
                    }

                    try
                    {
                        await assetRepository.UpdateQuoteAsync(asset.Id, price, attemptedAt);
                        updated++;
                    }
                    catch (Exception) when (!cancellationToken.IsCancellationRequested)
                    {
                        failures.Add(new QuoteRefreshFailureDto(asset.Ticker, "A cotação foi obtida, mas não foi possível salvá-la. Tente atualizar novamente."));
                    }
                }
            }

            return CreateResponse(pageNumber, pageSize, assetsToRefresh.Count, updated, failures, skipped);
        }

        public async Task<QuoteRefreshResponseDto?> RefreshQuoteAsync(
            int assetId,
            CancellationToken cancellationToken = default)
        {
            var settingsTask = settingsRepository.GetSettingsAsync();
            var assetTask = assetRepository.GetAssetByIdAsync(assetId);
            await Task.WhenAll(settingsTask, assetTask);

            var asset = await assetTask;
            if (asset is null) return null;

            var attemptedAt = DateTimeOffset.UtcNow;
            var batchResult = await GetQuoteBatchWithRetryAsync([asset], (await settingsTask).ApiToken, cancellationToken);
            if (batchResult.Error is not null)
            {
                await RecordQuoteAttemptsAsync([asset], attemptedAt);
                return CreateResponse(1, 1, 1, 0, [new QuoteRefreshFailureDto(asset.Ticker, batchResult.Error)]);
            }

            if (!batchResult.Prices!.TryGetValue(asset.Ticker, out var price))
            {
                await RecordQuoteAttemptsAsync([asset], attemptedAt);
                return CreateResponse(1, 1, 1, 0, [new QuoteRefreshFailureDto(asset.Ticker, "A API não retornou cotação para este ticker.")]);
            }

            try
            {
                await assetRepository.UpdateQuoteAsync(asset.Id, price, attemptedAt);
                return CreateResponse(1, 1, 1, 1, Array.Empty<QuoteRefreshFailureDto>());
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return CreateResponse(1, 1, 1, 0, [new QuoteRefreshFailureDto(asset.Ticker, "A cotação foi obtida, mas não foi possível salvá-la. Tente atualizar novamente.")]);
            }
        }

        private async Task RecordQuoteAttemptsAsync(IEnumerable<Asset> assets, DateTimeOffset attemptedAt)
        {
            foreach (var asset in assets)
            {
                await assetRepository.RecordQuoteAttemptAsync(asset.Id, attemptedAt);
            }
        }

        private async Task<QuoteBatchResult> GetQuoteBatchWithRetryAsync(
            IReadOnlyCollection<Asset> assets,
            string? token,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"api/v2/stocks/quote?symbols={Uri.EscapeDataString(string.Join(',', assets.Select(asset => asset.Ticker)))}");
                    AddToken(request, token);

                    using var response = await httpClient.SendAsync(request, cancellationToken);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        return ParseQuotes(body);
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (IsQuotaExhausted(response))
                        {
                            return QuoteBatchResult.Failed("A cota da API foi esgotada. Consulte o uso da conta e tente novamente após a renovação do ciclo.");
                        }

                        if (attempt < MaxAttempts && await WaitForRetryAfterAsync(response, cancellationToken))
                            continue;

                        return QuoteBatchResult.Failed(FormatApiError(response.StatusCode, body, "Limite temporário de requisições atingido."));
                    }

                    if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                    {
                        await Task.Delay(GetBackoff(attempt), cancellationToken);
                        continue;
                    }

                    return QuoteBatchResult.Failed(FormatApiError(response.StatusCode, body, "A API recusou a consulta."));
                }
                catch (HttpRequestException) when (attempt < MaxAttempts)
                {
                    await Task.Delay(GetBackoff(attempt), cancellationToken);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
                {
                    await Task.Delay(GetBackoff(attempt), cancellationToken);
                }
                catch (HttpRequestException)
                {
                    return QuoteBatchResult.Failed("Não foi possível conectar à API de cotações após algumas tentativas.");
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return QuoteBatchResult.Failed("A consulta excedeu o tempo limite após algumas tentativas.");
                }
                catch (JsonException)
                {
                    return QuoteBatchResult.Failed("A resposta da API veio em formato inesperado.");
                }
            }

            return QuoteBatchResult.Failed("Não foi possível obter as cotações após algumas tentativas.");
        }

        private async Task<PlanLimits> GetPlanLimitsAsync(string? token, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token))
                return PlanLimits.Free;

            var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            if (PlanCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
                return cached.Limits;

            var gate = PlanLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (PlanCache.TryGetValue(cacheKey, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
                    return cached.Limits;

                var limits = PlanLimits.Free;
                var cacheDuration = FailedPlanCacheDuration;
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "api/v2/user/usage");
                    AddToken(request, token);
                    using var response = await httpClient.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                        if (json.RootElement.TryGetProperty("usage", out var usage)
                            && usage.TryGetProperty("planName", out var planName)
                            && planName.ValueKind == JsonValueKind.String)
                        {
                            limits = planName.GetString()?.ToLowerInvariant() switch
                            {
                                "startup" => new PlanLimits(10),
                                "pro" => new PlanLimits(20),
                                _ => PlanLimits.Free
                            };
                            cacheDuration = PlanCacheDuration;
                        }
                    }
                }
                catch (HttpRequestException)
                {

                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {

                }
                catch (JsonException)
                {

                }

                PlanCache[cacheKey] = new CachedPlan(limits, DateTimeOffset.UtcNow.Add(cacheDuration));
                return limits;
            }
            finally
            {
                gate.Release();
            }
        }

        private static QuoteBatchResult ParseQuotes(string body)
        {
            using var json = JsonDocument.Parse(body);
            var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return QuoteBatchResult.Failed("A API respondeu sem a lista de cotações esperada.");

            foreach (var result in results.EnumerateArray())
            {
                var ticker = ReadString(result, "requestedSymbol") ?? ReadString(result, "symbol");
                if (string.IsNullOrWhiteSpace(ticker)
                    || !result.TryGetProperty("regularMarketPrice", out var priceElement)
                    || !priceElement.TryGetDecimal(out var price)
                    || price < 0)
                    continue;

                prices[ticker] = price;
            }

            return QuoteBatchResult.Succeeded(prices);
        }

        private static string FormatApiError(HttpStatusCode statusCode, string body, string fallback)
        {
            try
            {
                using var json = JsonDocument.Parse(body);
                var message = ReadString(json.RootElement, "message")
                    ?? (json.RootElement.TryGetProperty("error", out var error)
                        ? (error.ValueKind == JsonValueKind.String ? error.GetString() : ReadString(error, "message"))
                        : null);
                if (!string.IsNullOrWhiteSpace(message))
                    return $"A API respondeu {(int)statusCode}: {message}";
            }
            catch (JsonException)
            {

            }

            return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? "A API recusou a consulta. Confira o token e o plano da brapi."
                : $"{fallback} (HTTP {(int)statusCode}).";
        }

        private static bool IsQuotaExhausted(HttpResponseMessage response) =>
            response.Headers.TryGetValues("RateLimit-Remaining", out var values)
            && values.FirstOrDefault() is { } remaining
            && int.TryParse(remaining, out var count)
            && count == 0;

        private static async Task<bool> WaitForRetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var retryAfter = response.Headers.RetryAfter;
            var delay = retryAfter?.Delta
                ?? (retryAfter?.Date is { } retryDate ? retryDate - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(5));
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            if (delay > TimeSpan.FromSeconds(30))
                return false;

            await Task.Delay(delay, cancellationToken);
            return true;
        }

        private static bool IsTransient(HttpStatusCode statusCode) =>
            statusCode is HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;

        private static TimeSpan GetBackoff(int attempt) =>
            TimeSpan.FromMilliseconds(Math.Pow(2, attempt - 1) * 500 + Random.Shared.Next(100, 400));

        private static void AddToken(HttpRequestMessage request, string? token)
        {
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private static string? ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

        private static QuoteRefreshResponseDto CreateResponse(
            int pageNumber,
            int pageSize,
            int requested,
            int updated,
            IReadOnlyCollection<QuoteRefreshFailureDto> failures,
            int skipped = 0) => new()
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Requested = requested,
            Updated = updated,
            Skipped = skipped,
            Failed = failures,
            RefreshedAt = DateTimeOffset.UtcNow
        };

        private sealed record PlanLimits(int MaxTickersPerRequest)
        {
            public static PlanLimits Free { get; } = new(1);
        }

        private sealed record CachedPlan(PlanLimits Limits, DateTimeOffset ExpiresAt);
        private sealed record QuoteBatchResult(Dictionary<string, decimal>? Prices, string? Error)
        {
            public static QuoteBatchResult Succeeded(Dictionary<string, decimal> prices) => new(prices, null);
            public static QuoteBatchResult Failed(string error) => new(null, error);
        }
    }
}
