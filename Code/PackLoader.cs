using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using Erenshor.CustomSimFramework.Data;
using UnityEngine;

namespace Erenshor.CustomSimFramework
{
    /// <summary>
    /// Discovers and parses content packs from
    ///   BepInEx/plugins/CustomSimFramework/Packs/&lt;pack name&gt;/
    /// Each pack folder may contain:
    ///   *.sim.json            one custom sim per file
    ///   global.dialogue.json  additions to the generic-sim dialogue pools
    ///   zones.dialogue.json   additions to per-zone ambient chatter
    /// Parsing happens once at plugin Awake. Injection happens later, at
    /// SimPlayerMngr.Start and ZoneAnnounce.Start, using these parsed results.
    ///
    /// TWO-STAGE PARSING (v0.8.0): Unity's JsonUtility never populates fields
    /// whose type is a [Serializable] class from a runtime-loaded (BepInEx)
    /// assembly. Flat fields (strings, List&lt;string&gt;) parse fine, but nested
    /// sections silently stay empty. Proved this 2026-07-19 with controlled
    /// in-game probes (a nested-only file appended 0 lines, a flat-only file
    /// appended 2) and an IL trace down to the FromJsonInternal native call,
    /// which showed no managed filtering. So nested sections get extracted
    /// from the file text and each one is parsed as its own ROOT object.
    /// That is the flat case, which provably works.
    /// </summary>
    internal static class PackLoader
    {
        // Same shape vanilla sim names use. Also applied to generated-name
        // additions, since the game builds save filenames and whisper targets
        // from names, and whisper target parsing stops at the first space.
        private static readonly Regex NamePattern = new Regex("^[A-Za-z0-9-]+$", RegexOptions.Compiled);

        internal static readonly List<SimDefinition> Sims = new List<SimDefinition>();
        internal static readonly List<GlobalDialogueDefinition> GlobalDialogues = new List<GlobalDialogueDefinition>();
        internal static readonly List<ZoneDialogueDefinition> ZoneDialogues = new List<ZoneDialogueDefinition>();
        internal static readonly List<TopicsDefinition> TopicPacks = new List<TopicsDefinition>();

        // Packs live under BepInEx\config, the location mod managers treat
        // as user data and preserve across updates, disables and reinstalls
        // (and include in profile exports).
        internal static string PacksRoot
        {
            get { return Path.Combine(Path.Combine(Paths.ConfigPath, "CustomSimFramework"), "Packs"); }
        }

        /// <summary>
        /// First-run seed of the bundled example pack (v0.9.2). The pack is
        /// EMBEDDED IN THE DLL (resources named "ShippedPack::&lt;pack&gt;::&lt;file&gt;",
        /// see the csproj) and extracted into PacksRoot on first run. Why not
        /// just ship it as folders in the zip? Mod managers FLATTEN any
        /// unrecognized zip subfolders into plugins/&lt;Author&gt;-CustomSimFramework/.
        /// Tested this with r2modman on 2026-07-20 and the Packs/Paul tree
        /// really did arrive as loose files. That managed folder also gets
        /// wiped on every update. PacksRoot lives OUTSIDE it precisely so
        /// users' packs survive updates. A pack folder that already exists is
        /// never touched, user edits win. A reworked shipped example gets
        /// picked up by deleting the local copy.
        /// </summary>
        private const string ShippedPackPrefix = "ShippedPack::";

