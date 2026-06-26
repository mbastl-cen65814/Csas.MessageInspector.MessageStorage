using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Csas.MessageInspector.MessageStorage.Api;
using Csas.MessageInspector.MessageStorage.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA1031 // catch without throw

namespace Csas.MessageInspector.MessageStorage
{
    /// <summary>
    /// Implementace <see cref="IMessageStorage"/> ukládající zprávy do souborového systému.
    /// </summary>
    /// <remarks>
    /// Struktura adresářů:
    /// <code>
    /// {BaseDirectory}/
    ///   api.example.com/
    ///     v1/
    ///       users/
    ///         {guid}.request.txt
    ///         {guid}.response.txt
    /// </code>
    /// </remarks>
    public class FileMessageStorage : IMessageStorage
    {
        private const string RequestSuffix = ".request.txt";
        private const string ResponseSuffix = ".response.txt";

        private readonly MessageStorageOptions options;
        private readonly ILogger logger;

        /// <summary>
        /// Vytvoří novou instanci <see cref="FileMessageStorage"/>.
        /// </summary>
        public FileMessageStorage(MessageStorageOptions options, ILogger<FileMessageStorage> logger)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = (ILogger)logger ?? NullLogger.Instance;
        }

        /// <inheritdoc />
        public void StoreRequest(string url, Guid correlationId, string content)
        {
            WriteFile(url, correlationId, RequestSuffix, content);
        }

        /// <inheritdoc />
        public void StoreResponse(string url, Guid correlationId, string content)
        {
            WriteFile(url, correlationId, ResponseSuffix, content);
        }

        /// <inheritdoc />
        public string ReadRequest(string url, Guid correlationId)
        {
            return ReadFile(url, correlationId, RequestSuffix);
        }

        /// <inheritdoc />
        public string ReadResponse(string url, Guid correlationId)
        {
            return ReadFile(url, correlationId, ResponseSuffix);
        }

        /// <inheritdoc />
        public IEnumerable<Guid> GetCorrelationIds(string url)
        {
            var dir = GetDirectoryPath(url);

            if (!Directory.Exists(dir))
                yield break;

            foreach (var filePath in Directory.GetFiles(dir, "*" + RequestSuffix))
            {
                var fileName = Path.GetFileName(filePath);
                var guidStr = fileName.Substring(0, fileName.Length - RequestSuffix.Length);
                Guid guid;
                if (Guid.TryParse(guidStr, out guid))
                    yield return guid;
            }
        }

        private void WriteFile(string url, Guid correlationId, string suffix, string content)
        {
            if (!options.IsEnabled)
                return;

            try
            {
                var dir = GetDirectoryPath(url);
                Directory.CreateDirectory(dir);

                var filePath = Path.Combine(dir, correlationId.ToString() + suffix);
                File.WriteAllText(filePath, content ?? string.Empty, Encoding.UTF8);

                logger.LogDebug("Zpráva uložena: {FilePath}", filePath);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Nastala chyba při ukládání zprávy pro URL '{Url}'.", url);
            }
        }

        private string ReadFile(string url, Guid correlationId, string suffix)
        {
            try
            {
                var filePath = Path.Combine(GetDirectoryPath(url), correlationId.ToString() + suffix);

                if (!File.Exists(filePath))
                    return null;

                return File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Nastala chyba při čtení zprávy pro URL '{Url}'.", url);
                return null;
            }
        }

        private string GetDirectoryPath(string url)
        {
            var baseDir = GetRootedBaseDirectory();
            var relativePath = UrlHelper.ToRelativePath(url);
            return Path.Combine(baseDir, relativePath);
        }

        private string GetRootedBaseDirectory()
        {
            var baseDir = options.BaseDirectory;
            if (Path.IsPathRooted(baseDir))
                return baseDir;

            var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return Path.Combine(assemblyDir, baseDir);
        }
    }
}
