﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using Castle.Core.Logging;
using CQRS.Kernel.MultitenantSupport;
using CQRS.Shared.MultitenantSupport;
using ikvm.extensions;
using Jarvis.DocumentStore.Client;
using Jarvis.DocumentStore.Client.Model;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Jobs.PollingJobs;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.DocumentStore.Core.Services;
using Jarvis.DocumentStore.Core.Support;
using Jarvis.DocumentStore.Shared.Jobs;
using Microsoft.SqlServer.Server;
using Newtonsoft.Json;
using DocumentFormat = Jarvis.DocumentStore.Core.Domain.Document.DocumentFormat;
using DocumentHandle = Jarvis.DocumentStore.Client.Model.DocumentHandle;

namespace Jarvis.DocumentStore.Core.Jobs.OutOfProcessPollingJobs
{
    public abstract class AbstractOutOfProcessPollerFileJob : IPollerJob
    {
        public bool IsOutOfProcess
        {
            get { return true; }
        }

        public String QueueName { get; protected set; }

        public PipelineId PipelineId { get; protected set; }

        public virtual bool IsActive { get { return true; } }

        public ILogger Logger { get; set; }

        public ConfigService ConfigService { get; set; }

        public DocumentStoreConfiguration DocumentStoreConfiguration { get; set; }

        public Boolean Started { get; private set; }

        private String _identity;

        private String _handle;

        public AbstractOutOfProcessPollerFileJob()
        {
            _identity = Environment.MachineName + "_" + System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        private List<DsEndpoint> _dsEndpoints;

        private class DsEndpoint
        {
            public DsEndpoint(string getNextJobUrl, string setJobCompleted, Uri baseUrl)
            {
                GetNextJobUrl = getNextJobUrl;
                SetJobCompleted = setJobCompleted;
                BaseUrl = baseUrl;
            }

            public String GetNextJobUrl { get; private set; }

            public String SetJobCompleted { get; private set; }

            public Uri BaseUrl { get; set; }
        }

        public void Start(List<String> documentStoreAddressUrls, String handle)
        {
            if (Started) return;
            _handle = handle;
            if (documentStoreAddressUrls.Count == 0) throw new ArgumentException("Component needs at least a document store url", "documentStoreAddressUrls");
            _dsEndpoints = documentStoreAddressUrls
                .Select(addr => new DsEndpoint(
                        addr.TrimEnd('/') + "/queue/getnextjob",
                        addr.TrimEnd('/') + "/queue/setjobcomplete",
                        new Uri(addr)))
                .ToList();
            Start(DocumentStoreConfiguration.QueueStreamPollInterval);
            Started = true;
        }

        public void Stop()
        {
            if (!Started) return;
            Started = false;
            _pollingTimer.Dispose();
            _pollingTimer = null;
        }

        private Timer _pollingTimer;

        private void Start(Int32 pollingTimeInMs)
        {
            _pollingTimer = new Timer(pollingTimeInMs);
            _pollingTimer.Elapsed += pollingTimer_Elapsed;
            _pollingTimer.Start();
        }

        void pollingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _pollingTimer.Stop();
            String workingFolder = null;
            try
            {
                do
                {
                    QueuedJob nextJob = DsGetNextJob();
                    if (nextJob == null) return;

                    var baseParameters = ExtractJobParameters(nextJob);
                    //remember to enter the right tenant.
                    TenantContext.Enter(new TenantId(baseParameters.TenantId));
                    workingFolder = Path.Combine(
                            ConfigService.GetWorkingFolder(baseParameters.TenantId, GetType().Name),
                            baseParameters.InputBlobId
                        );
                    if (Directory.Exists(workingFolder)) Directory.Delete(workingFolder, true);
                    Directory.CreateDirectory(workingFolder);
                    try
                    {
                        var task = OnPolling(baseParameters, workingFolder);
                        Logger.DebugFormat("Finished Job: {0} with result;", nextJob.Id, task.Result);
                        DsSetJobExecuted(QueueName, nextJob.Id, "");
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorFormat(ex, "Error executing queued job {0} on tenant {1}", nextJob.Id,
                            nextJob.Parameters[JobKeys.TenantId]);
                        DsSetJobExecuted(QueueName, nextJob.Id, ex.Message);
                    }
                    finally
                    {
                        DeleteWorkingFolder(workingFolder);
                    }


                } while (true); //Exit is in the internal loop

            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Poller error: {0}", ex.Message);
            }
            finally
            {
                if (Started && _pollingTimer != null) _pollingTimer.Start();
            }
        }

