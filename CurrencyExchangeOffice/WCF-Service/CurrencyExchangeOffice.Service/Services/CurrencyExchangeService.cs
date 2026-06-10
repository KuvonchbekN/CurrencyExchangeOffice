using CurrencyExchangeOffice.Contracts;
using CurrencyExchangeOffice.Service.Data;

namespace CurrencyExchangeOffice.Service.Services;

public class CurrencyExchangeService : ICurrencyExchangeService
{
    private static readonly string[] FallbackCurrencies = ["PLN", "USD", "EUR", "GBP", "CHF", "CZK"];

    private readonly CurrencyRepository _repository;
    private readonly PasswordHasher _passwordHasher;
    private readonly NbpClient _nbpClient;
    private readonly ILogger<CurrencyExchangeService> _logger;

    public CurrencyExchangeService(
        CurrencyRepository repository,
        PasswordHasher passwordHasher,
        NbpClient nbpClient,
        ILogger<CurrencyExchangeService> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _nbpClient = nbpClient;
        _logger = logger;
    }

    public ServiceResponse Ping()
    {
        return Ok("Currency Exchange Office SOAP service is running.");
    }

    public UserResponse RegisterUser(string username, string password, string fullName)
    {
        username = (username ?? string.Empty).Trim();
        fullName = (fullName ?? string.Empty).Trim();

        if (username.Length < 3)
        {
            return UserFail("Username must contain at least 3 characters.");
        }

        if ((password ?? string.Empty).Length < 6)
        {
            return UserFail("Password must contain at least 6 characters.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return UserFail("Full name is required.");
        }

        if (_repository.UsernameExists(username))
        {
            return UserFail("This username is already taken.");
        }

        var salt = _passwordHasher.CreateSalt();
        var hash = _passwordHasher.HashPassword(password ?? string.Empty, salt);
        var userId = _repository.CreateUser(username, salt, hash, fullName);
        _repository.TopUpBalance(userId, "PLN", 0m);

        return new UserResponse
        {
            Success = true,
            Message = "Registration successful.",
            UserId = userId,
            Username = username,
            FullName = fullName
        };
    }

    public UserResponse Login(string username, string password)
    {
        username = (username ?? string.Empty).Trim();
        var user = _repository.GetUserByUsername(username);

        if (user is null || !_passwordHasher.Verify(password ?? string.Empty, user.PasswordSalt, user.PasswordHash))
        {
            return UserFail("Invalid username or password.");
        }

        return new UserResponse
        {
            Success = true,
            Message = "Login successful.",
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName
        };
    }

    public SupportedCurrenciesResponse GetSupportedCurrencies()
    {
        try
        {
            var codes = _nbpClient.GetCurrentRatesAsync().GetAwaiter().GetResult()
                .Select(rate => rate.CurrencyCode)
                .Prepend("PLN")
                .Distinct()
                .OrderBy(code => code == "PLN" ? "000" : code)
                .ToList();

            return new SupportedCurrenciesResponse
            {
                Success = true,
                Message = "Supported currencies loaded from NBP Table C.",
                Currencies = codes
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load supported currencies from NBP.");
            return new SupportedCurrenciesResponse
            {
                Success = true,
                Message = "NBP is unavailable. Showing common demo currencies.",
                Currencies = FallbackCurrencies.ToList()
            };
        }
    }

    public RatesResponse GetCurrentRates()
    {
        try
        {
            return new RatesResponse
            {
                Success = true,
                Message = "Current NBP Table C rates loaded.",
                Rates = _nbpClient.GetCurrentRatesAsync().GetAwaiter().GetResult()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NBP current rates failed.");
            return new RatesResponse { Success = false, Message = "Could not load current rates from NBP." };
        }
    }

    public RateResponse GetRate(string currencyCode)
    {
        currencyCode = NbpClient.NormalizeCode(currencyCode);
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return new RateResponse { Success = false, Message = "Currency code is required." };
        }

        try
        {
            var rate = _nbpClient.GetRateAsync(currencyCode).GetAwaiter().GetResult();
            return rate is null
                ? new RateResponse { Success = false, Message = $"Currency '{currencyCode}' is not available in NBP Table C." }
                : new RateResponse { Success = true, Message = "Rate loaded.", Rate = rate };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NBP rate lookup failed for {CurrencyCode}.", currencyCode);
            return new RateResponse { Success = false, Message = $"Could not load rate for {currencyCode}." };
        }
    }

    public HistoricalRatesResponse GetHistoricalRates(string currencyCode, DateTime startDate, DateTime endDate)
    {
        currencyCode = NbpClient.NormalizeCode(currencyCode);

        if (currencyCode == "PLN")
        {
            return new HistoricalRatesResponse { Success = false, Message = "Historical NBP Table C lookup is only for foreign currencies." };
        }

        var validationMessage = ValidateDateRange(startDate, endDate);
        if (validationMessage is not null)
        {
            return new HistoricalRatesResponse { Success = false, Message = validationMessage };
        }

        try
        {
            var rates = _nbpClient.GetHistoricalRatesAsync(currencyCode, startDate.Date, endDate.Date).GetAwaiter().GetResult();
            return rates.Count == 0
                ? new HistoricalRatesResponse { Success = false, Message = "No historical rates found for this currency/date range." }
                : new HistoricalRatesResponse { Success = true, Message = "Historical rates loaded.", Rates = rates };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NBP historical rates failed for {CurrencyCode}.", currencyCode);
            return new HistoricalRatesResponse { Success = false, Message = "Could not load historical rates from NBP." };
        }
    }

    public ServiceResponse TopUpBalance(int userId, string currencyCode, decimal amount)
    {
        currencyCode = NbpClient.NormalizeCode(currencyCode);

        var validation = ValidateUserCurrencyAndAmount(userId, currencyCode, amount, allowPln: true);
        if (!validation.Success)
        {
            return validation;
        }

        _repository.TopUpBalance(userId, currencyCode, amount);
        return Ok($"Balance topped up by {amount:F2} {currencyCode}.");
    }

    public WalletResponse GetWallet(int userId)
    {
        if (!_repository.UserExists(userId))
        {
            return new WalletResponse { Success = false, Message = "User does not exist." };
        }

        return new WalletResponse
        {
            Success = true,
            Message = "Wallet loaded.",
            Balances = _repository.GetWallet(userId)
        };
    }

    public ServiceResponse BuyCurrency(int userId, string targetCurrencyCode, decimal plnAmount)
    {
        targetCurrencyCode = NbpClient.NormalizeCode(targetCurrencyCode);
        var validation = ValidateUserCurrencyAndAmount(userId, targetCurrencyCode, plnAmount, allowPln: false);
        if (!validation.Success)
        {
            return validation;
        }

        var rateResponse = GetRate(targetCurrencyCode);
        if (!rateResponse.Success || rateResponse.Rate is null)
        {
            return Fail(rateResponse.Message);
        }

        var targetAmount = Math.Round(plnAmount / rateResponse.Rate.Ask, 4);
        var completed = _repository.BuyCurrency(userId, targetCurrencyCode, plnAmount, rateResponse.Rate.Ask, targetAmount);

        return completed
            ? Ok($"Bought {targetAmount:F4} {targetCurrencyCode} for {plnAmount:F2} PLN.")
            : Fail("Insufficient PLN balance. Overdraft is not allowed.");
    }

    public ServiceResponse SellCurrency(int userId, string sourceCurrencyCode, decimal amount)
    {
        sourceCurrencyCode = NbpClient.NormalizeCode(sourceCurrencyCode);
        var validation = ValidateUserCurrencyAndAmount(userId, sourceCurrencyCode, amount, allowPln: false);
        if (!validation.Success)
        {
            return validation;
        }

        var rateResponse = GetRate(sourceCurrencyCode);
        if (!rateResponse.Success || rateResponse.Rate is null)
        {
            return Fail(rateResponse.Message);
        }

        var plnAmount = Math.Round(amount * rateResponse.Rate.Bid, 2);
        var completed = _repository.SellCurrency(userId, sourceCurrencyCode, amount, rateResponse.Rate.Bid, plnAmount);

        return completed
            ? Ok($"Sold {amount:F4} {sourceCurrencyCode} for {plnAmount:F2} PLN.")
            : Fail($"Insufficient {sourceCurrencyCode} balance. Overdraft is not allowed.");
    }

    public TransactionHistoryResponse GetTransactionHistory(int userId)
    {
        if (!_repository.UserExists(userId))
        {
            return new TransactionHistoryResponse { Success = false, Message = "User does not exist." };
        }

        return new TransactionHistoryResponse
        {
            Success = true,
            Message = "Transaction history loaded.",
            Transactions = _repository.GetTransactionHistory(userId)
        };
    }

    private ServiceResponse ValidateUserCurrencyAndAmount(int userId, string currencyCode, decimal amount, bool allowPln)
    {
        if (!_repository.UserExists(userId))
        {
            return Fail("User does not exist.");
        }

        if (amount <= 0)
        {
            return Fail("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return Fail("Currency code is required.");
        }

        if (!allowPln && currencyCode == "PLN")
        {
            return Fail("Use a foreign currency for buy/sell operations.");
        }

        if (currencyCode != "PLN" && GetRate(currencyCode).Rate is null)
        {
            return Fail($"Currency '{currencyCode}' is not supported by NBP Table C.");
        }

        return Ok("Validation passed.");
    }

    private static string? ValidateDateRange(DateTime startDate, DateTime endDate)
    {
        if (startDate == default || endDate == default)
        {
            return "Both start date and end date are required.";
        }

        if (startDate.Date > endDate.Date)
        {
            return "Start date cannot be after end date.";
        }

        if (endDate.Date > DateTime.Today)
        {
            return "End date cannot be in the future.";
        }

        if ((endDate.Date - startDate.Date).TotalDays > 93)
        {
            return "NBP allows a maximum 93-day range for one historical rates request.";
        }

        return null;
    }

    private static ServiceResponse Ok(string message)
    {
        return new ServiceResponse { Success = true, Message = message };
    }

    private static ServiceResponse Fail(string message)
    {
        return new ServiceResponse { Success = false, Message = message };
    }

    private static UserResponse UserFail(string message)
    {
        return new UserResponse { Success = false, Message = message };
    }
}
