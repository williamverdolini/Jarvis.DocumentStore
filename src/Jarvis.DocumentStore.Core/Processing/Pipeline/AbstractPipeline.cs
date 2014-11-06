using Castle.Core.Logging;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.DocumentStore.Core.Storage;

namespace Jarvis.DocumentStore.Core.Processing.Pipeline
{
    public abstract class AbstractPipeline : IPipeline
    {
        public ILogger Logger { get; set; }
        public IJobHelper JobHelper { get; set; }
        protected IPipelineManager PipelineManager { get; private set; }

        public IPipelineListener[] Listeners { get; set; }

        protected AbstractPipeline(string id)
        {
            this.Id = new PipelineId(id);
        }

        public PipelineId Id { get; private set; }
        public abstract bool ShouldHandleFile(DocumentId documentId, IBlobDescriptor storeDescriptor, IPipeline fromPipeline);

        public void Start(DocumentId documentId, IBlobDescriptor storeDescriptor)
        {
            OnStart(documentId, storeDescriptor);
            if (Listeners != null)
            {
                foreach (var pipelineListener in Listeners)
                {
                    pipelineListener.OnStart(this, documentId, storeDescriptor);
                }
            }
        }

        protected abstract void OnStart(
            DocumentId documentId, 
            IBlobDescriptor storeDescriptor
        );

        public void FormatAvailable(DocumentId documentId, DocumentFormat format, IBlobDescriptor descriptor)
        {
            OnFormatAvailable(documentId, format, descriptor);
            if (Listeners != null)
            {
                foreach (var pipelineListener in Listeners)
                {
                    pipelineListener.OnFormatAvailable(this, documentId, format, descriptor);
                }
            }
        }

        protected abstract void OnFormatAvailable(
            DocumentId documentId, 
            DocumentFormat format, 
            IBlobDescriptor descriptor
        );
        
        public void Attach(IPipelineManager manager)
        {
            this.PipelineManager = manager;
        }
    }
}