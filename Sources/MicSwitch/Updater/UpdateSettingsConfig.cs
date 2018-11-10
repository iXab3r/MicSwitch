using System;
using System.Linq;
using PoeShared.Modularity;

namespace MicSwitch.Updater
{
    internal sealed class UpdateSettingsConfig : IPoeEyeConfigVersioned
    {
        public static readonly TimeSpan DefaultAutoUpdateTimeout = TimeSpan.FromMinutes(30);

        public static readonly UpdateSourceInfo[] WellKnownUpdateSources =
        {
            new UpdateSourceInfo {Uri = @"https://github.com/iXab3r/MicSwitch", Description = "GitHub"},
        };

        public TimeSpan AutoUpdateTimeout { get; set; } = DefaultAutoUpdateTimeout;

        public UpdateSourceInfo UpdateSource { get; set; } = WellKnownUpdateSources.First();

        public int Version { get; set; } = 9;
    }
}