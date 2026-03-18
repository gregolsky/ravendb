using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Raven.Server.Commercial;
using Xunit;

namespace FastTests.Server.Commercial
{
    public class AcmeAriTests : NoDisposalNeeded
    {
        [Fact]
        public void ComputeAriCertId_Returns_Correct_Format()
        {
            // Create a self-signed certificate with AKI extension
            var cert = CreateCertificateWithAki();

            var certId = LetsEncryptClient.ComputeAriCertId(cert);

            // The CertID format is: base64url(AKI_keyIdentifier) "." base64url(serial)
            Assert.Contains(".", certId);
            var parts = certId.Split('.');
            Assert.Equal(2, parts.Length);
            Assert.False(string.IsNullOrEmpty(parts[0]), "AKI part should not be empty");
            Assert.False(string.IsNullOrEmpty(parts[1]), "Serial part should not be empty");

            // Verify base64url encoding (no +, /, or = characters)
            foreach (var part in parts)
            {
                Assert.DoesNotContain("+", part);
                Assert.DoesNotContain("/", part);
                Assert.DoesNotContain("=", part);
            }
        }

        [Fact]
        public void ComputeAriCertId_Consistent_For_Same_Certificate()
        {
            var cert = CreateCertificateWithAki();

            var certId1 = LetsEncryptClient.ComputeAriCertId(cert);
            var certId2 = LetsEncryptClient.ComputeAriCertId(cert);

            Assert.Equal(certId1, certId2);
        }

        [Fact]
        public void ComputeAriCertId_Different_For_Different_Certificates()
        {
            var cert1 = CreateCertificateWithAki();
            var cert2 = CreateCertificateWithAki();

            var certId1 = LetsEncryptClient.ComputeAriCertId(cert1);
            var certId2 = LetsEncryptClient.ComputeAriCertId(cert2);

            // Different certificates should produce different CertIDs
            // (they will have different serial numbers at minimum)
            Assert.NotEqual(certId1, certId2);
        }

        [Fact]
        public void ComputeAriCertId_Throws_On_Null_Certificate()
        {
            Assert.Throws<ArgumentNullException>(() => LetsEncryptClient.ComputeAriCertId(null));
        }

        [Fact]
        public void RenewalInfoResponse_Deserialization()
        {
            var json = @"{
                ""suggestedWindow"": {
                    ""start"": ""2025-01-01T00:00:00Z"",
                    ""end"": ""2025-01-15T00:00:00Z""
                },
                ""explanationURL"": ""https://example.com/docs/renewal""
            }";

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<LetsEncryptClient.RenewalInfoResponse>(json);

            Assert.NotNull(result);
            Assert.NotNull(result.SuggestedWindow);
            Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.SuggestedWindow.Start.ToUniversalTime());
            Assert.Equal(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), result.SuggestedWindow.End.ToUniversalTime());
            Assert.Equal("https://example.com/docs/renewal", result.ExplanationUrl);
        }

        [Fact]
        public void RenewalInfoResponse_Deserialization_Without_ExplanationUrl()
        {
            var json = @"{
                ""suggestedWindow"": {
                    ""start"": ""2025-03-01T00:00:00Z"",
                    ""end"": ""2025-03-15T00:00:00Z""
                }
            }";

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<LetsEncryptClient.RenewalInfoResponse>(json);

            Assert.NotNull(result);
            Assert.NotNull(result.SuggestedWindow);
            Assert.Null(result.ExplanationUrl);
        }

        private static X509Certificate2 CreateCertificateWithAki()
        {
            var random = new SecureRandom();
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(new KeyGenerationParameters(random, 2048));
            var keyPair = keyPairGenerator.GenerateKeyPair();

            var certificateGenerator = new X509V3CertificateGenerator();

            var serialNumber = new BigInteger(20 * 8, random);
            certificateGenerator.SetSerialNumber(serialNumber);

            var subjectDn = new X509Name("CN=Test Certificate");
            certificateGenerator.SetIssuerDN(subjectDn);
            certificateGenerator.SetSubjectDN(subjectDn);

            certificateGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certificateGenerator.SetNotAfter(DateTime.UtcNow.AddYears(1));

            certificateGenerator.SetPublicKey(keyPair.Public);

            // Add Authority Key Identifier extension
            var authorityKeyIdentifier = new AuthorityKeyIdentifierStructure(keyPair.Public);
            certificateGenerator.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id, false, authorityKeyIdentifier);

            ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private, random);
            var bouncyCert = certificateGenerator.Generate(signatureFactory);

            // Convert to .NET X509Certificate2
            var store = new Pkcs12StoreBuilder().Build();
            var certEntry = new X509CertificateEntry(bouncyCert);
            store.SetCertificateEntry("cert", certEntry);
            store.SetKeyEntry("cert", new AsymmetricKeyEntry(keyPair.Private), new[] { certEntry });

            using (var ms = new System.IO.MemoryStream())
            {
                store.Save(ms, Array.Empty<char>(), random);
                return new X509Certificate2(ms.ToArray(), (string)null, X509KeyStorageFlags.MachineKeySet);
            }
        }
    }
}
