﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CQRS.Kernel.Events;
using CQRS.Kernel.ProjectionEngine;
using Jarvis.DocumentStore.Core.Domain.Document.Events;
using Jarvis.DocumentStore.Core.ReadModel;
using Jarvis.DocumentStore.Core.Storage;

namespace Jarvis.DocumentStore.Core.EventHandlers
{
    public class DocumentStatsProjection : AbstractProjection,
        IEventHandler<DocumentCreated>,
        IEventHandler<DocumentDeleted>
    {
        readonly ICollectionWrapper<DocumentStats, string> _collection;
        readonly IFileStore _fileStore;
        
        public DocumentStatsProjection(ICollectionWrapper<DocumentStats, string> collection, IFileStore fileStore)
        {
            _collection = collection;
            _fileStore = fileStore;
            _collection.Attach(this,false);
        }

        public override void Drop()
        {
            _collection.Drop();
        }

        public override void SetUp()
        {
        }

        public void On(DocumentCreated e)
        {
            var descriptor = _fileStore.GetDescriptor(e.FileId);
            if (descriptor != null)
            {
                _collection.Upsert(e, e.FileName.Extension, 
                    () => new DocumentStats(){
                        Files = 1,
                        Bytes = descriptor.Length
                    },
                    s => { 
                        s.Files++;
                        s.Bytes += descriptor.Length;
                    });
            }
        }

        public void On(DocumentDeleted e)
        {
            var descriptor = _fileStore.GetDescriptor(e.FileId);
            if (descriptor != null)
            {
                _collection.FindAndModify(e, descriptor.FileNameWithExtension.Extension,
                    s =>{
                        s.Files--;
                        s.Bytes -= descriptor.Length;
                    });
            }
        }
    }
}