using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwiftLauncher
{
    public class DownloadMinecraft
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseDirectory;

        public DownloadMinecraft(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }

        public async Task DownloadAsync(string versionId, IProgress<DownloadProgress> progress = null)
        {
            string manifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
            string manifestJson = await _httpClient.GetStringAsync(manifestUrl);

            using JsonDocument doc = JsonDocument.Parse(manifestJson);
            string versionUrl = doc.RootElement.GetProperty("versions").EnumerateArray()
                .FirstOrDefault(v => v.GetProperty("id").GetString() == versionId)
                .GetProperty("url").GetString();

            if (string.IsNullOrEmpty(versionUrl))
                throw new Exception($"Version {versionId} not found in manifest.");

            string versionDir = Path.Combine(_baseDirectory, "versions", versionId);
            Directory.CreateDirectory(versionDir);

            string versionJsonPath = Path.Combine(versionDir, $"{versionId}.json");
            string versionJson = await _httpClient.GetStringAsync(versionUrl);
            await File.WriteAllTextAsync(versionJsonPath, versionJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var versionData = JsonSerializer.Deserialize<VersionMetadata>(versionJson, options);

            string clientJarPath = Path.Combine(versionDir, $"{versionId}.jar");
            progress?.Report(new DownloadProgress { Message = "Client.jar", CurrentBytes = 0, TotalBytes = versionData.Downloads.Client.Size });
            await DownloadFileAsync(versionData.Downloads.Client.Url, clientJarPath, progress, "Client.jar", versionData.Downloads.Client.Size);

            string assetIndexDir = Path.Combine(_baseDirectory, "assets", "indexes");
            Directory.CreateDirectory(assetIndexDir);
            string assetIndexPath = Path.Combine(assetIndexDir, $"{versionData.AssetIndex.Id}.json");

            string assetIndexJson = await _httpClient.GetStringAsync(versionData.AssetIndex.Url);
            await File.WriteAllTextAsync(assetIndexPath, assetIndexJson);

            var assetData = JsonSerializer.Deserialize<AssetIndex>(assetIndexJson, options);
            int count = 0;
            int total = assetData.Objects.Count;

            foreach (var kvp in assetData.Objects)
            {
                count++;
                progress?.Report(new DownloadProgress { Message = $"Assets ({count}/{total})", CurrentBytes = count, TotalBytes = total });

                string hash = kvp.Value.Hash;
                string subDir = hash.Substring(0, 2);
                string assetUrl = $"https://resources.download.minecraft.net/{subDir}/{hash}";
                string assetPath = Path.Combine(_baseDirectory, "assets", "objects", subDir, hash);

                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                await DownloadFileAsync(assetUrl, assetPath);
            }
            Console.WriteLine();

            string libDir = Path.Combine(_baseDirectory, "libraries");
            int libCount = 0;
            int libTotal = versionData.Libraries.Count;

            foreach (var lib in versionData.Libraries)
            {
                libCount++;
                progress?.Report(new DownloadProgress { Message = $"Libraries ({libCount}/{libTotal})", CurrentBytes = libCount, TotalBytes = libTotal });

                if (lib.Downloads?.Artifact != null)
                {
                    string libPath = Path.Combine(libDir, lib.Downloads.Artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(libPath));
                    await DownloadFileAsync(lib.Downloads.Artifact.Url, libPath);
                }
            }
        }

        private async Task DownloadFileAsync(string url, string filePath, IProgress<DownloadProgress> progress = null, string message = "", long knownSize = 0)
        {
            if (File.Exists(filePath)) return;

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long total = knownSize > 0 ? knownSize : (response.Content.Headers.ContentLength ?? 0);
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[8192];
            int read;
            long totalRead = 0;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (progress != null && total > 0)
                {
                    progress.Report(new DownloadProgress { Message = message, CurrentBytes = totalRead, TotalBytes = total });
                }
            }
        }
    }

    public class DownloadProgress
    {
        public string Message { get; set; }
        public long CurrentBytes { get; set; }
        public long TotalBytes { get; set; }
    }

    public class VersionMetadata
    {
        [JsonPropertyName("downloads")]
        public DownloadsContainer Downloads { get; set; }

        [JsonPropertyName("assetIndex")]
        public AssetIndexInfo AssetIndex { get; set; }

        [JsonPropertyName("libraries")]
        public List<Library> Libraries { get; set; }
    }

    public class DownloadsContainer
    {
        [JsonPropertyName("client")]
        public DownloadInfo Client { get; set; }
    }

    public class DownloadInfo
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    public class AssetIndexInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class Library
    {
        [JsonPropertyName("downloads")]
        public LibraryDownloads Downloads { get; set; }
    }

    public class LibraryDownloads
    {
        [JsonPropertyName("artifact")]
        public DownloadInfo Artifact { get; set; }
    }

    public class AssetIndex
    {
        [JsonPropertyName("objects")]
        public Dictionary<string, AssetObject> Objects { get; set; }
    }

    public class AssetObject
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; }
    }
}