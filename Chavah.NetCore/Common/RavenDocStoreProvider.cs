﻿using System.IO;
using System.Security.Cryptography.X509Certificates;
using BitShuva.Chavah.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace BitShuva.Chavah.Common
{
    /// <summary>
    /// Adds a Raven document store to the dependency injection services.
    /// </summary>
    public static class RavenDocStoreProvider
    {
        /// <summary>
        /// Adds a Raven <see cref="IDocumentStore"/> singleton to the dependency injection services.
        /// </summary>
        /// <param name="svc"></param>
        /// <returns></returns>
        public static IServiceCollection AddRavenDocStore(this IServiceCollection svc)
        {
            var settings = svc.BuildServiceProvider().GetRequiredService<IOptions<AppSettings>>().Value.DbConnection;
            var docStore = new DocumentStore
            {
                Urls = new[] { settings.Url },
                Database = settings.DatabaseName
            };

            // Configure the certificate if we have one in app settings.
            if (!string.IsNullOrEmpty(settings.CertFileName))
            {
                var provider = svc.BuildServiceProvider();
                var host = provider.GetRequiredService<IHostingEnvironment>();
                var certFilePath = Path.Combine(host.ContentRootPath, settings.CertFileName);
                docStore.Certificate = new X509Certificate2(certFilePath, settings.CertPassword);
            }

            docStore.Initialize();
            svc.AddSingleton<IDocumentStore>(docStore);
            return svc;
        }
    }
}
