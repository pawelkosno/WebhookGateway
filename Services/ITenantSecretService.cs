using WebhookGateway.Models;

namespace WebhookGateway.Services
{
	public interface ITenantSecretService
	{
		Task<TenantSecrets?> GetSecretsAsync(string tenantId, CancellationToken ct = default);
	}
}
