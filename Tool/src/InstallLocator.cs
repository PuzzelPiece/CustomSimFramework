using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PackStudio
{
    internal sealed class InstallTarget
    {
        internal string Label;      // e.g. "Steam install" / "r2modman profile 'Default'"
        internal string PacksRoot;  // <BepInEx>/plugins/CustomSimFramework/Packs

        public override string ToString() { return Label; }
    }

    /// <summary>
    /// Finds every place packs can live, so users never browse for folders:
    ///  1. the folder tree the EXE itself sits in (tool ships in the plugin dir),
    ///  2. the Steam install (registry + libraryfolders.vdf),
    ///  3. r2modman profiles, 4. Thunderstore Mod Manager profiles.
    /// All read-only probing. The Packs folder is created on demand only for
    /// the target the user picks.
    /// </summary>
    internal static class InstallLocator
    {
        internal static List<InstallTarget> FindAll()
        {
            List<InstallTarget> targets = new List<InstallTarget>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) walk up from the EXE looking for a BepInEx tree
            try
            {
                DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null)
                {
                    string bepinex = Path.Combine(dir.FullName, "BepInEx");
                    if (Directory.Exists(bepinex))
                    {
                        AddTarget(targets, seen, "This game install (" + dir.Name + ")", bepinex);
                        break;
                    }
                    if (dir.Name.Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
                    {
                        AddTarget(targets, seen, "This game install", dir.FullName);
                        break;
                    }
                    dir = dir.Parent;
                }
            }
            catch { }

            // 2) Steam
            foreach (string library in SteamLibraries())
            {
                string game = Path.Combine(Path.Combine(library, "steamapps"), Path.Combine("common", "Erenshor"));
                string bepinex = Path.Combine(game, "BepInEx");
                if (Directory.Exists(bepinex))
                {
                    AddTarget(targets, seen, "Steam install", bepinex);
                }
            }

            // 3 + 4) mod-manager profiles
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AddProfiles(targets, seen, Path.Combine(appdata, Path.Combine("r2modmanPlus-local", Path.Combine("Erenshor", "profiles"))), "r2modman profile");
            AddProfiles(targets, seen, Path.Combine(appdata, Path.Combine("Thunderstore Mod Manager", Path.Combine("DataFolder", Path.Combine("Erenshor", "profiles")))), "Mod Manager profile");

            return targets;
        }

        private static void AddProfiles(List<InstallTarget> targets, HashSet<string> seen, string profilesRoot, string label)
        {
            try
            {
                if (!Directory.Exists(profilesRoot)) { return; }
                foreach (string profile in Directory.GetDirectories(profilesRoot))
                {
                    string bepinex = Path.Combine(profile, "BepInEx");
                    if (Directory.Exists(bepinex))
                    {
                        AddTarget(targets, seen, label + " '" + Path.GetFileName(profile) + "'", bepinex);
                    }
                }
            }
            catch { }
        }

        private static void AddTarget(List<InstallTarget> targets, HashSet<string> seen, string label, string bepinexDir)
        {
            // Packs live under BepInEx\config since mod v0.9.3 (preserved by
            // mod managers across updates, and the mod migrates old installs).
            string packs = Path.Combine(bepinexDir, Path.Combine("config", Path.Combine("CustomSimFramework", "Packs")));
            if (seen.Add(packs))
            {
                targets.Add(new InstallTarget { Label = label, PacksRoot = packs });
            }
        }

        private static IEnumerable<string> SteamLibraries()
        {
            List<string> libraries = new List<string>();
            string steam = null;
            try
            {
                steam = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
            }
            catch { }
            if (string.IsNullOrEmpty(steam))
            {
                try
                {
                    steam = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string;
                }
                catch { }
            }
            if (string.IsNullOrEmpty(steam) || !Directory.Exists(steam))
            {
                return libraries;
            }
            steam = steam.Replace('/', '\\');
            libraries.Add(steam);
            try
            {
                string vdf = Path.Combine(steam, Path.Combine("steamapps", "libraryfolders.vdf"));
                if (File.Exists(vdf))
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    {
                        string path = m.Groups[1].Value.Replace("\\\\", "\\");
                        if (Directory.Exists(path) && !libraries.Contains(path))
                        {
                            libraries.Add(path);
                        }
                    }
                }
            }
            catch { }
            return libraries;
        }
    }
}
