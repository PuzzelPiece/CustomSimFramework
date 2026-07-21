using System;
using System.Collections.Generic;
using System.Reflection;
using Erenshor.CustomSimFramework.Data;
using UnityEngine;

namespace Erenshor.CustomSimFramework
{
    /// <summary>
    /// Builds a template GameObject per custom sim and registers it in
    /// SimPlayerMngr.ActualSims, the same list the game's own pre-authored sims live
    /// in. From there the vanilla pipeline handles everything: save data creation
    /// (ESSaveData/Sims&lt;Name&gt;), roster tracking, zone placement, spawning
    /// (SpawnMeInGame's premade branch copies our Bio/quirks/dialogue onto the live
    /// body), and whisper responses (FindSimPlayer returns our template).
    ///
    /// Templates are clones of BlankSPTemplate parented under an INACTIVE holder
    /// object, so their components never run Awake/Update. That mirrors the game's
    /// own premade sim prefabs, which are inactive prefab assets (checked via dump).
    /// </summary>
    internal static class SimTemplateBuilder
    {
        /// <summary>Custom sims by name, used by the spawn postfix to re-apply quirks.</summary>
        internal static readonly Dictionary<string, SimDefinition> BuiltSims =
            new Dictionary<string, SimDefinition>(StringComparer.OrdinalIgnoreCase);

