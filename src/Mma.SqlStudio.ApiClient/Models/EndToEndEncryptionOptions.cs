namespace Mma.SqlStudio.ApiClient.Models
{
    /// <summary>
    /// Browser-to-backend end-to-end encryption settings. The backend must implement
    /// the protocol documented in README.md; this package only relays ciphertext.
    /// </summary>
    public class EndToEndEncryptionOptions
    {
        /// <summary>Enables end-to-end encryption. Defaults to false.</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Trusted backend RSA-OAEP public key in JSON Web Key (JWK) format.
        /// The matching private key must remain exclusively with the backend.
        /// </summary>
        public string BackendPublicKeyJwk { get; set; } = "";

        /// <summary>Identifier for the configured public key, used for rotation and envelope validation.</summary>
        public string BackendKeyId { get; set; } = "";
    }
}
