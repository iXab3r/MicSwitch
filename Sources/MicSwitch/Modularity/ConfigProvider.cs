using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using Common.Logging;
using PoeEye;
using PoeShared.Modularity;
using PoeShared.Scaffolding;

namespace MicSwitch.Modularity
{
    internal sealed class ConfigProviderFromFile : IConfigProvider
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConfigProviderFromFile));

        private static readonly string ConfigFileDirectory = Path.Combine(AppArguments.Instance.AppDataDirectory);

        private static readonly string DebugConfigFileName = @"configDebugMode.cfg";
        private static readonly string ReleaseConfigFileName = @"config.cfg";

        private readonly string configFilePath;

        private readonly ISubject<Unit> configHasChanged = new Subject<Unit>();
        private readonly IConfigSerializer configSerializer;

        private readonly ConcurrentDictionary<string, IPoeEyeConfig> loadedConfigs = new ConcurrentDictionary<string, IPoeEyeConfig>();

        public ConfigProviderFromFile(IConfigSerializer configSerializer)
        {
            this.configSerializer = configSerializer;
            if (AppArguments.Instance.IsDebugMode)
            {
                Log.Debug("[ConfigProviderFromFile..ctor] Debug mode detected");
                configFilePath = Path.Combine(ConfigFileDirectory, DebugConfigFileName);
            }
            else
            {
                Log.Debug("[ConfigProviderFromFile..ctor] Release mode detected");
                configFilePath = Path.Combine(ConfigFileDirectory, ReleaseConfigFileName);
            }
        }

        public IObservable<Unit> ConfigHasChanged => configHasChanged;

        public void Reload()
        {
            Log.Debug("[ConfigProviderFromFile.Reload] Reloading configuration...");

            var config = LoadInternal();
            loadedConfigs.Clear();

            config.Items
                  .ToList()
                  .Select(x => x.Content)
                  .Select(ValidateConfigVersion)
                  .ForEach(x => loadedConfigs[x.GetType().FullName] = x);

            configHasChanged.OnNext(Unit.Default);
        }

        public void Save<TConfig>(TConfig config) where TConfig : IPoeEyeConfig, new()
        {
            var key = new PoeEyeConfigMetadata(config);
            loadedConfigs[key.ConfigTypeName] = config;

            var metaConfig = new PoeEyeCombinedConfig();
            loadedConfigs.Values.Select(x => new PoeEyeConfigMetadata(x)).ToList().ForEach(x => metaConfig.Add(x));

            SaveInternal(metaConfig);
        }

        public TConfig GetActualConfig<TConfig>() where TConfig : IPoeEyeConfig, new()
        {
            if (loadedConfigs.IsEmpty)
            {
                Reload();
            }

            return (TConfig)loadedConfigs.GetOrAdd(typeof(TConfig).FullName, key => (TConfig)Activator.CreateInstance(typeof(TConfig)));
        }

        private IPoeEyeConfig ValidateConfigVersion(IPoeEyeConfig loadedConfig)
        {
            var versionedLoadedConfig = loadedConfig as IPoeEyeConfigVersioned;
            Log.Debug(
                $"[ConfigProviderFromFile.ValidateConfigVersion] Validating config of type {loadedConfig} (version(-1 = unversioned): {versionedLoadedConfig?.Version ?? -1})...");
            if (versionedLoadedConfig == null)
            {
                return loadedConfig;
            }

            var configTemplate = (IPoeEyeConfigVersioned)loadedConfigs.GetOrAdd(
                loadedConfig.GetType().FullName,
                key => (IPoeEyeConfigVersioned)Activator.CreateInstance(loadedConfig.GetType()));

            if (configTemplate.Version != versionedLoadedConfig.Version)
            {
                Log.Debug(
                    $"[ConfigProviderFromFile.ValidateConfigVersion] Config version mismatch (expected: {configTemplate.Version}, got: {versionedLoadedConfig.Version})");
                Log.Debug(
                    $"[ConfigProviderFromFile.ValidateConfigVersion] Loaded config:\n{loadedConfig.DumpToText()}\n\nTemplate config:\n{configTemplate.DumpToText()}");
                return configTemplate;
            }

            return loadedConfig;
        }

        private void SaveInternal(PoeEyeCombinedConfig config)
        {
            try
            {
                Log.Debug("[ConfigProviderFromFile.Save] Serializing config data...");
                var serializedData = configSerializer.Serialize(config);

                Log.Debug($"[ConfigProviderFromFile.Save] Successfully serialized config, got {serializedData.Length} chars");

                Log.Debug($"[ConfigProviderFromFile.Save] Saving config to file '{configFilePath}'...");

                var directoryPath = Path.GetDirectoryName(configFilePath);
                if (directoryPath != null && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(configFilePath, serializedData, Encoding.Unicode);

                configHasChanged.OnNext(Unit.Default);
            }
            catch (Exception ex)
            {
                Log.Warn("[ConfigProviderFromFile.Save] Exception occurred, config was not saved correctly",ex);
            }
        }

        private PoeEyeCombinedConfig LoadInternal()
        {
            Log.Debug($"[ConfigProviderFromFile.Load] Loading config from file '{configFilePath}'...");
            loadedConfigs.Clear();

            if (!File.Exists(configFilePath))
            {
                Log.Debug($"[ConfigProviderFromFile.Load] File not found, fileName: '{configFilePath}'");
                return new PoeEyeCombinedConfig();
            }

            PoeEyeCombinedConfig result = null;
            try
            {
                var fileData = File.ReadAllText(configFilePath);
                Log.Debug($"[ConfigProviderFromFile.Load] Successfully read {fileData.Length} chars, deserializing...");

                result = configSerializer.Deserialize<PoeEyeCombinedConfig>(fileData);
                Log.Debug("[ConfigProviderFromFile.Load] Successfully deserialized config data");
            }
            catch (Exception ex)
            {
                Log.Warn("[ConfigProviderFromFile.Load] Could not deserialize config data, default config will be used", ex);
                CreateBackupOfConfig();
            }

            return result ?? new PoeEyeCombinedConfig();
        }

        private void CreateBackupOfConfig()
        {
            try
            {
                if (!File.Exists(configFilePath))
                {
                    return;
                }

                var backupFileName = Path.Combine(Path.GetDirectoryName(configFilePath),
                                                  $"{Path.GetFileNameWithoutExtension(configFilePath)}.bak{Path.GetExtension(configFilePath)}");
                Log.Debug($"[PoeEyeConfigProviderFromFile.Load] Creating a backup of existing config data '{configFilePath}' to '{backupFileName}'");
                File.Copy(configFilePath, backupFileName);
            }
            catch (Exception ex)
            {
                Log.Warn("[PoeEyeConfigProviderFromFile.CreateBackupOfConfig] Failed to create a backup", ex);
            }
        }

        private sealed class PoeEyeCombinedConfig
        {
            private readonly ICollection<PoeEyeConfigMetadata> items = new List<PoeEyeConfigMetadata>();

            public int Version { get; set; } = 1;

            public IEnumerable<PoeEyeConfigMetadata> Items
            {
                get => items;
            }

            public PoeEyeCombinedConfig Add(PoeEyeConfigMetadata item)
            {
                items.Add(item);
                return this;
            }
        }

        private sealed class PoeEyeConfigMetadata
        {
            public PoeEyeConfigMetadata(IPoeEyeConfig content)
            {
                Content = content;
            }

            public string ConfigTypeName => Content.GetType().FullName;

            public IPoeEyeConfig Content { get; }
        }
    }
}