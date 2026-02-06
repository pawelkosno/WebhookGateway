# Resilient Multi-Tenant Webhook Gateway

Azure Functions application that receives webhooks from external services (Stripe, GitHub, etc.), validates them, and reliably forwards them to tenant-specific endpoints.

## Features

- **Multi-tenant** – Single deployment serves multiple clients with isolated secrets
- **Managed Identity** – No secrets in code, authenticates to Key Vault automatically
- **HMAC-SHA256 verification** – Validates webhook signatures to prevent spoofing
- **Retry with Polly** – 3 attempts with exponential backoff on 5xx errors
- **Dead Letter Queue** – Failed messages stored in Azure Queue for later processing

## Tech Stack

- .NET 8, Azure Functions v4 (Isolated Worker)
- Azure Key Vault + Managed Identity
- Azure Queue Storage
- Polly for resilience

## How It Works

1. External service sends POST to `/api/webhook/{tenantId}`
2. Function fetches tenant's secrets from Key Vault (cached 5 min)
3. Validates HMAC-SHA256 signature from `X-Webhook-Signature` header
4. Forwards payload to tenant's target URL with retry policy
5. On failure after retries → saves to Dead Letter Queue

## Project Structure

```
WebhookGateway/
├── Models/
│   ├── TenantSecrets.cs
│   └── DeadLetterMessage.cs
├── Services/
│   ├── TenantSecretService.cs    # Key Vault + cache
│   ├── SignatureValidator.cs     # HMAC-SHA256
│   ├── WebhookForwarder.cs       # HttpClient + Polly
│   └── DeadLetterService.cs      # Azure Queue
├── WebhookReceiver.cs            # HTTP trigger
└── Program.cs                    # DI setup
```

## Azure Resources Required

- Resource Group
- Storage Account
- Key Vault
- Function App (Consumption plan)

Function App needs System-assigned Managed Identity with "Key Vault Secrets User" role.

## Key Vault Secrets Convention

For each tenant, add two secrets:
- `{tenantId}--WebhookSecret` – HMAC key for signature verification
- `{tenantId}--TargetUrl` – Where to forward the webhook

## Local Development

1. Clone and restore.

2. Create `local.settings.json`:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<storage-connection-string>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "KeyVaultUri": "https://<your-keyvault>.vault.azure.net/"
  }
}
```

3. Login to Azure and run:
```bash
az login
func start
```

## Response Codes

- `200` – Delivered successfully
- `400` – Missing tenant ID or empty payload
- `401` – Invalid signature
- `404` – Unknown tenant
- `502` – Delivery failed, message queued to DLQ

## Deployment

```bash
func azure functionapp publish <function-app-name>
```

## License

MIT
