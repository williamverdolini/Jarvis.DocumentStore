﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.DocumentStore.Core.Model;

namespace Jarvis.DocumentStore.Core.Domain.Document.Commands
{
    public class CreateDocument : DocumentCommand
    {
        public FileId FileId { get; private set; }

        public CreateDocument(DocumentId documentId, FileId fileId)
            :base(documentId)
        {
            FileId = fileId;
        }
    }
}
