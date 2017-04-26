using Jarvis.DocumentStore.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Path = Jarvis.DocumentStore.Shared.Helpers.DsPath;
using File = Jarvis.DocumentStore.Shared.Helpers.DsFile;
using Directory = Jarvis.DocumentStore.Shared.Helpers.DsDirectory;
using Newtonsoft.Json;

namespace Jarvis.DocumentStore.Core.Storage.FileSystem
{
    internal class FileSystemBlobDescriptorStore
    {
        private readonly DirectoryManager _directoryManager;

        internal FileSystemBlobDescriptorStore(DirectoryManager directoryManager)
        {
            if (directoryManager == null)
                throw new ArgumentNullException(nameof(directoryManager));

            _directoryManager = directoryManager;
        }

        public FileSystemBlobDescriptor Load(BlobId blobId)
        {
            if (blobId == null)
                throw new ArgumentNullException(nameof(blobId));

            var fileName = _directoryManager.GetDescriptorFileNameFromBlobId(blobId);
            if (!File.Exists(fileName))
                return null;
            var descriptor = JsonConvert.DeserializeObject<FileSystemBlobDescriptor>(File.ReadAllText(fileName));
            descriptor.SetLocalFileName(_directoryManager.GetBlobNameFromDescriptorFileName(fileName));
            return descriptor;
        }

        public void Save(FileSystemBlobDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            var fileName = _directoryManager.GetDescriptorFileNameFromBlobId(descriptor.BlobId);
            File.WriteAllText(fileName, JsonConvert.SerializeObject(descriptor));
        }
    }
}
