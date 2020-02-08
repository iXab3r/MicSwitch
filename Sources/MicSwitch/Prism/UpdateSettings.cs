using PoeShared.Squirrel.Updater;

namespace MicSwitch.Prism
{
    internal sealed class UpdateSettings
    {
        public static readonly UpdateSourceInfo[] WellKnownUpdateSources =
        {
            new UpdateSourceInfo {Uri = @"https://github.com/iXab3r/MicSwitch", Description = "GitHub"}
        };
    }
}