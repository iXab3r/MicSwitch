using PoeShared.Squirrel.Updater;

namespace MicSwitch.Prism
{
    internal sealed class UpdateSettings
    {
        public static readonly UpdateSourceInfo[] WellKnownUpdateSources =
        {
            new UpdateSourceInfo
            {
                Uris = new[]
                {
                    @"https://github.com/iXab3r/MicSwitch"
                },
                Id = "Github",
                Name = "GitHub"
            }
        };
    }
}