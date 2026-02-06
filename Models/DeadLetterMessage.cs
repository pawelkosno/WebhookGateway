namespace WebhookGateway.Models
{
	public record DeadLetterMessage(
		string TenantId,
		string TargetUrl,
		string Payload,
		string Error,
		DateTime FailedAt
	);
}
