using Castle.Core.Logging;
using Jarvis.ConfigurationService.Client;
using Jarvis.DocumentStore.Core.Storage.FileSystem;
using Jarvis.DocumentStore.Core.Support;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using MongoDB.Driver;
using System;
using System.Text.RegularExpressions;

namespace Jarvis.DocumentStore.Host.Support
{
    public class DocumentStoreTenantSettings : TenantSettings
    {
        private readonly DocumentStoreConfiguration _config;

        public DocumentStoreTenantSettings(
            string tenantId,
            DocumentStoreConfiguration config)
            : base(new TenantId(tenantId))
        {
            _config = config;
            SetConnectionString("events");
            SetConnectionString("originals");
            SetConnectionString("artifacts");
            SetConnectionString("system");
            SetConnectionString("readmodel");

            Set("system.db", GetDatabase("system"));
            Set("readmodel.db", GetDatabase("readmodel"));

            Set("originals.db", GetDatabase("originals"));
            Set("artifacts.db", GetDatabase("artifacts"));

            switch (_config.StorageType)
            {
                case StorageType.GridFs:
                    Set("originals.db", GetDatabase("originals"));
                    Set("artifacts.db", GetDatabase("artifacts"));
                    break;
                case StorageType.FileSystem:
                    //we can simply use the connectionstring
                    SetFileSystemBaseDirectory("originals");
                    SetFileSystemBaseDirectory("artifacts");
                    break;
            }
        }

        private void SetConnectionString(string name)
        {
            Set(
                "connectionstring." + name,
                ConfigurationServiceClient.Instance.GetSetting($"connectionStrings.{TenantId}.{name}")
            );
        }

        private void SetFileSystemBaseDirectory(string name)
        {
            var storageValue = ConfigurationServiceClient.Instance.GetSetting($"storage.fileSystem.{TenantId}-{name}-baseDirectory");
            if (!String.IsNullOrEmpty(_config.StorageUserName) )
            {
                var match = Regex.Match(storageValue, @"(?<root>\\\\.+?\\.+?)(\\|$)");
                if (match.Success)
                {
                    var shareRoot = match.Groups["root"].Value.TrimEnd('/', '\\');
                    var errors = PinvokeWindowsNetworking.ConnectToRemote(shareRoot, _config.StorageUserName, _config.StoragePassword);
                    //TODO: Logging errors.
                    //if (!String.IsNullOrEmpty(errors))
                    //{
                    //    _logger.Error($"Unable to map network share {storageValue} with username {_config.StorageUserName}. Error: {errors}");
                    //}
                }
            }
            Set("storage.fs." + name, storageValue);
        }
    }
}