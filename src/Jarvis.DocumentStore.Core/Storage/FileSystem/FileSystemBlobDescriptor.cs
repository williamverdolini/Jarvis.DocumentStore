﻿using System;
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
using MongoDB.Bson.Serialization.Attributes;

namespace Jarvis.DocumentStore.Core.Storage
{
    /// <summary>
    /// This is the class that will be stored inside Mongodb to store additional
    /// information of the files.
    /// This is also the class that will be used to open a read stream.
    /// </summary>
    public class FileSystemBlobDescriptor : IBlobDescriptor
    {
        [BsonId]
        public BlobId BlobId { get; set; }

        public FileNameWithExtension FileNameWithExtension { get; set; }

        public Int64 Length { get; set; }

        public DateTime Timestamp { get; set; }

        public String Md5 { get; set; }

        public String ContentType { get; set; }

        public FileHash Hash
        {
            get { return new FileHash(Md5); }
        }

        private String _localFileName;

        /// <summary>
        /// This class is persisted to MongoDb, but we do not want to hardcode the 
        /// full path of the file. This data is not persisted to mongo and was set
        /// by the caller before returning this value to the caller.
        /// </summary>
        /// <param name="localFileName"></param>
        internal void SetLocalFileName(String localFileName)
        {
            _localFileName = localFileName;
        }

        public Stream OpenRead()
        {
            if (String.IsNullOrEmpty(_localFileName))
                throw new Exception("Local file name was not correctly set by the blob store");
            return new FileStream(_localFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }
}
