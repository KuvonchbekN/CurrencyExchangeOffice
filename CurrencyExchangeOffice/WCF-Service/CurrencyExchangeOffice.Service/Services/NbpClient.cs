using System.Globalization;
using System.Net;
using System.Text.Json;
using CurrencyExchangeOffice.Contracts;

namespace CurrencyExchangeOffice.Service.Services;

public class NbpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public NbpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ExchangeRateDto>> GetCurrentRatesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await GetFromNbpAsync<List<NbpTable>>("exchangerates/tables/C?format=json", cancellationToken);
        var table = tables.FirstOrDefault() ?? throw new InvalidOperationException("NBP did not return Table C data.");

        return table.Rates
            .OrderBy(rate => rate.Code)
            .Select(rate => new ExchangeRateDto
            {
                CurrencyCode = rate.Code.ToUpperInvariant(),
                CurrencyName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rate.Currency),
                Bid = rate.Bid,
                Ask = rate.Ask,
                EffectiveDate = DateTime.Parse(table.EffectiveDate, CultureInfo.InvariantCulture)
            })
            .ToList();
    }

    public async Task<ExchangeRateDto?> GetRateAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(currencyCode);

        if (normalizedCode == "PLN")
        {
            return new ExchangeRateDto
            {
                CurrencyCode = "PLN",
                CurrencyName = "Polish Zloty",
                Bid = 1m,
                Ask = 1m,
                EffectiveDate = DateTime.Today
            };
        }

        try
        {
            var response = await GetFromNbpAsync<NbpRateSeries>($"exchangerates/rates/C/{normalizedCode}?format=json", cancellationToken);
            var latest = response.Rates.LastOrDefault();
            if (latest is null)
            {
                return null;
            }

            return new ExchangeRateDto
            {
                CurrencyCode = response.Code.ToUpperInvariant(),
                CurrencyName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(response.Currency),
                Bid = latest.Bid,
                Ask = latest.Ask,
                EffectiveDate = DateTime.Parse(latest.EffectiveDate, CultureInfo.InvariantCulture)
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<HistoricalRateDto>> GetHistoricalRatesAsync(
        string currencyCode,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(currencyCode);
        var start = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        try
        {
            var response = await GetFromNbpAsync<NbpRateSeries>($"exchangerates/rates/C/{normalizedCode}/{start}/{end}?format=json", cancellationToken);
            return response.Rates
                .Select(rate => new HistoricalRateDto
                {
                    CurrencyCode = response.Code.ToUpperInvariant(),
                    RateDate = DateTime.Parse(rate.EffectiveDate, CultureInfo.InvariantCulture),
                    Bid = rate.Bid,
                    Ask = rate.Ask
                })
                .ToList();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }
    }

    public static string NormalizeCode(string currencyCode)
    {
        return (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
    }

    private async Task<T> GetFromNbpAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("NBP returned an empty response.");
    }

    private sealed class NbpTable
    {
        public string EffectiveDate { get; set; } = string.Empty;
        public List<NbpRate> Rates { get; set; } = [];
    }

    private sealed class NbpRateSeries
    {
        public string Currency { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public List<NbpRate> Rates { get; set; } = [];
    }

    private sealed class NbpRate
    {
        public string Currency { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string EffectiveDate { get; set; } = string.Empty;
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
    }
}