        private static GameObject _holder;
        private static readonly Dictionary<string, GameObject> _templates =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Called from the SimPlayerMngr.Start prefix, before LoadActualSims runs.
        /// Idempotent: safe if Start runs again on a later login (templates are
        /// DontDestroyOnLoad and get re-registered into the new manager instance).
        /// </summary>
        internal static void InjectAll(SimPlayerMngr mngr)
        {
            if (PackLoader.Sims.Count == 0)
            {
                return;
            }
            if (mngr.BlankSPTemplate == null)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] BlankSPTemplate is null - cannot build custom sims.");
                return;
            }
            // A template registered with a null CharacterClass would NRE inside
            // vanilla's LoadActualSims and break the whole login load. Skipping
            // this login degrades gracefully instead: existing custom-sim saves
            // just load through the generic path until the next login retries.
            if (GameData.ClassDB == null)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] GameData.ClassDB is null - custom sims skipped this login.");
                return;
            }
            EnsureHolder();

            foreach (SimDefinition def in PackLoader.Sims)
            {
                try
                {
                    InjectOne(mngr, def);
                }
                catch (Exception ex)
                {
                    CustomSimFrameworkPlugin.Log.LogError("[Inject] Failed to inject sim '" + def.Name + "': " + ex);
                }
            }
        }

        private static void InjectOne(SimPlayerMngr mngr, SimDefinition def)
        {
            // Name collision with a vanilla pre-authored sim: never touch those.
            foreach (GameObject existing in mngr.ActualSims)
            {
                if (existing != null && string.Equals(existing.name, def.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_templates.ContainsKey(def.Name))
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Inject] '" + def.Name
                            + "' collides with an existing sim - skipped (" + def.SourceFile + ").");
                    }
                    return; // already ours from a previous login, or a vanilla sim
                }
            }

            // A save file whose name differs from ours only by letter case is THE
            // SAME FILE on Windows (case-insensitive filesystem), while the game
            // compares sim names case-sensitively. Two sims would silently share
            // one save. Refuse to inject into that situation.
            string caseVariant = FindCaseVariantSaveFile(def.Name);
            if (caseVariant != null)
            {
                CustomSimFrameworkPlugin.Log.LogWarning("[Inject] '" + def.Name + "' (" + def.SourceFile
                    + ") clashes with the existing sim save '" + caseVariant
                    + "' (same name, different casing) - skipped. Rename the sim, or use the exact same"
                    + " casing to take that sim over.");
                return;
            }
            RemoveCaseVariantNames(mngr, def.Name);

            GameObject template;
            if (!_templates.TryGetValue(def.Name, out template) || template == null)
            {
                template = BuildTemplate(mngr, def);
                _templates[def.Name] = template;
                BuiltSims[def.Name] = def;
            }

            mngr.ActualSims.Add(template);
            CustomSimFrameworkPlugin.Log.LogInfo("[Inject] Custom sim '" + def.Name + "' ("
                + def.Class + " lvl " + def.Level + ") registered from " + def.SourceFile);
        }

        private static void EnsureHolder()
        {
            if (_holder != null)
            {
                return;
            }
            _holder = new GameObject("CustomSimFramework_Templates");
            _holder.SetActive(false); // children never become active-in-hierarchy -> no Awake/Update
            UnityEngine.Object.DontDestroyOnLoad(_holder);
        }

        private static GameObject BuildTemplate(SimPlayerMngr mngr, SimDefinition def)
        {
            // Instantiating under the inactive holder keeps the clone dormant,
            // mirroring the inactive prefab assets the vanilla premades are.
            GameObject go = UnityEngine.Object.Instantiate(mngr.BlankSPTemplate, _holder.transform);
            go.name = def.Name;

            NPC npc = go.GetComponent<NPC>();
            npc.NPCName = def.Name;

            Stats stats = go.GetComponent<Stats>();
            stats.Level = Mathf.Clamp(def.Level, 1, 35);
            stats.CharacterClass = ResolveClass(def.Class, def.Name);

            Inventory inv = go.GetComponent<Inventory>();
            inv.isMale = !string.Equals(def.Gender, "Female", StringComparison.OrdinalIgnoreCase);
            if (inv.EquippedItems == null)
            {
                inv.EquippedItems = new List<Item>();
            }

            SimPlayer sp = go.GetComponent<SimPlayer>();
            ApplyQuirks(sp, def);
            sp.SkillLevel = def.SkillLevel;
            sp.Bio = def.Bio ?? "";
            sp.PersonalityType = def.PersonalityType;
            sp.BioIndex = -1;
            sp.Rival = def.Rival;
            sp.IsGMCharacter = false;
            sp.InTutorial = false;
            sp.TiedToSlot = 99;
            sp.Greed = def.Greed;
            sp.Patience = def.Patience;
            sp.GearChase = def.GearChase;
            sp.LoreChase = def.LoreChase;
            sp.SocialChase = def.SocialChase;
            sp.Troublemaker = def.Troublemaker;
            sp.DedicationLevel = def.DedicationLevel;

            ResolveAppearance(mngr, def, sp);

            sp.SignOffLine = new List<string>();
            if (def.SignOffLines != null)
            {
                sp.SignOffLine.AddRange(def.SignOffLines);
            }

            BuildLanguage(mngr, go, def);
            return go;
        }

        /// <summary>
        /// Runs once per login after the roster finishes loading. Persists JSON
        /// personality, gender and slot tie into the sim's tracking and save file
        /// so they survive without per-spawn fix-ups and degrade correctly on
        /// uninstall:
        ///   - tracking.Personality: LoadSimPlayerTemplate reads this at every
        ///     spawn (vanilla ignores the template's PersonalityType for actual sims)
        ///   - tracking.TiedToSlot: read live by progression logic (and by
        ///     SimPassiveLevelingOverhaul's tick classification); SaveSim
        ///     round-trips it back into the save file
        ///   - save.PersonalityType / save.Male / save.TiedToSlot: what the
        ///     generic-sim loader reads if the framework is ever removed
        /// </summary>
        internal static void ApplyPostLoadFixups(SimPlayerMngr mngr)
        {
            foreach (KeyValuePair<string, SimDefinition> pair in BuiltSims)
            {
                SimDefinition def = pair.Value;
                try
                {
                    SimPlayerTracking tracking = null;
                    if (mngr.SimDict != null)
                    {
                        mngr.SimDict.TryGetValue(def.Name, out tracking);
                    }
                    if (tracking != null)
                    {
                        if (def.PersonalityType != 0)
                        {
                            tracking.Personality = def.PersonalityType;
                        }
                        // The game forces rivals to 99 every login, don't fight it.
                        // The loader already warns and clears TiedToSlot on Rival defs.
                        if (def.TiedToSlot != -1 && !tracking.Rival)
                        {
                            tracking.TiedToSlot = def.TiedToSlot;
                        }
                    }

                    SimPlayerSaveData save = FindLatestSaveRecord(def.Name);
                    if (save != null)
                    {
                        bool dirty = false;
                        int male = string.Equals(def.Gender, "Female", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                        if (save.Male != male)
                        {
                            save.Male = male;
                            dirty = true;
                        }
                        if (def.PersonalityType != 0 && save.PersonalityType != def.PersonalityType)
                        {
                            save.PersonalityType = def.PersonalityType;
                            dirty = true;
                        }
                        if (def.TiedToSlot != -1 && !def.Rival && save.TiedToSlot != def.TiedToSlot)
                        {
                            save.TiedToSlot = def.TiedToSlot;
                            dirty = true;
                        }
                        if (dirty)
                        {
                            SimPlayerDataManager.SaveSimData(save);
                            CustomSimFrameworkPlugin.LogDebug("Post-load fixup saved for '" + def.Name
                                + "' (Male=" + save.Male + ", PersonalityType=" + save.PersonalityType
                                + ", TiedToSlot=" + save.TiedToSlot + ")");
                        }
                    }
                }
                catch (Exception ex)
                {
                    CustomSimFrameworkPlugin.Log.LogError("[Inject] Post-load fixup failed for '" + def.Name + "': " + ex);
                }
            }
        }

        /// <summary>
        /// The game's SimPlayerDataManager.SimPlayerData list is never cleared.
        /// Every login APPENDS a freshly-read record per sim, and the game's own
        /// FindMyDataInList returns the OLDEST match. The newest match is the
        /// current login's record, the same object vanilla mutates during load
        /// (and the one SimPassiveLevelingOverhaul caches), so mutating it keeps
        /// every writer coherent.
        /// </summary>
        private static SimPlayerSaveData FindLatestSaveRecord(string simName)
        {
            List<SimPlayerSaveData> records = SimPlayerDataManager.SimPlayerData;
            if (records == null)
            {
                return null;
            }
            for (int i = records.Count - 1; i >= 0; i--)
            {
                SimPlayerSaveData record = records[i];
                if (record != null && record.NPCName == simName)
                {
                    return record;
                }
            }
            return null;
        }

        internal static void ApplyQuirks(SimPlayer sp, SimDefinition def)
        {
            sp.TypesInAllCaps = def.TypesInAllCaps;
            sp.TypesInAllLowers = def.TypesInAllLowers;
            sp.TypesInThirdPerson = def.TypesInThirdPerson;
            sp.RefersToSelfAs = def.RefersToSelfAs ?? "";
            sp.LovesEmojis = def.LovesEmojis;
            sp.Abbreviates = def.Abbreviates;
            sp.TypoRate = def.TypoRate;
            sp.TypoChance = def.TypoChance;
        }

        // ────────────────────────────────────────────────────────────────
        // Dialogue
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lists LoadSPChat sources from manager pools rather than the global
        /// SimPlayerLanguage. We honor the same mapping when filling gaps.
        /// </summary>
        private static readonly Dictionary<string, string> ManagerPoolFillMap = new Dictionary<string, string>
        {
            { "Greetings", "GenericGreeting" },
            { "ReturnGreeting", "GenericGreeting" },
            { "Invites", "Invites" },
            { "Justifications", "InviteEnd" },
            { "GenericLines", "SmallTalk" },
            { "Confirms", "Affirmations" },
            { "DeclineGroup", "DenyGroup" },
            { "OTW", "OTW" },
            { "AcknowledgeGratitude", "AcknowledgeGratitude" },
            { "Died", "Died" },
            { "Impressed", "Impressed" },
            { "ImpressedEnd", "ImpressedEnd" },
            { "LevelUpCelebration", "LevelUpCongratulations" }
        };

        /// <summary>
        /// Populates the template's SimPlayerLanguage: pack-provided lists verbatim;
        /// anything left empty is filled from the game's pools (manager pool per the
        /// LoadSPChat mapping, else the global language list, else BlankSPTemplate's
        /// own serialized list). Guarantees no list the game indexes directly is empty
        /// unless vanilla has no content for it either.
        /// </summary>
        private static void BuildLanguage(SimPlayerMngr mngr, GameObject go, SimDefinition def)
        {
            SimPlayerLanguage lang = go.GetComponent<SimPlayerLanguage>();
            if (lang == null)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] Template for '" + def.Name
                    + "' has no SimPlayerLanguage component!");
                return;
            }
            lang.Public = false;

            SimPlayerLanguage globalLang = mngr.GetComponent<SimPlayerLanguage>();
            SimPlayerLanguage blankLang = mngr.BlankSPTemplate.GetComponent<SimPlayerLanguage>();

            int customLists = 0;
            int filledLists = 0;
            List<string> emptyLists = null;

            FieldInfo[] langFields = typeof(SimPlayerLanguage).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in langFields)
            {
                if (field.FieldType != typeof(List<string>))
                {
                    continue;
                }

                List<string> provided = GetDefinitionList(def, field.Name);
                List<string> target = new List<string>();

                if (provided != null && provided.Count > 0)
                {
                    string alternative;
                    if (CategoryInfo.DeadSimListAlternatives.TryGetValue(field.Name, out alternative))
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Inject] '" + def.Name + "' (" + def.SourceFile
                            + "): list '" + field.Name + "' is never read by the game - these lines will not appear."
                            + " Use instead: " + alternative);
                    }
                    target.AddRange(provided);
                    customLists++;
                }
                else
                {
                    List<string> fill = ResolveFillSource(mngr, globalLang, blankLang, field);
                    if (fill != null)
                    {
                        target.AddRange(fill);
                        filledLists++;
                    }
                    else
                    {
                        // Empty even after fallbacks: vanilla has no content either.
                        // Harmless (getters have hardcoded fallbacks) but worth surfacing.
                        if (emptyLists == null)
                        {
                            emptyLists = new List<string>();
                        }
                        emptyLists.Add(field.Name);
                    }
                }
                field.SetValue(lang, target);
            }

            if (CustomSimFrameworkPlugin.Verbose)
            {
                CustomSimFrameworkPlugin.LogDebug("'" + def.Name + "' language: " + customLists
                    + " custom list(s), " + filledLists + " filled from game pools"
                    + (emptyLists != null ? (", EMPTY: " + string.Join(", ", emptyLists.ToArray())) : ""));
            }
        }

        private static List<string> GetDefinitionList(SimDefinition def, string fieldName)
        {
            FieldInfo field = typeof(SimDefinition).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(List<string>))
            {
                return null;
            }
            return (List<string>)field.GetValue(def);
        }

        private static List<string> ResolveFillSource(SimPlayerMngr mngr, SimPlayerLanguage globalLang,
            SimPlayerLanguage blankLang, FieldInfo langField)
        {
            // 1) Manager pool, per LoadSPChat's mapping.
            string poolName;
            if (ManagerPoolFillMap.TryGetValue(langField.Name, out poolName))
            {
                FieldInfo poolField = typeof(SimPlayerMngr).GetField(poolName, BindingFlags.Public | BindingFlags.Instance);
                if (poolField != null && poolField.FieldType == typeof(List<string>))
                {
                    List<string> pool = (List<string>)poolField.GetValue(mngr);
                    if (pool != null && pool.Count > 0)
                    {
                        return pool;
                    }
                }
            }
            // 2) Same-named list on the global SimPlayerLanguage.
            if (globalLang != null)
            {
                List<string> fromGlobal = (List<string>)langField.GetValue(globalLang);
                if (fromGlobal != null && fromGlobal.Count > 0)
                {
                    return fromGlobal;
                }
            }
            // 3) BlankSPTemplate's own serialized list.
            if (blankLang != null)
            {
                List<string> fromBlank = (List<string>)langField.GetValue(blankLang);
                if (fromBlank != null && fromBlank.Count > 0)
                {
                    return fromBlank;
                }
            }
            return null;
        }

        // ────────────────────────────────────────────────────────────────
        // Small helpers
        // ────────────────────────────────────────────────────────────────

        private static Class ResolveClass(string className, string simName)
        {
            ClassDB db = GameData.ClassDB;
            if (db == null)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] GameData.ClassDB unavailable for '" + simName + "'.");
                return null;
            }
            switch ((className ?? "").Trim().ToLowerInvariant())
            {
                case "paladin": return db.Paladin;
                case "arcanist": return db.Arcanist;
                case "druid": return db.Druid;
                case "duelist": return db.Duelist;
                case "stormcaller": return db.Stormcaller;
                case "reaver": return db.Reaver;
                default:
                    CustomSimFrameworkPlugin.Log.LogWarning("[Inject] Unknown class '" + className
                        + "' for sim '" + simName + "' - defaulting to Duelist.");
                    return db.Duelist;
            }
        }

        /// <summary>
        /// JSON-pinned appearance always wins. For unspecified fields (-1/empty),
        /// reuse what the sim's existing save file holds. Vanilla persists
        /// template-driven appearance every login (SimPlayerMngr.cs:323-327,417),
        /// so without this a fresh random roll per app launch would drift the
        /// sim's look between sessions. Saw that happen in testing. The first
        /// ever run has no save, so roll once, vanilla persists it that login,
        /// and it stays stable from then on.
        /// </summary>
        private static void ResolveAppearance(SimPlayerMngr mngr, SimDefinition def, SimPlayer sp)
        {
            int hairCount = (mngr.HairColors != null) ? mngr.HairColors.Count : 0;
            int skinCount = (mngr.SkinColors != null) ? mngr.SkinColors.Count : 0;

            string hairName = def.HairName;
            int hairIdx = def.HairColorIndex;
            int skinIdx = def.SkinColorIndex;

            // The game renders hair by exact child-name match and silently shows
            // a bald head for any name that doesn't exist (incl. Chr_Hair_03,
            // which is missing from the 37 shipped styles).
            if (!string.IsNullOrEmpty(hairName) && !IsValidHairName(hairName))
            {
                CustomSimFrameworkPlugin.Log.LogWarning("[Inject] HairName='" + hairName + "' for sim '" + def.Name
                    + "' is not a real style (valid: Chr_Hair_01..Chr_Hair_38, except 03 which does not exist)"
                    + " - using a random style instead.");
                hairName = "";
            }

            if (string.IsNullOrEmpty(hairName) || hairIdx < 0 || skinIdx < 0)
            {
                SimPlayerSaveData existing = TryReadExistingSave(def.Name);
                if (existing != null)
                {
                    // Invalid saved names (e.g. a previously rolled Chr_Hair_03)
                    // are ignored so the sim heals to a real style.
                    if (string.IsNullOrEmpty(hairName) && IsValidHairName(existing.hairName))
                    {
                        hairName = existing.hairName;
                    }
                    if (hairIdx < 0 && existing.HairColorIndex >= 0 && existing.HairColorIndex < hairCount)
                    {
                        hairIdx = existing.HairColorIndex;
                    }
                    if (skinIdx < 0 && existing.SkinColorIndex >= 0 && existing.SkinColorIndex < skinCount)
                    {
                        skinIdx = existing.SkinColorIndex;
                    }
                }
            }

            sp.HairName = string.IsNullOrEmpty(hairName) ? RandomHairName() : hairName;
            sp.HairColor = ClampOrRandomIndex(hairIdx, hairCount, def.Name, "HairColorIndex");
            sp.SkinColor = ClampOrRandomIndex(skinIdx, skinCount, def.Name, "SkinColorIndex");
        }

        private static SimPlayerSaveData TryReadExistingSave(string simName)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.Combine(Application.persistentDataPath, "ESSaveData"), "Sims" + simName);
                if (!System.IO.File.Exists(path))
                {
                    return null;
                }
                return JsonUtility.FromJson<SimPlayerSaveData>(System.IO.File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.LogDebug("Could not read existing save for '" + simName + "': " + ex.Message);
                return null;
            }
        }

        private static int ClampOrRandomIndex(int index, int count, string simName, string fieldName)
        {
            if (count <= 0)
            {
                return 0;
            }
            if (index < 0)
            {
                return UnityEngine.Random.Range(0, count);
            }
            if (index >= count)
            {
                CustomSimFrameworkPlugin.Log.LogWarning("[Inject] " + fieldName + "=" + index + " out of range (max "
                    + (count - 1) + ") for sim '" + simName + "' - clamping.");
                return count - 1;
            }
            return index;
        }

        private static string RandomHairName()
        {
            // Chr_Hair_03 does not exist (the dump shows 37 styles) and renders
            // bald. Vanilla's own generator has this bug. Ours skips it.
            int num;
            do
            {
                num = UnityEngine.Random.Range(1, 39);
            }
            while (num == 3);
            return (num >= 10) ? ("Chr_Hair_" + num) : ("Chr_Hair_0" + num);
        }

        /// <summary>True for the styles that actually exist under the template:
        /// Chr_Hair_01..Chr_Hair_38 minus the missing Chr_Hair_03.</summary>
        private static bool IsValidHairName(string name)
        {
            const string prefix = "Chr_Hair_";
            if (string.IsNullOrEmpty(name) || name.Length != prefix.Length + 2
                || !name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
            int num;
            if (!int.TryParse(name.Substring(prefix.Length), out num))
            {
                return false;
            }
            return num >= 1 && num <= 38 && num != 3;
        }

        /// <summary>
        /// Returns the existing sim's name if a save file matches ours
        /// case-insensitively but not exactly (Windows treats them as one file
        /// while the game compares names case-sensitively). Exact matches are
        /// fine, that's the documented takeover of an existing generic sim.
        /// </summary>
        private static string FindCaseVariantSaveFile(string simName)
        {
            try
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "ESSaveData");
                if (!System.IO.Directory.Exists(dir))
                {
                    return null;
                }
                string target = "Sims" + simName;
                foreach (string file in System.IO.Directory.GetFiles(dir, "Sims*"))
                {
                    if (System.IO.Path.GetExtension(file) != "")
                    {
                        continue; // .tmp/.bak artifacts, same filter the game's loader uses
                    }
                    string fileName = System.IO.Path.GetFileName(file);
                    if (string.Equals(fileName, target, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(fileName, target, StringComparison.Ordinal))
                    {
                        return fileName.Substring("Sims".Length);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.LogDebug("Case-variant save scan failed for '" + simName + "': " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// The generated-name databases are matched case-sensitively by the game,
        /// so a pool name differing from ours only by case could later mint a
        /// generic sim that shares our save file (case-insensitive filesystem).
        /// Extends the game's own used-name removal to case-insensitive, scoped
        /// to our sims. Exact matches are left for vanilla's NamesUsed pass.
        /// </summary>
        private static void RemoveCaseVariantNames(SimPlayerMngr mngr, string simName)
        {
            RemoveCaseVariantsFrom(mngr.NameDatabaseMale, simName);
            RemoveCaseVariantsFrom(mngr.NameDatabaseFemale, simName);
        }

        private static void RemoveCaseVariantsFrom(List<string> names, string simName)
        {
            if (names == null)
            {
                return;
            }
            for (int i = names.Count - 1; i >= 0; i--)
            {
                if (string.Equals(names[i], simName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(names[i], simName, StringComparison.Ordinal))
                {
                    CustomSimFrameworkPlugin.LogDebug("Removed '" + names[i]
                        + "' from the generated-name pool (case-variant of custom sim '" + simName + "').");
                    names.RemoveAt(i);
                }
            }
        }
    }
}
