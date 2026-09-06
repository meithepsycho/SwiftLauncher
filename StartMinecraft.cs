using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwiftLauncher
{
    public class StartMinecraft
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseDirectory;

        public StartMinecraft(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }

        public async Task<Process> StartAsync(string versionId, int ramMB, string username = "Player")
        {
            string versionDir = Path.Combine(_baseDirectory, "versions", versionId);
            string versionJsonPath = Path.Combine(versionDir, $"{versionId}.json");

            if (!File.Exists(versionJsonPath))
                throw new Exception("Version JSON not found. Please download Minecraft first.");

            string versionJson = await File.ReadAllTextAsync(versionJsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var versionData = JsonSerializer.Deserialize<StartVersionMetadata>(versionJson, options);

            string javaPath = GetJavaPath(versionId);
            string nativesDir = Path.Combine(versionDir, "natives-temp");
            string classpath = await BuildClasspathAndExtractNativesAsync(versionData, versionDir, nativesDir);

            string args = BuildGameArguments(versionData, versionId, username, nativesDir);

            string jvmArgs = $"-Xmx{ramMB}M -Xms{ramMB}M -Djava.library.path=\"{nativesDir}\" -cp \"{classpath}\" {versionData.MainClass} {args}";

            var processInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = jvmArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _baseDirectory
            };

            return Process.Start(processInfo);
        }

        private string GetJavaPath(string versionId)
        {
            int javaVersion = GetRequiredJavaVersion(versionId);
            string javaBaseDir = Path.Combine(_baseDirectory, "runtime", $"jre-{javaVersion}");
            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";

            if (!Directory.Exists(javaBaseDir))
                throw new Exception("Java runtime directory not found. Please ensure Java is downloaded.");

            string[] files = Directory.GetFiles(javaBaseDir, exeName, SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];

            throw new Exception("Java executable not found. Please ensure Java is downloaded.");
        }

        private async Task<string> BuildClasspathAndExtractNativesAsync(StartVersionMetadata versionData, string versionDir, string nativesDir)
        {
            if (Directory.Exists(nativesDir))
                Directory.Delete(nativesDir, true);
            Directory.CreateDirectory(nativesDir);

            string libDir = Path.Combine(_baseDirectory, "libraries");
            List<string> classpath = new List<string>();
            string osKey = GetOsKey();

            foreach (var lib in versionData.Libraries)
            {
                if (lib.Downloads?.Artifact != null)
                {
                    string libPath = Path.Combine(libDir, lib.Downloads.Artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(libPath)) classpath.Add(libPath);
                }

                if (lib.Natives != null && lib.Natives.ContainsKey(osKey) && lib.Downloads?.Classifiers != null)
                {
                    string nativeKey = lib.Natives[osKey].Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32");
                    if (lib.Downloads.Classifiers.ContainsKey(nativeKey))
                    {
                        var dl = lib.Downloads.Classifiers[nativeKey];
                        string nativePath = Path.Combine(libDir, dl.Path.Replace('/', Path.DirectorySeparatorChar));

                        if (!File.Exists(nativePath))
                        {
                            string nativeDir = Path.GetDirectoryName(nativePath);
                            if (!Directory.Exists(nativeDir))
                                Directory.CreateDirectory(nativeDir);

                            using var resp = await _httpClient.GetAsync(dl.Url);
                            resp.EnsureSuccessStatusCode();

                            using var fs = new FileStream(nativePath, FileMode.Create);
                            using var stream = await resp.Content.ReadAsStreamAsync();
                            await stream.CopyToAsync(fs);
                        }

                        using ZipArchive archive = ZipFile.OpenRead(nativePath);
                        foreach (var entry in archive.Entries)
                        {
                            if (!entry.FullName.EndsWith(".git") && !entry.FullName.StartsWith("META-INF"))
                            {
                                string destPath = Path.Combine(nativesDir, entry.FullName);
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                entry.ExtractToFile(destPath, true);
                            }
                        }
                    }
                }
            }

            string clientJar = Path.Combine(versionDir, $"{versionData.Assets}.jar");
            if (!File.Exists(clientJar)) clientJar = Path.Combine(versionDir, $"{versionData.Id}.jar");
            classpath.Add(clientJar);

            return string.Join(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":", classpath);
        }

        private string BuildGameArguments(StartVersionMetadata versionData, string versionId, string username, string nativesDir)
        {
            string args = versionData.MinecraftArguments ?? string.Empty;

            args = args.Replace("${auth_player_name}", username);
            args = args.Replace("${version_name}", versionId);
            args = args.Replace("${game_directory}", $"\"{_baseDirectory}\"");
            args = args.Replace("${assets_root}", $"\"{Path.Combine(_baseDirectory, "assets")}\"");
            args = args.Replace("${assets_index_name}", versionData.Assets);
            args = args.Replace("${auth_uuid}", "00000000-0000-0000-0000-000000000000");
            args = args.Replace("${auth_access_token}", "offline");
            args = args.Replace("${user_type}", "legacy");
            args = args.Replace("${version_type}", "SwiftLauncher");
            args = args.Replace("${user_properties}", "{}");
            args = args.Replace("${profile_properties}", "{}");
            args = args.Replace("${language}", "en_US");

            return args;
        }

        private string GetOsKey()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx";
            return "linux";
        }

        private int GetRequiredJavaVersion(string version)
        {
            string cleanVersion = version.Split('-')[0];
            var parts = cleanVersion.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            int major = parts.Length > 0 ? parts[0] : 0;
            int minor = parts.Length > 1 ? parts[1] : 0;
            int patch = parts.Length > 2 ? parts[2] : 0;

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
    }

    public class StartVersionMetadata
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("mainClass")]
        public string MainClass { get; set; }

        [JsonPropertyName("minecraftArguments")]
        public string MinecraftArguments { get; set; }

        [JsonPropertyName("assets")]
        public string Assets { get; set; }

        [JsonPropertyName("libraries")]
        public List<StartLibrary> Libraries { get; set; }
    }

    public class StartLibrary
    {
        [JsonPropertyName("natives")]
        public Dictionary<string, string> Natives { get; set; }

        [JsonPropertyName("downloads")]
        public StartLibraryDownloads Downloads { get; set; }
    }

    public class StartLibraryDownloads
    {
        [JsonPropertyName("artifact")]
        public StartDownloadInfo Artifact { get; set; }

        [JsonPropertyName("classifiers")]
        public Dictionary<string, StartDownloadInfo> Classifiers { get; set; }
    }

    public class StartDownloadInfo
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}