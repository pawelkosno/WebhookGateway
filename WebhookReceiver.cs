using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using WebhookGateway.Models;
using WebhookGateway.Services;

namespace WebhookGateway;

public class WebhookReceiver
{
	private readonly ITenantSecretService _secretService;
	private readonly ISignatureValidator _signatureValidator;
	private readonly IWebhookForwarder _forwarder;
	private readonly IDeadLetterService _deadLetterService;
	private readonly ILogger<WebhookReceiver> _logger;

	private const string SignatureHeader = "X-Webhook-Signature";

	public WebhookReceiver(
		ITenantSecretService secretService,
		ISignatureValidator signatureValidator,
		IWebhookForwarder forwarder,
		IDeadLetterService deadLetterService,
		ILogger<WebhookReceiver> logger)
	{
		_secretService = secretService;
		_signatureValidator = signatureValidator;
		_forwarder = forwarder;
		_deadLetterService = deadLetterService;
		_logger = logger;
	}

	[Function("WebhookReceiver")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook/{tenantId}")]
		HttpRequestData req,
		string tenantId,
		CancellationToken ct)
	{
		_logger.LogInformation("Webhook received for tenant: {TenantId}", tenantId);

		// 1. Validate tenantId
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			return await CreateResponse(req, HttpStatusCode.BadRequest, "Missing tenantId");
		}

		// 2. Read body
		byte[] payload;
		using (var ms = new MemoryStream())
		{
			await req.Body.CopyToAsync(ms, ct);
			payload = ms.ToArray();
		}

		if (payload.Length == 0)
		{
			return await CreateResponse(req, HttpStatusCode.BadRequest, "Empty payload");
		}

		// 3. Get tenant's secrets
		var secrets = await _secretService.GetSecretsAsync(tenantId, ct);
		if (secrets is null)
		{
			_logger.LogWarning("Unknown tenant: {TenantId}", tenantId);
			return await CreateResponse(req, HttpStatusCode.NotFound, "Unknown tenant");
		}

		// 4. Verify HMAC
		var signature = req.Headers.TryGetValues(SignatureHeader, out var sigValues)
			? sigValues.FirstOrDefault()
			: null;

		if (!_signatureValidator.Validate(secrets.WebhookSecret, payload, signature ?? ""))
		{
			_logger.LogWarning("Invalid signature for tenant: {TenantId}", tenantId);
			return await CreateResponse(req, HttpStatusCode.Unauthorized, "Invalid signature");
		}

		_logger.LogInformation("Signature valid for tenant: {TenantId}", tenantId);

		// 5. Forward to TargetUrl
		var result = await _forwarder.ForwardAsync(secrets.TargetUrl, payload, ct);

		if (result.Success)
		{
			return await CreateResponse(req, HttpStatusCode.OK, "Webhook delivered");
		}

		// 6. Dead Letter Queue in case of failure
		var deadLetter = new DeadLetterMessage(
			TenantId: tenantId,
			TargetUrl: secrets.TargetUrl,
			Payload: Encoding.UTF8.GetString(payload),
			Error: result.Error ?? "Unknown error",
			FailedAt: DateTime.UtcNow
		);

		await _deadLetterService.EnqueueAsync(deadLetter, ct);

		return await CreateResponse(req, HttpStatusCode.BadGateway,
			$"Delivery failed after retries: {result.Error}");
	}

	private static async Task<HttpResponseData> CreateResponse(
		HttpRequestData req,
		HttpStatusCode status,
		string message)
	{
		var response = req.CreateResponse(status);
		response.Headers.Add("Content-Type", "application/json");
		await response.WriteStringAsync($"{{\"message\":\"{message}\"}}");
		return response;
	}
}