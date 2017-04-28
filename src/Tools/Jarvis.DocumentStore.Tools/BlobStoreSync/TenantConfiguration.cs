using Castle.Core.Logging;
using Jarvis.ConfigurationService.Client;
using Jarvis.DocumentStore.Core.Storage;
using Jarvis.DocumentStore.Core.Storage.GridFs;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.DocumentStore.Shell.BlobStoreSync
{
    public class TenantConfiguration
    {
        public TenantConfiguration(
            String tenantId,
            ILogger logger)
        {
            TenantId = tenantId;
            var eventStoreConnectionString = ConfigurationServiceClient.Instance.GetSetting("connectionStrings." + tenantId + ".events");
            EventStoreDb = GetDb(eventStoreConnectionString);

            var originalConnectionString = ConfigurationServiceClient.Instance.GetSetting("connectionStrings." + tenantId + ".originals");
            //Null counter service is used to ensure that no new blob could be created.
            OriginalGridFsBlobStore = new GridFsBlobStore(GetLegacyDb(originalConnectionString), null) { Logger = logger};
            var originalFileSystemStorage = ConfigurationServiceClient.Instance.GetSetting($"storage.fileSystem.{tenantId}-originals-baseDirectory");
            OriginalFileSystemBlobStore = new FileSystemBlobStore(GetDb(originalConnectionString), "originals.descriptor", originalFileSystemStorage, null) { Logger = logger };

            var artifactConnectionString = ConfigurationServiceClient.Instance.GetSetting("connectionStrings." + tenantId + ".artifacts");
            var artifactsFileSystemStorage = ConfigurationServiceClient.Instance.GetSetting($"storage.fileSystem.{tenantId}-artifacts-baseDirectory");
            ArtifactsFileSystemBlobStore = new FileSystemBlobStore(GetDb(artifactConnectionString), "artifacts.descriptor", artifactsFileSystemStorage, null) { Logger = logger };

            //Null counter service is used to ensure that no new blob could be created.
            ArtifactsGridFsBlobStore = new GridFsBlobStore(GetLegacyDb(artifactConnectionString), null) { Logger = logger };
        }

        public String TenantId { get; }

        public GridFsBlobStore OriginalGridFsBlobStore { get; private set; }

        public IMongoDatabase EventStoreDb { get; private set; }

        public GridFsBlobStore ArtifactsGridFsBlobStore { get; private set; }

        public FileSystemBlobStore OriginalFileSystemBlobStore { get; private set; }

        public FileSystemBlobStore ArtifactsFileSystemBlobStore { get; private set; }

        private MongoDatabase GetLegacyDb(String connectionString)
        {
            var url = new MongoUrl(connectionString);
            return new MongoClient(url).GetServer().GetDatabase(url.DatabaseName);
        }

        private IMongoDatabase GetDb(String connectionString)
        {
            var url = new MongoUrl(connectionString);
            return new MongoClient(url).GetDatabase(url.DatabaseName);
        }
    }
}
