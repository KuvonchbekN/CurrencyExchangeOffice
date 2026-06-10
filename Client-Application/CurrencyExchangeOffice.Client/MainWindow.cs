using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CurrencyExchangeOffice.Client;

public class MainWindow : Window
{
    private readonly SoapServiceClient _service = new("http://localhost:5000/CurrencyExchangeService.svc");
    private readonly List<string> _currencies = ["PLN", "USD", "EUR"];

    private int? _userId;
    private string _userName = string.Empty;

    private readonly TextBlock _statusText = new() { Text = "Start the SOAP service, then log in.", Foreground = Brushes.DimGray };
    private readonly TextBlock _userText = new() { Text = "Not logged in", FontWeight = FontWeight.SemiBold };

    private readonly TextBox _loginUsername = new() { PlaceholderText = "Username", Text = "demo" };
    private readonly TextBox _registerUsername = new() { PlaceholderText = "Username" };
    private readonly TextBox _registerFullName = new() { PlaceholderText = "Full name" };
    private readonly TextBox _loginPassword = new() { PlaceholderText = "Password", PasswordChar = '*' };
    private readonly TextBox _registerPassword = new() { PlaceholderText = "Password", PasswordChar = '*' };

    private readonly ListBox _walletList = new() { MinHeight = 170 };
    private readonly ComboBox _topUpCurrency = new();
    private readonly TextBox _topUpAmount = new() { PlaceholderText = "Amount", Text = "1000" };

    private readonly ListBox _ratesList = new() { MinHeight = 300 };
    private readonly ComboBox _buyCurrency = new();
    private readonly TextBox _buyPlnAmount = new() { PlaceholderText = "PLN amount", Text = "500" };
    private readonly ComboBox _sellCurrency = new();
    private readonly TextBox _sellAmount = new() { PlaceholderText = "Amount", Text = "10" };

    private readonly StackPanel _historyPanel = new() { MinHeight = 360, Spacing = 10 };
    private readonly ComboBox _historicalCurrency = new();
    private readonly TextBox _historicalStart = new() { PlaceholderText = "yyyy-mm-dd", Text = DateTime.Today.AddDays(-14).ToString("yyyy-MM-dd") };
    private readonly TextBox _historicalEnd = new() { PlaceholderText = "yyyy-mm-dd", Text = DateTime.Today.ToString("yyyy-MM-dd") };
    private readonly ListBox _historicalList = new() { MinHeight = 300 };

    public MainWindow()
    {
        Title = "Currency Exchange Office System";
        Width = 1100;
        Height = 760;
        MinWidth = 900;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.Parse("#f7f7f4"));

        _loginPassword.Text = "demo123";

