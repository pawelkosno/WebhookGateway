using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WebhookGateway.Models;

namespace WebhookGateway.Services
{
	public class DeadLetterService : IDeadLetterService
	{
		private readonly QueueClient _queueClient;
		private readonly ILogger<DeadLetterService> _logger;

		public DeadLetterService(QueueClient queueClient, ILogger<DeadLetterService> logger)
		{
			_queueClient = queueClient;
			_logger = logger;
		}

		public async Task EnqueueAsync(DeadLetterMessage message, CancellationToken ct = default)
		{
			var json = JsonSerializer.Serialize(message);
			var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

			_logger.LogWarning(
				"Enqueueing dead letter for tenant {TenantId}, target {TargetUrl}",
				message.TenantId,
				message.TargetUrl);

			await _queueClient.SendMessageAsync(base64, ct);
		}
	}
}
