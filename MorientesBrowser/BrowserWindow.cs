using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MorientesBrowser
{
    internal sealed class BrowserTab
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Title = "Tab Baru";
        public string Url = BrowserInput.HomeUrl;
        public bool Loading, Failed;
        public WebView2 View;
    }
    internal sealed class BrowserDownload
    {
        public string Id = Guid.NewGuid().ToString("N");
        public CoreWebView2DownloadOperation Operation;
    }

    public sealed class BrowserWindow : Form
    {
        private readonly BrowserStore store = new BrowserStore();
        private readonly List<BrowserTab> tabs = new List<BrowserTab>();
        private readonly Stack<string> closedTabs = new Stack<string>();
        private readonly List<BrowserDownload> downloads = new List<BrowserDownload>();
        private readonly Panel pages = new Panel { Dock = DockStyle.Fill };
        private readonly WebView2 chrome = new WebView2 { Dock = DockStyle.Top, Height = 92 };
        private readonly WebView2 sidebar = new WebView2 { Dock = DockStyle.Right, Width = 340, Visible = false };
        private readonly UpdateService updater = new UpdateService(new VelopackBackend());
        private readonly Timer updateTimer = new Timer { Interval = 60 * 60 * 1000 };
        private readonly Timer stateTimer = new Timer { Interval = 120 };
        private CoreWebView2Environment environment;
        private BrowserTab active;
        private string sidebarMode = "bookmarks", initialUrl;
        private bool closing, fullscreen;
        private Rectangle previousBounds;
        private FormWindowState previousWindowState;
        private const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public BrowserWindow(string url)
        {
            initialUrl = url;
            Text = "Morientes Browser";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 520);
            Size = new Size(1280, 850);
            if (Screen.PrimaryScreen.WorkingArea.Height < Height) Size = new Size(1200, Screen.PrimaryScreen.WorkingArea.Height - 30);
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(5);
            BackColor = Color.FromArgb(234, 238, 237);
            try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "morientes.ico")); } catch { }
            Controls.Add(pages);
            Controls.Add(sidebar);
            Controls.Add(chrome);
            updater.Changed += () =>
            {
                if (closing || IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(Publish)); } catch (InvalidOperationException) { }
            };
            updateTimer.Tick += async (s, e) => { if (store.Data.settings.autoUpdates) await updater.CheckAsync(); };
            stateTimer.Tick += (s, e) => { stateTimer.Stop(); PublishNow(); };
            Shown += async (s, e) => await StartAsync();
            FormClosing += (s, e) =>
            {
                closing = true;
                stateTimer.Stop(); updateTimer.Stop();
                store.Data.session = tabs.Where(t => !t.Failed).Select(t => t.Url).Where(BrowserInput.IsHttp).ToList();
                SaveQuietly();
            };
            FormClosed += (s, e) =>
            {
                foreach (var tab in tabs) tab.View.Dispose();
                sidebar.Dispose(); chrome.Dispose(); stateTimer.Dispose(); updateTimer.Dispose();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int corner = 2; DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int)); } catch { }
        }

        private async Task StartAsync()
        {
            try
            {
                environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(store.Folder, "WebProfile"));
                await SetupUiAsync(chrome, BrowserInput.ChromeUrl);
                await SetupUiAsync(sidebar, BrowserInput.SidebarUrl);
                var saved = store.Data.settings.restoreSession ? store.Data.session.ToArray() : new string[0];
                if (!string.IsNullOrEmpty(initialUrl)) await CreateTabAsync(BrowserInput.Normalize(initialUrl, store.Data.settings.engine));
                else if (saved.Length > 0)
                {
                    // Restored background tabs remain unloaded until selected.
                    foreach (string url in saved.Take(30)) AddDormantTab(url);
                    await ActivateTabAsync(tabs[0]);
                }
                else await CreateTabAsync(BrowserInput.HomeUrl);
                Publish();
                updateTimer.Start();
                if (store.Data.settings.autoUpdates) await updater.CheckAsync();
            }
            catch (Exception ex)
            {
                if (!closing) MessageBox.Show(this, "Browser belum bisa dimuat.\n\n" + ex.Message + "\n\nPastikan folder aplikasi sudah diekstrak seluruhnya dan WebView2 Runtime terpasang.", "Morientes Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private async Task SetupUiAsync(WebView2 view, string address)
        {
            await view.EnsureCoreWebView2Async(environment);
            var core = view.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("ui.morientes.invalid", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.NavigationStarting += (s, e) => { if (e.Uri != address) e.Cancel = true; };
            core.NewWindowRequested += (s, e) => e.Handled = true;
            core.PermissionRequested += (s, e) => e.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += async (s, e) =>
            {
                if (!BrowserInput.IsTrustedUi(e.Source) || e.Source != address || closing) return;
                await HandleMessageAsync(e.WebMessageAsJson, false);
            };
            core.NavigationCompleted += (s, e) => Publish();
            WireKeys(view);
            core.Navigate(address);
        }

        private BrowserTab AddDormantTab(string url)
        {
            var tab = new BrowserTab { Url = url, Title = url == BrowserInput.HomeUrl ? "Tab Baru" : HostLabel(url), View = new WebView2 { Dock = DockStyle.Fill, Visible = false } };
            tabs.Add(tab); pages.Controls.Add(tab.View); return tab;
        }

        private async Task<BrowserTab> CreateTabAsync(string url, bool navigate = true)
        {
            if (tabs.Count >= 30) { Toast("Maksimal 30 tab. Tutup beberapa tab untuk melanjutkan."); return null; }
            var tab = AddDormantTab(url);
            await ActivateTabAsync(tab, navigate);
            return tab;
        }

        private async Task ActivateTabAsync(BrowserTab tab, bool navigate = true)
        {
            if (closing || tab == null || !tabs.Contains(tab)) return;
            active = tab;
            foreach (var candidate in tabs) candidate.View.Visible = candidate == tab;
            tab.View.BringToFront();
            if (tab.View.CoreWebView2 == null)
            {
                await tab.View.EnsureCoreWebView2Async(environment);
                if (closing || !tabs.Contains(tab)) return;
                SetupPage(tab);
                if (navigate) tab.View.CoreWebView2.Navigate(tab.Url);
            }
            if (active == tab) tab.View.Focus();
            Publish();
        }

        private void SetupPage(BrowserTab tab)
        {
            var core = tab.View.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("start.morientes.invalid", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", "home"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreHostObjectsAllowed = false;
            core.NavigationStarting += (s, e) =>
            {
                if (!BrowserInput.IsHttp(e.Uri) && e.Uri != "about:blank") { e.Cancel = true; Toast("Alamat ini tidak didukung. Gunakan HTTP atau HTTPS."); return; }
                tab.View.ZoomFactor = e.Uri == BrowserInput.HomeUrl ? 1.0 : store.Data.settings.defaultZoom / 100.0;
                tab.Loading = true;
                if (e.Uri != "about:blank") { tab.Url = e.Uri; tab.Failed = false; }
                Publish();
            };
            core.SourceChanged += (s, e) =>
            {
                if (core.Source != "about:blank") tab.Url = core.Source;
                Publish();
            };
            core.DocumentTitleChanged += (s, e) =>
            {
                tab.Title = tab.Url == BrowserInput.HomeUrl ? "Tab Baru" : Clip(core.DocumentTitle, 200);
                if (string.IsNullOrWhiteSpace(tab.Title)) tab.Title = HostLabel(tab.Url);
                if (tab == active) Text = tab.Title + " — Morientes Browser";
                Publish();
            };
            core.HistoryChanged += (s, e) => Publish();
            core.NavigationCompleted += (s, e) =>
            {
                tab.Loading = false;
                if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    tab.Failed = true;
                    tab.Title = "Halaman tidak tersedia";
                    string host = WebUtility.HtmlEncode(HostLabel(tab.Url));
                    core.NavigateToString("<!doctype html><html><head><meta charset='utf-8'><title>Halaman tidak tersedia</title></head><body style='font:16px Segoe UI,sans-serif;background:#f5f8f6;color:#243b36;display:grid;place-items:center;height:90vh'><main style='max-width:480px'><p style='font-size:44px'>↗</p><h1>Halaman belum bisa dibuka.</h1><p>Periksa koneksi internet atau alamat <strong>" + host + "</strong>.</p><p>Tekan tombol muat ulang untuk mencoba lagi.</p></main></body></html>");
                }
                else if (e.IsSuccess && !tab.Failed && tab.Url != BrowserInput.HomeUrl)
                {
                    try { store.Visit(tab.Title, tab.Url); } catch { Toast("Riwayat belum bisa disimpan."); }
                }
                Publish(); SendHome(tab);
            };
            core.WebMessageReceived += async (s, e) =>
            {
                if (e.Source != BrowserInput.HomeUrl || closing) return;
                await HandleMessageAsync(e.WebMessageAsJson, true);
            };
            core.NewWindowRequested += async (s, e) =>
            {
                e.Handled = true;
                if (!e.IsUserInitiated || (!BrowserInput.IsHttp(e.Uri) && e.Uri != "about:blank")) return;
                using (var deferral = e.GetDeferral())
                {
                    try
                    {
                        var popup = await CreateTabAsync(e.Uri == "about:blank" ? BrowserInput.HomeUrl : e.Uri, false);
                        if (popup != null && popup.View.CoreWebView2 != null) e.NewWindow = popup.View.CoreWebView2;
                    }
                    catch { Toast("Tab baru belum bisa dibuka."); }
                    finally { deferral.Complete(); }
                }
            };
            core.PermissionRequested += (s, e) =>
            {
                e.SavesInProfile = false;
                if (tab != active || closing) { e.State = CoreWebView2PermissionState.Deny; return; }
                var deferral = e.GetDeferral();
                // Display the modal after the WebView2 event returns; nested
                // message loops inside a callback are unsupported by WebView2.
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (closing || tab != active) { e.State = CoreWebView2PermissionState.Deny; return; }
                        var answer = MessageBox.Show(this, HostLabel(e.Uri) + " meminta akses " + PermissionLabel(e.PermissionKind) + ".\n\nIzinkan untuk permintaan ini?", "Izin website", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        e.State = answer == DialogResult.Yes ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
                    }
                    finally { deferral.Complete(); deferral.Dispose(); }
                }));
            };
            core.DownloadStarting += (s, e) =>
            {
                var item = new BrowserDownload { Operation = e.DownloadOperation };
                downloads.Insert(0, item);
                item.Operation.BytesReceivedChanged += (sender, args) => Publish();
                item.Operation.StateChanged += (sender, args) => Publish();
                Toast("Unduhan dimulai. Lihat progres di panel Unduhan."); Publish();
            };
            core.ProcessFailed += (s, e) =>
            {
                tab.Loading = false; tab.Failed = true;
                Toast("Proses halaman berhenti. Muat ulang tab untuk mencoba lagi."); Publish();
            };
            WireKeys(tab.View);
        }

        private async Task HandleMessageAsync(string json, bool fromHome)
        {
            if (json.Length > 20000) return;
            try
            {
                var message = store.Json.Deserialize<Dictionary<string, object>>(json);
                string action = Field(message, "action"), value = Field(message, "value"), id = Field(message, "id");
                if (fromHome && !new[] { "ready", "navigate", "newTab" }.Contains(action)) return;
                switch (action)
                {
                    case "ready": Publish(); foreach (var tab in tabs) SendHome(tab); break;
                    case "navigate": Navigate(value); break;
                    case "newTab": await CreateTabAsync(BrowserInput.Normalize(value, store.Data.settings.engine)); break;
                    case "activateTab": await ActivateTabAsync(tabs.FirstOrDefault(t => t.Id == id)); break;
                    case "closeTab": await CloseTabAsync(tabs.FirstOrDefault(t => t.Id == id)); break;
                    case "back": if (active?.View.CoreWebView2?.CanGoBack == true) active.View.CoreWebView2.GoBack(); break;
                    case "forward": if (active?.View.CoreWebView2?.CanGoForward == true) active.View.CoreWebView2.GoForward(); break;
                    case "reload": Reload(); break;
                    case "home": Navigate(BrowserInput.HomeUrl); break;
                    case "bookmark": ToggleBookmark(); break;
                    case "removeBookmark": store.Data.bookmarks.RemoveAll(x => x.id == id); SaveQuietly(); Publish(); break;
                    case "sidebar": SetSidebar(value); break;
                    case "clearHistory": store.Data.history.Clear(); SaveQuietly(); Publish(); break;
                    case "checkUpdates": await updater.CheckAsync(); break;
                    case "setting":
                        if (id == "engine" && new[] { "google", "duckduckgo", "bing" }.Contains(value)) store.Data.settings.engine = value;
                        if (id == "theme" && new[] { "light", "dark" }.Contains(value)) store.Data.settings.theme = value;
                        if (id == "autoUpdates") store.Data.settings.autoUpdates = value == "true";
                        if (id == "restoreSession") store.Data.settings.restoreSession = value == "true";
                        if (id == "defaultZoom" && int.TryParse(value, out int percent) && percent >= 25 && percent <= 300)
                        {
                            store.Data.settings.defaultZoom = percent;
                            foreach (var tab in tabs.Where(t => t.View.CoreWebView2 != null))
                                tab.View.ZoomFactor = tab.Url == BrowserInput.HomeUrl ? 1.0 : percent / 100.0;
                        }
                        SaveQuietly(); Publish(); foreach (var tab in tabs) SendHome(tab); break;
                    case "showDownload":
                        var file = downloads.FirstOrDefault(x => x.Id == id);
                        if (file != null && file.Operation.State == CoreWebView2DownloadState.Completed && File.Exists(file.Operation.ResultFilePath))
                            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + file.Operation.ResultFilePath + "\"") { UseShellExecute = true });
                        break;
                    case "cancelDownload": downloads.FirstOrDefault(x => x.Id == id)?.Operation.Cancel(); break;
                    case "window":
                        if (value == "close") Close();
                        else if (value == "minimize") WindowState = FormWindowState.Minimized;
                        else if (value == "maximize") WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                        else if (value == "drag" && !fullscreen) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero); }
                        break;
                }
            }
            catch (ArgumentException ex) { Toast(ex.Message); }
            catch (Exception) { if (!closing) Toast("Perintah belum bisa dijalankan. Coba lagi."); }
        }

        private void Navigate(string value)
        {
            if (active?.View.CoreWebView2 == null) return;
            string url = BrowserInput.Normalize(value, store.Data.settings.engine);
            active.Failed = false; active.Url = url; active.View.CoreWebView2.Navigate(url);
            active.View.Focus();
        }
        private void Reload()
        {
            if (active?.View.CoreWebView2 == null) return;
            if (active.Loading) active.View.CoreWebView2.Stop();
            else if (active.Failed) Navigate(active.Url);
            else active.View.CoreWebView2.Reload();
        }
        private async Task CloseTabAsync(BrowserTab tab)
        {
            if (tab == null) return;
            int index = tabs.IndexOf(tab);
            closedTabs.Push(tab.Url);
            tabs.Remove(tab); pages.Controls.Remove(tab.View); tab.View.Dispose();
            if (tabs.Count == 0) { active = null; await CreateTabAsync(BrowserInput.HomeUrl); }
            else if (active == tab) await ActivateTabAsync(tabs[Math.Min(index, tabs.Count - 1)]);
            Publish();
        }
        private void ToggleBookmark()
        {
            if (active == null || active.Url == BrowserInput.HomeUrl || active.Failed) { Toast("Buka website terlebih dahulu untuk menambahkan bookmark."); return; }
            int removed = store.Data.bookmarks.RemoveAll(x => x.url == active.Url);
            if (removed == 0) store.Data.bookmarks.Add(new SavedLink { title = active.Title, url = active.Url });
            SaveQuietly(); Publish(); foreach (var tab in tabs) SendHome(tab);
            Toast(removed == 0 ? "Ditambahkan ke bookmark." : "Bookmark dihapus.");
        }
        private void SetSidebar(string mode)
        {
            if (mode == "close" || (sidebar.Visible && sidebarMode == mode)) sidebar.Visible = false;
            else if (new[] { "bookmarks", "history", "downloads", "settings", "about" }.Contains(mode)) { sidebarMode = mode; sidebar.Visible = true; }
            Publish();
        }

        private void Publish()
        {
            if (!closing && !IsDisposed && !stateTimer.Enabled) stateTimer.Start();
        }
        private void PublishNow()
        {
            if (closing || IsDisposed) return;
            var state = new
            {
                type = "state", version = Application.ProductVersion.Split('+')[0], updates = updater.State, activeId = active?.Id,
                tabs = tabs.Select(t => new { id = t.Id, title = t.Title, url = t.Url == BrowserInput.HomeUrl ? "morientes:home" : t.Url, loading = t.Loading, failed = t.Failed }).ToArray(),
                canBack = active?.View.CoreWebView2?.CanGoBack ?? false,
                canForward = active?.View.CoreWebView2?.CanGoForward ?? false,
                bookmarked = active != null && store.Data.bookmarks.Any(x => x.url == active.Url),
                settings = store.Data.settings, bookmarks = store.Data.bookmarks, history = store.Data.history,
                sidebarOpen = sidebar.Visible, sidebarMode,
                downloads = downloads.Select(d => new { id = d.Id, name = Path.GetFileName(d.Operation.ResultFilePath), received = d.Operation.BytesReceived, total = d.Operation.TotalBytesToReceive, status = d.Operation.State.ToString() }).ToArray()
            };
            Send(chrome, state); Send(sidebar, state);
        }
        private void SendHome(BrowserTab tab)
        {
            if (!closing && tab.View.CoreWebView2?.Source == BrowserInput.HomeUrl)
                Send(tab.View, new { type = "homeState", settings = store.Data.settings, bookmarks = store.Data.bookmarks.Take(8).ToArray() });
        }
        private void Send(WebView2 view, object value)
        {
            try { view.CoreWebView2?.PostWebMessageAsJson(store.Json.Serialize(value)); } catch (InvalidOperationException) { } catch (COMException) { }
        }
        private void Toast(string text) { if (!closing) Send(chrome, new { type = "toast", text }); }
        private void SaveQuietly() { try { store.Save(); } catch { Toast("Instalasi ini belum bisa menyimpan data. Periksa izin folder pengguna."); } }
        private static string Field(Dictionary<string, object> data, string key) { object value; return data != null && data.TryGetValue(key, out value) && value != null ? value.ToString() : ""; }
        private static string Clip(string text, int length) { text = text ?? ""; return text.Length <= length ? text : text.Substring(0, length); }
        private static string HostLabel(string value) { Uri uri; return Uri.TryCreate(value, UriKind.Absolute, out uri) ? uri.Host : value; }
        private static string PermissionLabel(CoreWebView2PermissionKind kind)
        {
            if (kind == CoreWebView2PermissionKind.Camera) return "kamera";
            if (kind == CoreWebView2PermissionKind.Microphone) return "mikrofon";
            if (kind == CoreWebView2PermissionKind.Geolocation) return "lokasi";
            if (kind == CoreWebView2PermissionKind.Notifications) return "notifikasi";
            if (kind == CoreWebView2PermissionKind.ClipboardRead) return "clipboard";
            return kind.ToString();
        }

        // WebView2's accelerator event is synchronous. Defer all COM/UI work.
        private void WireKeys(WebView2 view)
        {
            view.KeyDown += (s, e) =>
            {
                Keys keys = e.KeyData;
                if (IsBrowserKey(keys))
                {
                    e.Handled = true;
                    if (!closing) BeginInvoke(new Action(async () => await HandleKeysAsync(keys)));
                }
            };
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsBrowserKey(keyData)) { if (!closing) BeginInvoke(new Action(async () => await HandleKeysAsync(keyData))); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private static bool IsBrowserKey(Keys keys)
        {
            return keys == (Keys.Control | Keys.L) || keys == (Keys.Control | Keys.T) || keys == (Keys.Control | Keys.W)
                || keys == (Keys.Control | Keys.Shift | Keys.T) || keys == (Keys.Control | Keys.R) || keys == Keys.F5
                || keys == (Keys.Control | Keys.D) || keys == (Keys.Control | Keys.B) || keys == (Keys.Control | Keys.H)
                || keys == (Keys.Control | Keys.J) || keys == (Keys.Alt | Keys.Left) || keys == (Keys.Alt | Keys.Right)
                || keys == (Keys.Control | Keys.Tab) || keys == (Keys.Control | Keys.Shift | Keys.Tab) || keys == Keys.F11
                || keys == (Keys.Control | Keys.Oemplus) || keys == (Keys.Control | Keys.Add) || keys == (Keys.Control | Keys.OemMinus)
                || keys == (Keys.Control | Keys.Subtract) || keys == (Keys.Control | Keys.D0);
        }
        private async Task HandleKeysAsync(Keys keys)
        {
            try
            {
                if (keys == (Keys.Control | Keys.L)) { chrome.Focus(); Send(chrome, new { type = "focusAddress" }); }
                else if (keys == (Keys.Control | Keys.T)) { await CreateTabAsync(BrowserInput.HomeUrl); chrome.Focus(); Send(chrome, new { type = "focusAddress" }); }
                else if (keys == (Keys.Control | Keys.W)) await CloseTabAsync(active);
                else if (keys == (Keys.Control | Keys.Shift | Keys.T)) { if (closedTabs.Count > 0) await CreateTabAsync(closedTabs.Pop()); }
                else if (keys == (Keys.Control | Keys.R) || keys == Keys.F5) Reload();
                else if (keys == (Keys.Control | Keys.D)) ToggleBookmark();
                else if (keys == (Keys.Control | Keys.B)) SetSidebar("bookmarks");
                else if (keys == (Keys.Control | Keys.H)) SetSidebar("history");
                else if (keys == (Keys.Control | Keys.J)) SetSidebar("downloads");
                else if (keys == (Keys.Alt | Keys.Left) && active?.View.CoreWebView2?.CanGoBack == true) active.View.CoreWebView2.GoBack();
                else if (keys == (Keys.Alt | Keys.Right) && active?.View.CoreWebView2?.CanGoForward == true) active.View.CoreWebView2.GoForward();
                else if ((keys == (Keys.Control | Keys.Tab) || keys == (Keys.Control | Keys.Shift | Keys.Tab)) && active != null)
                {
                    int step = keys.HasFlag(Keys.Shift) ? -1 : 1;
                    await ActivateTabAsync(tabs[(tabs.IndexOf(active) + step + tabs.Count) % tabs.Count]);
                }
                else if (keys == Keys.F11) ToggleFullscreen();
                else if (active?.View.CoreWebView2 != null)
                {
                    double zoom = active.View.ZoomFactor;
                    if (keys == (Keys.Control | Keys.D0)) zoom = 1;
                    else if (keys == (Keys.Control | Keys.OemMinus) || keys == (Keys.Control | Keys.Subtract)) zoom -= 0.1;
                    else zoom += 0.1;
                    active.View.ZoomFactor = Math.Max(0.25, Math.Min(3, zoom));
                }
            }
            catch { Toast("Perintah belum bisa dijalankan."); }
        }
        private void ToggleFullscreen()
        {
            fullscreen = !fullscreen;
            if (fullscreen)
            {
                previousBounds = Bounds; previousWindowState = WindowState;
                WindowState = FormWindowState.Normal; Bounds = Screen.FromControl(this).Bounds;
                chrome.Visible = false; sidebar.Visible = false; Padding = Padding.Empty;
            }
            else
            {
                Bounds = previousBounds; WindowState = previousWindowState; chrome.Visible = true; Padding = new Padding(5);
            }
        }
        protected override void WndProc(ref Message message)
        {
            const int WM_NCHITTEST = 0x84;
            if (message.Msg == WM_NCHITTEST && !fullscreen && WindowState == FormWindowState.Normal)
            {
                var position = PointToClient(new Point(unchecked((short)(long)message.LParam), unchecked((short)((long)message.LParam >> 16))));
                bool left = position.X < 5, right = position.X >= ClientSize.Width - 5, top = position.Y < 5, bottom = position.Y >= ClientSize.Height - 5;
                if (left || right || top || bottom) { message.Result = (IntPtr)(top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : 11); return; }
            }
            base.WndProc(ref message);
        }
    }
}
