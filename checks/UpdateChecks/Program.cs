using MorientesBrowser;

static class Program
{
    static int count;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); count++; }
    static async Task Main()
    {
        var backend = new FakeBackend { IsInstalled = false }; var service = new UpdateService(backend);
        await service.CheckAsync(); Check(backend.Checks == 0 && !service.State.ready, "uninstalled portable copy never downloads updates");
        backend.IsInstalled = true; await service.CheckAsync(); Check(backend.Checks == 1 && backend.Downloads == 0 && !service.State.busy, "no new version does not download or restart");
        backend.Available = true; backend.FailDownload = true; await service.CheckAsync(); Check(!service.State.ready && !service.State.busy, "failed or unverified download is never marked ready");
        backend.FailDownload = false; backend.Hold = new TaskCompletionSource<bool>();
        var pending = service.CheckAsync(); await service.CheckAsync(); Check(backend.Checks == 3, "parallel manual and background checks cannot overlap");
        backend.Hold.SetResult(true); await pending; Check(service.State.ready && !service.State.busy && backend.Downloads == 2, "successful download waits for the next launch");
        await service.CheckAsync(); Check(backend.Checks == 3 && service.State.ready, "a staged update is not downloaded again");
        var failing = new UpdateService(new FakeBackend { FailCheck = true }); await failing.CheckAsync(); Check(!failing.State.ready && !failing.State.busy, "offline check releases its lock and keeps browsing available");
        Console.WriteLine(count + " updater checks passed.");
    }
    sealed class FakeBackend : IUpdateBackend
    {
        public bool IsInstalled { get; set; } = true;
        public bool Available, FailCheck, FailDownload;
        public int Checks, Downloads;
        public TaskCompletionSource<bool> Hold;
        public async Task<bool> CheckAsync() { Checks++; if (FailCheck) throw new IOException("offline"); if (Hold != null) await Hold.Task; return Available; }
        public Task DownloadAsync() { Downloads++; if (FailDownload) throw new IOException("checksum failure"); return Task.CompletedTask; }
    }
}