        private void DsSetJobExecuted(string queueName, string jobId, string message)
        {
            string pollerResult;
            using (WebClientEx client = new WebClientEx())
            {
                //TODO: use round robin if a document store is down.
                var firstUrl = _dsEndpoints.First();
                var payload = JsonConvert.SerializeObject(new
                {
                    QueueName = queueName,
                    JobId = jobId,
                    ErrorMessage = message
                });
                Logger.DebugFormat("SetJobExecuted url: {0} with payload {1}", firstUrl.SetJobCompleted, payload);
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                pollerResult = client.UploadString(firstUrl.SetJobCompleted, payload);
                Logger.DebugFormat("SetJobExecuted Result: {0}", pollerResult);
            }

        }

        private static PollerJobParameters ExtractJobParameters(QueuedJob nextJob)
        {
            PollerJobParameters parameters = new PollerJobParameters();
            parameters.FileExtension = nextJob.Parameters[JobKeys.FileExtension];
            parameters.InputDocumentId = new DocumentId(nextJob.Parameters[JobKeys.DocumentId]);
            parameters.InputDocumentFormat = new DocumentFormat(nextJob.Parameters[JobKeys.Format]);
            parameters.InputBlobId = new BlobId(nextJob.Parameters[JobKeys.BlobId]);
            parameters.TenantId = new TenantId(nextJob.Parameters[JobKeys.TenantId]);
            parameters.All = nextJob.Parameters;
            return parameters;
        }

        private QueuedJob DsGetNextJob()
        {
            QueuedJob nextJob = null;
            string pollerResult;
            using (WebClientEx client = new WebClientEx())
            {
                //TODO: use round robin if a document store is down.
                var firstUrl = _dsEndpoints.First();
                var payload = JsonConvert.SerializeObject(new
                {
                    QueueName = this.QueueName,
                    Identity = this._identity,
                    Handle = this._handle,
                });
                Logger.DebugFormat("Polling url: {0} with payload {1}", firstUrl, payload);
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                pollerResult = client.UploadString(firstUrl.GetNextJobUrl, payload);
                Logger.DebugFormat("GetNextJobResult: {0}", pollerResult);
            }
            if (!pollerResult.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                nextJob = JsonConvert.DeserializeObject<QueuedJob>(pollerResult);
            }
            return nextJob;
        }

        protected async Task<Boolean> AddFormatToDocumentFromFile(string tenantId,
            DocumentId documentId,
            Client.Model.DocumentFormat format,
            string pathToFile,
            IDictionary<string, object> customData)
        {
            DocumentStoreServiceClient client = new DocumentStoreServiceClient(
               _dsEndpoints.First().BaseUrl, tenantId);
            AddFormatFromFileToDocumentModel model = new AddFormatFromFileToDocumentModel
            {
                CreatedById = this.PipelineId,
                DocumentId = documentId,
                Format = format,
                PathToFile = pathToFile
            };

            var response = await client.AddFormatToDocument(model, customData);
            return response != null;
        }

        protected async Task<Boolean> AddFormatToDocumentFromObject(string tenantId,
              DocumentId documentId,
              Client.Model.DocumentFormat format,
              Object obj,
              IDictionary<string, object> customData)
        {
            DocumentStoreServiceClient client = new DocumentStoreServiceClient(
               _dsEndpoints.First().BaseUrl, tenantId);
            AddFormatFromObjectToDocumentModel model = new AddFormatFromObjectToDocumentModel
            {
                CreatedById = this.PipelineId,
                DocumentId = documentId,
                Format = format,
                StringContent = JsonConvert.SerializeObject(obj),
            };
            var response = await client.AddFormatToDocument(model, customData);
            return response != null;
        }



        protected async Task<String> DownloadBlob(
            TenantId tenantId,
            BlobId blobId,
            String extension,
            String workingFolder)
        {
            String fileName = Path.Combine(workingFolder, blobId.toString() + "." + extension);
            DocumentStoreServiceClient client = new DocumentStoreServiceClient(
                _dsEndpoints.First().BaseUrl, tenantId);
            using (var reader = client.OpenBlobIdForRead(blobId))
            {
                using (var downloaded = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    var stream = await reader.ReadStream;
                    stream.CopyTo(downloaded);
                }
            }
            Logger.DebugFormat("Downloaded blob {0} for tenant {1} in local file {2}", blobId, tenantId, fileName);
            return fileName;
        }

        private void DeleteWorkingFolder(String workingFolder)
        {
            if (!String.IsNullOrEmpty(workingFolder))
            {
                try
                {
                    if (Directory.Exists(workingFolder))
                        Directory.Delete(workingFolder, true);
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat(ex, "Error deleting {0}", workingFolder);
                }
            }
        }


        protected abstract Task<Boolean> OnPolling(
            PollerJobParameters parameters,
            String workingFolder);
    }

}