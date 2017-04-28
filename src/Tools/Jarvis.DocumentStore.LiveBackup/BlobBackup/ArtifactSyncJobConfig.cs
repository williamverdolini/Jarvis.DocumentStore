﻿
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jarvis.DocumentStore.LiveBackup.Support;
using Jarvis.DocumentStore.Core.Storage;
using Castle.Core.Logging;

namespace Jarvis.DocumentStore.LiveBackup.BlobBackup
{
    /// <summary>
    /// Configuration for continuous backup, need only the connection for eventstore, destination directory
    /// and the connection to the original <see cref="IBlobStore"/> instance.
    /// </summary>
    public class ArtifactSyncJobConfig
    {
        public ArtifactSyncJobConfig(String baseDumpDirectory, Configuration.TenantSettings tenantSetting, ILogger logger)
        {
            EvenstoreConnection = tenantSetting.EventStoreConnectionString;
            OriginalBlobConnection = tenantSetting.GetBlobStore(logger);
            Directory = System.IO.Path.Combine(baseDumpDirectory, tenantSetting.TenantId);
            if (!System.IO.Directory.Exists(Directory))
                System.IO.Directory.CreateDirectory(Directory);
        }

        public String EvenstoreConnection { get; private set; }

        public IBlobStore OriginalBlobConnection { get; private set; }

        public String Directory { get; private set; }
    }
}
