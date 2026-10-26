using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mma.SqlStudio.TestApi.Services;

/// <summary>Development/test backend implementation of the SqlStudio E2EE v1 envelope.</summary>
public sealed class TestApiEncryptionService : IDisposable
{
    private const string Algorithm = "RSA-OAEP-256+A256GCM";
    private const int AesTagSize = 16;
    private const int MaxTrackedRequests = 50_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RSA _rsa = RSA.Create(3072);
    private readonly ConcurrentDictionary<string, byte> _requestIds = new(StringComparer.Ordinal);
    private readonly object _rsaLock = new();

    public string KeyId { get; } = $"test-api-{Guid.NewGuid():N}";

    public string PublicKeyJwk
    {
        get
        {
            var parameters = _rsa.ExportParameters(false);
            return JsonSerializer.Serialize(new
            {
                kty = "RSA",
                n = Base64Url(parameters.Modulus!),
                e = Base64Url(parameters.Exponent!),
                alg = "RSA-OAEP-256",
                ext = true,
                key_ops = new[] { "encrypt" }
            }, JsonOptions);
        }
    }

    public DecryptedRequest Decrypt(JsonElement request, string route)
    {
        if (route is not ("query" or "schema" or "history"))
            throw new InvalidDataException("Unsupported E2EE route.");

        var envelope = request.Deserialize<RequestEnvelope>(JsonOptions)
            ?? throw new InvalidDataException("Missing E2EE envelope.");
        if (envelope.Version != 1 || envelope.Algorithm != Algorithm || envelope.KeyId != KeyId ||
            !Guid.TryParse(envelope.RequestId, out _))
            throw new InvalidDataException("Invalid E2EE envelope metadata.");

        var wrappedKey = Convert.FromBase64String(envelope.WrappedKey);
        var iv = Convert.FromBase64String(envelope.Iv);
        var encrypted = Convert.FromBase64String(envelope.Ciphertext);
        if (iv.Length != 12 || encrypted.Length < AesTagSize)
            throw new InvalidDataException("Invalid E2EE ciphertext.");

        byte[] aesKey;
        lock (_rsaLock)
            aesKey = _rsa.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA256);
        if (aesKey.Length != 32)
        {
            CryptographicOperations.ZeroMemory(aesKey);
            throw new InvalidDataException("Invalid wrapped AES key.");
        }

        byte[]? plaintext = null;
        try
        {
            plaintext = new byte[encrypted.Length - AesTagSize];
            var tag = encrypted.AsSpan(encrypted.Length - AesTagSize, AesTagSize);
            var ciphertext = encrypted.AsSpan(0, encrypted.Length - AesTagSize);
            using (var aes = new AesGcm(aesKey, AesTagSize))
            {
                aes.Decrypt(iv, ciphertext, tag, plaintext,
                    BuildAdditionalData(envelope.KeyId, envelope.RequestId, route, "request"));
            }

            var payload = JsonDocument.Parse(plaintext);
            if (!payload.RootElement.TryGetProperty("requestId", out var payloadRequestId) ||
                payloadRequestId.GetString() != envelope.RequestId)
            {
                payload.Dispose();
                throw new InvalidDataException("Request ID mismatch.");
            }

            if (_requestIds.Count >= MaxTrackedRequests || !_requestIds.TryAdd(envelope.RequestId, 0))
            {
                payload.Dispose();
                throw new InvalidDataException("Request replayed or request capacity reached.");
            }

            var decryptedRequest = new DecryptedRequest(envelope.KeyId, envelope.RequestId, route, aesKey, payload, plaintext);
            plaintext = null; // The JsonDocument may retain the supplied memory until it is disposed.
            return decryptedRequest;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(aesKey);
            throw;
        }
        finally
        {
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public ResponseEnvelope EncryptResponse(object response, DecryptedRequest request)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
        var iv = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesTagSize];
        using (var aes = new AesGcm(request.AesKey, AesTagSize))
        {
            aes.Encrypt(iv, plaintext, ciphertext, tag,
                BuildAdditionalData(request.KeyId, request.RequestId, request.Route, "response"));
        }

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);
        CryptographicOperations.ZeroMemory(plaintext);
        return new ResponseEnvelope(1, request.KeyId, request.RequestId,
            Convert.ToBase64String(iv), Convert.ToBase64String(combined));
    }

    private static byte[] BuildAdditionalData(string keyId, string requestId, string route, string direction) =>
        Encoding.UTF8.GetBytes($"Mma.SqlStudio.E2EE.v1|{keyId}|{requestId}|{route}|{direction}");

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _rsa.Dispose();

    public sealed record RequestEnvelope(
        int Version,
        string Algorithm,
        string KeyId,
        string RequestId,
        string WrappedKey,
        string Iv,
        string Ciphertext);

    public sealed record ResponseEnvelope(
        int Version,
        string KeyId,
        string RequestId,
        string Iv,
        string Ciphertext);

    public sealed class DecryptedRequest : IDisposable
    {
        private readonly byte[] _plaintext;

        internal DecryptedRequest(string keyId, string requestId, string route, byte[] aesKey, JsonDocument payload, byte[] plaintext)
        {
            KeyId = keyId;
            RequestId = requestId;
            Route = route;
            AesKey = aesKey;
            Payload = payload;
            _plaintext = plaintext;
        }

        public string KeyId { get; }
        public string RequestId { get; }
        public string Route { get; }
        public byte[] AesKey { get; }
        public JsonDocument Payload { get; }

        public void Dispose()
        {
            Payload.Dispose();
            CryptographicOperations.ZeroMemory(AesKey);
            CryptographicOperations.ZeroMemory(_plaintext);
        }
    }
}
