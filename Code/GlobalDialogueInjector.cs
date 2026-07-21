using System;
using System.Collections.Generic;
using System.Reflection;
using Erenshor.CustomSimFramework.Data;

namespace Erenshor.CustomSimFramework
{
    /// <summary>
    /// Appends pack-provided lines to the pools generic sims draw from:
    /// SimPlayerMngr's speech pools, the global SimPlayerLanguage lists, the
    /// generated-sim name databases, and the personality bio pools.
    /// All appends are deduplicated (exact string match) so re-running on a later
    /// login, or two packs sharing lines, never double-adds.
    /// </summary>
    internal static class GlobalDialogueInjector
    {
        private static bool _groupingApplied;
        private static bool _guildApplied;

        internal static void ResetPerLogin()
        {
            _groupingApplied = false;
            _guildApplied = false;
        }

        /// <summary>
        /// Appends GroupingPools additions to GameData.SimPlayerGrouping. That
        /// reference may not be assigned yet at SimPlayerMngr.Start prefix time,
        /// so this is attempted there and retried from the post-load poll.
        /// </summary>
        internal static void ApplyGroupingPools()
        {
            if (_groupingApplied || PackLoader.GlobalDialogues.Count == 0)
            {
                return;
            }
            SimPlayerGrouping grouping = GameData.SimPlayerGrouping;
            if (grouping == null)
            {
                return; // retried later
            }
            _groupingApplied = true;
            int added = 0;
            foreach (GlobalDialogueDefinition def in PackLoader.GlobalDialogues)
            {
                if (def.GroupingPools != null)
                {
                    added += AppendByFieldNames(def.GroupingPools, grouping);
                }
            }
            if (added > 0)
            {
                CustomSimFrameworkPlugin.Log.LogInfo("[Global] " + added + " group-chat line(s) appended to SimPlayerGrouping.");
            }
        }

        /// <summary>
        /// Appends GuildPools additions to GameData.GuildManager. The manager is
        /// assigned in its own Awake (before any Start runs), but null-guard and
        /// retry from the post-load poll anyway, mirroring the grouping pools.
        /// </summary>
        internal static void ApplyGuildPools()
        {
            if (_guildApplied || PackLoader.GlobalDialogues.Count == 0)
            {
                return;
            }
            GuildManager guildMngr = GameData.GuildManager;
            if (guildMngr == null)
            {
                return; // retried later
            }
            _guildApplied = true;
            int added = 0;
            foreach (GlobalDialogueDefinition def in PackLoader.GlobalDialogues)
            {
                if (def.GuildPools != null)
                {
                    added += AppendByFieldNames(def.GuildPools, guildMngr);
                }
            }
            if (added > 0)
            {
                CustomSimFrameworkPlugin.Log.LogInfo("[Global] " + added + " guild-chat line(s) appended to GuildManager.");
            }
        }

        internal static void Apply(SimPlayerMngr mngr)
        {
            if (PackLoader.GlobalDialogues.Count == 0)
            {
                return;
            }
            ApplyGroupingPools();
            ApplyGuildPools();
            SimPlayerLanguage globalLang = mngr.GetComponent<SimPlayerLanguage>();
            SimPlayerLanguage templateLang = (mngr.BlankSPTemplate != null)
                ? mngr.BlankSPTemplate.GetComponent<SimPlayerLanguage>() : null;

            foreach (GlobalDialogueDefinition def in PackLoader.GlobalDialogues)
            {
                int added = 0;
                try
                {
                    if (def.ManagerPools != null)
                    {
                        WarnDeadLists(def.ManagerPools, CategoryInfo.DeadManagerPools, def.SourceFile);
                        added += AppendByFieldNames(def.ManagerPools, mngr);
                    }
                    if (def.GlobalLanguage != null && globalLang != null)
                    {
                        WarnDeadLists(def.GlobalLanguage, CategoryInfo.DeadGlobalLanguageLists, def.SourceFile);
                        added += AppendByFieldNames(def.GlobalLanguage, globalLang);
                    }
                    // BlankSPTemplate mutation is precedented, vanilla's own
                    // LoadSPChat rewrites this component every login. We only
                    // touch lists LoadSPChat never refills, and dedup makes the
                    // per-login re-append a no-op on the shared prefab asset.
                    if (def.TemplateLanguage != null && templateLang != null)
                    {
                        added += AppendByFieldNames(def.TemplateLanguage, templateLang);
                    }
                    WarnNameCollisions(mngr, def);
                    added += AppendLines(mngr.NameDatabaseMale, def.MaleNames);
                    added += AppendLines(mngr.NameDatabaseFemale, def.FemaleNames);
                    added += AppendLines(mngr.NiceDesciptions, def.NiceBios);
                    added += AppendLines(mngr.TryhardDescriptions, def.TryhardBios);
                    added += AppendLines(mngr.MeanDescriptions, def.MeanBios);

                    CustomSimFrameworkPlugin.Log.LogInfo("[Global] " + def.SourceFile + ": " + added + " line(s) appended.");
                }
                catch (Exception ex)
                {
                    CustomSimFrameworkPlugin.Log.LogError("[Global] Failed applying " + def.SourceFile + ": " + ex);
                }
            }
        }

