using System;
using System.Net;
using System.Threading.Tasks;
using Raven.Server.Commercial;
using Raven.Server.Routing;
using Sparrow.Json;

namespace Raven.Server.Web.System
{
    public sealed class AdminAriDebugHandler : ServerRequestHandler
    {
        [RavenAction("/admin/debug/certificates/ari-renewal-info", "GET", AuthorizationStatus.Operator,
            // intentionally not a debug info endpoint because it makes a live HTTP call to an external ACME server
            IsDebugInformationEndpoint = false)]
        public async Task GetAriRenewalInfo()
        {
            var currentCertificate = Server.Certificate;
            if (currentCertificate?.ServerCertificate == null)
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                using (ServerStore.ContextPool.AllocateOperationContext(out JsonOperationContext context))
                await using (var writer = new AsyncBlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Error");
                    writer.WriteString("No server certificate is configured.");
                    writer.WriteEndObject();
                }
                return;
            }

            var acmeUrl = Server.Configuration.Core.AcmeUrl;
            if (string.IsNullOrEmpty(acmeUrl))
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                using (ServerStore.ContextPool.AllocateOperationContext(out JsonOperationContext context))
                await using (var writer = new AsyncBlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Error");
                    writer.WriteString("No ACME URL is configured.");
                    writer.WriteEndObject();
                }
                return;
            }

            string certId = null;
            string certIdError = null;
            try
            {
                certId = LetsEncryptClient.ComputeAriCertId(currentCertificate.ServerCertificate);
            }
            catch (Exception e)
            {
                certIdError = e.Message;
            }

            var acmeClient = new LetsEncryptClient(acmeUrl);
            await acmeClient.FetchDirectory(ServerStore.ServerShutdown);

            using (ServerStore.ContextPool.AllocateOperationContext(out JsonOperationContext context))
            await using (var writer = new AsyncBlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                writer.WriteStartObject();

                writer.WritePropertyName("AcmeUrl");
                writer.WriteString(acmeUrl);
                writer.WriteComma();

                writer.WritePropertyName("CertificateThumbprint");
                writer.WriteString(currentCertificate.ServerCertificate.Thumbprint);
                writer.WriteComma();

                writer.WritePropertyName("CertificateSubject");
                writer.WriteString(currentCertificate.ServerCertificate.Subject);
                writer.WriteComma();

                writer.WritePropertyName("CertificateNotAfter");
                writer.WriteString(currentCertificate.ServerCertificate.NotAfter.ToUniversalTime().ToString("u"));
                writer.WriteComma();

                writer.WritePropertyName("AriCertId");
                if (certId != null)
                    writer.WriteString(certId);
                else
                    writer.WriteNull();
                writer.WriteComma();

                writer.WritePropertyName("AriCertIdError");
                if (certIdError != null)
                    writer.WriteString(certIdError);
                else
                    writer.WriteNull();
                writer.WriteComma();

                writer.WritePropertyName("SupportsAri");
                writer.WriteBool(acmeClient.SupportsAri);
                writer.WriteComma();

                writer.WritePropertyName("RenewalInfo");
                if (acmeClient.SupportsAri && certId != null)
                {
                    var renewalInfo = await acmeClient.GetRenewalInfo(currentCertificate.ServerCertificate, ServerStore.ServerShutdown);
                    if (renewalInfo != null)
                    {
                        writer.WriteStartObject();

                        writer.WritePropertyName("SuggestedWindow");
                        if (renewalInfo.SuggestedWindow != null)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("Start");
                            writer.WriteString(renewalInfo.SuggestedWindow.Start.ToUniversalTime().ToString("u"));
                            writer.WriteComma();
                            writer.WritePropertyName("End");
                            writer.WriteString(renewalInfo.SuggestedWindow.End.ToUniversalTime().ToString("u"));
                            writer.WriteEndObject();
                        }
                        else
                        {
                            writer.WriteNull();
                        }

                        writer.WriteComma();
                        writer.WritePropertyName("ExplanationURL");
                        if (renewalInfo.ExplanationURL != null)
                            writer.WriteString(renewalInfo.ExplanationURL);
                        else
                            writer.WriteNull();

                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WriteNull();
                    }
                }
                else
                {
                    writer.WriteNull();
                }

                writer.WriteEndObject();
            }
        }
    }
}
