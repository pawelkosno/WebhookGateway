using WebhookGateway.Models;

namespace WebhookGateway.Services
{
	public interface IDeadLetterService
	{
		Task EnqueueAsync(DeadLetterMessage message, CancellationToken ct = default);
	}
}
