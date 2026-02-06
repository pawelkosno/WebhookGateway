using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WebhookGateway.Models;

namespace WebhookGateway.Services;

public class TenantSecretService : ITenantSecretService
{
	private readonly SecretClient _secretClient;
	private readonly IMemoryCache _cache;
	private readonly ILogger<TenantSecretService> _logger;

	private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

	public TenantSecretService(
		SecretClient secretClient,
		IMemoryCache cache,
		ILogger<TenantSecretService> logger)
	{
		_secretClient = secretClient;
		_cache = cache;
		_logger = logger;
	}

	public async Task<TenantSecrets?> GetSecretsAsync(string tenantId, CancellationToken ct = default)
	{
		var cacheKey = $"tenant-secrets-{tenantId}";

		if (_cache.TryGetValue(cacheKey, out TenantSecrets? cached))
		{
			_logger.LogDebug("Cache hit for tenant {TenantId}", tenantId);
			return cached;
		}

		try
		{
			var secretName = $"{tenantId}--WebhookSecret";
			var urlName = $"{tenantId}--TargetUrl";

			_logger.LogInformation("Fetching secrets for tenant {TenantId}", tenantId);

			var secretTask = _secretClient.GetSecretAsync(secretName, cancellationToken: ct);
			var urlTask = _secretClient.GetSecretAsync(urlName, cancellationToken: ct);

			await Task.WhenAll(secretTask, urlTask);

			var secrets = new TenantSecrets(
				WebhookSecret: secretTask.Result.Value.Value,
				TargetUrl: urlTask.Result.Value.Value
			);

			_cache.Set(cacheKey, secrets, CacheDuration);

			return secrets;
		}
		catch (Azure.RequestFailedException ex) when (ex.Status == 404)
		{
			_logger.LogWarning("Secrets not found for tenant {TenantId}", tenantId);
			return null;
		}
	}
}