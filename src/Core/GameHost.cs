using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hakoniwa.UI;

namespace Hakoniwa.Core;

public static class GameHost
{
    public static string TerrariaDirectory { get; private set; } = "";
    public static string ContentDirectory { get; private set; } = "";

    public static void Run(string[] args)
    {
        TerrariaDirectory = LocateTerraria();
        ContentDirectory = LocateContent();
        Directory.SetCurrentDirectory(TerrariaDirectory);
        SetDllDirectory(ContentDirectory);
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        if (string.IsNullOrEmpty(Terraria.Program.SavePath))
            Terraria.Program.SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "My Games", "Terraria");

        CheatState.Load();
        InventoryPacks.Load();
        using var hooks = new HookManager();
        CheatHooks.Install(hooks);
        ItemTooltipExtra.Install(hooks);
        NativeTextInput.Install(hooks);
        HakoniwaUi.Install();
        Terraria.Program.LaunchGame(args);
    }

    private static string LocateTerraria()
    {
        string? dir = Path.GetDirectoryName(typeof(GameHost).Assembly.Location);
        if (dir is not null && File.Exists(Path.Combine(dir, "Terraria.exe")))
            return dir;

        string? parent = dir is null ? null : Directory.GetParent(dir)?.FullName;
        if (parent is not null && File.Exists(Path.Combine(parent, "Terraria.exe")))
            return parent;

        const string steam = @"D:\SteamLibrary\steamapps\common\Terraria";
        if (File.Exists(Path.Combine(steam, "Terraria.exe")))
            return steam;

        throw new DirectoryNotFoundException("Terraria.exe not found. Set TerrariaDir or copy Hakoniwa next to it.");
    }

    private static string LocateContent()
    {
        string exeDir = Path.GetDirectoryName(typeof(GameHost).Assembly.Location) ?? TerrariaDirectory;
        if (File.Exists(Path.Combine(exeDir, "Terraria.exe")))
            return Path.Combine(TerrariaDirectory, "Hakoniwa");
        return exeDir;
    }

    private static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name ?? "";
        if (name.Length == 0 || name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (string folder in new[] { ContentDirectory, TerrariaDirectory })
        {
            string path = Path.Combine(folder, name + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);
        }

        return LoadEmbedded(name);
    }

    private static Assembly? LoadEmbedded(string name)
    {
        var terraria = typeof(Terraria.Program).Assembly;
        string suffix = name + ".dll";
        foreach (string resource in terraria.GetManifestResourceNames())
        {
            if (!resource.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;
            using var stream = terraria.GetManifestResourceStream(resource);
            if (stream is null)
                throw new FileNotFoundException($"Terraria embedded resource '{resource}' is empty.", name);
            var bytes = new byte[checked((int)stream.Length)];
            _ = stream.Read(bytes, 0, bytes.Length);
            return Assembly.Load(bytes);
        }

        return null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);
}
