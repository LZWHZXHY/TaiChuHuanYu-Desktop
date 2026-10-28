using System.Security.Cryptography;
using System.Text;

namespace TaiChuWeb_V2.Services
{
    /// <summary>AES-256 加解密（用于存储用户自带 AI 的 API Key）。</summary>
    public static class AiKeyCrypto
    {
        private static byte[] DeriveKey(string secret)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        }

        public static string Encrypt(string plain, string secret)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            using var aes = Aes.Create();
            aes.Key = DeriveKey(secret);
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plain);
            var cipher = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(aes.IV) + ":" + Convert.ToBase64String(cipher);
        }

        public static string Decrypt(string data, string secret)
        {
            if (string.IsNullOrEmpty(data)) return string.Empty;
            try
            {
                var parts = data.Split(':');
                if (parts.Length != 2) return string.Empty;
                var iv = Convert.FromBase64String(parts[0]);
                var cipher = Convert.FromBase64String(parts[1]);
                using var aes = Aes.Create();
                aes.Key = DeriveKey(secret);
                aes.IV = iv;
                using var dec = aes.CreateDecryptor();
                var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return string.Empty; }
        }

        /// <summary>脱敏显示：sk-****abcd</summary>
        public static string Mask(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            if (plain.Length <= 8) return "****";
            return plain[..4] + "****" + plain[^4..];
        }
    }
}
