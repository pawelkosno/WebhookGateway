using Microsoft.Extensions.Logging;

namespace WebhookGateway.Services
{
	public class WebhookForwarder : IWebhookForwarder
	{
		private readonly HttpClient _httpClient;
		private readonly ILogger<WebhookForwarder> _logger;

		public WebhookForwarder(HttpClient httpClient, ILogger<WebhookForwarder> logger)
		{
			_httpClient = httpClient;
			_logger = logger;
		}

		public async Task<ForwardResult> ForwardAsync(
			string targetUrl,
			byte[] payload,
			CancellationToken ct = default)
		{
			try
			{
				using var content = new ByteArrayContent(payload);
				content.Headers.ContentType = new("application/json");

				_logger.LogInformation("Forwarding webhook to {TargetUrl}", targetUrl);

				var response = await _httpClient.PostAsync(targetUrl, content, ct);

				if (response.IsSuccessStatusCode)
				{
					_logger.LogInformation("Webhook forwarded successfully: {StatusCode}",
						(int)response.StatusCode);
					return new ForwardResult(true, (int)response.StatusCode, null);
				}

				_logger.LogWarning("Webhook forward failed: {StatusCode}",
					(int)response.StatusCode);
				return new ForwardResult(false, (int)response.StatusCode, response.ReasonPhrase);
			}
			catch (TaskCanceledException) when (!ct.IsCancellationRequested)
			{
				_logger.LogError("Webhook forward timed out after all retries");
				return new ForwardResult(false, null, "Timeout after retries");
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "Webhook forward failed with exception");
				return new ForwardResult(false, null, ex.Message);
			}
		}
	}
}