        /// <summary>
        /// For every populated List&lt;string&gt; field on the additions object, appends
        /// its lines to the identically named List&lt;string&gt; field on the target.
        /// </summary>
        private static int AppendByFieldNames(object additions, object target)
        {
            int added = 0;
            FieldInfo[] sourceFields = additions.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            Type targetType = target.GetType();

            foreach (FieldInfo sourceField in sourceFields)
            {
                if (sourceField.FieldType != typeof(List<string>))
                {
                    continue;
                }
                List<string> lines = (List<string>)sourceField.GetValue(additions);
                if (lines == null || lines.Count == 0)
                {
                    continue;
                }
                FieldInfo targetField = targetType.GetField(sourceField.Name, BindingFlags.Public | BindingFlags.Instance);
                if (targetField == null || targetField.FieldType != typeof(List<string>))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Global] No pool named '" + sourceField.Name
                        + "' on " + targetType.Name + " - lines ignored.");
                    continue;
                }
                List<string> pool = (List<string>)targetField.GetValue(target);
                if (pool == null)
                {
                    pool = new List<string>();
                    targetField.SetValue(target, pool);
                }
                added += AppendLines(pool, lines);
            }
            return added;
        }

        /// <summary>
        /// Warns when a pack populates a pool the game never reads (established
        /// in the decompile audit, see Packs/CATEGORY_REFERENCE.md). Lines are
        /// still appended in case a game update revives the pool.
        /// </summary>
        private static void WarnDeadLists(object additions, HashSet<string> deadNames, string sourceFile)
        {
            foreach (FieldInfo field in additions.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(List<string>) || !deadNames.Contains(field.Name))
                {
                    continue;
                }
                List<string> lines = (List<string>)field.GetValue(additions);
                if (lines != null && lines.Count > 0)
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Global] " + sourceFile + ": '" + field.Name
                        + "' is never read by the game - these lines will not appear."
                        + " See Packs/CATEGORY_REFERENCE.md for the live alternative.");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        // v0.8.2 name-collision warnings (warn-only, never remove).
        // Runs per def BEFORE its names are appended, so the checks see the
        // pre-append state: pack-internal duplicates are caught at parse
        // time, everything earlier (vanilla DBs + prior packs) here.
        // ────────────────────────────────────────────────────────────────

        // Once per app launch. Apply reruns every login against identical
        // data, and repeating the same warnings would just be noise.
        private static readonly HashSet<string> _nameWarningsIssued =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static void WarnNameCollisions(SimPlayerMngr mngr, GlobalDialogueDefinition def)
        {
            Dictionary<string, string> saves = ListSimSaveFiles();
            CheckNameAdditions(def.MaleNames, mngr.NameDatabaseFemale, "female", saves, def.SourceFile);
            CheckNameAdditions(def.FemaleNames, mngr.NameDatabaseMale, "male", saves, def.SourceFile);
        }

        private static void CheckNameAdditions(List<string> names, List<string> oppositeDb,
            string oppositeLabel, Dictionary<string, string> saves, string sourceFile)
        {
            if (names == null)
            {
                return;
            }
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || _nameWarningsIssued.Contains(name))
                {
                    continue;
                }
                // 1) Case-variant of a custom sim: the inject pass will strip
                // it from the pool anyway, so say that here rather than logging
                // two uncoordinated messages about one name.
                SimDefinition custom = PackLoader.FindSimByName(name);
                if (custom != null && !string.Equals(custom.Name, name, StringComparison.Ordinal))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Global] " + sourceFile + ": generated name \"" + name
                        + "\" is a case-variant of custom sim '" + custom.Name
                        + "' - it will be removed from the name pool.");
                    _nameWarningsIssued.Add(name);
                    continue;
                }
                // 2) Case-variant of an existing sim save file. An EXACT match
                // is the normal state for a previously-minted pack name and
                // must never warn. Only differing casings share a file on
                // Windows while the game treats them as two sims.
                string existing;
                if (saves != null && saves.TryGetValue(name, out existing)
                    && !string.Equals(existing, name, StringComparison.Ordinal))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Global] " + sourceFile + ": generated name \"" + name
                        + "\" is a case-variant of the existing sim save '" + existing
                        + "' - a sim minted with this name would share that save file"
                        + " (Windows filenames are case-insensitive).");
                    _nameWarningsIssued.Add(name);
                    continue;
                }
                // 3) Cross-gender database membership (vanilla + earlier packs).
                if (oppositeDb == null)
                {
                    continue;
                }
                foreach (string other in oppositeDb)
                {
                    if (!string.Equals(other, name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (string.Equals(other, name, StringComparison.Ordinal))
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Global] " + sourceFile + ": generated name \"" + name
                            + "\" is also in the " + oppositeLabel + " name database - sims with this name will"
                            + " ALWAYS resolve male (the game's male-list check wins).");
                    }
                    else
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Global] " + sourceFile + ": generated name \"" + name
                            + "\" has the case-variant \"" + other + "\" in the " + oppositeLabel
                            + " name database - two minted sims would share one save file.");
                    }
                    _nameWarningsIssued.Add(name);
                    break;
                }
            }
        }

        /// <summary>
        /// One directory listing per Apply (not per name): sim save names,
        /// case-insensitive key -> actual on-disk name.
        /// </summary>
        private static Dictionary<string, string> ListSimSaveFiles()
        {
            try
            {
                string dir = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "ESSaveData");
                if (!System.IO.Directory.Exists(dir))
                {
                    return null;
                }
                Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in System.IO.Directory.GetFiles(dir, "Sims*"))
                {
                    if (System.IO.Path.GetExtension(file) != "")
                    {
                        continue; // .tmp/.bak artifacts, same filter the game uses
                    }
                    string name = System.IO.Path.GetFileName(file).Substring("Sims".Length);
                    if (name.Length > 0 && !result.ContainsKey(name))
                    {
                        result[name] = name;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.LogDebug("Save-name scan failed: " + ex.Message);
                return null;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // v0.8.2 zone-target validation (warn-only, once per app launch).
        // ────────────────────────────────────────────────────────────────

        private static bool _zoneTargetsChecked;

        /// <summary>
        /// Warns about Zones entries matching no atlas zone (scene OR display
        /// name). Retries until ZoneAtlas.Atlas is available. Zone data is
        /// static, so once per app launch is enough.
        /// </summary>
        internal static void ValidateZoneTargets()
        {
            if (_zoneTargetsChecked || PackLoader.ZoneDialogues.Count == 0)
            {
                return;
            }
            ZoneAtlasEntry[] atlas = ZoneAtlas.Atlas;
            if (atlas == null)
            {
                return; // retried from the next zone entry / post-load pass
            }
            _zoneTargetsChecked = true;
            HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZoneAtlasEntry entry in atlas)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ZoneName))
                {
                    continue;
                }
                known.Add(entry.ZoneName);
                string display = GetCommonTerms.GetZoneTerm(entry.ZoneName);
                if (!string.IsNullOrEmpty(display))
                {
                    known.Add(display);
                }
            }
            foreach (ZoneDialogueDefinition def in PackLoader.ZoneDialogues)
            {
                if (def.Zones == null)
                {
                    continue;
                }
                foreach (ZoneLines entry in def.Zones)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Zone) || known.Contains(entry.Zone))
                    {
                        continue;
                    }
                    CustomSimFrameworkPlugin.Log.LogWarning("[Packs] " + def.SourceFile + ": zone \"" + entry.Zone
                        + "\" matches no zone in the game's atlas - check the spelling"
                        + " (special/event scenes may not be listed).");
                }
            }
        }

        internal static int AppendLines(List<string> pool, List<string> lines)
        {
            if (pool == null || lines == null)
            {
                return 0;
            }
            int added = 0;
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line) && !pool.Contains(line))
                {
                    pool.Add(line);
                    added++;
                }
            }
            return added;
        }
    }
}
