using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace MorientesBrowser
{
    internal sealed class VelopackBackend : IUpdateBackend
    {
        // Public repository: no GitHub credentials are stored in the application.
        private readonly UpdateManager manager = new UpdateManager(new GithubSource(
            "https://github.com/muhammadramzi1711-png/morientes-browser", null, false));
        private UpdateInfo update;
        public bool IsInstalled => manager.IsInstalled;
        public async Task<bool> CheckAsync()
        {
            update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            return update != null;
        }
        public Task DownloadAsync() => manager.DownloadUpdatesAsync(update);
    }
}
