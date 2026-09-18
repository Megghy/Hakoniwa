using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hakoniwa.Core;

internal static class AppUpdate
{
    private const string Feed = "https://i.suki.club/hakoniwa/latest.json";

    public static bool TryApply(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg == "--updated" || arg == "--no-update")
                return false;
        }

        string current = CurrentVersion();
        if (current.StartsWith("dev", StringComparison.OrdinalIgnoreCase))
            return false;

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        Latest latest;
        try
        {
            latest = Fetch();
        }
        catch
        {
            return false;
        }

        if (latest.Version == current)
            return false;

        Apply(latest);
        return true;
    }

    private static string CurrentVersion()
    {
        var attr = (AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(
            typeof(AppUpdate).Assembly, typeof(AssemblyInformationalVersionAttribute));
        string value = attr?.InformationalVersion ?? "dev";
        int plus = value.IndexOf('+');
        if (plus >= 0)
            value = value.Substring(0, plus);
        return value.Length == 0 ? "dev" : value;
    }

    private static Latest Fetch()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        string json = http.GetStringAsync(Feed).GetAwaiter().GetResult();
        var latest = JsonSerializer.Deserialize<Latest>(json)
            ?? throw new InvalidDataException("update feed");
        if (latest.Version.Length == 0 || latest.Url.Length == 0 || latest.Sha256.Length != 64)
            throw new InvalidDataException("update feed");
        return latest;
    }

    private static void Apply(Latest latest)
    {
        string dest = GameDir();
        string work = Path.Combine(Path.GetTempPath(), "HakoniwaUpdate");
        if (Directory.Exists(work))
            Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        string zip = Path.Combine(work, "Hakoniwa.zip");

        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
        using (var resp = http.GetAsync(latest.Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
        {
            resp.EnsureSuccessStatusCode();
            using var fs = File.Create(zip);
            resp.Content.CopyToAsync(fs).GetAwaiter().GetResult();
        }

        if (!Hash(zip).Equals(latest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("update hash");

        string extract = Path.Combine(work, "pack");
        ZipFile.ExtractToDirectory(zip, extract);

        string cmd = Path.Combine(work, "apply.cmd");
        int pid = Process.GetCurrentProcess().Id;
        File.WriteAllText(cmd,
            "@echo off\r\n:wait\r\ntimeout /t 1 /nobreak >nul\r\n" +
            "tasklist /FI \"PID eq " + pid + "\" | find /I \"Hakoniwa.exe\" >nul\r\n" +
            "if not errorlevel 1 goto wait\r\n" +
            "xcopy /E /Y /Q /I \"" + extract + "\\*\" \"" + dest + "\\\"\r\n" +
            "start \"\" \"" + dest + "\\Hakoniwa.exe\" --updated\r\n");
        Process.Start(new ProcessStartInfo(cmd) { UseShellExecute = true });
    }

    private static string GameDir()
    {
        string dir = Path.GetDirectoryName(typeof(AppUpdate).Assembly.Location)
            ?? throw new InvalidOperationException("exe dir");
        if (File.Exists(Path.Combine(dir, "Terraria.exe")))
            return dir;
        string? parent = Directory.GetParent(dir)?.FullName;
        if (parent is not null && File.Exists(Path.Combine(parent, "Terraria.exe")))
            return parent;
        return dir;
    }

    private static string Hash(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private sealed class Latest
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";
    }
}
