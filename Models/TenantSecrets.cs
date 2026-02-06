namespace WebhookGateway.Models
{
	public record TenantSecrets(
	string WebhookSecret,
	string TargetUrl
);
}
