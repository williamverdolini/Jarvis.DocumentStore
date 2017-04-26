using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor;
using Jarvis.DocumentStore.Core.Model;
using Castle.Core.Logging;
using System.Security.Cryptography;
using MongoDB.Driver;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.DocumentStore.Core.Storage.FileSystem;
using Jarvis.Framework.Shared.Helpers;
using Path = Jarvis.DocumentStore.Shared.Helpers.DsPath;
using File = Jarvis.DocumentStore.Shared.Helpers.DsFile;
using Directory = Jarvis.DocumentStore.Shared.Helpers.DsDirectory;
using MongoDB.Bson;
using Newtonsoft.Json;

namespace Jarvis.DocumentStore.Core.Storage
{
    /// <summary>
    /// Class that implements a file system that stores binary blob on disk and
    /// additional informations in a standard mongo collection. This is needed to 
    /// avoid having GB of data inside of a GridFs, and keeping in mongo only
    /// the information that are necessary, leaving the binary blob to file system.
    /// </summary>
    public class FileSystemBlobStore : IBlobStore
    {
        public ILogger Logger { get; set; }

        private readonly String _baseDirectory;
        private readonly ICounterService _counterService;
        private readonly DirectoryManager _directoryManager;
        private readonly FileSystemBlobDescriptorStore _fileSystemBlobDescriptorStore;

        /// <summary>
        /// Standard Constructor
        /// </summary>
        /// <param name="db">The database where I want to store information</param>
        /// <param name="collectionName">The name of the collection that will be used
        /// to store information of the file</param>
        /// <param name="baseDirectory">Base directory on filesystem where binary blob
        /// will be stored</param>
        /// <param name="counterService">Counter service to generate new <see cref="BlobId"/></param>
        public FileSystemBlobStore(
            String collectionName,
            String baseDirectory,
            ICounterService counterService)
        {
            _baseDirectory = baseDirectory;
            _directoryManager = new DirectoryManager(_baseDirectory);
            _fileSystemBlobDescriptorStore = new FileSystemBlobDescriptorStore(_directoryManager);
            _counterService = counterService;
        }

        public IBlobWriter CreateNew(DocumentFormat format, FileNameWithExtension fname)
        {
            var blobId = new BlobId(format, _counterService.GetNext(format));
            return new FileSystemBlobWriter(blobId, fname, GetFileNameFromBlobIdAndRemoveDuplicates(blobId), _fileSystemBlobDescriptorStore, Logger);
        }

        public IBlobDescriptor GetDescriptor(BlobId blobId)
        {
            if (blobId == null)
                throw new ArgumentNullException(nameof(blobId));

            Logger.DebugFormat($"GetDescriptor for blobid {blobId} on {_directoryManager.BaseDirectory}");

            var descriptorFileName = _directoryManager.GetDescriptorFileNameFromBlobId(blobId);
            if (!File.Exists(descriptorFileName))
                throw new Exception($"Descriptor for {blobId} not found in {descriptorFileName}");

            IBlobDescriptor descriptor = _fileSystemBlobDescriptorStore.Load(blobId);
            if (descriptor == null)
            {
                var message = $"Descriptor for blobid {blobId}, not found in store {_directoryManager.BaseDirectory}!";
                Logger.DebugFormat(message);
                throw new Exception(message);
            }
            return descriptor;
        }