        Content = BuildLayout();
        Opened += async (_, _) => await InitializeAsync();
    }

    private Control BuildLayout()
    {
        var root = new DockPanel();

        var header = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#d8d8d2")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 14)
        };
        DockPanel.SetDock(header, Dock.Top);

        header.Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Currency Exchange Office System",
                            FontSize = 22,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#24312f"))
                        },
                        _statusText
                    }
                },
                _userText
            }
        };
        Grid.SetColumn(_userText, 1);

        var tabs = new TabControl
        {
            Margin = new Thickness(18),
            Items =
            {
                new TabItem { Header = "Login / Register", Content = Scroll(BuildLoginTab()) },
                new TabItem { Header = "Dashboard / Wallet", Content = Scroll(BuildWalletTab()) },
                new TabItem { Header = "Top Up", Content = Scroll(BuildTopUpTab()) },
                new TabItem { Header = "Exchange Rates", Content = Scroll(BuildRatesTab()) },
                new TabItem { Header = "Buy / Sell", Content = Scroll(BuildExchangeTab()) },
                new TabItem { Header = "Transaction History", Content = Scroll(BuildHistoryTab()) },
                new TabItem { Header = "Historical Rates", Content = Scroll(BuildHistoricalTab()) }
            }
        };

        root.Children.Add(header);
        root.Children.Add(tabs);
        return root;
    }

    private Control BuildLoginTab()
    {
        var loginButton = PrimaryButton("Sign in");
        loginButton.Click += async (_, _) => await LoginAsync();

        var registerButton = PrimaryButton("Create account");
        registerButton.Click += async (_, _) => await RegisterAsync();

        var demoButton = SecondaryButton("Use demo account");
        demoButton.Click += async (_, _) => await LoginAsDemoAsync();

        var pingButton = SecondaryButton("Check service");
        pingButton.Click += async (_, _) => await PingAsync();

        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                IntroPanel(
                    "Account access",
                    "Sign in with the demo account for presentation, or create a separate user to test registration."),
                TwoColumn(
                    Section("Sign in",
                        FormField("Username", _loginUsername),
                        FormField("Password", _loginPassword),
                        Row(loginButton, demoButton, pingButton),
                        MutedText("Demo: username demo, password demo123")),
                    Section("Create account",
                        FormField("Username", _registerUsername),
                        FormField("Full name", _registerFullName),
                        FormField("Password", _registerPassword),
                        registerButton,
                        MutedText("New accounts start with an empty PLN wallet. Use Top Up after registration.")))
            }
        };
    }

    private Control BuildWalletTab()
    {
        var refreshButton = SecondaryButton("Refresh wallet");
        refreshButton.Click += async (_, _) => await RefreshWalletAsync();

        return Section("Wallet balances",
            new TextBlock { Text = "Logged-in user balances", FontWeight = FontWeight.SemiBold },
            _walletList,
            refreshButton);
    }

    private Control BuildTopUpTab()
    {
        var button = PrimaryButton("Top up balance");
        button.Click += async (_, _) => await TopUpAsync();

        return Section("Add balance",
            Label("Currency"), _topUpCurrency,
            Label("Amount"), _topUpAmount,
            button);
    }

    private Control BuildRatesTab()
    {
        var button = SecondaryButton("Refresh current rates");
        button.Click += async (_, _) => await LoadCurrentRatesAsync();

        return Section("Current NBP Table C buy/sell rates",
            _ratesList,
            button);
    }

    private Control BuildExchangeTab()
    {
        var buyButton = PrimaryButton("Buy currency");
        buyButton.Click += async (_, _) => await BuyAsync();

        var sellButton = PrimaryButton("Sell currency");
        sellButton.Click += async (_, _) => await SellAsync();

        return TwoColumn(
            Section("Buy foreign currency",
                Label("Target currency"), _buyCurrency,
                Label("PLN amount to spend"), _buyPlnAmount,
                buyButton),
            Section("Sell foreign currency",
                Label("Source currency"), _sellCurrency,
                Label("Amount to sell"), _sellAmount,
                sellButton));
    }

    private Control BuildHistoryTab()
    {
        var button = SecondaryButton("Refresh history");
        button.Click += async (_, _) => await RefreshHistoryAsync();

        return Section("Transactions",
            MutedText("Newest transactions appear first. Each item shows what changed in the wallet."),
            _historyPanel,
            button);
    }

    private Control BuildHistoricalTab()
    {
        var button = PrimaryButton("Load historical rates");
        button.Click += async (_, _) => await LoadHistoricalRatesAsync();

        return Section("Historical NBP rates",
            Label("Currency"), _historicalCurrency,
            Label("Start date"), _historicalStart,
            Label("End date"), _historicalEnd,
            button,
            _historicalList);
    }

    private async Task InitializeAsync()
    {
        await PingAsync();
        await LoadCurrenciesAsync();
        await LoadCurrentRatesAsync();
    }

    private async Task PingAsync()
    {
        await RunUiAction(async () =>
        {
            var result = await _service.PingAsync();
            SetStatus(result.Message, result.Success);
        });
    }

    private async Task LoginAsync()
    {
        await RunUiAction(async () =>
        {
            var result = await _service.LoginAsync(_loginUsername.Text ?? string.Empty, _loginPassword.Text ?? string.Empty);
            if (!result.Success)
            {
                SetStatus(result.Message, false);
                return;
            }

            _userId = result.UserId;
            _userName = result.FullName;
            _userText.Text = $"{result.FullName} ({result.Username})";
            SetStatus(result.Message, true);
            await RefreshWalletAsync();
            await RefreshHistoryAsync();
        });
    }

    private async Task LoginAsDemoAsync()
    {
        _loginUsername.Text = "demo";
        _loginPassword.Text = "demo123";
        await LoginAsync();
    }

    private async Task RegisterAsync()
    {
        await RunUiAction(async () =>
        {
            var result = await _service.RegisterUserAsync(
                _registerUsername.Text ?? string.Empty,
                _registerPassword.Text ?? string.Empty,
                _registerFullName.Text ?? string.Empty);

            if (!result.Success)
            {
                SetStatus(result.Message, false);
                return;
            }

            _userId = result.UserId;
            _userName = result.FullName;
            _userText.Text = $"{result.FullName} ({result.Username})";
            SetStatus(result.Message, true);
            await RefreshWalletAsync();
            await RefreshHistoryAsync();
        });
    }

    private async Task LoadCurrenciesAsync()
    {
        await RunUiAction(async () =>
        {
            var currencies = await _service.GetSupportedCurrenciesAsync();
            _currencies.Clear();
            _currencies.AddRange(currencies.Count == 0 ? ["PLN", "USD", "EUR"] : currencies);
            RefreshCurrencyControls();
        });
    }

    private async Task LoadCurrentRatesAsync()
    {
        await RunUiAction(async () =>
        {
            var rates = await _service.GetCurrentRatesAsync();
            _ratesList.ItemsSource = rates.Select(rate =>
                $"{rate.CurrencyCode,-4} {rate.CurrencyName,-24} bid {rate.Bid:F4} PLN  ask {rate.Ask:F4} PLN  {rate.EffectiveDate:yyyy-MM-dd}")
                .ToList();
            SetStatus("Current exchange rates loaded.", true);
        });
    }

    private async Task RefreshWalletAsync()
    {
        if (!RequireLogin())
        {
            return;
        }

        await RunUiAction(async () =>
        {
            var balances = await _service.GetWalletAsync(_userId!.Value);
            _walletList.ItemsSource = balances.Select(balance => $"{balance.CurrencyCode}: {balance.Amount:F4}").ToList();
            SetStatus($"Wallet loaded for {_userName}.", true);
        });
    }

    private async Task TopUpAsync()
    {
        if (!RequireLogin() || !TryReadAmount(_topUpAmount.Text, out var amount))
        {
            return;
        }

        await RunUiAction(async () =>
        {
            var result = await _service.TopUpBalanceAsync(_userId!.Value, SelectedCurrency(_topUpCurrency), amount);
            SetStatus(result.Message, result.Success);
            if (result.Success)
            {
                await RefreshWalletAsync();
                await RefreshHistoryAsync();
            }
        });
    }

    private async Task BuyAsync()
    {
        if (!RequireLogin() || !TryReadAmount(_buyPlnAmount.Text, out var amount))
        {
            return;
        }

        await RunUiAction(async () =>
        {
            var result = await _service.BuyCurrencyAsync(_userId!.Value, SelectedCurrency(_buyCurrency), amount);
            SetStatus(result.Message, result.Success);
            if (result.Success)
            {
                await RefreshWalletAsync();
                await RefreshHistoryAsync();
            }
        });
    }

    private async Task SellAsync()
    {
        if (!RequireLogin() || !TryReadAmount(_sellAmount.Text, out var amount))
        {
            return;
        }

        await RunUiAction(async () =>
        {
            var result = await _service.SellCurrencyAsync(_userId!.Value, SelectedCurrency(_sellCurrency), amount);
            SetStatus(result.Message, result.Success);
            if (result.Success)
            {
                await RefreshWalletAsync();
                await RefreshHistoryAsync();
            }
        });
    }

    private async Task RefreshHistoryAsync()
    {
        if (!RequireLogin())
        {
            return;
        }

        await RunUiAction(async () =>
        {
            var rows = await _service.GetTransactionHistoryAsync(_userId!.Value);
            _historyPanel.Children.Clear();

            if (rows.Count == 0)
            {
                _historyPanel.Children.Add(MutedText("No transactions yet. Use Top Up or Buy / Sell to create the first entry."));
            }

            foreach (var row in rows)
            {
                _historyPanel.Children.Add(TransactionCard(row));
            }

            SetStatus("Transaction history loaded.", true);
        });
    }

    private static Border TransactionCard(TransactionRow row)
    {
        var title = TransactionTitle(row);
        var movement = TransactionMovement(row);
        var rateNote = TransactionRateNote(row);
        var accent = row.Type.ToUpperInvariant() switch
        {
            "TOP_UP" => "#2f7d4f",
            "BUY" => "#24574c",
            "SELL" => "#7a5a21",
            _ => "#68736f"
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#24312f"))
        });

        var dateText = new TextBlock
        {
            Text = row.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Foreground = new SolidColorBrush(Color.Parse("#68736f")),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(dateText, 1);
        header.Children.Add(dateText);

        var details = new StackPanel
        {
            Spacing = 5,
            Children =
            {
                new TextBlock
                {
                    Text = movement,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#35423f"))
                },
                new TextBlock
                {
                    Text = rateNote,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#68736f"))
                },
                new TextBlock
                {
                    Text = row.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#68736f")),
                    FontSize = 13
                }
            }
        };

        var accentBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse(accent)),
            CornerRadius = new CornerRadius(3)
        };

        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                header,
                details
            }
        };
        Grid.SetColumn(content, 1);

        var cardGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("6,*"),
            ColumnSpacing = 12
        };
        cardGrid.Children.Add(accentBar);
        cardGrid.Children.Add(content);

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#d8d8d2")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = cardGrid
        };
    }

    private static string TransactionTitle(TransactionRow row)
    {
        return row.Type.ToUpperInvariant() switch
        {
            "TOP_UP" => $"Top-up {row.TargetCurrency}",
            "BUY" => $"Bought {row.TargetCurrency}",
            "SELL" => $"Sold {row.SourceCurrency}",
            _ => row.Type
        };
    }

    private static string TransactionMovement(TransactionRow row)
    {
        return row.Type.ToUpperInvariant() switch
        {
            "TOP_UP" => $"Added {FormatMoney(row.TargetAmount, row.TargetCurrency)} to the wallet.",
            "BUY" => $"Spent {FormatMoney(row.SourceAmount, row.SourceCurrency)} and received {FormatMoney(row.TargetAmount, row.TargetCurrency)}.",
            "SELL" => $"Sold {FormatMoney(row.SourceAmount, row.SourceCurrency)} and received {FormatMoney(row.TargetAmount, row.TargetCurrency)}.",
            _ => $"{FormatMoney(row.SourceAmount, row.SourceCurrency)} -> {FormatMoney(row.TargetAmount, row.TargetCurrency)}"
        };
    }

    private static string TransactionRateNote(TransactionRow row)
    {
        return row.Type.ToUpperInvariant() switch
        {
            "BUY" => $"Ask rate used: 1 {row.TargetCurrency} = {row.Rate:F4} PLN.",
            "SELL" => $"Bid rate used: 1 {row.SourceCurrency} = {row.Rate:F4} PLN.",
            "TOP_UP" => "No exchange rate was used.",
            _ => $"Rate used: {row.Rate:F4}"
        };
    }

    private static string FormatMoney(decimal amount, string currencyCode)
    {
        var decimals = currencyCode == "PLN" ? 2 : 4;
        return $"{amount.ToString($"F{decimals}", CultureInfo.InvariantCulture)} {currencyCode}";
    }

    private async Task LoadHistoricalRatesAsync()
    {
        if (!DateTime.TryParseExact(_historicalStart.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !DateTime.TryParseExact(_historicalEnd.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            SetStatus("Dates must use yyyy-mm-dd format.", false);
            return;
        }

        await RunUiAction(async () =>
        {
            var rows = await _service.GetHistoricalRatesAsync(SelectedCurrency(_historicalCurrency), start, end);
            _historicalList.ItemsSource = rows.Select(row =>
                $"{row.RateDate:yyyy-MM-dd}  {row.CurrencyCode}  bid {row.Bid:F4} PLN  ask {row.Ask:F4} PLN")
                .ToList();
            SetStatus("Historical rates loaded.", true);
        });
    }

    private async Task RunUiAction(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
        }
    }

    private bool RequireLogin()
    {
        if (_userId.HasValue)
        {
            return true;
        }

        SetStatus("Please log in first.", false);
        return false;
    }

    private bool TryReadAmount(string? text, out decimal amount)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) && amount > 0)
        {
            return true;
        }

        SetStatus("Amount must be a positive number. Use a dot for decimals.", false);
        amount = 0m;
        return false;
    }

    private void RefreshCurrencyControls()
    {
        var foreignCurrencies = _currencies.Where(code => code != "PLN").ToList();

        SetComboItems(_topUpCurrency, _currencies, "PLN");
        SetComboItems(_buyCurrency, foreignCurrencies, "USD");
        SetComboItems(_sellCurrency, foreignCurrencies, "USD");
        SetComboItems(_historicalCurrency, foreignCurrencies, "USD");
    }

    private static void SetComboItems(ComboBox comboBox, List<string> items, string preferred)
    {
        comboBox.ItemsSource = items;
        comboBox.SelectedItem = items.Contains(preferred) ? preferred : items.FirstOrDefault();
    }

    private static string SelectedCurrency(ComboBox comboBox)
    {
        return comboBox.SelectedItem?.ToString() ?? "PLN";
    }

    private void SetStatus(string message, bool success)
    {
        _statusText.Text = message;
        _statusText.Foreground = success
            ? new SolidColorBrush(Color.Parse("#20633b"))
            : new SolidColorBrush(Color.Parse("#a33b32"));
    }

    private static ScrollViewer Scroll(Control child)
    {
        return new ScrollViewer { Content = child };
    }

    private static Border Section(string title, params Control[] children)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#d8d8d2")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#24312f"))
                    }
                }
            }
        }.WithChildren(children);
    }

    private static Border IntroPanel(string title, string body)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#eef4f1")),
            BorderBrush = new SolidColorBrush(Color.Parse("#c7d9d1")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 20,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#24312f"))
                    },
                    new TextBlock
                    {
                        Text = body,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#465653"))
                    }
                }
            }
        };
    }

    private static StackPanel FormField(string label, TextBox textBox)
    {
        textBox.MinHeight = 38;
        textBox.HorizontalAlignment = HorizontalAlignment.Stretch;

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Label(label),
                textBox
            }
        };
    }

    private static TextBlock MutedText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#68736f")),
            FontSize = 13
        };
    }

    private static Grid TwoColumn(Control left, Control right)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        grid.Children.Add(left);
        grid.Children.Add(right);
        Grid.SetColumn(right, 1);
        return grid;
    }

    private static StackPanel Row(params Control[] children)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock { Text = text, FontWeight = FontWeight.SemiBold };
    }

    private static Button PrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(18, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.Parse("#24574c")),
            Foreground = Brushes.White
        };
    }

    private static Button SecondaryButton(string text)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(18, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }
}

internal static class BorderExtensions
{
    public static Border WithChildren(this Border border, IEnumerable<Control> controls)
    {
        if (border.Child is StackPanel panel)
        {
            foreach (var control in controls)
            {
                panel.Children.Add(control);
            }
        }

        return border;
    }
}
