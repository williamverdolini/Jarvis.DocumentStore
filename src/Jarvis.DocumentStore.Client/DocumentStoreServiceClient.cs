﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Jarvis.DocumentStore.Client
{
    public class DocumentStoreServiceClient
    {
        readonly Uri _apiRoot;
        public string TempFolder { get; set; }

        public DocumentStoreServiceClient(Uri apiRoot)
        {
            _apiRoot = apiRoot;
            TempFolder = Path.Combine(Path.GetTempPath(), "jarvis.client");
        }

        public string ZipHtmlPage(string pathToFile)
        {
            if (!Directory.Exists(TempFolder))
                Directory.CreateDirectory(TempFolder);

            string pathToZip = Path.ChangeExtension(Path.Combine(
                TempFolder,
                Path.GetFileName(pathToFile)
            ), ".htmlzip");

            File.Delete(pathToZip);

            using (ZipArchive zip = ZipFile.Open(pathToZip, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(pathToFile, Path.GetFileName(pathToFile));
                var attachmentFolder = FindAttachmentFolder(pathToFile);
                if (attachmentFolder != null)
                {
                    var subfolderName = Path.GetFileName(attachmentFolder);
                    foreach (var fname in Directory.GetFiles(attachmentFolder))
                    {
                        // filter some extensions?
                        zip.CreateEntryFromFile(
                            fname,
                            subfolderName + "/" + Path.GetFileName(fname)
                        );
                    }
                }
            }

            return pathToZip;
        }

        static string FindAttachmentFolder(string pathToFile)
        {
            var attachmentFolder = Path.Combine(
                Path.GetDirectoryName(pathToFile),
                Path.GetFileNameWithoutExtension(pathToFile) + "_files"
                );

            if (Directory.Exists(attachmentFolder))
            {
                return attachmentFolder;
            }

            return null;
        }


        public async Task Upload(string pathToFile, string resourceId, IDictionary<string, object> customData = null)
        {
            var fileExt = Path.GetExtension(pathToFile).ToLowerInvariant();
            if (fileExt == ".html" || fileExt == ".htm")
            {
                var zippedFile = ZipHtmlPage(pathToFile);
                try
                {
                    await InnerUpload(zippedFile, resourceId, customData);
                    return;
                }
                finally
                {
                    File.Delete(zippedFile);
                }
            }

            await InnerUpload(pathToFile, resourceId, customData);
        }

        private async Task InnerUpload(
            string pathToFile,
            string resourceId,
            IDictionary<string, object> customData
        )
        {
            string fileName = Path.GetFileNameWithoutExtension(pathToFile);
            string fileNameWithExtension = Path.GetFileName(pathToFile);

            using (var client = new HttpClient())
            {
                using (var content = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    using (var sourceStream = File.OpenRead(pathToFile))
                    {
                        content.Add(
                            new StreamContent(sourceStream),
                            fileName, fileNameWithExtension
                        );

                        if (customData != null)
                        {
                            var stringContent = new StringContent(await ToJson(customData));
                            content.Add(stringContent, "custom-data");
                        }

                        var endPoint = new Uri(_apiRoot, "file/upload/" + resourceId);

                        using (var message = await client.PostAsync(endPoint, content))
                        {
                            var input = message.Content.ReadAsStringAsync().Result;
                            message.EnsureSuccessStatusCode();
                        }
                    }
                }
            }
        }

        private Task<string> ToJson(IDictionary<string, object> data)
        {
            return Task.Factory.StartNew(() => JsonConvert.SerializeObject(data));
        }

        private Task<IDictionary<string, object>> FromJson(string data)
        {
            return Task.Factory.StartNew(() => JsonConvert.DeserializeObject<IDictionary<string, object>>(data));
        }

        public async Task<IDictionary<string, object>> GetCustomData(string resourceId)
        {
            using (var client = new HttpClient())
            {
                var endPoint = new Uri(_apiRoot, "file/" + resourceId +"/@customdata");

                var json = await client.GetStringAsync(endPoint);
                return await FromJson(json);
            }
        }
    }
}
