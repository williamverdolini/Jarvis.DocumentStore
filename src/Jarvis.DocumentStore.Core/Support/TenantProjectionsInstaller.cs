﻿using System.Configuration;
using Castle.Facilities.Startable;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using CQRS.Kernel.Engine.Snapshots;
using CQRS.Kernel.Events;
using CQRS.Kernel.ProjectionEngine;
using CQRS.Kernel.ProjectionEngine.Client;
using CQRS.Kernel.ProjectionEngine.RecycleBin;
using CQRS.Shared.Messages;
using CQRS.Shared.MultitenantSupport;
using CQRS.Shared.ReadModel;
using Jarvis.DocumentStore.Core.EventHandlers;
using Jarvis.DocumentStore.Core.ReadModel;
using MongoDB.Driver;

namespace Jarvis.DocumentStore.Core.Support
{
    public class TenantProjectionsInstaller<TNotifier> : IWindsorInstaller where TNotifier : INotifyToSubscribers
    {
        readonly ITenant _tenant;
        readonly bool _enableProjections;

        public TenantProjectionsInstaller(ITenant tenant, bool enableProjections)
        {
            _tenant = tenant;
            _enableProjections = enableProjections;
        }

        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            // add rm prefix to collections
            CollectionNames.Customize = n => "rm." + n;

            var config = new ProjectionEngineConfig
            {
                EventStoreConnectionString = _tenant.GetConnectionString("events"),
                Slots = ConfigurationManager.AppSettings["engine-slots"].Split(','),
                PollingMsInterval = int.Parse(ConfigurationManager.AppSettings["polling-interval-ms"]),
                ForcedGcSecondsInterval = int.Parse(ConfigurationManager.AppSettings["memory-collect-seconds"]),
                TenantId = _tenant.Id
            };

            var readModelDb = _tenant.Get<MongoDatabase>("readmodel.db");


            container.Register(
                Component
                    .For<IHandleWriter>()
                    .ImplementedBy<HandleWriter>()
                    .DependsOn(Dependency.OnValue<MongoDatabase>(readModelDb)),
                Component
                    .For(typeof(IReader<,>), typeof(IMongoDbReader<,>))
                    .ImplementedBy(typeof(MongoReaderForProjections<,>))
                    .DependsOn(Dependency.OnValue<MongoDatabase>(readModelDb))
            );
            
            if (!_enableProjections)
                return;

            container.Register(
                Component
                    .For<IHousekeeper>()
                    .ImplementedBy<NullHouseKeeper>(),
                Component
                    .For<INotifyToSubscribers>()
                    .ImplementedBy<TNotifier>(),
                Component
                    .For<CommitEnhancer>(),
                Component
                    .For<INotifyCommitHandled>()
                    .ImplementedBy<NullNotifyCommitHandled>(),
                Component
                    .For<ConcurrentProjectionsEngine,ITriggerProjectionsUpdate>()
                    .ImplementedBy<ConcurrentProjectionsEngine>()
                    .LifestyleSingleton()
                    .DependsOn(Dependency.OnValue<ProjectionEngineConfig>(config))
                    .StartUsingMethod(x => x.StartWithManualPoll)
                    .StopUsingMethod(x => x.Stop),
                Classes
                    .FromAssemblyContaining<DocumentProjection>()
                    .BasedOn<IProjection>()
                    .Configure(r => r
                        .DependsOn(Dependency.OnValue<TenantId>(_tenant.Id))
                    )
                    .WithServiceAllInterfaces()
                    .LifestyleSingleton(),
                Component
                    .For<IInitializeReadModelDb>()
                    .ImplementedBy<InitializeReadModelDb>(),
                Component
                    .For<IConcurrentCheckpointTracker>()
                    .ImplementedBy<ConcurrentCheckpointTracker>()
                    .DependsOn(Dependency.OnValue<MongoDatabase>(readModelDb)),
                Component
                    .For(new[]
                    {
                        typeof (ICollectionWrapper<,>),
                        typeof (IReadOnlyCollectionWrapper<,>)
                    })
                    .ImplementedBy(typeof(CollectionWrapper<,>))
                    .DependsOn(Dependency.OnValue<MongoDatabase>(readModelDb)),
                Component
                    .For<IPollingClient>()
                    .ImplementedBy<PollingClientWrapper>()
                    .DependsOn(Dependency.OnConfigValue("boost", ConfigurationManager.AppSettings["engine-multithread"])),
                Component
                    .For<IRebuildContext>()
                    .ImplementedBy<RebuildContext>()
                    .DependsOn(Dependency.OnValue<bool>(RebuildSettings.NitroMode)),
                Component
                    .For<IMongoStorageFactory>()
                    .ImplementedBy<MongoStorageFactory>()
                    .DependsOn(Dependency.OnValue<MongoDatabase>(readModelDb)),
                Component
                    .For<DocumentByHashReader>(),
                Component
                    .For<DeduplicationHelper>(),
                Component
                    .For<IRecycleBin>()
                    .ImplementedBy<RecycleBin>()
                    .DependsOn(Dependency.OnValue<MongoDatabase>(readModelDb))
                );

#if DEBUG
            container.Resolve<ConcurrentProjectionsEngine>();
#endif
        }
    }
}