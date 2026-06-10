using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace CurrencyExchangeOffice.Client;

public class SoapServiceClient
{
    private const string ServiceNamespace = "http://currencyexchangeoffice/service";
    private readonly HttpClient _httpClient;
    private readonly string _endpointUrl;

    public SoapServiceClient(string endpointUrl)
    {
        _endpointUrl = endpointUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public Task<BasicResult> PingAsync()
    {
        return InvokeBasicAsync("Ping");
    }

    public Task<UserResult> RegisterUserAsync(string username, string password, string fullName)
    {
        return InvokeUserAsync("RegisterUser", new()
        {
            ["username"] = username,
            ["password"] = password,
            ["fullName"] = fullName
        });
    }

    public Task<UserResult> LoginAsync(string username, string password)
    {
        return InvokeUserAsync("Login", new()
        {
            ["username"] = username,
            ["password"] = password
        });
    }

    public async Task<List<string>> GetSupportedCurrenciesAsync()
    {
        var result = await InvokeAsync("GetSupportedCurrencies");
        EnsureSuccess(result);
        return result.Descendants()
            .Where(element => element.Name.LocalName == "string")
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .ToList();
    }

    public async Task<List<RateRow>> GetCurrentRatesAsync()
    {
        var result = await InvokeAsync("GetCurrentRates");
        EnsureSuccess(result);
        return result.Descendants()
            .Where(element => element.Name.LocalName == "ExchangeRateDto")
            .Select(ParseRate)
            .ToList();
    }

    public async Task<BasicResult> TopUpBalanceAsync(int userId, string currencyCode, decimal amount)
    {
        return await InvokeBasicAsync("TopUpBalance", new()
        {
            ["userId"] = userId,
            ["currencyCode"] = currencyCode,
            ["amount"] = amount
        });
    }

    public async Task<List<BalanceRow>> GetWalletAsync(int userId)
    {
        var result = await InvokeAsync("GetWallet", new() { ["userId"] = userId });
        EnsureSuccess(result);
        return result.Descendants()
            .Where(element => element.Name.LocalName == "BalanceDto")
            .Select(element => new BalanceRow(Get(element, "CurrencyCode"), ReadDecimal(element, "Amount")))
            .ToList();
    }

    public Task<BasicResult> BuyCurrencyAsync(int userId, string targetCurrencyCode, decimal plnAmount)
    {
        return InvokeBasicAsync("BuyCurrency", new()
        {
            ["userId"] = userId,
            ["targetCurrencyCode"] = targetCurrencyCode,
            ["plnAmount"] = plnAmount
        });
    }

    public Task<BasicResult> SellCurrencyAsync(int userId, string sourceCurrencyCode, decimal amount)
    {
        return InvokeBasicAsync("SellCurrency", new()
        {
            ["userId"] = userId,
            ["sourceCurrencyCode"] = sourceCurrencyCode,
            ["amount"] = amount
        });
    }

    public async Task<List<TransactionRow>> GetTransactionHistoryAsync(int userId)
    {
        var result = await InvokeAsync("GetTransactionHistory", new() { ["userId"] = userId });
        EnsureSuccess(result);
        return result.Descendants()
            .Where(element => element.Name.LocalName == "TransactionDto")
            .Select(element => new TransactionRow(
                ReadInt(element, "Id"),
                ReadDate(element, "CreatedAt"),
                Get(element, "Type"),
                Get(element, "SourceCurrency"),
                Get(element, "TargetCurrency"),
                ReadDecimal(element, "SourceAmount"),
                ReadDecimal(element, "TargetAmount"),
                ReadDecimal(element, "Rate"),
                Get(element, "Description")))
            .ToList();
    }

    public async Task<List<HistoricalRateRow>> GetHistoricalRatesAsync(string currencyCode, DateTime startDate, DateTime endDate)
    {
        var result = await InvokeAsync("GetHistoricalRates", new()
        {
            ["currencyCode"] = currencyCode,
            ["startDate"] = startDate,
            ["endDate"] = endDate
        });
        EnsureSuccess(result);

        return result.Descendants()
            .Where(element => element.Name.LocalName == "HistoricalRateDto")
            .Select(element => new HistoricalRateRow(
                Get(element, "CurrencyCode"),
                ReadDate(element, "RateDate"),
                ReadDecimal(element, "Bid"),
                ReadDecimal(element, "Ask")))
            .ToList();
    }

    private async Task<BasicResult> InvokeBasicAsync(string operation, Dictionary<string, object?>? parameters = null)
    {
        var result = await InvokeAsync(operation, parameters);
        return ParseBasic(result);
    }

    private async Task<UserResult> InvokeUserAsync(string operation, Dictionary<string, object?> parameters)
    {
        var result = await InvokeAsync(operation, parameters);
        var basic = ParseBasic(result);
        return new UserResult(
            basic.Success,
            basic.Message,
            ReadInt(result, "UserId"),
            Get(result, "Username"),
            Get(result, "FullName"));
    }

    private async Task<XElement> InvokeAsync(string operation, Dictionary<string, object?>? parameters = null)
    {
        var envelope = BuildEnvelope(operation, parameters ?? []);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
        request.Headers.Add("SOAPAction", $"\"{ServiceNamespace}/ICurrencyExchangeService/{operation}\"");
        request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8");

        using var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"SOAP request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{content}");
        }

