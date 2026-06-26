namespace Csas.MessageInspector.MessageStorage.Options
{
    /// <summary>
    /// Konfigurace Message Inspectoru.
    /// </summary>
    public class MessageStorageOptions
    {
        /// <summary>
        /// Zapíná / vypíná ukládání zpráv.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Vrací uloženou Response
        /// </summary>
        public bool UseStoragePrimary { get; set; } = false;

		/// <summary>
		/// Základní adresář, do kterého se budou ukládat zprávy.
		/// Cesta může být relativní (bude doplněna vůči umístění assembly) nebo absolutní.
		/// </summary>
		public string BaseDirectory { get; set; } = "../MessageStorage";
    }
}
