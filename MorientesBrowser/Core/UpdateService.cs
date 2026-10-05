using System;
using System.Threading;
using System.Threading.Tasks;

namespace MorientesBrowser
{
    internal interface IUpdateBackend
    {
        bool IsInstalled { get; }
        Task<bool> CheckAsync();
        Task DownloadAsync();
    }

    internal sealed class UpdateState
    {
        public string status { get; set; }
        public bool busy { get; set; }
        public bool ready { get; set; }
    }

    internal sealed class UpdateService
    {
        private readonly IUpdateBackend backend;
        private int checking;
        public UpdateState State { get; private set; } = new UpdateState { status = "Pembaruan otomatis siap." };
        public event Action Changed;

        public UpdateService(IUpdateBackend backend) { this.backend = backend; }
        private void Set(string text, bool busy = false, bool ready = false)
        {
            State = new UpdateState { status = text, busy = busy, ready = ready };
            Changed?.Invoke();
        }
        public async Task CheckAsync()
        {
            if (Interlocked.CompareExchange(ref checking, 1, 0) != 0) return;
            try
            {
                if (State.ready) return;
                if (!backend.IsInstalled)
                {
                    Set("Pasang melalui installer untuk mengaktifkan auto-update.");
                    return;
                }
                Set("Memeriksa pembaruan…", true);
                if (!await backend.CheckAsync().ConfigureAwait(false))
                {
                    Set("Browser sudah memakai versi terbaru.");
                    return;
                }
                Set("Mengunduh pembaruan di latar belakang…", true);
                await backend.DownloadAsync().ConfigureAwait(false);
                Set("Pembaruan siap. Tutup dan buka kembali browser untuk memasangnya.", false, true);
            }
            catch (Exception)
            {
                // Network, checksum and lock failures must never interrupt browsing.
                Set("Pembaruan belum berhasil. Akan dicoba kembali; browser tetap bisa dipakai.");
            }
            finally { Interlocked.Exchange(ref checking, 0); }
        }
    }
}
