﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.ImageService.Client;
using Jarvis.ImageService.Host.Support;
using NUnit.Framework;

namespace Jarvis.ImageService.Core.Tests.PipelineTests
{
    [TestFixture]
    public class ThumbnailApiTests
    {
        ImageServiceBootstrapper _app;
        Uri _serverAddress;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _serverAddress = new Uri("http://localhost:5123");
            _app = new ImageServiceBootstrapper(_serverAddress);
            _app.Start();
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _app.Stop();
        }

        [Test]
        public async void can_upload_pdf()
        {
            var client = new ImageServiceClient(_serverAddress);
            await client.Upload(SampleData.PathToDocumentPdf, "Document_1");

            Debug.WriteLine("Done");
            Assert.IsTrue(true);
        }


    }

    [TestFixture, Explicit]
    public class client_call
    {
        [Test]
        public async void can_upload_pdf()
        {
            var client = new ImageServiceClient(new Uri("http://localhost:5123"));
            await client.Upload(SampleData.PathToDocumentPdf, "Document_1");

            Debug.WriteLine("Done");
        }
    }
}
