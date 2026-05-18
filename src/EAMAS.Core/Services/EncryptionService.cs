using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace EAMAS.Core.Services
{
    /// <summary>
    /// Encrypts sensitive values (API keys, tokens) using AES-256-GCM before MongoDB storage.
    /// Uses a machine-derived key so values are unreadable if the database is accessed from
    /// a different machine.  The key is derived via PBKDF2(SHA-256) using the Windows
    /// MachineGuid as the salt, making it computationally infeasible to brute-force even
    /// if an attacker knows the machine name.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class EncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService()
        {
            // Use the stable Windows MachineGuid as salt (unique per installation).
            // Fall back to MachineName if registry access is denied (e.g. unit-test sandbox).
            var salt = GetMachineGuidBytes();

            // PBKDF2 with 100,000 iterations is resistant to offline brute-force attacks.
            _key = Rfc2898DeriveBytes.Pbkdf2(
                password:      Encoding.UTF8.GetBytes(Environment.MachineName + "EAMAS_FIELD_ENC_v2"),
                salt:          salt,
                iterations:    100_000,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength:  32);   // 32 bytes → AES-256
        }

        private static byte[] GetMachineGuidBytes()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography", writable: false);
                var guid = key?.GetValue("MachineGuid") as string;
                if (!string.IsNullOrWhiteSpace(guid))
                    return Encoding.UTF8.GetBytes(guid);
            }
            catch { /* registry unavailable in sandboxed environments */ }

            // Stable fallback: hash of MachineName (deterministic, but weaker than MachineGuid)
            return SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName + "EAMAS_SALT_FB"));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            RandomNumberGenerator.Fill(nonce);

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // nonce(12) + tag(16) + cipher
            var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
            nonce.CopyTo(combined, 0);
            tag.CopyTo(combined, nonce.Length);
            cipherBytes.CopyTo(combined, nonce.Length + tag.Length);

            return Convert.ToBase64String(combined);
        }

        public string Decrypt(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return encryptedBase64;

            try
            {
                var combined = Convert.FromBase64String(encryptedBase64);
                int nonceLen = AesGcm.NonceByteSizes.MaxSize;
                int tagLen = AesGcm.TagByteSizes.MaxSize;

                var nonce = combined[..nonceLen];
                var tag = combined[nonceLen..(nonceLen + tagLen)];
                var cipherBytes = combined[(nonceLen + tagLen)..];

                var plainBytes = new byte[cipherBytes.Length];
                using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // Return as-is if not encrypted (migration of legacy plain values)
                return encryptedBase64;
            }
        }
    }
}