        public BlobId Upload(DocumentFormat format, string pathToFile)
        {
            FileInfo finfo = new FileInfo(pathToFile);
            if (!finfo.Exists)
                throw new ArgumentException($"File {pathToFile} not found");

            using (var fileStream = new FileStream(pathToFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var descriptor = SaveStream(format, new FileNameWithExtension(Path.GetFileName(pathToFile)), fileStream);
                _fileSystemBlobDescriptorStore.Save(descriptor);
                return descriptor.BlobId;
            }
        }

        public BlobId Upload(DocumentFormat format, FileNameWithExtension fileName, Stream sourceStream)
        {
            var descriptor = SaveStream(format, fileName, sourceStream);
            _fileSystemBlobDescriptorStore.Save(descriptor);
            return descriptor.BlobId;
        }

        /// <summary>
        /// This is the real function that save a stream to the destination
        /// file and create the descriptor
        /// </summary>
        /// <param name="format"></param>
        /// <param name="fileName"></param>
        /// <param name="sourceStream"></param>
        /// <returns></returns>
        private FileSystemBlobDescriptor SaveStream(DocumentFormat format, FileNameWithExtension fileName, Stream sourceStream)
        {
            var blobId = new BlobId(format, _counterService.GetNext(format));
            FileSystemBlobDescriptor descriptor = new FileSystemBlobDescriptor()
            {
                BlobId = blobId,
                FileNameWithExtension = fileName,
                Timestamp = DateTime.Now,
                ContentType = MimeTypes.GetMimeType(fileName)
            };
            string destinationFileName = GetFileNameFromBlobIdAndRemoveDuplicates(blobId);
            Logger.Debug($"File {fileName} was assigned blob {blobId} and will be saved in file {destinationFileName}");

            using (var md5 = MD5.Create())
            using (var fileStream = new FileStream(destinationFileName, FileMode.OpenOrCreate, FileAccess.Write))
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                fileStream.SetLength(0);

                Byte[] buffer = new Byte[8192];
                Int32 read;
                Int64 length = 0;
                while ((read = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    md5.TransformBlock(buffer, 0, read, buffer, 0);
                    fileStream.Write(buffer, 0, read);
                    length += read;
                }
                md5.TransformFinalBlock(buffer, 0, 0);
                descriptor.Length = length;
                descriptor.Md5 = BitConverter.ToString(md5.Hash).Replace("-", "");
            }
            Logger.Debug($"Blob {blobId} saved in file {destinationFileName} with hash {descriptor.Md5} and length {descriptor.Length}");
            return descriptor;
        }

        private string GetFileNameFromBlobIdAndRemoveDuplicates(BlobId blobId)
        {
            var fileName = _directoryManager.GetFileNameFromBlobId(blobId);
            if (File.Exists(fileName))
            {
                //Anomaly, we are trying to overwrite the blob
                Logger.Warn($"Destination file {blobId} already exists for id {blobId}");
                //Todo move in another folder ... maybe a lost and found.
                File.Move(fileName, fileName + "." + Guid.NewGuid().ToString());
            }

            return fileName;
        }

        public string Download(BlobId blobId, string folder)
        {
            if (blobId == null)
                throw new ArgumentNullException(nameof(blobId));

            if (String.IsNullOrEmpty(folder))
                throw new ArgumentNullException(nameof(folder));

            if (!Directory.Exists(folder))
                throw new ArgumentException($"folder {folder} does not exists", nameof(folder));

            var descriptorFileName = _directoryManager.GetDescriptorFileNameFromBlobId(blobId);
            if (!File.Exists(descriptorFileName))
                throw new ArgumentException($"Descriptor for {blobId} not found in {descriptorFileName}");

            var localFileName = _directoryManager.GetFileNameFromBlobId(blobId);
            if (!File.Exists(localFileName))
            {
                Logger.Error($"Blob {blobId} has descriptor, but blob file {localFileName} not found in the system.");
                throw new ArgumentException($"Blob {blobId} not found");
            }

            var descriptor = _fileSystemBlobDescriptorStore.Load(blobId);
            var originalFileName = descriptor.FileNameWithExtension.ToString();
            string destinationFileName = Path.Combine(folder, originalFileName);
            Int32 uniqueId = 1;
            while (File.Exists(destinationFileName))
            {
                destinationFileName = Path.Combine(folder, Path.GetFileNameWithoutExtension(originalFileName) + $" ({uniqueId++})") + Path.GetExtension(originalFileName);
            }

            File.Copy(localFileName, destinationFileName);
            return destinationFileName;
        }

        public void Delete(BlobId blobId)
        {
            var fileName = _directoryManager.GetFileNameFromBlobId(blobId);
            File.Delete(fileName);
            var descriptorFileName = _directoryManager.GetDescriptorFileNameFromBlobId(blobId);
            File.Delete(descriptorFileName);
        }
    }
}
