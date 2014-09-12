﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.ImageService.Host.Support;
using Topshelf;

namespace Jarvis.ImageService.Host
{
    class Program
    {
        static int Main(string[] args)
        {
            var exitCode = HostFactory.Run(host =>
            {
                host.UseOldLog4Net("log4net.config");

                host.Service<ImageServiceBootstrapper>(service =>
                {
                    service.ConstructUsing(() => new ImageServiceBootstrapper( new Uri("http://localhost:5123")));
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                host.RunAsNetworkService();

                host.SetDescription("Image service for JARVIS");
                host.SetDisplayName("Jarvis - Image service");
                host.SetServiceName("JarvisImageService");
            });

            return (int)exitCode;
        }
    }
}
