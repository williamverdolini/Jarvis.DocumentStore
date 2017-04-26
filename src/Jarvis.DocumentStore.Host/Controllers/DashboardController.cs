﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Jarvis.DocumentStore.Core.Jobs;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.DocumentStore.Core.ReadModel;
using Jarvis.DocumentStore.Core.Storage;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jarvis.DocumentStore.Host.Controllers
{
    public class DashboardController : ApiController, ITenantController
    {
        public IBlobStore BlobStore { get; set; }
        public IMongoDbReader<DocumentStats, string> DocStats { get; set; }
        public IDocumentWriter Handles { get; set; }

        public DashboardController(IBlobStore blobStore, IMongoDbReader<DocumentStats, string> docStats)
        {
            DocStats = docStats;
            BlobStore = blobStore;
        }

        [HttpGet]
        [Route("{tenantId}/dashboard")]
        public IHttpActionResult GetStats(TenantId tenantId)
        {
            var result = DocStats.Collection.Aggregate()
                .Group(BsonDocument.Parse("{_id:1, bytes:{$sum:'$Bytes'}, documents:{$sum:'$Files'}}"))
                .SingleOrDefault();

            int documents = result != null ? result["documents"].AsInt32 : 0;
            long bytes = result != null ? result["bytes"].AsInt64 : 0;
            long files = 0;

            var stats = new
            {
                Tenant = tenantId,
                Documents = documents,
                DocBytes = bytes,
                Handles = Handles.Count(),
                Files = files
            };

            return Ok(stats);
        }
    }
}
