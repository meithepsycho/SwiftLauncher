using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwiftLauncher
{
    class Program
    {
        private static readonly string mcDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        private static readonly string settingsPath = Path.Combine(mcDirectory, "settings.json");
        private static Settings settings = new Settings();

        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            Console.Title = "Swift Launcher";

            Directory.CreateDirectory(mcDirectory);
            LoadSettings();

            if (string.IsNullOrWhiteSpace(settings.Username) || settings.RamMB == 0 || string.IsNullOrWhiteSpace(settings.MinecraftVersion))
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== Swift Launcher Initial Setup ===");
                Console.ResetColor();

                Console.Write("Enter your Minecraft Username: ");
                settings.Username = Console.ReadLine();

                Console.Write("Enter RAM amount in MB (e.g., 2048): ");
                if (int.TryParse(Console.ReadLine(), out int ram)) settings.RamMB = ram;
                else settings.RamMB = 2048;

                Console.Write("Enter Minecraft version to download (e.g., 1.20.4 or 1.9): ");
                settings.MinecraftVersion = Console.ReadLine();

                SaveSettings();
            }

            await MainMenu();
        }

        private static async Task MainMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Swift Launcher | Version 1.0.0");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("1. Start Game");
                Console.WriteLine("2. Change Settings");
                Console.WriteLine("3. Quit Launcher");
                Console.WriteLine();
                Console.Write("Select an option: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await StartGame();
                        break;
                    case "2":
                        await ChangeSettings();
                        break;
                    case "3":
                        Environment.Exit(0);
                        break;
                }
            }
        }

        private static async Task StartGame()
        {
            Console.Clear();
            var progress = new Progress<DownloadProgress>(p => DrawProgressBar(p.Message, p.CurrentBytes, p.TotalBytes));

            var minecraftDownloader = new DownloadMinecraft(httpClient);
            var javaDownloader = new DownloadJava(httpClient);
            var starter = new StartMinecraft(httpClient);

            try
            {
                string versionDir = Path.Combine(mcDirectory, "versions", settings.MinecraftVersion);
                bool isDownloaded = File.Exists(Path.Combine(versionDir, $"{settings.MinecraftVersion}.json")) && File.Exists(Path.Combine(versionDir, $"{settings.MinecraftVersion}.jar"));

                if (settings.RedownloadMinecraft || !isDownloaded)
                {
                    if (settings.RedownloadMinecraft) Console.WriteLine("Redownloading Minecraft...");
                    else Console.WriteLine("Minecraft not found. Downloading...");

                    await minecraftDownloader.DownloadAsync(settings.MinecraftVersion, progress);
                    Console.WriteLine();
                    settings.RedownloadMinecraft = false;
                    SaveSettings();
                }

                Console.WriteLine("Checking Java...");
                await javaDownloader.DownloadAsync(settings.MinecraftVersion, progress);
                Console.WriteLine();

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Starting Minecraft {settings.MinecraftVersion}...");
                Console.ResetColor();

                var process = await starter.StartAsync(settings.MinecraftVersion, settings.RamMB, settings.Username);

                if (process != null)
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine(e.Data); Console.ResetColor(); } };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    Console.WriteLine("\nGame closed. Press any key to return to menu...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("Press any key to return...");
                Console.ReadKey();
            }
        }

        private static async Task ChangeSettings()
        {
            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== Settings ===");
                Console.ResetColor();
                Console.WriteLine($"Current Username: {settings.Username}");
                Console.WriteLine($"Current RAM: {settings.RamMB} MB");
                Console.WriteLine($"Current Version: {settings.MinecraftVersion}");
                Console.WriteLine($"Force Redownload: {settings.RedownloadMinecraft}");
                Console.WriteLine();
                Console.WriteLine("1. Change Username");
                Console.WriteLine("2. Change RAM");
                Console.WriteLine("3. Download New Version");
                Console.WriteLine("4. Change Version (Installed)");
                Console.WriteLine("5. Toggle Force Redownload");
                Console.WriteLine("6. Back to Main Menu");
                Console.Write("Select an option: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        Console.Write("New Username: ");
                        string u = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(u)) settings.Username = u;
                        SaveSettings();
                        break;
                    case "2":
                        Console.Write("New RAM in MB: ");
                        if (int.TryParse(Console.ReadLine(), out int ram)) settings.RamMB = ram;
                        SaveSettings();
                        break;
                    case "3":
                        await DownloadNewVersionMenu();
                        break;
                    case "4":
                        ChangeVersionMenu();
                        break;
                    case "5":
                        settings.RedownloadMinecraft = !settings.RedownloadMinecraft;
                        SaveSettings();
                        break;
                    case "6":
                        return;
                }
            }
        }

        private static async Task DownloadNewVersionMenu()
        {
            Console.Clear();
            Console.WriteLine("Fetching available versions from Mojang...");

            try
            {
                var fetcher = new MinecraftVersion(httpClient);
                List<MinecraftVersionInfo> releases = await fetcher.GetReleasesAsync();

                Console.WriteLine("Available Minecraft Releases:");
                for (int i = 0; i < releases.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {releases[i].Id}");
                }

                Console.WriteLine();
                Console.Write("Enter the number of the version to download (or 0 to cancel): ");
                if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= releases.Count)
                {
                    string selectedVersion = releases[choice - 1].Id;
                    settings.MinecraftVersion = selectedVersion;
                    settings.RedownloadMinecraft = true;
                    SaveSettings();

                    Console.Clear();
                    Console.WriteLine($"Starting download for {selectedVersion}...");
                    var progress = new Progress<DownloadProgress>(p => DrawProgressBar(p.Message, p.CurrentBytes, p.TotalBytes));
                    var downloader = new DownloadMinecraft(httpClient);

                    await downloader.DownloadAsync(selectedVersion, progress);
                    Console.WriteLine("\nDownload complete!");
                    settings.RedownloadMinecraft = false;
                    SaveSettings();

                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError fetching versions: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private static void ChangeVersionMenu()
        {
            Console.Clear();
            string versionsDir = Path.Combine(mcDirectory, "versions");
            List<string> installedVersions = new List<string>();

            if (Directory.Exists(versionsDir))
            {
                foreach (var dir in Directory.GetDirectories(versionsDir))
                {
                    string versionName = Path.GetFileName(dir);
                    if (File.Exists(Path.Combine(dir, $"{versionName}.json")) &&
                        File.Exists(Path.Combine(dir, $"{versionName}.jar")))
                    {
                        installedVersions.Add(versionName);
                    }
                }
            }

            if (installedVersions.Count == 0)
            {
                Console.WriteLine("No installed versions found. Please download a version first.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Installed Versions:");
            for (int i = 0; i < installedVersions.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {installedVersions[i]}");
            }

            Console.WriteLine();
            Console.Write("Enter the number of the version to select (or 0 to cancel): ");
            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= installedVersions.Count)
            {
                settings.MinecraftVersion = installedVersions[choice - 1];
                SaveSettings();
                Console.WriteLine($"Version changed to {settings.MinecraftVersion}!");
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void DrawProgressBar(string message, long current, long total)
        {
            if (total <= 0) return;
            double progress = (double)current / total;
            if (progress > 1) progress = 1;

            int width = 25;
            int bars = (int)(progress * width);
            Console.Write($"\r{message} [{new string('#', bars)}{new string('-', width - bars)}] {progress * 100,5:0.00}%");
        }

        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
            }
            catch { settings = new Settings(); }
        }

        private static void SaveSettings()
        {
            Directory.CreateDirectory(mcDirectory);
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
    }

    public class Settings
    {
        public string Username { get; set; } = "Player";
        public int RamMB { get; set; } = 2048;
        public string MinecraftVersion { get; set; } = "1.8.9";
        public bool RedownloadMinecraft { get; set; } = false;
    }
}