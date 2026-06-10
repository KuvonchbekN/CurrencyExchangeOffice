using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CurrencyExchangeOffice.Contracts;
using CurrencyExchangeOffice.Service.Data;
using CurrencyExchangeOffice.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<CurrencyRepository>();
builder.Services.AddScoped<CurrencyExchangeService>();
builder.Services.AddHttpClient<NbpClient>(client =>
{
    client.BaseAddress = new Uri("https://api.nbp.pl/api/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CurrencyExchangeOffice/1.0");
});

var app = builder.Build();

var repository = app.Services.GetRequiredService<CurrencyRepository>();
repository.InitializeDatabase();
repository.SeedDemoUser();

app.MapGet("/", () => Results.Text("""
    Currency Exchange Office SOAP service is running.
    SOAP endpoint: /CurrencyExchangeService.svc
    WSDL metadata: /CurrencyExchangeService.svc?wsdl
    """));

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<CurrencyExchangeService>();
    serviceBuilder.AddServiceEndpoint<CurrencyExchangeService, ICurrencyExchangeService>(
        new BasicHttpBinding(),
        "/CurrencyExchangeService.svc");

    var metadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    metadataBehavior.HttpGetEnabled = true;
});

app.Run();
