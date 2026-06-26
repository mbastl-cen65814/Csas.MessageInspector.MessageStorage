using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Csas.MessageInspector.MessageStorage.Api;
using Csas.MessageInspector.MessageStorage.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA1031 // catch without throw

namespace Csas.MessageInspector.MessageStorage.Rest
{
    /// <summary>
    /// HTTP Message Handler, který zachytává REST requesty a response a ukládá je
    /// do souborového systému pomocí <see cref="IMessageStorage"/>.
    /// </summary>
    /// <remarks>
    /// Použití s HttpClientFactory:
    /// <code>
    /// services.AddHttpClient("MyClient")
    ///         .AddHttpMessageHandler&lt;StoringHttpMessageHandler&gt;();
    /// </code>
    /// Nebo manuální registrace:
    /// <code>
    /// var handler = new StoringHttpMessageHandler(messageStorage, options)
    /// {
    ///     InnerHandler = new HttpClientHandler()
    /// };
    /// var client = new HttpClient(handler);
    /// </code>
    /// </remarks>
    public class StoringHttpMessageHandler : DelegatingHandler
    {
        private readonly IMessageStorage messageStorage;
        private readonly MessageStorageOptions options;
        private readonly ILogger logger;

        /// <summary>
        /// Vytvoří novou instanci <see cref="StoringHttpMessageHandler"/>.
        /// </summary>
        public StoringHttpMessageHandler(
            IMessageStorage messageStorage,
            MessageStorageOptions options,
            ILogger<StoringHttpMessageHandler> logger = null)
        {
            this.messageStorage = messageStorage ?? throw new ArgumentNullException(nameof(messageStorage));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = (ILogger)logger ?? NullLogger.Instance;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!options.IsEnabled)
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var correlationId = Guid.NewGuid();
            var url = request.RequestUri?.AbsoluteUri ?? "unknown";

            await StoreRequestAsync(url, correlationId, request).ConfigureAwait(false);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            await StoreResponseAsync(url, correlationId, response).ConfigureAwait(false);

            return response;
        }

        private async Task StoreRequestAsync(string url, Guid correlationId, HttpRequestMessage request)
        {
            try
            {
                var content = await ReadHttpContentAsync(request.Content).ConfigureAwait(false);
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("{0} {1}", request.Method, request.RequestUri));
                foreach (var header in request.Headers)
                {
                    sb.AppendLine(string.Format("{0}: {1}", header.Key, string.Join(", ", header.Value)));
                }

                if (request.Content != null)
                {
                    foreach (var header in request.Content.Headers)
                    {
                        sb.AppendLine(string.Format("{0}: {1}", header.Key, string.Join(", ", header.Value)));
                    }
                }

                sb.AppendLine();
                sb.Append(content);

                messageStorage.StoreRequest(url, correlationId, sb.ToString());
            }
            catch (Exception e)
            {
                logger.LogError(e, "Nastala chyba při ukládání REST requestu pro URL '{Url}'.", url);
            }
        }

        private async Task StoreResponseAsync(string url, Guid correlationId, HttpResponseMessage response)
        {
            try
            {
                var content = await ReadHttpContentAsync(response.Content).ConfigureAwait(false);
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("HTTP/{0} {1} {2}", response.Version, (int)response.StatusCode, response.ReasonPhrase));
                foreach (var header in response.Headers)
                {
                    sb.AppendLine(string.Format("{0}: {1}", header.Key, string.Join(", ", header.Value)));
                }

                if (response.Content != null)
                {
                    foreach (var header in response.Content.Headers)
                    {
                        sb.AppendLine(string.Format("{0}: {1}", header.Key, string.Join(", ", header.Value)));
                    }
                }

                sb.AppendLine();
                sb.Append(content);

                messageStorage.StoreResponse(url, correlationId, sb.ToString());
            }
            catch (Exception e)
            {
                logger.LogError(e, "Nastala chyba při ukládání REST response pro URL '{Url}'.", url);
            }
        }

        private static async Task<string> ReadHttpContentAsync(HttpContent httpContent)
        {
            if (httpContent == null)
                return string.Empty;

            // LoadIntoBufferAsync zajistí, že obsah lze číst opakovaně
            await httpContent.LoadIntoBufferAsync().ConfigureAwait(false);
            return await httpContent.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}
