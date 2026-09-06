using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SwiftLauncher
{
    public class DownloadJava
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseDirectory;

        public DownloadJava(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }

        public async Task DownloadAsync(string minecraftVersion, IProgress<DownloadProgress> progress = null)
        {
            int javaVersion = GetRequiredJavaVersion(minecraftVersion);
            string os = GetAdoptiumOs();
            string arch = GetAdoptiumArch();

            string downloadUrl = $"https://api.adoptium.net/v3/binary/latest/{javaVersion}/ga/{os}/{arch}/jre/hotspot/normal/eclipse?project=jdk";
            string runtimeDir = Path.Combine(_baseDirectory, "runtime", $"jre-{javaVersion}");

            if (Directory.Exists(runtimeDir) && Directory.GetFiles(runtimeDir, "java*", SearchOption.AllDirectories).Length > 0)
            {
                return;
            }

            string tempZipPath = Path.Combine(Path.GetTempPath(), $"jre-{javaVersion}_{Guid.NewGuid()}.zip");

            try
            {
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? 0;
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[8192];
                int read;
                long totalRead = 0;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;
                    progress?.Report(new DownloadProgress { Message = "Java", CurrentBytes = totalRead, TotalBytes = total });
                }

                if (Directory.Exists(runtimeDir))
                    Directory.Delete(runtimeDir, true);

                Directory.CreateDirectory(runtimeDir);
                ZipFile.ExtractToDirectory(tempZipPath, runtimeDir, overwriteFiles: true);
            }
            finally
            {
                if (File.Exists(tempZipPath))
                    File.Delete(tempZipPath);
            }
        }

        private int GetRequiredJavaVersion(string version)
        {
            string cleanVersion = version.Split('-')[0];
            var parts = cleanVersion.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;

            if (major > 1) return 21;
            if (major == 1)
            {
                if (minor > 20) return 21;
                if (minor == 20 && patch >= 5) return 21;
                if (minor >= 18) return 17;
                if (minor == 17) return 16;
                return 8;
            }
            return 8;
        }

        private string GetAdoptiumOs()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "mac";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
            return "windows";
        }

        private string GetAdoptiumArch()
        {
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X64: return "x64";
                case Architecture.X86: return "x32";
                case Architecture.Arm64: return "arm64";
                default: return "x64";
            }
        }
    }
}