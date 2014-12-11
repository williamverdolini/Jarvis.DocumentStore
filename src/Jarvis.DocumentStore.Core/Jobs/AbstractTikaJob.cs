using System.IO;
using System.Text;
using Jarvis.DocumentStore.Core.Domain.Document.Commands;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.DocumentStore.Core.Processing;
using Jarvis.DocumentStore.Core.Processing.Conversions;
using Jarvis.DocumentStore.Core.Services;
using Jarvis.DocumentStore.Core.Storage;
using Quartz;

namespace Jarvis.DocumentStore.Core.Jobs
{
    public abstract class AbstractTikaJob : AbstractFileJob
    {
        protected abstract ITikaAnalyzer BuildAnalyzer();
        protected override void OnExecute(IJobExecutionContext context)
        {
            var analyzer = BuildAnalyzer();

            string pathToFile = DownloadFileToWorkingFolder(this.InputBlobId);
            string content = analyzer.GetHtmlContent(pathToFile);
            var tikaFileName = new FileNameWithExtension(this.InputBlobId + ".tika.html");
            BlobId tikaBlobId;
            string htmlSource = null;
            using (var htmlReader = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                tikaBlobId = BlobStore.Upload(DocumentFormats.Tika, tikaFileName, htmlReader);
                htmlReader.Seek(0, SeekOrigin.Begin);
                using (var sr = new StreamReader(htmlReader, Encoding.UTF8))
                {
                    htmlSource = sr.ReadToEnd();
                }
            }

            Logger.DebugFormat("Tika result: file {0} has {1} chars", InputBlobId, content.Length);
            CommandBus.Send(new AddFormatToDocument(
                this.InputDocumentId,
                DocumentFormats.Tika,
                tikaBlobId,
                this.PipelineId
                ));

            if (!string.IsNullOrWhiteSpace(htmlSource))
            {
                var documentContent = ContentFormatBuilder.CreateFromTikaPlain(htmlSource);
                var contentId = BlobStore.Save(DocumentFormats.Content, documentContent);
                Logger.DebugFormat("Content: {0} has {1} pages", InputDocumentId, documentContent.Pages);

                CommandBus.Send(new AddFormatToDocument(
                    this.InputDocumentId,
                    DocumentFormats.Content,
                    contentId,
                    this.PipelineId
                    ));
            }
        }
    }
}