﻿using Jarvis.Framework.Shared.Commands;

namespace Jarvis.DocumentStore.Core.Domain.DocumentDescriptor.Commands
{
    public abstract class DocumentDescriptorCommand : Command<DocumentDescriptorId>
    {
        protected DocumentDescriptorCommand(DocumentDescriptorId aggregateId) : base(aggregateId)
        {
        }
    }
}