using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Xml;
using Csas.MessageInspector.MessageStorage.Api;
using Csas.MessageInspector.MessageStorage.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA1031 // catch without throw

namespace Csas.MessageInspector.MessageStorage.Soap
{
    /// <summary>
    /// WCF Message Inspector, který zachytává SOAP requesty a response a ukládá je
    /// do souborového systému pomocí <see cref="IMessageStorage"/>.
    /// </summary>
    /// <remarks>
    /// Použití – přidání behavioru na WCF klienta:
    /// <code>
    /// client.Endpoint.EndpointBehaviors.Add(
    ///     new StoringMessageInspector(messageStorage, options));
    /// </code>
    /// </remarks>
    public class StoringMessageInspector : IClientMessageInspector, IEndpointBehavior
    {
        private readonly IMessageStorage messageStorage;
        private readonly MessageStorageOptions options;
        private readonly ILogger logger;

        /// <summary>
        /// Vytvoří novou instanci <see cref="StoringMessageInspector"/>.
        /// </summary>
        public StoringMessageInspector(
            IMessageStorage messageStorage,
            MessageStorageOptions options,
            ILogger<StoringMessageInspector> logger = null)
        {
            this.messageStorage = messageStorage ?? throw new ArgumentNullException(nameof(messageStorage));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = (ILogger)logger ?? NullLogger.Instance;
        }

        // ── IClientMessageInspector ──────────────────────────────────────────────

        /// <inheritdoc />
        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            if (!options.IsEnabled)
                return null;

            var correlationId = Guid.NewGuid();
            var url = ResolveUrl(request, channel);

            try
            {
                var content = ReadMessageAsString(ref request);
                messageStorage.StoreRequest(url, correlationId, content);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Nastala chyba při ukládání SOAP requestu pro URL '{Url}'.", url);
            }

            // correlationState nese URL i correlationId, aby AfterReceiveReply
            // mohl uložit response do správného adresáře
            return new SoapCorrelationContext(url, correlationId);
        }

        /// <inheritdoc />
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            if (!options.IsEnabled)
                return;

            var context = correlationState as SoapCorrelationContext;
            if (context == null)
                return;

            try
            {
                var content = ReadMessageAsString(ref reply);
                messageStorage.StoreResponse(context.Url, context.CorrelationId, content);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Nastala chyba při ukládání SOAP response pro URL '{Url}'.", context.Url);
            }
        }

        // ── IEndpointBehavior ────────────────────────────────────────────────────

        /// <inheritdoc />
        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(this);
        }

        /// <inheritdoc />
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }

        /// <inheritdoc />
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) { }

        /// <inheritdoc />
        public void Validate(ServiceEndpoint endpoint) { }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Vytvoří kopii zprávy a vrátí jej jako XML řetězec, aniž by zprávu spotřeboval.
        /// </summary>
        private static string ReadMessageAsString(ref Message message)
        {
            // CreateBufferedCopy spotřebuje původní zprávu; nahradíme ji novou kopií
            var buffer = message.CreateBufferedCopy(int.MaxValue);
            message = buffer.CreateMessage();

            var copy = buffer.CreateMessage();
            try
            {
                var sb = new StringBuilder();
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    Encoding = Encoding.UTF8
                };

                using (var sw = new StringWriter(sb))
                using (var xmlWriter = XmlWriter.Create(sw, settings))
                {
                    copy.WriteMessage(xmlWriter);
                }

                return sb.ToString();
            }
            finally
            {
                copy.Close();
                buffer.Close();
            }
        }

        private static string ResolveUrl(Message request, IClientChannel channel)
        {
            var contextChannel = channel as IContextChannel;
            var remoteUri = contextChannel?.RemoteAddress?.Uri;
            if (remoteUri != null)
                return remoteUri.AbsoluteUri;

            return request.Headers.To?.ToString() ?? "unknown";
        }
    }

    /// <summary>
    /// Interní přenosový objekt pro korelaci SOAP requestu a response.
    /// </summary>
    internal sealed class SoapCorrelationContext
    {
        public string Url { get; }
        public Guid CorrelationId { get; }

        public SoapCorrelationContext(string url, Guid correlationId)
        {
            Url = url;
            CorrelationId = correlationId;
        }
    }
}