        private static void SeedShippedPacks()
        {
            try
            {
                Assembly assembly = typeof(PackLoader).Assembly;
                HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string resource in assembly.GetManifestResourceNames())
                {
                    if (!resource.StartsWith(ShippedPackPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string[] parts = resource.Substring(ShippedPackPrefix.Length)
                        .Split(new[] { "::" }, StringSplitOptions.None);
                    if (parts.Length != 2)
                    {
                        continue;
                    }
                    string packName = parts[0];
                    string fileName = parts[1];
                    if (existing.Contains(packName))
                    {
                        continue;
                    }
                    string packDir = Path.Combine(PacksRoot, packName);
                    if (!created.Contains(packName))
                    {
                        if (Directory.Exists(packDir))
                        {
                            existing.Add(packName); // user's copy - never touch it
                            continue;
                        }
                        Directory.CreateDirectory(packDir);
                        created.Add(packName);
                        CustomSimFrameworkPlugin.Log.LogInfo("[Packs] Installed the bundled '" + packName
                            + "' example pack to " + packDir + " (first run; your packs live here,"
                            + " outside the mod manager's folder, so mod updates never touch them).");
                    }
                    try
                    {
                        using (Stream source = assembly.GetManifestResourceStream(resource))
                        using (FileStream dest = new FileStream(Path.Combine(packDir, fileName),
                            FileMode.Create, FileAccess.Write))
                        {
                            source.CopyTo(dest);
                        }
                    }
                    catch (Exception ex)
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Packs] Could not extract '" + fileName
                            + "' of bundled pack '" + packName + "': " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.LogDebug("Pack seeding skipped: " + ex.Message);
            }
        }

        internal static void LoadAll()
        {
            Sims.Clear();
            GlobalDialogues.Clear();
            ZoneDialogues.Clear();
            TopicPacks.Clear();

            RunJsonSelfTest();
            SeedShippedPacks();

            if (!Directory.Exists(PacksRoot))
            {
                Directory.CreateDirectory(PacksRoot);
                CustomSimFrameworkPlugin.Log.LogInfo("[Packs] No Packs folder found - created empty one at " + PacksRoot);
                return;
            }

            string[] packDirs = Directory.GetDirectories(PacksRoot);
            foreach (string packDir in packDirs)
            {
                string packName = Path.GetFileName(packDir);
                try
                {
                    LoadPack(packDir, packName);
                }
                catch (Exception ex)
                {
                    CustomSimFrameworkPlugin.Log.LogError("[Packs] Failed loading pack '" + packName + "': " + ex);
                }
            }

            CustomSimFrameworkPlugin.Log.LogInfo("[Packs] Loaded " + packDirs.Length + " pack(s): "
                + Sims.Count + " sim(s), "
                + GlobalDialogues.Count + " global dialogue file(s), "
                + ZoneDialogues.Count + " zone dialogue file(s), "
                + TopicPacks.Count + " topic file(s).");
        }

        private static void LoadPack(string packDir, string packName)
        {
            foreach (string simFile in Directory.GetFiles(packDir, "*.sim.json"))
            {
                SimDefinition def = ParseJson<SimDefinition>(simFile);
                if (def == null)
                {
                    continue;
                }
                def.SourceFile = packName + "/" + Path.GetFileName(simFile);
                if (string.IsNullOrEmpty(def.Name) || def.Name.Trim().Length == 0)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + def.SourceFile + " has no Name - skipped.");
                    continue;
                }
                def.Name = def.Name.Trim();
                // The game builds save paths as "Sims" + name and targets whispers by
                // name token, so restrict to filename- and chat-safe characters.
                // Letters, digits and hyphens, the same shape vanilla sim names use.
                if (!NamePattern.IsMatch(def.Name))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] Sim name '" + def.Name + "' in " + def.SourceFile
                        + " contains unsupported characters (allowed: A-Z, a-z, 0-9, hyphen; no spaces) - skipped.");
                    continue;
                }
                if (FindSimByName(def.Name) != null)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] Duplicate sim name '" + def.Name
                        + "' in " + def.SourceFile + " - skipped (first definition wins).");
                    continue;
                }
                if (def.TiedToSlot != -1 && def.Rival)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + def.SourceFile
                        + ": TiedToSlot is ignored for Rival sims (the game forces rivals to 99).");
                    def.TiedToSlot = -1;
                }
                // Only vanilla-legal values may ever reach a save file: the game
                // dereferences GameData.SaveSlots[TiedToSlot] for 0-10 (12 slots
                // always exist) and treats 12/99 specially. Anything else could
                // break vanilla's own login loader.
                if (def.TiedToSlot != -1 && (def.TiedToSlot < 0 || def.TiedToSlot > 10)
                    && def.TiedToSlot != 12 && def.TiedToSlot != 99)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + def.SourceFile
                        + ": TiedToSlot=" + def.TiedToSlot + " is not valid (-1, 0-10, 12 or 99) - using -1.");
                    def.TiedToSlot = -1;
                }
                // v0.9.1: Gender resolution is a case-insensitive "Female"
                // comparison. Anything else silently produced a MALE sim with
                // no log line (unlike Class, which warns). Trim + warn.
                def.Gender = (def.Gender ?? "").Trim();
                if (!string.Equals(def.Gender, "Male", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(def.Gender, "Female", StringComparison.OrdinalIgnoreCase))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + def.SourceFile
                        + ": Gender '" + def.Gender + "' is not 'Male' or 'Female' - defaulting to Male.");
                }
                def.RefersToSelfAs = (def.RefersToSelfAs ?? "").Trim();
                Sims.Add(def);
                CustomSimFrameworkPlugin.Log.LogInfo("[Packs] Sim '" + def.Name + "' loaded from " + def.SourceFile);
            }

            string globalFile = Path.Combine(packDir, "global.dialogue.json");
            if (File.Exists(globalFile))
            {
                GlobalDialogueDefinition def = ParseGlobalDialogue(globalFile);
                if (def != null)
                {
                    def.SourceFile = packName + "/global.dialogue.json";
                    GlobalDialogues.Add(def);
                }
            }

            string zonesFile = Path.Combine(packDir, "zones.dialogue.json");
            if (File.Exists(zonesFile))
            {
                ZoneDialogueDefinition def = ParseZoneDialogue(zonesFile);
                if (def != null)
                {
                    def.SourceFile = packName + "/zones.dialogue.json";
                    ZoneDialogues.Add(def);
                }
            }

            string topicsFile = Path.Combine(packDir, "topics.dialogue.json");
            if (File.Exists(topicsFile))
            {
                TopicsDefinition def = ParseTopics(topicsFile);
                if (def != null)
                {
                    def.SourceFile = packName + "/topics.dialogue.json";
                    TopicPacks.Add(def);
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Guild topic conversations (v0.9.0).
        // ────────────────────────────────────────────────────────────────

        private static TopicsDefinition ParseTopics(string path)
        {
            string display = Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileName(path);
            try
            {
                string json = File.ReadAllText(path);
                TopicsDefinition def = JsonUtility.FromJson<TopicsDefinition>(json);
                if (def == null)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + path + " parsed to null (malformed JSON?) - skipped.");
                    return null;
                }
                // Topics is a list of nested objects. Parse each element as its
                // own root TopicDefinition (flat fields, which provably parse).
                string text;
                if (TopLevelJsonSections(json).TryGetValue("Topics", out text))
                {
                    def.Topics = new List<TopicDefinition>();
                    foreach (string element in TopLevelArrayElements(text))
                    {
                        TopicDefinition entry = ParseSection<TopicDefinition>(element, "Topics entry", display);
                        if (entry != null)
                        {
                            CheckKeysAtLevel(element, typeof(TopicDefinition), "a Topics entry", display);
                            def.Topics.Add(entry);
                        }
                    }
                }
                CheckKeysAtLevel(json, typeof(TopicsDefinition), "the top level of topics.dialogue.json", display);
                CleanStringLists(def, path);
                ValidateTopics(def, display);
                int topicCount = (def.Topics != null) ? def.Topics.Count : 0;
                int lineCount = 0;
                if (def.Topics != null)
                {
                    foreach (TopicDefinition t in def.Topics)
                    {
                        lineCount += CountLines(t);
                    }
                }
                CustomSimFrameworkPlugin.Log.LogInfo("[Packs] " + display + " parsed: "
                    + topicCount + " topic(s) with " + lineCount + " line(s).");
                return def;
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Packs] Could not parse " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Topic validation. The crash-preventing rules here are MANDATORY, see
        /// the design audit. Responses is required because the keyword reply
        /// path indexes it unguarded (GuildManager.cs:810). A topic with no
        /// openers AND no activation words is unreachable. And activation words
        /// with edge punctuation can never word-boundary match.
        /// </summary>
        private static void ValidateTopics(TopicsDefinition def, string displayName)
        {
            if (def.Topics == null)
            {
                return;
            }
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = def.Topics.Count - 1; i >= 0; i--)
            {
                TopicDefinition topic = def.Topics[i];
                topic.Name = (topic.Name ?? "").Trim();
                if (topic.Name.Length == 0)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName
                        + ": a Topics entry has no Name - skipped.");
                    def.Topics.RemoveAt(i);
                    continue;
                }
                if (topic.Responses == null || topic.Responses.Count == 0)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": topic '" + topic.Name
                        + "' has no Responses - skipped (a matched topic without responses would crash the game).");
                    def.Topics.RemoveAt(i);
                    continue;
                }
                bool noOpeners = topic.SimPlayerActivations == null || topic.SimPlayerActivations.Count == 0;
                bool noTriggers = topic.ActivationWords == null || topic.ActivationWords.Count == 0;
                if (noOpeners && noTriggers)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": topic '" + topic.Name
                        + "' has neither SimPlayerActivations nor ActivationWords - unreachable, skipped.");
                    def.Topics.RemoveAt(i);
                    continue;
                }
                if (!names.Add(topic.Name))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": duplicate topic name '"
                        + topic.Name + "' - both are injected, but logs will be ambiguous.");
                }
                if (topic.Responses.Count < 3)
                {
                    CustomSimFrameworkPlugin.Log.LogInfo("[Packs] " + displayName + ": topic '" + topic.Name
                        + "' has only " + topic.Responses.Count + " response(s) - conversations will repeat"
                        + " (and the game's dedup loop idles); 3+ recommended.");
                }
                if (topic.ActivationWords != null)
                {
                    foreach (string phrase in topic.ActivationWords)
                    {
                        foreach (string word in phrase.Split(' '))
                        {
                            if (word.Length > 0 && (!char.IsLetterOrDigit(word[0])
                                || !char.IsLetterOrDigit(word[word.Length - 1])))
                            {
                                CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": topic '"
                                    + topic.Name + "' trigger \"" + phrase + "\" contains a punctuation-edged"
                                    + " word (\"" + word + "\") - the game's word-boundary matching can never"
                                    + " match it (write \"join discord\", not \"join discord?\").");
                                break;
                            }
                        }
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Two-stage parsers (see class summary for why these exist).
        // ────────────────────────────────────────────────────────────────

        private static GlobalDialogueDefinition ParseGlobalDialogue(string path)
        {
            // Pack-qualified name for logs: two packs can both ship a
            // "global.dialogue.json", so the bare filename is ambiguous.
            string display = Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileName(path);
            try
            {
                string json = File.ReadAllText(path);
                // Stage 1: flat fields (MaleNames/FemaleNames/bios) parse normally.
                GlobalDialogueDefinition def = JsonUtility.FromJson<GlobalDialogueDefinition>(json);
                if (def == null)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + path + " parsed to null (malformed JSON?) - skipped.");
                    return null;
                }
                // Stage 2: each nested section parsed as its own root object.
                Dictionary<string, string> sections = TopLevelJsonSections(json);
                string text;
                if (sections.TryGetValue("ManagerPools", out text))
                {
                    def.ManagerPools = ParseSection<ManagerPoolAdditions>(text, "ManagerPools", display);
                }
                if (sections.TryGetValue("GlobalLanguage", out text))
                {
                    def.GlobalLanguage = ParseSection<GlobalLanguageAdditions>(text, "GlobalLanguage", display);
                }
                if (sections.TryGetValue("GroupingPools", out text))
                {
                    def.GroupingPools = ParseSection<GroupingPoolAdditions>(text, "GroupingPools", display);
                }
                if (sections.TryGetValue("GuildPools", out text))
                {
                    def.GuildPools = ParseSection<GuildPoolAdditions>(text, "GuildPools", display);
                }
                if (sections.TryGetValue("TemplateLanguage", out text))
                {
                    def.TemplateLanguage = ParseSection<TemplateLanguageAdditions>(text, "TemplateLanguage", display);
                }

                CheckKeysAtLevel(json, typeof(GlobalDialogueDefinition), LevelLabelFor(typeof(GlobalDialogueDefinition)), display);
                if (sections.TryGetValue("ManagerPools", out text))
                {
                    CheckKeysAtLevel(text, typeof(ManagerPoolAdditions), "ManagerPools", display);
                }
                if (sections.TryGetValue("GlobalLanguage", out text))
                {
                    CheckKeysAtLevel(text, typeof(GlobalLanguageAdditions), "GlobalLanguage", display);
                }
                if (sections.TryGetValue("GroupingPools", out text))
                {
                    CheckKeysAtLevel(text, typeof(GroupingPoolAdditions), "GroupingPools", display);
                }
                if (sections.TryGetValue("GuildPools", out text))
                {
                    CheckKeysAtLevel(text, typeof(GuildPoolAdditions), "GuildPools", display);
                }
                if (sections.TryGetValue("TemplateLanguage", out text))
                {
                    CheckKeysAtLevel(text, typeof(TemplateLanguageAdditions), "TemplateLanguage", display);
                }
                CleanStringLists(def, path);
                ValidateNameAdditions(def, display);
                WarnIntraPackGenderDuplicates(def, display);
                WarnEdgePunctuationInListenPools(def.ManagerPools, display);
                CustomSimFrameworkPlugin.Log.LogInfo("[Packs] " + display + " parsed:"
                    + " ManagerPools=" + CountLines(def.ManagerPools)
                    + ", GlobalLanguage=" + CountLines(def.GlobalLanguage)
                    + ", GroupingPools=" + CountLines(def.GroupingPools)
                    + ", GuildPools=" + CountLines(def.GuildPools)
                    + ", TemplateLanguage=" + CountLines(def.TemplateLanguage)
                    + ", Names=" + (ListCount(def.MaleNames) + ListCount(def.FemaleNames))
                    + ", Bios=" + (ListCount(def.NiceBios) + ListCount(def.TryhardBios) + ListCount(def.MeanBios))
                    + " line(s).");
                return def;
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Packs] Could not parse " + path + ": " + ex.Message);
                return null;
            }
        }

        private static ZoneDialogueDefinition ParseZoneDialogue(string path)
        {
            string display = Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileName(path);
            try
            {
                string json = File.ReadAllText(path);
                // Stage 1: AllZones (flat) parses normally.
                ZoneDialogueDefinition def = JsonUtility.FromJson<ZoneDialogueDefinition>(json);
                if (def == null)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + path + " parsed to null (malformed JSON?) - skipped.");
                    return null;
                }
                // Stage 2: Zones is a list of nested objects. Parse each
                // element as its own root ZoneLines (flat: string + list).
                string text;
                if (TopLevelJsonSections(json).TryGetValue("Zones", out text))
                {
                    def.Zones = new List<ZoneLines>();
                    foreach (string element in TopLevelArrayElements(text))
                    {
                        CheckKeysAtLevel(element, typeof(ZoneLines), "a Zones entry", display);
                        ZoneLines entry = ParseSection<ZoneLines>(element, "Zones entry", display);
                        if (entry != null)
                        {
                            // Edge whitespace would break the exact zone-name
                            // match at injection time (v0.8.2).
                            entry.Zone = (entry.Zone ?? "").Trim();
                            def.Zones.Add(entry);
                        }
                    }
                }

                CheckKeysAtLevel(json, typeof(ZoneDialogueDefinition), LevelLabelFor(typeof(ZoneDialogueDefinition)), display);
                CleanStringLists(def, path);
                int zoneLines = 0;
                if (def.Zones != null)
                {
                    foreach (ZoneLines entry in def.Zones)
                    {
                        zoneLines += (entry != null) ? ListCount(entry.Lines) : 0;
                    }
                }
                CustomSimFrameworkPlugin.Log.LogInfo("[Packs] " + display + " parsed:"
                    + " AllZones=" + ListCount(def.AllZones)
                    + ", Zones=" + ((def.Zones != null) ? def.Zones.Count : 0)
                    + " zone entr(y/ies) with " + zoneLines + " line(s).");
                return def;
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Packs] Could not parse " + path + ": " + ex.Message);
                return null;
            }
        }

        private static T ParseSection<T>(string sectionJson, string sectionName, string displayName) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(sectionJson);
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": section '"
                    + sectionName + "' could not be parsed (" + ex.Message + ") - section skipped.");
                return null;
            }
        }

        /// <summary>Sum of all List&lt;string&gt; entries on a section object (0 if null).</summary>
        private static int CountLines(object section)
        {
            if (section == null)
            {
                return 0;
            }
            int count = 0;
            foreach (FieldInfo field in section.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(List<string>))
                {
                    count += ListCount((List<string>)field.GetValue(section));
                }
            }
            return count;
        }

        private static int ListCount(List<string> list)
        {
            return (list != null) ? list.Count : 0;
        }

        /// <summary>
        /// Startup guard: verifies at every launch that this environment can
        /// parse a section type as a root object (the mechanism the two-stage
        /// parser relies on), and reports if the engine ever starts handling
        /// nested plugin types (an engine change worth knowing about).
        /// </summary>
        private static void RunJsonSelfTest()
        {
            ManagerPoolAdditions probe = null;
            try
            {
                probe = JsonUtility.FromJson<ManagerPoolAdditions>("{\"GroupRequest\":[\"selftest\"]}");
            }
            catch (Exception)
            {
            }
            if (probe == null || probe.GroupRequest == null || probe.GroupRequest.Count != 1)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Packs] JSON self-test FAILED - this environment cannot parse"
                    + " dialogue sections at all; global/zone dialogue additions will not work this session.");
                return;
            }
            GlobalDialogueDefinition nested = null;
            try
            {
                nested = JsonUtility.FromJson<GlobalDialogueDefinition>("{\"ManagerPools\":{\"GroupRequest\":[\"selftest\"]}}");
            }
            catch (Exception)
            {
            }
            if (nested != null && nested.ManagerPools != null && nested.ManagerPools.GroupRequest != null
                && nested.ManagerPools.GroupRequest.Count == 1)
            {
                CustomSimFrameworkPlugin.Log.LogInfo("[Packs] Note: the engine now parses nested plugin-class JSON"
                    + " (engine change?). Two-stage parsing remains in use and is unaffected.");
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Minimal JSON span scanner: string- and escape-aware extraction of
        // top-level key values / array elements. Only used on files that
        // already passed JsonUtility's own (strict) full-file parse.
        // ────────────────────────────────────────────────────────────────

        /// <summary>Raw text span of every top-level key's value in a JSON object.</summary>
        private static Dictionary<string, string> TopLevelJsonSections(string json)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            int i = 0;
            int n = json.Length;
            while (i < n && json[i] != '{')
            {
                i++;
            }
            if (i >= n)
            {
                return result;
            }
            i++; // past the root '{'
            while (i < n)
            {
                while (i < n && (char.IsWhiteSpace(json[i]) || json[i] == ','))
                {
                    i++;
                }
                if (i >= n || json[i] == '}')
                {
                    break;
                }
                if (json[i] != '"')
                {
                    break; // unexpected token - stop scanning defensively
                }
                i++;
                System.Text.StringBuilder key = new System.Text.StringBuilder();
                while (i < n && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < n)
                    {
                        key.Append(json[i + 1]);
                        i += 2;
                        continue;
                    }
                    key.Append(json[i]);
                    i++;
                }
                i++; // past the closing quote
                while (i < n && char.IsWhiteSpace(json[i]))
                {
                    i++;
                }
                if (i >= n || json[i] != ':')
                {
                    break;
                }
                i++;
                while (i < n && char.IsWhiteSpace(json[i]))
                {
                    i++;
                }
                if (i >= n)
                {
                    break;
                }
                int valueStart = i;
                SkipJsonValue(json, ref i);
                result[key.ToString()] = json.Substring(valueStart, i - valueStart);
            }
            return result;
        }

        /// <summary>Raw text of each element in a JSON array.</summary>
        private static List<string> TopLevelArrayElements(string arrayJson)
        {
            List<string> elements = new List<string>();
            int i = 0;
            int n = arrayJson.Length;
            while (i < n && arrayJson[i] != '[')
            {
                i++;
            }
            if (i >= n)
            {
                return elements;
            }
            i++; // past the '['
            while (i < n)
            {
                while (i < n && (char.IsWhiteSpace(arrayJson[i]) || arrayJson[i] == ','))
                {
                    i++;
                }
                if (i >= n || arrayJson[i] == ']')
                {
                    break;
                }
                int start = i;
                SkipJsonValue(arrayJson, ref i);
                elements.Add(arrayJson.Substring(start, i - start));
            }
            return elements;
        }

        /// <summary>Advances i past one JSON value (object, array, string or literal).</summary>
        private static void SkipJsonValue(string json, ref int i)
        {
            int n = json.Length;
            char first = json[i];
            if (first == '{' || first == '[')
            {
                int depth = 0;
                bool inString = false;
                while (i < n)
                {
                    char c = json[i];
                    if (inString)
                    {
                        if (c == '\\')
                        {
                            i += 2;
                            continue;
                        }
                        if (c == '"')
                        {
                            inString = false;
                        }
                    }
                    else if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == '{' || c == '[')
                    {
                        depth++;
                    }
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            i++;
                            return;
                        }
                    }
                    i++;
                }
            }
            else if (first == '"')
            {
                i++;
                while (i < n)
                {
                    char c = json[i];
                    if (c == '\\')
                    {
                        i += 2;
                        continue;
                    }
                    i++;
                    if (c == '"')
                    {
                        return;
                    }
                }
            }
            else
            {
                while (i < n && json[i] != ',' && json[i] != '}' && json[i] != ']')
                {
                    i++;
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Pack-content validation (v0.8.0 hardening).
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generated-name additions must be filename- and whisper-safe like sim
        /// names (the game saves "Sims&lt;name&gt;" files and parses whisper targets
        /// up to the first space). Invalid entries are removed with a warning.
        /// </summary>
        private static void ValidateNameAdditions(GlobalDialogueDefinition def, string displayName)
        {
            int removed = RemoveInvalidNames(def.MaleNames) + RemoveInvalidNames(def.FemaleNames);
            if (removed > 0)
            {
                CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": removed " + removed
                    + " generated-name entr(y/ies) with unsupported characters (allowed: A-Z, a-z, 0-9, hyphen;"
                    + " no spaces - names become save filenames and whisper targets).");
            }
        }

        private static int RemoveInvalidNames(List<string> names)
        {
            if (names == null)
            {
                return 0;
            }
            return names.RemoveAll(delegate(string name) { return !NamePattern.IsMatch(name); });
        }

        /// <summary>
        /// The game matches LISTEN pools with \b-bounded regex, so a line that
        /// starts or ends with punctuation can never match at message edges.
        /// "wanna duo?" typed as a whole message never fires. Warn only, the
        /// line still joins the pool.
        /// </summary>
        private static void WarnEdgePunctuationInListenPools(ManagerPoolAdditions pools, string displayName)
        {
            if (pools == null)
            {
                return;
            }
            foreach (FieldInfo field in typeof(ManagerPoolAdditions).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(List<string>) || !CategoryInfo.ListenManagerPools.Contains(field.Name))
                {
                    continue;
                }
                List<string> lines = (List<string>)field.GetValue(pools);
                if (lines == null)
                {
                    continue;
                }
                foreach (string line in lines)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    if (!char.IsLetterOrDigit(line[0]) || !char.IsLetterOrDigit(line[line.Length - 1]))
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": listen line \""
                            + line + "\" in ManagerPools." + field.Name + " starts/ends with punctuation - the game's"
                            + " word-boundary matching can never match it at message edges (write \"wanna duo\", not"
                            + " \"wanna duo?\").");
                    }
                }
            }
        }

        internal static SimDefinition FindSimByName(string name)
        {
            foreach (SimDefinition def in Sims)
            {
                if (string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return def;
                }
            }
            return null;
        }

        private static T ParseJson<T>(string path) where T : class
        {
            try
            {
                string json = File.ReadAllText(path);
                T result = JsonUtility.FromJson<T>(json);
                if (result == null)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + path + " parsed to null (malformed JSON?) - skipped.");
                }
                else
                {
                    CheckKeysAtLevel(json, typeof(T), LevelLabelFor(typeof(T)), Path.GetFileName(path));
                    CleanStringLists(result, path);
                }
                return result;
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Packs] Could not parse " + path + ": " + ex.Message);
                return null;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Level-aware key checking (v0.8.2). JsonUtility silently ignores
        // unknown keys, and a SCHEMA key at the wrong nesting level is just
        // as silently dead (say, a top-level "Goodnight" in
        // global.dialogue.json). Same failure class as the original
        // nested-parse bug. So every JSON level gets checked against only
        // ITS schema type, structurally via the span scanner so string
        // values can never false-positive. A key that is valid somewhere
        // else gets a warning listing every place it would actually work.
        // ────────────────────────────────────────────────────────────────

        private static Dictionary<string, List<string>> _schemaLocations;

        /// <summary>Every schema field name -> the level(s) where it is valid.</summary>
        private static Dictionary<string, List<string>> SchemaLocations
        {
            get
            {
                if (_schemaLocations == null)
                {
                    _schemaLocations = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    AddSchemaLocation(typeof(SimDefinition), "a *.sim.json file");
                    AddSchemaLocation(typeof(GlobalDialogueDefinition), "the top level of global.dialogue.json");
                    AddSchemaLocation(typeof(ManagerPoolAdditions), "ManagerPools");
                    AddSchemaLocation(typeof(GlobalLanguageAdditions), "GlobalLanguage");
                    AddSchemaLocation(typeof(GroupingPoolAdditions), "GroupingPools");
                    AddSchemaLocation(typeof(GuildPoolAdditions), "GuildPools");
                    AddSchemaLocation(typeof(TemplateLanguageAdditions), "TemplateLanguage");
                    AddSchemaLocation(typeof(ZoneDialogueDefinition), "the top level of zones.dialogue.json");
                    AddSchemaLocation(typeof(ZoneLines), "a Zones entry");
                    AddSchemaLocation(typeof(TopicsDefinition), "the top level of topics.dialogue.json");
                    AddSchemaLocation(typeof(TopicDefinition), "a Topics entry");
                }
                return _schemaLocations;
            }
        }

        private static void AddSchemaLocation(Type type, string label)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // [NonSerialized] fields (SourceFile, RolledBioIndex) are not
                // JSON keys. Treating them as valid would let an author write
                // them silently dead (v0.9.1).
                if (field.IsNotSerialized)
                {
                    continue;
                }
                List<string> locations;
                if (!_schemaLocations.TryGetValue(field.Name, out locations))
                {
                    locations = new List<string>();
                    _schemaLocations[field.Name] = locations;
                }
                if (!locations.Contains(label))
                {
                    locations.Add(label);
                }
            }
        }

        private static string LevelLabelFor(Type type)
        {
            if (type == typeof(SimDefinition))
            {
                return "a *.sim.json file";
            }
            if (type == typeof(GlobalDialogueDefinition))
            {
                return "the top level of global.dialogue.json";
            }
            if (type == typeof(ZoneDialogueDefinition))
            {
                return "the top level of zones.dialogue.json";
            }
            if (type == typeof(TopicsDefinition))
            {
                return "the top level of topics.dialogue.json";
            }
            return type.Name;
        }

        /// <summary>Checks one JSON object's keys against the schema type of that level.</summary>
        private static void CheckKeysAtLevel(string jsonObjectText, Type levelType, string levelLabel, string displayName)
        {
            foreach (string key in TopLevelJsonSections(jsonObjectText).Keys)
            {
                // Keys starting with "_" are the documented comment convention.
                if (key.StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }
                FieldInfo levelField = levelType.GetField(key, BindingFlags.Public | BindingFlags.Instance);
                if (levelField != null && !levelField.IsNotSerialized)
                {
                    continue; // valid here
                }
                string casing = FindFieldCaseInsensitive(levelType, key);
                if (casing != null)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": unknown key \"" + key
                        + "\" is ignored by the game - did you mean \"" + casing + "\"?");
                    continue;
                }
                List<string> locations;
                if (SchemaLocations.TryGetValue(key, out locations))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": key \"" + key
                        + "\" is not valid in " + levelLabel + " - its content is silently dead there."
                        + " Valid location(s): " + string.Join(", ", locations.ToArray()) + ".");
                    continue;
                }
                CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": unknown key \"" + key
                    + "\" is ignored by the game (typo? see Packs/README.md for valid keys).");
            }
        }

        private static string FindFieldCaseInsensitive(Type type, string key)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!field.IsNotSerialized
                    && string.Equals(field.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return field.Name;
                }
            }
            return null;
        }

        /// <summary>
        /// v0.8.2: a name listed in BOTH gender lists of one pack always
        /// resolves male in-game (the game's male-database check runs last),
        /// and a case-variant pair would mint two sims sharing one save file
        /// (Windows filenames are case-insensitive). Warn-only, since choosing
        /// a list is the author's call. Cross-pack and vanilla-database
        /// collisions are checked at apply time by the injector.
        /// </summary>
        private static void WarnIntraPackGenderDuplicates(GlobalDialogueDefinition def, string displayName)
        {
            // v0.9.0: case-variant pairs WITHIN one list carry the same
            // shared-save-file hazard as cross-list pairs (two minted sims,
            // one NTFS file). Wasn't checked before this.
            WarnSameListCaseVariants(def.MaleNames, "MaleNames", displayName);
            WarnSameListCaseVariants(def.FemaleNames, "FemaleNames", displayName);
            if (def.MaleNames == null || def.FemaleNames == null)
            {
                return;
            }
            foreach (string male in def.MaleNames)
            {
                foreach (string female in def.FemaleNames)
                {
                    if (!string.Equals(male, female, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (string.Equals(male, female, StringComparison.Ordinal))
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": \"" + male
                            + "\" is in both MaleNames and FemaleNames - sims with this name will ALWAYS"
                            + " resolve male (the game's male-list check wins).");
                    }
                    else
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": MaleNames \"" + male
                            + "\" and FemaleNames \"" + female + "\" differ only by casing - two minted sims"
                            + " would share one save file (Windows filenames are case-insensitive).");
                    }
                }
            }
        }

        private static void WarnSameListCaseVariants(List<string> names, string listName, string displayName)
        {
            if (names == null)
            {
                return;
            }
            for (int i = 0; i < names.Count; i++)
            {
                for (int j = i + 1; j < names.Count; j++)
                {
                    if (string.Equals(names[i], names[j], StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(names[i], names[j], StringComparison.Ordinal))
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + displayName + ": " + listName
                            + " entries \"" + names[i] + "\" and \"" + names[j] + "\" differ only by casing"
                            + " - two minted sims would share one save file (Windows filenames are"
                            + " case-insensitive).");
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Empty-line scrub: an empty ("") entry in any dialogue list would
        // enter the game's rotation and render as "lol" (PersonalizeString's
        // empty-input fallback). Strip them everywhere with one warning.
        // ────────────────────────────────────────────────────────────────

        private static void CleanStringLists(object obj, string path)
        {
            int removed = RemoveEmptyEntries(obj, 0);
            if (removed > 0)
            {
                CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + Path.GetFileName(path) + ": removed "
                    + removed + " empty line(s) from dialogue lists (an empty line would make sims say \"lol\").");
            }
        }

        private static int RemoveEmptyEntries(object obj, int depth)
        {
            if (obj == null || depth > 3)
            {
                return 0;
            }
            int removed = 0;
            foreach (FieldInfo field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(obj);
                if (value == null)
                {
                    continue;
                }
                List<string> lines = value as List<string>;
                if (lines != null)
                {
                    // Trim first: edge whitespace breaks the game's \b-bounded
                    // listen matching and pads composed lines.
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i] != null)
                        {
                            lines[i] = lines[i].Trim();
                        }
                    }
                    removed += lines.RemoveAll(string.IsNullOrWhiteSpace);
                    continue;
                }
                // Recurse into nested schema objects and lists of them
                // (ManagerPools, GuildPools, Zones entries, ...).
                Type fieldType = field.FieldType;
                if (fieldType.Namespace != null && fieldType.Namespace.StartsWith("Erenshor.CustomSimFramework"))
                {
                    removed += RemoveEmptyEntries(value, depth + 1);
                }
                else if (value is System.Collections.IEnumerable && fieldType.IsGenericType
                    && fieldType.GetGenericTypeDefinition() == typeof(List<>)
                    && fieldType.GetGenericArguments()[0].Namespace != null
                    && fieldType.GetGenericArguments()[0].Namespace.StartsWith("Erenshor.CustomSimFramework"))
                {
                    foreach (object element in (System.Collections.IEnumerable)value)
                    {
                        removed += RemoveEmptyEntries(element, depth + 1);
                    }
                }
            }
            return removed;
        }
    }
}
