using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebhookGateway.Services;
using Polly;
using Polly.Extensions.Http;
using Azure.Storage.Queues;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Key Vault
var kvUri = builder.Configuration["KeyVaultUri"]
	?? throw new InvalidOperationException("KeyVaultUri not configured");

builder.Services.AddSingleton(new SecretClient(
	new Uri(kvUri),
	new DefaultAzureCredential()
));

// Cache
builder.Services.AddMemoryCache();

// Services
builder.Services.AddSingleton<ITenantSecretService, TenantSecretService>();
builder.Services.AddSingleton<ISignatureValidator, SignatureValidator>();

builder.Services.AddHttpClient<IWebhookForwarder, WebhookForwarder>(client =>
{
	client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(GetRetryPolicy());


// Dead Letter Queue
var storageConnectionString = builder.Configuration["AzureWebJobsStorage"]
	?? throw new InvalidOperationException("AzureWebJobsStorage not configured");

var queueClient = new QueueClient(storageConnectionString, "webhook-failed");
queueClient.CreateIfNotExists();
builder.Services.AddSingleton(queueClient);
builder.Services.AddSingleton<IDeadLetterService, DeadLetterService>();


// Application Insights
builder.Services
	.AddApplicationInsightsTelemetryWorkerService()
	.ConfigureFunctionsApplicationInsights();

builder.Build().Run();


static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
	return HttpPolicyExtensions
		.HandleTransientHttpError() // 5xx + 408 + HttpRequestException
		.OrResult(r => r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
		.WaitAndRetryAsync(
			retryCount: 3,
			sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
			onRetry: (outcome, delay, attempt, _) =>
			{
				Console.WriteLine($"Retry {attempt} after {delay.TotalSeconds}s - " +
					$"Status: {outcome.Result?.StatusCode}");
			});
}