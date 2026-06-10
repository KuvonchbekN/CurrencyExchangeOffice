# Currency Exchange Office System

Course: Network Application Development  
Author: Kuvonchbek Normakhmatov  
Student ID: 64526

## Short Description

Currency Exchange Office System is a distributed desktop application for managing currency wallets and exchanging money using real National Bank of Poland exchange rates. The project uses a SOAP/WCF-style CoreWCF service, a cross-platform Avalonia client, SQLite storage, and live NBP API integration.

## Technologies Used

- .NET 8
- CoreWCF with BasicHttpBinding for SOAP-style service communication
- Avalonia UI for the macOS-friendly desktop client
- SQLite with `Microsoft.Data.Sqlite`
- National Bank of Poland API, Table C buy/sell exchange rates
- VS Code compatible project and solution structure

## Architecture

The application is split into a client, a SOAP service, and a database.

- Client application: handles user input, tabs, forms, wallet/rate/history display, and SOAP requests.
- CoreWCF service: handles business logic, server-side validation, password hashing, currency conversion, NBP API calls, and database access.
- SQLite database: stores users, wallet balances, and exchange/top-up transactions.
- NBP API: provides current and historical exchange rates.

The client does not connect directly to the database or NBP API. All important operations go through the SOAP service.

## Repository Structure

```text
CurrencyExchangeOffice
├── WCF-Service
│   ├── CurrencyExchangeOffice.Contracts
│   └── CurrencyExchangeOffice.Service
├── Client-Application
│   └── CurrencyExchangeOffice.Client
├── Database
│   ├── schema.sql
│   └── seed-data.sql
├── Documentation
│   └── ProjectReport.md
├── CurrencyExchangeOffice.sln
└── README.md
```

## Features

- User registration
- User login
- Salted SHA-256 password hashing
- PLN and currency wallet display
- PLN balance top-up
- Current NBP Table C exchange rates
- Buy foreign currency using PLN
- Sell foreign currency for PLN
- Transaction history per user
- Historical exchange rates by currency and date range
- SOAP service ping/status check
- Server-side validation for invalid users, currencies, dates, negative amounts, and overdrafts

## Database Schema

- `Users`: stores account information, password salt, password hash, full name, and creation date.
- `Balances`: stores wallet balances per user and currency code.
- `Transactions`: stores top-up, buy, and sell operations with amounts, currencies, rate, description, and timestamp.

The SQL scripts are in `Database/schema.sql` and `Database/seed-data.sql`. The service also creates and seeds the local SQLite database automatically at startup.

## Demo Login Credentials

- Username: `demo`
- Password: `demo123`

The demo user starts with `10000 PLN`, `50 USD`, and `25 EUR`.

## How To Run On macOS Using VS Code

Open the `CurrencyExchangeOffice` folder in VS Code.

Restore and build:

```bash
dotnet restore
dotnet build
```

Start the SOAP service:

```bash
dotnet run --project WCF-Service/CurrencyExchangeOffice.Service/CurrencyExchangeOffice.Service.csproj --urls http://localhost:5000
```

The service endpoint is:

```text
http://localhost:5000/CurrencyExchangeService.svc
```

The WSDL metadata endpoint is:

```text
http://localhost:5000/CurrencyExchangeService.svc?wsdl
```

In a second terminal, start the client application:

```bash
dotnet run --project Client-Application/CurrencyExchangeOffice.Client/CurrencyExchangeOffice.Client.csproj
```

Use the demo account or register a new user.

## Service Operations

- `Ping()`
- `RegisterUser(username, password, fullName)`
- `Login(username, password)`
- `GetSupportedCurrencies()`
- `GetCurrentRates()`
- `GetRate(currencyCode)`
- `GetHistoricalRates(currencyCode, startDate, endDate)`
- `TopUpBalance(userId, currencyCode, amount)`
- `GetWallet(userId)`
- `BuyCurrency(userId, targetCurrencyCode, plnAmount)`
- `SellCurrency(userId, sourceCurrencyCode, amount)`
- `GetTransactionHistory(userId)`

## Known Limitations And Future Improvements

- The client uses a simple hand-written SOAP client to keep the project Mac-friendly and easy to understand.
- The database is local SQLite, so it is intended for demo/single-machine usage.
- NBP Table C historical API requests are limited to 93 days per request.
- A future version could add admin tools, richer charts, export to CSV, and generated WCF proxy support.
