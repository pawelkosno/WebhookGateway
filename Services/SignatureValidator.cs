using System.Security.Cryptography;
using System.Text;

namespace WebhookGateway.Services
{
	public class SignatureValidator : ISignatureValidator
	{
		public bool Validate(string secret, byte[] payload, string expectedSignature)
		{
			if (string.IsNullOrEmpty(expectedSignature))
				return false;

			using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
			var hash = hmac.ComputeHash(payload);
			var computed = Convert.ToHexString(hash).ToLowerInvariant();

			var expected = expectedSignature
				.Replace("sha256=", "", StringComparison.OrdinalIgnoreCase)
				.ToLowerInvariant();

			return CryptographicOperations.FixedTimeEquals(
				Encoding.UTF8.GetBytes(computed),
				Encoding.UTF8.GetBytes(expected)
			);
		}
	}
}
