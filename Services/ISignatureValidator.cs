namespace WebhookGateway.Services
{
	public interface ISignatureValidator
	{
		bool Validate(string secret, byte[] payload, string expectedSignature);
	}
}
