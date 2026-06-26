using System;
using System.Collections.Generic;

namespace Csas.MessageInspector.MessageStorage.Api
{
    /// <summary>
    /// Interface pro ukládání a čtení zpráv Message Inspectoru.
    /// </summary>
    public interface IMessageStorage
    {
        /// <summary>
        /// Uloží request jako textový soubor. Soubor je pojmenován podle <paramref name="correlationId"/>.
        /// </summary>
        /// <param name="url">URL volaného endpointu, určuje podadresář pro uložení.</param>
        /// <param name="correlationId">Identifikátor párující request s odpovídajícím response.</param>
        /// <param name="content">Obsah requestu (XML pro SOAP, JSON nebo jiný text pro REST).</param>
        void StoreRequest(string url, Guid correlationId, string content);

        /// <summary>
        /// Uloží response jako textový soubor. Soubor je pojmenován podle <paramref name="correlationId"/>.
        /// </summary>
        /// <param name="url">URL volaného endpointu, určuje podadresář pro uložení.</param>
        /// <param name="correlationId">Identifikátor párující response s odpovídajícím requestem.</param>
        /// <param name="content">Obsah response (XML pro SOAP, JSON nebo jiný text pro REST).</param>
        void StoreResponse(string url, Guid correlationId, string content);

        /// <summary>
        /// Načte dříve uložený request.
        /// </summary>
        /// <param name="url">URL volaného endpointu.</param>
        /// <param name="correlationId">Identifikátor requestu.</param>
        /// <returns>Obsah requestu, nebo <c>null</c> pokud soubor neexistuje.</returns>
        string ReadRequest(string url, Guid correlationId);

        /// <summary>
        /// Načte dříve uložený response.
        /// </summary>
        /// <param name="url">URL volaného endpointu.</param>
        /// <param name="correlationId">Identifikátor response.</param>
        /// <returns>Obsah response, nebo <c>null</c> pokud soubor neexistuje.</returns>
        string ReadResponse(string url, Guid correlationId);

        /// <summary>
        /// Vrátí seznam identifikátorů (GUID) všech uložených párů request/response pro danou URL.
        /// </summary>
        /// <param name="url">URL volaného endpointu.</param>
        /// <returns>Výčet identifikátorů.</returns>
        IEnumerable<Guid> GetCorrelationIds(string url);
    }
}
