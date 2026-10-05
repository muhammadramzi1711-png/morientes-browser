using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;
using Velopack;
using Microsoft.Web.WebView2.Core;

namespace MorientesBrowser
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // First call: handle install/update hooks before opening a window or profile.
            VelopackApp.Build().Run();
            using (var instance = new Mutex(true, @"Local\MorientesBrowser.Instance", out bool firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("Morientes sudah terbuka. Gunakan jendela yang sedang berjalan.", "Morientes Browser");
                    return;
                }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                CoreWebView2Environment.GetAvailableBrowserVersionString();
                Application.Run(new BrowserWindow(args.Length > 0 ? args[0] : null));
            }
            catch (WebView2RuntimeNotFoundException)
            {
                var answer = MessageBox.Show("WebView2 Runtime belum terpasang.\n\nBuka halaman resmi Microsoft untuk mengunduhnya?", "Morientes Browser", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer == DialogResult.Yes) Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Browser belum bisa dijalankan.\n\n" + ex.Message, "Morientes Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            }
        }
    }
}
