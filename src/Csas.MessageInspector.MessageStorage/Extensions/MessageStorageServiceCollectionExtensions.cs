using System;
using Csas.MessageInspector.MessageStorage.Api;
using Csas.MessageInspector.MessageStorage.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Csas.MessageInspector.MessageStorage.Extensions
{
    /// <summary>
    /// Extensions pro registraci ukládání zpráv Message Inspectoru do DI kontejneru.
    /// </summary>
    public static class MessageStorageServiceCollectionExtensions
    {
        /// <summary>
        /// Registruje služby pro ukládání zpráv Message Inspectoru do DI kontejneru.
        /// </summary>
        /// <param name="services">DI kontejner.</param>
        /// <param name="configureOptions">Konfigurace nastavení úložiště.</param>
        /// <returns>DI kontejner pro řetězení volání.</returns>
        public static IServiceCollection AddMessageInspectorStorage(
            this IServiceCollection services,
            Action<MessageStorageOptions> configureOptions)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

            var options = new MessageStorageOptions();
            configureOptions(options);

            services.AddSingleton(options);
            services.AddSingleton<IMessageStorage>(sp =>
                new FileMessageStorage(
                    sp.GetRequiredService<MessageStorageOptions>(),
                    sp.GetRequiredService<ILogger<FileMessageStorage>>()));

            services.AddTransient<Rest.StoringHttpMessageHandler>();

            return services;
        }
    }
}
