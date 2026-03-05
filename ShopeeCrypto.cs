
using System;
using System.Security.Cryptography;
using System.Text;

namespace ShopeeIntegration
{
    public static class ShopeeCrypto
    {
        /// <summary>
        /// Calcula HMAC-SHA256 e retorna em hex minúsculo.
        /// </summary>
        public static string ComputeHmacSha256(string key, string data)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (data is null) data = string.Empty;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Resolve a chave do parceiro para bytes. Chaves que começam com "shpk" são
        /// hex-encoded (após o prefixo) e devem ser decodificadas; caso contrário usa UTF-8.
        /// </summary>
        public static byte[] GetPartnerKeyBytes(string partnerKey)
        {
            if (partnerKey is null) throw new ArgumentNullException(nameof(partnerKey));
            if (partnerKey.Length > 4 &&
                partnerKey.StartsWith("shpk", StringComparison.OrdinalIgnoreCase))
            {
                var hex = partnerKey.Substring(4);
                if (hex.Length % 2 == 0 && IsHexString(hex))
                    return Convert.FromHexString(hex);
            }
            return Encoding.UTF8.GetBytes(partnerKey);
        }

        private static bool IsHexString(string s)
        {
            foreach (var c in s)
                if (!char.IsAsciiHexDigit(c)) return false;
            return true;
        }

        /// <summary>
        /// HMAC-SHA256 usando a chave do parceiro Shopee (suporta formato "shpk" + hex).
        /// Retorna digest em hex minúsculo.
        /// </summary>
        public static string ComputeHmacSha256PartnerKey(string partnerKey, string data)
        {
            if (partnerKey is null) throw new ArgumentNullException(nameof(partnerKey));
            if (data is null) data = string.Empty;
            var keyBytes = GetPartnerKeyBytes(partnerKey);
            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}