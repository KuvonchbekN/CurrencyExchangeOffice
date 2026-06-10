using System.Runtime.Serialization;
using CoreWCF;

namespace CurrencyExchangeOffice.Contracts;

[ServiceContract(Namespace = ServiceNamespace)]
public interface ICurrencyExchangeService
{
    public const string ServiceNamespace = "http://currencyexchangeoffice/service";

    [OperationContract]
    ServiceResponse Ping();

    [OperationContract]
    UserResponse RegisterUser(string username, string password, string fullName);

    [OperationContract]
    UserResponse Login(string username, string password);

    [OperationContract]
    SupportedCurrenciesResponse GetSupportedCurrencies();

    [OperationContract]
    RatesResponse GetCurrentRates();

    [OperationContract]
    RateResponse GetRate(string currencyCode);

    [OperationContract]
    HistoricalRatesResponse GetHistoricalRates(string currencyCode, DateTime startDate, DateTime endDate);

    [OperationContract]
    ServiceResponse TopUpBalance(int userId, string currencyCode, decimal amount);

    [OperationContract]
    WalletResponse GetWallet(int userId);

    [OperationContract]
    ServiceResponse BuyCurrency(int userId, string targetCurrencyCode, decimal plnAmount);

    [OperationContract]
    ServiceResponse SellCurrency(int userId, string sourceCurrencyCode, decimal amount);

    [OperationContract]
    TransactionHistoryResponse GetTransactionHistory(int userId);
}

[DataContract]
public class ServiceResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;
}

[DataContract]
public class UserResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public int UserId { get; set; }

    [DataMember(Order = 4)]
    public string Username { get; set; } = string.Empty;

    [DataMember(Order = 5)]
    public string FullName { get; set; } = string.Empty;
}

[DataContract]
public class SupportedCurrenciesResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public List<string> Currencies { get; set; } = [];
}

[DataContract]
public class RateResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public ExchangeRateDto? Rate { get; set; }
}

[DataContract]
public class RatesResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public List<ExchangeRateDto> Rates { get; set; } = [];
}

[DataContract]
public class HistoricalRatesResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public List<HistoricalRateDto> Rates { get; set; } = [];
}

[DataContract]
public class WalletResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public List<BalanceDto> Balances { get; set; } = [];
}

[DataContract]
public class TransactionHistoryResponse
{
    [DataMember(Order = 1)]
    public bool Success { get; set; }

    [DataMember(Order = 2)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public List<TransactionDto> Transactions { get; set; } = [];
}

[DataContract]
public class ExchangeRateDto
{
    [DataMember(Order = 1)]
    public string CurrencyCode { get; set; } = string.Empty;

    [DataMember(Order = 2)]
    public string CurrencyName { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public decimal Bid { get; set; }

    [DataMember(Order = 4)]
    public decimal Ask { get; set; }

    [DataMember(Order = 5)]
    public DateTime EffectiveDate { get; set; }
}

[DataContract]
public class HistoricalRateDto
{
    [DataMember(Order = 1)]
    public string CurrencyCode { get; set; } = string.Empty;

    [DataMember(Order = 2)]
    public DateTime RateDate { get; set; }

    [DataMember(Order = 3)]
    public decimal Bid { get; set; }

    [DataMember(Order = 4)]
    public decimal Ask { get; set; }
}

[DataContract]
public class BalanceDto
{
    [DataMember(Order = 1)]
    public string CurrencyCode { get; set; } = string.Empty;

    [DataMember(Order = 2)]
    public decimal Amount { get; set; }
}

[DataContract]
public class TransactionDto
{
    [DataMember(Order = 1)]
    public int Id { get; set; }

    [DataMember(Order = 2)]
    public DateTime CreatedAt { get; set; }

    [DataMember(Order = 3)]
    public string Type { get; set; } = string.Empty;

    [DataMember(Order = 4)]
    public string SourceCurrency { get; set; } = string.Empty;

    [DataMember(Order = 5)]
    public string TargetCurrency { get; set; } = string.Empty;

    [DataMember(Order = 6)]
    public decimal SourceAmount { get; set; }

    [DataMember(Order = 7)]
    public decimal TargetAmount { get; set; }

    [DataMember(Order = 8)]
    public decimal Rate { get; set; }

    [DataMember(Order = 9)]
    public string Description { get; set; } = string.Empty;
}
