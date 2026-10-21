using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace KickFox
{
    /// <summary>
    /// Verifies that an update package was signed with the KickFox release key.
    /// Signature format: Base64( RSA PKCS#1 v1.5 signature over SHA-256 of the raw zip bytes ).
    /// The public key is embedded as the manifest resource "UpdatePublicKey.xml".
    /// Shared by KickFox.exe (before launching the updater) and Updater.exe (before writing files).
    /// </summary>
    public static class UpdateVerifier
    {
        public const string PublicKeyResourceName = "UpdatePublicKey.xml";
        private const int PROV_RSA_AES = 24; // CSP type that supports SHA-256 signatures

        public static bool Verify(string zipPath, string sigPath, out string reason)
        {
            reason = null;
            try
            {
                string publicKeyXml = LoadPublicKey();
                if (string.IsNullOrEmpty(publicKeyXml)) { reason = "No update public key is embedded in this build."; return false; }
                if (!File.Exists(zipPath)) { reason = "Update package not found."; return false; }
                if (!File.Exists(sigPath)) { reason = "Signature file not found."; return false; }

                byte[] signature = Convert.FromBase64String(File.ReadAllText(sigPath).Trim());
                byte[] data = File.ReadAllBytes(zipPath);

                using (var rsa = new RSACryptoServiceProvider(new CspParameters(PROV_RSA_AES)))
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(publicKeyXml);
                    if (!rsa.VerifyData(data, "SHA256", signature))
                    {
                        reason = "Signature does not match. The package may be corrupted or tampered with.";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        private static string LoadPublicKey()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(PublicKeyResourceName))
            {
                if (s == null) return null;
                using (var reader = new StreamReader(s)) return reader.ReadToEnd();
            }
        }
    }
}