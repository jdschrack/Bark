using Bark.Tools;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Bark.Extensions
{
    /// <summary>
    /// String encryption utilities for obfuscating configuration values.
    /// NOTE: This provides obfuscation only, not cryptographic security.
    /// The key is embedded in the binary and can be extracted by decompilation.
    /// </summary>
    public static class StringExtensions
    {
        // Key for obfuscation - not intended for secure encryption
        private static readonly string _key = "ShibaAspectAndTundraSmellLikeDog";

        // Legacy zero IV for backwards compatibility with existing encrypted strings
        private static readonly byte[] _legacyIv = new byte[16];

        public static string EncryptString(this string plainText)
        {
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_key);
                aes.IV = _legacyIv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }

                        array = memoryStream.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(array);
        }

        public static string DecryptString(this string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            try
            {
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(_key);
                    aes.IV = _legacyIv;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(buffer))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (FormatException ex)
            {
                Logging.Warning($"DecryptString: Invalid base64 format - {ex.Message}");
                return string.Empty;
            }
            catch (CryptographicException ex)
            {
                Logging.Warning($"DecryptString: Decryption failed - {ex.Message}");
                return string.Empty;
            }
        }
    }
}

