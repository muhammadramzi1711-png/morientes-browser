using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace MorientesBrowser
{
    public class SavedLink
    {
        public string id { get; set; } = Guid.NewGuid().ToString("N");
        public string title { get; set; }
        public string url { get; set; }
        public string time { get; set; } = DateTimeOffset.Now.ToString("o");
    }
    public class BrowserSettings
    {
        public string engine { get; set; } = "google";
        public string theme { get; set; } = "light";
        public bool restoreSession { get; set; } = true;
        public int defaultZoom { get; set; } = 80;
        public bool autoUpdates { get; set; } = true;
    }
    public class BrowserData
    {
        public BrowserSettings settings { get; set; } = new BrowserSettings();
        public List<SavedLink> bookmarks { get; set; } = new List<SavedLink>();
        public List<SavedLink> history { get; set; } = new List<SavedLink>();
        public List<string> session { get; set; } = new List<string>();
    }
    public sealed class BrowserStore
    {
        public readonly string Folder;
        public BrowserData Data { get; private set; }
        public readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        public BrowserStore()
        {
            Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MorientesBrowser");
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "browser.json");
            try { Data = File.Exists(path) ? Json.Deserialize<BrowserData>(File.ReadAllText(path)) : new BrowserData(); }
            catch { Data = new BrowserData(); }
            Data = Data ?? new BrowserData();
            Data.settings = Data.settings ?? new BrowserSettings();
            if (Data.settings.defaultZoom < 25 || Data.settings.defaultZoom > 300) Data.settings.defaultZoom = 80;
            Data.bookmarks = (Data.bookmarks ?? new List<SavedLink>()).Where(x => x != null && BrowserInput.IsHttp(x.url)).Take(1000).ToList();
            Data.history = (Data.history ?? new List<SavedLink>()).Where(x => x != null && BrowserInput.IsHttp(x.url) && x.url != BrowserInput.HomeUrl).Take(1000).ToList();
            Data.session = (Data.session ?? new List<string>()).Where(x => BrowserInput.IsHttp(x)).Take(30).ToList();
            if (!new[] { "google", "duckduckgo", "bing" }.Contains(Data.settings.engine)) Data.settings.engine = "google";
            if (!new[] { "light", "dark" }.Contains(Data.settings.theme)) Data.settings.theme = "light";
        }
        public void Save()
        {
            string path = Path.Combine(Folder, "browser.json"), temp = path + ".tmp";
            File.WriteAllText(temp, Json.Serialize(Data));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        public void Visit(string title, string url)
        {
            if (!BrowserInput.IsHttp(url) || url == BrowserInput.HomeUrl) return;
            if (Data.history.Count > 0 && Data.history[0].url == url)
            { Data.history[0].title = title; Data.history[0].time = DateTimeOffset.Now.ToString("o"); }
            else Data.history.Insert(0, new SavedLink { title = title, url = url });
            if (Data.history.Count > 1000) Data.history.RemoveRange(1000, Data.history.Count - 1000);
            Save();
        }
    }
}
