using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Csas.MessageInspector.MessageStorage
{
    /// <summary>
    /// Pomocná třída pro převod URL na bezpečný relativní adresář.
    /// </summary>
    internal static class UrlHelper
    {
        private static readonly Regex InvalidCharPattern = new Regex(@"[^\w\-.]", RegexOptions.Compiled);

        /// <summary>
        /// Převede URL na relativní cestu použitelnou jako adresářovou strukturupro uložení REST API path requestů
        /// Např. <c>https://api.example.com/v1/users/create?id=1</c>
        /// → <c>api.example.com\v1\users\create</c>
        /// </summary>
        public static string ToRelativePath(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "_unknown";

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                return SanitizeSegment(url);

            var host = SanitizeSegment(uri.Host);

            // Rozebereme cestu na segmenty a každý segment zvlášť sanitizujeme
            var pathSegments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new string[pathSegments.Length + 1];
            parts[0] = host;

            for (int i = 0; i < pathSegments.Length; i++)
            {
                parts[i + 1] = SanitizeSegment(pathSegments[i]);
            }

            return string.Join(Path.DirectorySeparatorChar.ToString(), parts);
        }

        /// <summary>
        /// bezpečné nahrazení znaků - za _
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        private static string SanitizeSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return "_";

            var sanitized = InvalidCharPattern.Replace(segment, "_");
            return sanitized.Length > 0 ? sanitized : "_";
        }
    }
}
