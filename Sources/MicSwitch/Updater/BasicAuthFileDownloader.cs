using System;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using PoeShared.Scaffolding;
using PoeShared.UI;
using Squirrel;

namespace MicSwitch.Updater
{
    internal sealed class BasicAuthFileDownloader : IFileDownloader
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BasicAuthFileDownloader));

        private readonly NetworkCredential credentials;

        public BasicAuthFileDownloader(NetworkCredential credentials)
        {
            this.credentials = credentials;
        }

        public async Task DownloadFile(string url, string targetFile, Action<int> progress)
        {
            using (var wc = CreateClient())
            {
                var progressAnchors = new CompositeDisposable();
                Observable.FromEventPattern<DownloadProgressChangedEventHandler, DownloadProgressChangedEventArgs>(
                              h => wc.DownloadProgressChanged += h,
                              h => wc.DownloadProgressChanged -= h)
                          .Where(x => progress != null)
                          .Sample(UiConstants.ArtificialShortDelay)
                          .Subscribe(x => progress(x.EventArgs.ProgressPercentage))
                          .AddTo(progressAnchors);
                try
                {
                    Log.Debug($"[WebClient.DownloadFile] Downloading file to '{targetFile}', uri: {url} ");
                    await wc.DownloadFileTaskAsync(url, targetFile);
                }
                finally
                {
                    progressAnchors.Dispose();
                }
            }
        }

        public async Task<byte[]> DownloadUrl(string url)
        {
            using (var wc = CreateClient())
            {
                Log.Debug($"[WebClient.DownloadUrl] Downloading data, uri: {url} ");

                return await wc.DownloadDataTaskAsync(url);
            }
        }

        private WebClient CreateClient()
        {
            var result = new WebClient
            {
                Credentials = credentials
            };

            if (!string.IsNullOrEmpty(credentials.UserName))
            {
                var credentialsBuilder = new StringBuilder();
                credentialsBuilder.Append(credentials.UserName);
                if (!string.IsNullOrEmpty(credentials.Password))
                {
                    credentialsBuilder.Append($":{credentials.Password}");
                }

                var credentialsString = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentialsBuilder.ToString()));
                result.Headers[HttpRequestHeader.Authorization] = $"Basic {credentialsString}";
            }

            return result;
        }
    }
}