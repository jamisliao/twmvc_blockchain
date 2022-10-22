using Blocto.SDK.Flow;
using Blocto.Sdk.Flow.Utility;
using Flow.FCL;
using Flow.FCL.Config;
using Flow.FCL.Models;
using Flow.FCL.Utility;
using Flow.FCL.WalletProvider;
using Flow.Net.Sdk.Client.Http;
using Flow.Net.Sdk.Core.Client;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Transaction = Flow.FCL.Transaction;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .WriteTo.Console()
            .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<Config>(provider => {
                                          var config = new Config();
                                          config.Put("discovery.wallet", "https://flow-wallet-testnet.blocto.app/api/flow/authn")
                                                .Put("accessNode.api", "https://rest-testnet.onflow.org/v1")
                                                .Put("flow.network", "testnet");
                                          return config;
                                      });
builder.Services.AddScoped<IWalletProvider>(provider => {
                                                var flowClient = provider.GetRequiredService<IFlowClient>();
                                                var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                                                var resolveUtility = provider.GetRequiredService<IResolveUtility>();
                                                var logger = provider.GetRequiredService<ILogger<BloctoWalletProvider>>();
                                                
                                                var bloctoWalletProvider = new BloctoWalletProvider(logger, httpClientFactory, flowClient, resolveUtility, "testnet", Guid.Parse("4271a8b2-3198-4646-b6a2-fe825f982220"));
                                                return bloctoWalletProvider;
                                            });
builder.Services.AddScoped<IResolveUtility>(provider => new ResolveUtility());
builder.Services.AddScoped<IEncodeUtility, EncodeUtility>();
builder.Services.AddScoped<IFlowClient>(provider => {
                                            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                                            var httpClient = httpClientFactory.CreateClient();
                                            var flowClient = new FlowHttpClient(httpClient, new FlowClientOptions
                                                                                            {
                                                                                                ServerUrl = "https://rest-testnet.onflow.org/v1"
                                                                                            });
                                            return flowClient;
                                        });
builder.Services.AddScoped<Transaction>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<AppUtility>();
builder.Services.AddScoped<FlowClientLibrary>(provider => {
                                                  var config = provider.GetRequiredService<Config>();
                                                  FlowClientLibrary.SetConfig(config);
                                                  
                                                  var walletProvider = provider.GetRequiredService<IWalletProvider>();
                                                  var transaction = provider.GetRequiredService<Transaction>();
                                                  var currentUser = provider.GetRequiredService<CurrentUser>();
                                                  var fcl = new FlowClientLibrary(walletProvider, transaction, currentUser);
                                                  
                                                  return fcl;
                                              });

builder.Host.UseSerilog(Log.Logger, dispose: true);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();