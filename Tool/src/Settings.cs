using System;
using System.IO;

namespace PackStudio
{
    /// <summary>
    /// Tiny persisted preferences. Lives NEXT TO THE EXE as
    /// "PackStudio.settings.json" (self-describing and per-copy, so a tool
    /// shipped inside a mod-manager profile remembers that profile's own
    /// install), falling back to %AppData%\ErenshorPackStudio\ only when the
    /// EXE's folder isn't writable (e.g. Program Files). Failures are silent -
    /// settings are always optional.
    /// </summary>
    internal static class Settings
    {
        internal static string TargetPacksRoot;

        private static string ExeSidePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PackStudio.settings.json");
        }

        private static string AppDataPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ErenshorPackStudio", "settings.json");
        }

        internal static void Load()
        {
            if (!TryLoad(ExeSidePath()))
            {
                TryLoad(AppDataPath());
            }
        }

        private static bool TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path)) { return false; }
                JObj root = Json.Parse(File.ReadAllText(path)) as JObj;
                if (root == null) { return false; }
                JStr target = root["TargetPacksRoot"] as JStr;
                if (target != null) { TargetPacksRoot = target.Value; }
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void Save()
        {
            JObj root = new JObj();
            root["_ABOUT"] = new JStr("Erenshor Pack Studio settings - safe to delete;"
                + " the tool recreates it when you pick an install.");
            if (!string.IsNullOrEmpty(TargetPacksRoot))
            {
                root["TargetPacksRoot"] = new JStr(TargetPacksRoot);
            }
            string text = Json.Write(root);
            try
            {
                File.WriteAllText(ExeSidePath(), text, new System.Text.UTF8Encoding(false));
                return;
            }
            catch { }
            try
            {
                string fallback = AppDataPath();
                Directory.CreateDirectory(Path.GetDirectoryName(fallback));
                File.WriteAllText(fallback, text, new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}
