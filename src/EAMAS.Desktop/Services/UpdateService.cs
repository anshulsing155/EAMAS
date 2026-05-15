using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace EAMAS.Desktop.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }

    public class UpdateService
    {
        // ── Configure this URL to point to your hosted version.json ─────────────
        // Host version.json at any public HTTPS URL and update it before each release.
        // Recommended free options:
        //   GitHub raw:  https://raw.githubusercontent.com/YOU/EAMAS/main/version.json
        //   GitHub Gist: https://gist.githubusercontent.com/YOU/GIST_ID/raw/version.json
        public const string VersionManifestUrl =
            "https://raw.githubusercontent.com/anshulsing155/EAMAS-updates/main/version.json";
        // ────────────────────────────────────────────────────────────────────────

        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(15)   // for manifest check only
        };

        private static readonly HttpClient _downloadHttp = new()
        {
            Timeout = Timeout.InfiniteTimeSpan   // download can be 50+ MB
        };

        /// <summary>The version currently installed (read from assembly metadata).</summary>
        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 1, 0);

        /// <summary>
        /// Fetches the remote version manifest and returns update info when a newer version
        /// is available; returns null when already up-to-date or when the check fails.
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(VersionManifestUrl).ConfigureAwait(false);
                var info = JsonSerializer.Deserialize<UpdateInfo>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (info == null || string.IsNullOrWhiteSpace(info.Version)) return null;
                if (!Version.TryParse(info.Version, out var latest)) return null;

                return latest > CurrentVersion ? info : null;
            }
            catch
            {
                return null; // network unavailable or manifest not yet hosted — silent fail
            }
        }

        /// <summary>
        /// Downloads the installer at <paramref name="downloadUrl"/> into %TEMP%,
        /// then launches it and exits the current application.
        /// </summary>
        public async Task DownloadAndInstallAsync(string downloadUrl,
            IProgress<int>? progress = null)
        {
            var dest = Path.Combine(Path.GetTempPath(), "EAMAS-Update-Setup.exe");

            // Remove any leftover file from a previous attempt so File.Create doesn't fight it
            if (File.Exists(dest))
                try { File.Delete(dest); } catch { /* already gone or locked — overwrite below */ }

            using var response = await _downloadHttp.GetAsync(
                downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total      = response.Content.Headers.ContentLength ?? -1L;
            var downloaded = 0L;

            // Explicit nested scope so both streams are fully closed/disposed
            // BEFORE Process.Start — otherwise the file is still locked.
            await using (var src = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var file = File.Create(dest))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await src.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    downloaded += read;
                    if (total > 0)
                        progress?.Report((int)(downloaded * 100 / total));
                }
            } // file and src are fully closed here

            // WorkingDirectory must be a writable location — never the app's install dir
            // (C:\Program Files) because that is protected and causes "access denied" on launch.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName         = dest,
                Arguments        = "/SILENT",
                UseShellExecute  = true,
                WorkingDirectory = Path.GetTempPath()
            });

            App.ExitApp();
        }
    }
}
