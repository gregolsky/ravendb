using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FastTests;
using Raven.Server.Commercial;
using Raven.Server.Utils;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FastTests.Server.Certificates;

public class AcmeAriTests : NoDisposalNeeded
{
    public AcmeAriTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Creates a CA certificate and a leaf certificate issued by it, returning both.
    /// The CA cert is included in extraCerts so it can be passed to ComputeAriCertId.
    /// </summary>
    private static (X509Certificate2 LeafCert, X509Certificate2Collection ExtraCerts) CreateTestCertWithCA(string commonName)
    {
        var caCert = CertificateUtils.CreateCertificateAuthorityCertificate($"{commonName} CA", out var caSubjectName, generateNewKeyPair: true);

        CertificateUtils.CreateSelfSignedCertificateBasedOnPrivateKey(
            commonNameValue: commonName,
            issuerCN: caSubjectName,
            issuerKeyPair: (caCert.GetExportableRsaPrivateKey(), caCert.GetRSAPublicKey()),
            isClientCertificate: false,
            isCaCertificate: false,
            notAfter: DateTime.UtcNow.Date.AddMonths(3),
            certBytes: out var leafCertBytes,
            sans: [commonName, "localhost"]);

        var leafCert = new X509Certificate2(leafCertBytes, (string)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);

        var extraCerts = new X509Certificate2Collection();
        extraCerts.Add(caCert);

        return (leafCert, extraCerts);
    }

    [RavenFact(RavenTestCategory.Certificates)]
    public void ComputeAriCertId_WithCertIssuedByCA_ReturnsCorrectFormat()
    {
        var (leafCert, extraCerts) = CreateTestCertWithCA("ari-test.example.com");
        using (leafCert)
        {
            // ComputeAriCertId should return a string with exactly two base64url parts separated by '.'
            var certId = LetsEncryptClient.ComputeAriCertId(leafCert, extraCerts);

            Assert.NotNull(certId);
            Assert.NotEmpty(certId);

            var parts = certId.Split('.');
            Assert.Equal(2, parts.Length);

            // Each part should be a non-empty base64url encoded value
            Assert.NotEmpty(parts[0]);
            Assert.NotEmpty(parts[1]);

            // Verify base64url characters only (no +, /, or = padding)
            foreach (var part in parts)
            {
                Assert.DoesNotContain("+", part);
                Assert.DoesNotContain("/", part);
                Assert.DoesNotContain("=", part);
            }
        }
    }

    [RavenFact(RavenTestCategory.Certificates)]
    public void ComputeAriCertId_IsDeterministic_ForSameCertificate()
    {
        var (leafCert, extraCerts) = CreateTestCertWithCA("ari-deterministic.example.com");
        using (leafCert)
        {
            var certId1 = LetsEncryptClient.ComputeAriCertId(leafCert, extraCerts);
            var certId2 = LetsEncryptClient.ComputeAriCertId(leafCert, extraCerts);

            Assert.Equal(certId1, certId2);
        }
    }

    [RavenFact(RavenTestCategory.Certificates)]
    public void ComputeAriCertId_SerialNumberPart_MatchesCertificateSerial()
    {
        var (leafCert, extraCerts) = CreateTestCertWithCA("ari-serial.example.com");
        using (leafCert)
        {
            var certId = LetsEncryptClient.ComputeAriCertId(leafCert, extraCerts);
            var serialPart = certId.Split('.')[1];

            // Decode the base64url serial and compare with the certificate's hex-encoded serial
            var paddingNeeded = (4 - serialPart.Length % 4) % 4;
            var base64 = serialPart.Replace('-', '+').Replace('_', '/') + new string('=', paddingNeeded);
            var decodedSerial = Convert.FromBase64String(base64);
            var expectedSerial = Convert.FromHexString(leafCert.SerialNumber);

            Assert.Equal(expectedSerial, decodedSerial);
        }
    }

    [RavenFact(RavenTestCategory.Certificates)]
    public void ComputeAriCertId_SelfSignedCertWithNoIssuerInChain_ThrowsInvalidOperation()
    {
        // Create a truly self-signed cert (no intermediate CA): the chain has only 1 element
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=self-signed-only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var selfSigned = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
        using var cert = new X509Certificate2(selfSigned.Export(X509ContentType.Pfx));

        var ex = Assert.Throws<InvalidOperationException>(() => LetsEncryptClient.ComputeAriCertId(cert));
        Assert.Contains("fewer than 2 elements", ex.Message);
    }
}
