using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor.Events;
using Jarvis.DocumentStore.Core.ReadModel;
using Jarvis.DocumentStore.Core.Storage;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;

namespace Jarvis.DocumentStore.Core.EventHandlers
{
    public class TenantStatsProjection : AbstractProjection,
        IEventHandler<DocumentDescriptorInitialized>,
        IEventHandler<DocumentDescriptorDeleted>,
        IEventHandler<DocumentFormatHasBeenDeleted>,
        IEventHandler<FormatAddedToDocumentDescriptor>
    {
        private readonly ICollectionWrapper<TenantStatsReadModel, string> _collection;
        private readonly IBlobStore _blobStore;

        public TenantStatsProjection(ICollectionWrapper<TenantStatsReadModel, string> collection, IBlobStore blobStore)
        {
            _collection = collection;
            _blobStore = blobStore;
            _collection.Attach(this, false);
        }

        public override void Drop()
        {
            _collection.Drop();
            _collection.Insert(null, new TenantStatsReadModel()
            {
                Files = 0,
                Documents = 0,
                DocumentSize = 0,
                Handles = 0,
            });
        }

        public override void SetUp()
        {
        }

        public override string GetSignature()
        {
            return "v1";
        }

        public void On(DocumentDescriptorInitialized e)
        {
            var descriptor = _blobStore.GetDescriptor(e.BlobId);
            if (descriptor != null)
            {
                _collection.FindAndModify(e, TenantId.ToString(),                
                    s =>
                    {
                        s.Files++;
                        s.DocumentSize += descriptor.Length;
                    });
            }
        }

        public void On(DocumentDescriptorDeleted e)
        {
            var descriptor = _blobStore.GetDescriptor(e.BlobId);
            if (descriptor != null)
            {
                _collection.FindAndModify(e, descriptor.FileNameWithExtension.Extension,
                    s =>
                    {
                        s.Files--;
                        s.DocumentSize -= descriptor.Length;
                    });
            }
        }

        public void On(FormatAddedToDocumentDescriptor e)
        {
            var descriptor = _blobStore.GetDescriptor(e.BlobId);
            if (descriptor != null)
            {
                _collection.FindAndModify(e, descriptor.FileNameWithExtension.Extension,
                    s =>
                    {
                        s.Files++;
                        s.DocumentSize -= descriptor.Length;
                    });
            }
        }

        public void On(DocumentFormatHasBeenDeleted e)
        {
          
        }
    }
}
