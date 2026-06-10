using CurrencyExchangeOffice.Contracts;
using Microsoft.Data.Sqlite;

namespace CurrencyExchangeOffice.Service.Data;

public class CurrencyRepository
{
    private readonly string _connectionString;

    public CurrencyRepository(IConfiguration configuration)
    {
        var databasePath = configuration.GetValue<string>("DatabasePath") ?? "currency-exchange.db";
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public void InitializeDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordSalt TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                FullName TEXT NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS Balances (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                CurrencyCode TEXT NOT NULL,
                Amount REAL NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE (UserId, CurrencyCode),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Type TEXT NOT NULL,
                SourceCurrency TEXT NOT NULL,
                TargetCurrency TEXT NOT NULL,
                SourceAmount REAL NOT NULL,
                TargetAmount REAL NOT NULL,
                Rate REAL NOT NULL,
                Description TEXT NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Balances_UserId ON Balances(UserId);
            CREATE INDEX IF NOT EXISTS IX_Transactions_UserId_CreatedAt ON Transactions(UserId, CreatedAt DESC);
            """;
        command.ExecuteNonQuery();
    }

    public void SeedDemoUser()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var userCommand = connection.CreateCommand())
        {
            userCommand.Transaction = transaction;
            userCommand.CommandText = """
                INSERT OR IGNORE INTO Users (Id, Username, PasswordSalt, PasswordHash, FullName)
                VALUES (1, 'demo', 'demo-salt-64526', '98b42914ad06b93c94bd929c75e3dc579dc2206c323497ed3d96026b64fbc511', 'Demo User');
                """;
            userCommand.ExecuteNonQuery();
        }

        UpsertBalance(connection, transaction, 1, "PLN", 10000m, replaceAmount: true);
        UpsertBalance(connection, transaction, 1, "USD", 50m, replaceAmount: true);
        UpsertBalance(connection, transaction, 1, "EUR", 25m, replaceAmount: true);

        transaction.Commit();
    }

    public bool UserExists(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", userId);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public bool UsernameExists(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Users WHERE lower(Username) = lower($username);";
        command.Parameters.AddWithValue("$username", username);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public int CreateUser(string username, string salt, string hash, string fullName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Username, PasswordSalt, PasswordHash, FullName)
            VALUES ($username, $salt, $hash, $fullName);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$salt", salt);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$fullName", fullName);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public UserRecord? GetUserByUsername(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, FullName, PasswordSalt, PasswordHash
            FROM Users
            WHERE lower(Username) = lower($username);
            """;
        command.Parameters.AddWithValue("$username", username);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new UserRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4));
    }

    public List<BalanceDto> GetWallet(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CurrencyCode, Amount
            FROM Balances
            WHERE UserId = $userId
            ORDER BY CASE CurrencyCode WHEN 'PLN' THEN 0 ELSE 1 END, CurrencyCode;
            """;
        command.Parameters.AddWithValue("$userId", userId);

        using var reader = command.ExecuteReader();
        var balances = new List<BalanceDto>();
        while (reader.Read())
        {
            balances.Add(new BalanceDto
            {
                CurrencyCode = reader.GetString(0),
                Amount = Convert.ToDecimal(reader.GetDouble(1))
            });
        }

        return balances;
    }

    public void TopUpBalance(int userId, string currencyCode, decimal amount)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertBalance(connection, transaction, userId, currencyCode, amount, replaceAmount: false);
        InsertTransaction(
            connection,
            transaction,
            userId,
            "TOP_UP",
            currencyCode,
            currencyCode,
            amount,
            amount,
            1m,
            $"Top-up {amount:F2} {currencyCode}");
        transaction.Commit();
    }

    public bool BuyCurrency(int userId, string targetCurrencyCode, decimal plnAmount, decimal askRate, decimal targetAmount)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var plnBalance = GetBalance(connection, transaction, userId, "PLN");
        if (plnBalance < plnAmount)
        {
            return false;
        }

        UpdateBalance(connection, transaction, userId, "PLN", plnBalance - plnAmount);
        UpsertBalance(connection, transaction, userId, targetCurrencyCode, targetAmount, replaceAmount: false);
        InsertTransaction(
            connection,
            transaction,
            userId,
            "BUY",
            "PLN",
            targetCurrencyCode,
            plnAmount,
            targetAmount,
            askRate,
            $"Bought {targetAmount:F4} {targetCurrencyCode} for {plnAmount:F2} PLN");

        transaction.Commit();
        return true;
    }

    public bool SellCurrency(int userId, string sourceCurrencyCode, decimal sourceAmount, decimal bidRate, decimal plnAmount)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var sourceBalance = GetBalance(connection, transaction, userId, sourceCurrencyCode);
        if (sourceBalance < sourceAmount)
        {
            return false;
        }

        UpdateBalance(connection, transaction, userId, sourceCurrencyCode, sourceBalance - sourceAmount);
        UpsertBalance(connection, transaction, userId, "PLN", plnAmount, replaceAmount: false);
        InsertTransaction(
            connection,
            transaction,
            userId,
            "SELL",
            sourceCurrencyCode,
            "PLN",
            sourceAmount,
            plnAmount,
            bidRate,
            $"Sold {sourceAmount:F4} {sourceCurrencyCode} for {plnAmount:F2} PLN");

        transaction.Commit();
        return true;
    }

    public List<TransactionDto> GetTransactionHistory(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CreatedAt, Type, SourceCurrency, TargetCurrency, SourceAmount, TargetAmount, Rate, Description
            FROM Transactions
            WHERE UserId = $userId
            ORDER BY datetime(CreatedAt) DESC, Id DESC;
            """;
        command.Parameters.AddWithValue("$userId", userId);

        using var reader = command.ExecuteReader();
        var transactions = new List<TransactionDto>();
        while (reader.Read())
        {
            transactions.Add(new TransactionDto
            {
                Id = reader.GetInt32(0),
                CreatedAt = DateTime.Parse(reader.GetString(1)),
                Type = reader.GetString(2),
                SourceCurrency = reader.GetString(3),
                TargetCurrency = reader.GetString(4),
                SourceAmount = Convert.ToDecimal(reader.GetDouble(5)),
                TargetAmount = Convert.ToDecimal(reader.GetDouble(6)),
                Rate = Convert.ToDecimal(reader.GetDouble(7)),
                Description = reader.GetString(8)
            });
        }

        return transactions;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    private static decimal GetBalance(SqliteConnection connection, SqliteTransaction transaction, int userId, string currencyCode)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Amount FROM Balances WHERE UserId = $userId AND CurrencyCode = $currencyCode;";
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$currencyCode", currencyCode);
        var value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
    }

    private static void UpdateBalance(SqliteConnection connection, SqliteTransaction transaction, int userId, string currencyCode, decimal amount)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Balances
            SET Amount = $amount,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE UserId = $userId AND CurrencyCode = $currencyCode;
            """;
        command.Parameters.AddWithValue("$amount", amount);
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$currencyCode", currencyCode);
        command.ExecuteNonQuery();
    }

    private static void UpsertBalance(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int userId,
        string currencyCode,
        decimal amount,
        bool replaceAmount)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = replaceAmount
            ? """
              INSERT INTO Balances (UserId, CurrencyCode, Amount)
              VALUES ($userId, $currencyCode, $amount)
              ON CONFLICT(UserId, CurrencyCode)
              DO UPDATE SET Amount = $amount, UpdatedAt = CURRENT_TIMESTAMP;
              """
            : """
              INSERT INTO Balances (UserId, CurrencyCode, Amount)
              VALUES ($userId, $currencyCode, $amount)
              ON CONFLICT(UserId, CurrencyCode)
              DO UPDATE SET Amount = Amount + $amount, UpdatedAt = CURRENT_TIMESTAMP;
              """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$currencyCode", currencyCode);
        command.Parameters.AddWithValue("$amount", amount);
        command.ExecuteNonQuery();
    }

    private static void InsertTransaction(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int userId,
        string type,
        string sourceCurrency,
        string targetCurrency,
        decimal sourceAmount,
        decimal targetAmount,
        decimal rate,
        string description)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Transactions
                (UserId, Type, SourceCurrency, TargetCurrency, SourceAmount, TargetAmount, Rate, Description)
            VALUES
                ($userId, $type, $sourceCurrency, $targetCurrency, $sourceAmount, $targetAmount, $rate, $description);
            """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$sourceCurrency", sourceCurrency);
        command.Parameters.AddWithValue("$targetCurrency", targetCurrency);
        command.Parameters.AddWithValue("$sourceAmount", sourceAmount);
        command.Parameters.AddWithValue("$targetAmount", targetAmount);
        command.Parameters.AddWithValue("$rate", rate);
        command.Parameters.AddWithValue("$description", description);
        command.ExecuteNonQuery();
    }
}

public record UserRecord(int Id, string Username, string FullName, string PasswordSalt, string PasswordHash);