        var document = XDocument.Parse(content);
        var fault = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Fault");
        if (fault is not null)
        {
            throw new InvalidOperationException(fault.Value.Trim());
        }

        return document.Descendants().FirstOrDefault(element => element.Name.LocalName == operation + "Result")
               ?? throw new InvalidOperationException($"SOAP response did not contain {operation}Result.");
    }

    private static string BuildEnvelope(string operation, Dictionary<string, object?> parameters)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace service = ServiceNamespace;

        var operationElement = new XElement(service + operation,
            parameters.Select(parameter => new XElement(service + parameter.Key, FormatValue(parameter.Value))));

        return new XDocument(
            new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "s", soap),
                new XElement(soap + "Body", operationElement)))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            DateTime date => date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static void EnsureSuccess(XElement result)
    {
        var basic = ParseBasic(result);
        if (!basic.Success)
        {
            throw new InvalidOperationException(basic.Message);
        }
    }

    private static BasicResult ParseBasic(XElement result)
    {
        return new BasicResult(ReadBool(result, "Success"), Get(result, "Message"));
    }

    private static RateRow ParseRate(XElement element)
    {
        return new RateRow(
            Get(element, "CurrencyCode"),
            Get(element, "CurrencyName"),
            ReadDecimal(element, "Bid"),
            ReadDecimal(element, "Ask"),
            ReadDate(element, "EffectiveDate"));
    }

    private static string Get(XElement element, string localName)
    {
        return element.Descendants().FirstOrDefault(child => child.Name.LocalName == localName)?.Value ?? string.Empty;
    }

    private static bool ReadBool(XElement element, string localName)
    {
        return bool.TryParse(Get(element, localName), out var value) && value;
    }

    private static int ReadInt(XElement element, string localName)
    {
        return int.TryParse(Get(element, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static decimal ReadDecimal(XElement element, string localName)
    {
        return decimal.TryParse(Get(element, localName), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private static DateTime ReadDate(XElement element, string localName)
    {
        return DateTime.TryParse(Get(element, localName), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value)
            ? value
            : DateTime.MinValue;
    }
}

public record BasicResult(bool Success, string Message);
public record UserResult(bool Success, string Message, int UserId, string Username, string FullName);
public record BalanceRow(string CurrencyCode, decimal Amount);
public record RateRow(string CurrencyCode, string CurrencyName, decimal Bid, decimal Ask, DateTime EffectiveDate);
public record HistoricalRateRow(string CurrencyCode, DateTime RateDate, decimal Bid, decimal Ask);
public record TransactionRow(
    int Id,
    DateTime CreatedAt,
    string Type,
    string SourceCurrency,
    string TargetCurrency,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal Rate,
    string Description);
