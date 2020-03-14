using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Helium.Util
{
    public static class CertUtil
    {
        public static async Task<X509Certificate2> LoadOrGenerateCertificate(string path) {
            if(!File.Exists(path)) {
                var distinguishedName = new X500DistinguishedName("CN=helium");
                using var rsa = RSA.Create(2048);
                
                var certRequest = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false
                ));

                var cert = certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1000));
                await File.WriteAllBytesAsync(path, cert.Export(X509ContentType.Pfx));
                return cert;
            }

            return new X509Certificate2(await File.ReadAllBytesAsync(path));
        }
    }
}