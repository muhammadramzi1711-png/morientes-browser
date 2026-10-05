using System;
using System.Text.RegularExpressions;

namespace MorientesBrowser
{
    public static class BrowserInput
    {
        public const string HomeUrl = "https://start.morientes.invalid/home.html";
        public const string ChromeUrl = "https://ui.morientes.invalid/chrome.html";
        public const string SidebarUrl = "https://ui.morientes.invalid/sidebar.html";

        public static bool IsHttp(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
        }

        public static bool IsTrustedUi(string source)
        {
            return string.Equals(source, ChromeUrl, StringComparison.Ordinal)
                || string.Equals(source, SidebarUrl, StringComparison.Ordinal);
        }

        public static string Normalize(string input, string engine = "google")
        {
            string text = (input ?? "").Trim();
            if (text.Length > 8192 || Regex.IsMatch(text, "[\\x00-\\x1F\\x7F]"))
                throw new ArgumentException("Alamat tidak valid.");
            if (text.Length == 0 || text == "morientes:home" || text == HomeUrl) return HomeUrl;
            if (IsHttp(text)) return new Uri(text).AbsoluteUri;
            bool local = Regex.IsMatch(text, @"^localhost(?::\d{1,5})?(?:[/?#]|$)", RegexOptions.IgnoreCase);
            bool host = Regex.IsMatch(text, @"^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9-]+(?::\d{1,5})?(?:[/?#]|$)", RegexOptions.IgnoreCase);
            bool ipv6 = Regex.IsMatch(text, @"^\[[0-9a-f:]+\](?::\d{1,5})?(?:[/?#]|$)", RegexOptions.IgnoreCase);
            if (!Regex.IsMatch(text, @"\s") && (local || host || ipv6))
            {
                string candidate = (local ? "http://" : "https://") + text;
                if (IsHttp(candidate)) return new Uri(candidate).AbsoluteUri;
                throw new ArgumentException("Alamat tidak valid.");
            }
            if (Regex.IsMatch(text, @"^[a-zA-Z][a-zA-Z0-9+.-]*:"))
                throw new ArgumentException("Gunakan alamat HTTP/HTTPS atau kata pencarian.");
            string encoded = Uri.EscapeDataString(text);
            if (engine == "duckduckgo") return "https://duckduckgo.com/?q=" + encoded;
            if (engine == "bing") return "https://www.bing.com/search?q=" + encoded;
            return "https://www.google.com/search?q=" + encoded;
        }
    }
}
