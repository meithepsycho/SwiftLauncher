using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwiftLauncher
{
    public class MinecraftVersion
    {
        private readonly HttpClient _httpClient;

        public MinecraftVersion(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<MinecraftVersionInfo>> GetReleasesAsync()
        {
            string url = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
            string jsonResponse = await _httpClient.GetStringAsync(url);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var manifest = JsonSerializer.Deserialize<VersionManifest>(jsonResponse, options);

            if (manifest?.Versions != null)
            {
                return manifest.Versions
                    .Where(v => v.Type.Equals("release", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new List<MinecraftVersionInfo>();
        }
    }

    public class VersionManifest
    {
        [JsonPropertyName("latest")]
        public LatestVersion Latest { get; set; }

        [JsonPropertyName("versions")]
        public List<MinecraftVersionInfo> Versions { get; set; }
    }

    public class LatestVersion
    {
        [JsonPropertyName("release")]
        public string Release { get; set; }

        [JsonPropertyName("snapshot")]
        public string Snapshot { get; set; }
    }

    public class MinecraftVersionInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        [JsonPropertyName("releaseTime")]
        public DateTime ReleaseTime { get; set; }
    }
}