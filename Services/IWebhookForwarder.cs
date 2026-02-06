
namespace WebhookGateway.Services
{
	public record ForwardResult(bool Success, int? StatusCode, string? Error);

	public interface IWebhookForwarder
	{
		Task<ForwardResult> ForwardAsync(
			string targetUrl,
			byte[] payload,
			CancellationToken ct = default);
	}
}
