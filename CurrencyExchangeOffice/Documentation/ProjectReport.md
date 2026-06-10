# Project Report

Course: Network Application Development  
Project title: Currency Exchange Office System  
Author: Kuvonchbek Normakhmatov  
Student ID: 64526

## Project Overview

Currency Exchange Office System is a distributed application that demonstrates network service communication, a client UI, database integration, and an external public API. The system lets users register, log in, top up PLN, view wallet balances, check exchange rates, buy and sell currencies, view transaction history, and load historical NBP rates.

The project is designed for macOS and VS Code. It avoids Windows-only WPF and .NET Framework technology while still using SOAP/WCF-style communication through CoreWCF.

## Architecture

The system has three main parts:

- `CurrencyExchangeOffice.Contracts`: shared SOAP service contract and data transfer objects.
- `CurrencyExchangeOffice.Service`: CoreWCF SOAP service, business logic, validation, database access, password hashing, and NBP integration.
- `CurrencyExchangeOffice.Client`: Avalonia desktop UI that communicates with the service through SOAP requests.

The client only handles presentation and user interaction. The service owns all business rules and protects the database from invalid operations.

## SOAP/CoreWCF Service Operations

The service exposes a BasicHttp SOAP endpoint at `/CurrencyExchangeService.svc`.

- `Ping()` checks service availability.
- `RegisterUser(username, password, fullName)` creates a new user with salted SHA-256 password hashing.
- `Login(username, password)` verifies credentials and returns the user ID.
- `GetSupportedCurrencies()` returns PLN plus currencies available from NBP Table C.
- `GetCurrentRates()` returns current buy/sell rates from NBP Table C.
- `GetRate(currencyCode)` returns the current rate for one currency.
- `GetHistoricalRates(currencyCode, startDate, endDate)` returns historical buy/sell rates.
- `TopUpBalance(userId, currencyCode, amount)` adds funds to a wallet.
- `GetWallet(userId)` returns wallet balances.
- `BuyCurrency(userId, targetCurrencyCode, plnAmount)` exchanges PLN into a foreign currency.
- `SellCurrency(userId, sourceCurrencyCode, amount)` exchanges a foreign currency into PLN.
- `GetTransactionHistory(userId)` returns user transactions.

## Client Application Description

The client is a cross-platform Avalonia desktop application. It contains tabs for:

- Login / Register
- Dashboard / Wallet
- Top Up
- Exchange Rates
- Buy / Sell Currency
- Transaction History
- Historical Rates

Currency fields use dropdowns loaded from the service. The client shows success and error messages in the header area and refreshes wallet/history after successful operations.

## Database Design

SQLite is used for local macOS-friendly storage.

- `Users`: stores `Id`, `Username`, `PasswordSalt`, `PasswordHash`, `FullName`, and `CreatedAt`.
- `Balances`: stores `UserId`, `CurrencyCode`, `Amount`, and `UpdatedAt`.
- `Transactions`: stores operation type, source/target currencies, source/target amounts, rate, description, and timestamp.

Buy and sell operations use database transactions so balance updates and transaction inserts happen atomically.

## NBP API Integration

The service integrates with the National Bank of Poland API:

- Current rates use Table C: `https://api.nbp.pl/api/exchangerates/tables/C`
- Single-currency lookup uses Table C rates by currency code.
- Historical rates use the Table C historical endpoint.

The service handles invalid currency codes, unavailable data, API failures, future dates, reversed date ranges, and the NBP historical 93-day range limit.

## Testing/Demo Scenario

1. Start the service from VS Code terminal.
2. Start the Avalonia client from a second terminal.
3. Use the demo credentials: `demo` / `demo123`.
4. Ping the service from the login screen.
5. Open the wallet tab and confirm PLN, USD, and EUR balances.
6. Top up PLN.
7. Load current NBP exchange rates.
8. Buy USD or EUR using PLN.
9. Sell part of the foreign currency back to PLN.
10. Open transaction history and confirm the operations were recorded.
11. Open historical rates and load rates for a foreign currency over a valid date range.

## Conclusion

The project satisfies the main requirements of the Network Application Development module: a SOAP/CoreWCF network service, a cross-platform client application with UI, SQLite database integration, NBP API integration, server-side validation, and documentation suitable for a public GitHub repository.
