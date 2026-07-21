using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PackStudio
{
    /// <summary>
    /// A pack = one folder under Packs/ holding *.sim.json plus the three
    /// optional dialogue files. The model is a thin TYPED VIEW over the raw
    /// Json DOM: edits mutate the DOM in place, so "_" comment keys, unknown
    /// keys and key order all survive a load/edit/save round-trip untouched.
    /// </summary>
    internal sealed class Pack
    {
        internal string Dir;
        internal string Name;
        internal readonly List<SimFile> Sims = new List<SimFile>();
        internal PackJsonFile Global;   // global.dialogue.json (or null)
        internal PackJsonFile Zones;    // zones.dialogue.json (or null)
        internal PackJsonFile Topics;   // topics.dialogue.json (or null)

        internal static Pack Load(string dir)
        {
            Pack pack = new Pack();
            pack.Dir = dir;
            pack.Name = Path.GetFileName(dir);
            foreach (string file in Directory.GetFiles(dir, "*.sim.json"))
            {
                pack.Sims.Add(new SimFile(file));
            }
            pack.Sims.Sort(delegate(SimFile a, SimFile b)
            {
                return string.Compare(a.SimName, b.SimName, StringComparison.OrdinalIgnoreCase);
            });
            string path = Path.Combine(dir, "global.dialogue.json");
            if (File.Exists(path)) { pack.Global = new PackJsonFile(path); }
            path = Path.Combine(dir, "zones.dialogue.json");
            if (File.Exists(path)) { pack.Zones = new PackJsonFile(path); }
            path = Path.Combine(dir, "topics.dialogue.json");
            if (File.Exists(path)) { pack.Topics = new PackJsonFile(path); }
            return pack;
        }

        internal static Pack CreateNew(string packsRoot, string name)
        {
            string dir = Path.Combine(packsRoot, name);
            Directory.CreateDirectory(dir);
            return Load(dir);
        }

        internal IEnumerable<PackJsonFile> AllFiles()
        {
            foreach (SimFile sim in Sims) { yield return sim; }
            if (Global != null) { yield return Global; }
            if (Zones != null) { yield return Zones; }
            if (Topics != null) { yield return Topics; }
        }

        internal PackJsonFile EnsureGlobal()
        {
            if (Global == null)
            {
                Global = new PackJsonFile(System.IO.Path.Combine(Dir, "global.dialogue.json"), new JObj());
            }
            return Global;
        }

        internal PackJsonFile EnsureTopics()
        {
            if (Topics == null)
            {
                Topics = new PackJsonFile(System.IO.Path.Combine(Dir, "topics.dialogue.json"), new JObj());
            }
            return Topics;
        }

        internal PackJsonFile EnsureZones()
        {
            if (Zones == null)
            {
                Zones = new PackJsonFile(System.IO.Path.Combine(Dir, "zones.dialogue.json"), new JObj());
            }
            return Zones;
        }

        /// <summary>Saves every dirty file and returns the names of files that
        /// were REFUSED (they failed to parse at load, and overwriting them
        /// would destroy the hand-authored original).</summary>
        internal List<string> SaveAll()
        {
            List<string> refused = new List<string>();
            foreach (PackJsonFile file in AllFiles())
            {
                if (file.Dirty && !file.Save())
                {
                    refused.Add(file.FileName);
                }
            }
            return refused;
        }
    }

    /// <summary>One JSON file: raw DOM + typed accessors + backup-on-save.</summary>
    internal class PackJsonFile
    {
        internal string Path;
        internal JObj Root;
        internal bool Dirty;
        internal string LoadError; // non-null if the file failed to parse

        internal PackJsonFile(string path)
        {
            Path = path;
            try
            {
                JNode node = Json.Parse(File.ReadAllText(path));
                Root = node as JObj;
                if (Root == null)
                {
                    LoadError = "top-level JSON is not an object";
                    Root = new JObj();
                }
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
                Root = new JObj();
            }
        }

        internal PackJsonFile(string path, JObj root)
        {
            Path = path;
            Root = root;
            Dirty = true;
        }

        internal string FileName { get { return System.IO.Path.GetFileName(Path); } }

        /// <summary>False = refused: the on-disk file failed to parse at load,
        /// and writing our (near-empty) DOM would destroy the original.</summary>
        internal bool Save()
        {
            if (LoadError != null)
            {
                return false;
            }
            // An added-but-never-typed row must not land in the JSON as ""
            // (the loader would strip it with a log warning at game load).
            ScrubEmptyArrayStrings(Root);
            string text = Json.Write(Root);
            if (File.Exists(Path))
            {
                File.Copy(Path, Path + ".bak", true);
            }
            // UTF-8 without BOM, matching the hand-authored files.
            File.WriteAllText(Path, text, new UTF8Encoding(false));
            Dirty = false;
            return true;
        }

        private static void ScrubEmptyArrayStrings(JNode node)
        {
            JObj obj = node as JObj;
            if (obj != null)
            {
                foreach (string key in obj.Keys)
                {
                    ScrubEmptyArrayStrings(obj[key]);
                }
                return;
            }
            JArr arr = node as JArr;
            if (arr == null)
            {
                return;
            }
            for (int i = arr.Items.Count - 1; i >= 0; i--)
            {
                JStr str = arr.Items[i] as JStr;
                if (str != null)
                {
                    if (string.IsNullOrWhiteSpace(str.Value))
                    {
                        arr.Items.RemoveAt(i);
                    }
                }
                else
                {
                    ScrubEmptyArrayStrings(arr.Items[i]);
                }
            }
        }

        // ── typed helpers over the DOM ──────────────────────────────

        internal string GetString(string key, string fallback)
        {
            JStr str = Root[key] as JStr;
            return str != null ? str.Value : fallback;
        }

        internal void SetString(string key, string value)
        {
            Root[key] = new JStr(value);
            Dirty = true;
        }

        internal void SetInt(string key, int value)
        {
            Root[key] = new JNum(value);
            Dirty = true;
        }

        internal void SetBool(string key, bool value)
        {
            Root[key] = new JBool(value);
            Dirty = true;
        }

        internal void SetDouble(string key, double value)
        {
            Root[key] = new JNum(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            Dirty = true;
        }

        internal int GetInt(string key, int fallback)
        {
            JNum num = Root[key] as JNum;
            return num != null ? num.AsInt() : fallback;
        }

        internal double GetDouble(string key, double fallback)
        {
            JNum num = Root[key] as JNum;
            return num != null ? num.AsDouble() : fallback;
        }

        internal bool GetBool(string key, bool fallback)
        {
            JBool b = Root[key] as JBool;
            return b != null ? b.Value : fallback;
        }

        /// <summary>Lines of a top-level list key ([] if absent/non-array).</summary>
        internal List<string> GetLines(string key)
        {
            return LinesOf(Root[key] as JArr);
        }

        internal static List<string> LinesOf(JArr arr)
        {
            List<string> lines = new List<string>();
            if (arr != null)
            {
                foreach (JNode item in arr.Items)
                {
                    JStr str = item as JStr;
                    if (str != null)
                    {
                        lines.Add(str.Value);
                    }
                }
            }
            return lines;
        }

        internal void SetLines(string key, IList<string> lines)
        {
            JArr arr = new JArr();
            foreach (string line in lines)
            {
                arr.Items.Add(new JStr(line));
            }
            Root[key] = arr;
            Dirty = true;
        }

        /// <summary>Nested section object (e.g. global's "ManagerPools"), or null.</summary>
        internal JObj GetSection(string key)
        {
            return Root[key] as JObj;
        }

        /// <summary>Lines of a list inside a nested section ([] if absent).</summary>
        internal List<string> GetLinesIn(string sectionKey, string listKey)
        {
            JObj section = GetSection(sectionKey);
            return LinesOf(section != null ? section[listKey] as JArr : null);
        }

        internal void SetLinesIn(string sectionKey, string listKey, IList<string> lines)
        {
            JObj section = GetSection(sectionKey);
            if (section == null)
            {
                section = new JObj();
                Root[sectionKey] = section;
            }
            JArr arr = new JArr();
            foreach (string line in lines)
            {
                arr.Items.Add(new JStr(line));
            }
            section[listKey] = arr;
            Dirty = true;
        }

        // ── zones.dialogue.json "Zones" array access ────────────────

        internal JObj FindZoneEntry(string scene, string display)
        {
            JArr zones = Root["Zones"] as JArr;
            if (zones == null) { return null; }
            foreach (JNode node in zones.Items)
            {
                JObj entry = node as JObj;
                if (entry == null) { continue; }
                JStr zone = entry["Zone"] as JStr;
                if (zone == null) { continue; }
                string name = zone.Value.Trim();
                if (name.Equals(scene, StringComparison.OrdinalIgnoreCase)
                    || name.Equals(display, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        internal List<string> GetZoneLines(string scene, string display)
        {
            JObj entry = FindZoneEntry(scene, display);
            return LinesOf(entry != null ? entry["Lines"] as JArr : null);
        }

        internal void SetZoneLines(string scene, string display, IList<string> lines)
        {
            JObj entry = FindZoneEntry(scene, display);
            if (entry == null)
            {
                JArr zones = Root["Zones"] as JArr;
                if (zones == null)
                {
                    zones = new JArr();
                    Root["Zones"] = zones;
                }
                entry = new JObj();
                entry["Zone"] = new JStr(scene); // scene names are the safe keys
                zones.Items.Add(entry);
            }
            JArr arr = new JArr();
            foreach (string line in lines)
            {
                arr.Items.Add(new JStr(line));
            }
            entry["Lines"] = arr;
            Dirty = true;
        }
    }

    internal sealed class SimFile : PackJsonFile
    {
        internal SimFile(string path) : base(path) { }

        internal SimFile(string path, JObj root) : base(path, root) { }

        internal string SimName { get { return GetString("Name", FileName.Replace(".sim.json", "")); } }
    }
}
