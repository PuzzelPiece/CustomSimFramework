using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Erenshor.CustomSimFramework
{
    /// <summary>
    /// Diagnostic-only pass (phase 1 of the framework): dumps everything the injection
    /// layers will later rely on, captured at the exact moments we plan to inject.
    /// Verifies at runtime:
    ///   - which components live on BlankSPTemplate / ActualSims entries, and their active state
    ///   - whether GameData.ClassDB / GameData.SimLang are ready at SimPlayerMngr.Start (prefix time)
    ///   - the full contents of every global dialogue pool (manager + global SimPlayerLanguage)
    ///   - per-sim dialogue and personality quirks of the pre-authored sims (pack author reference)
    ///   - each zone's ZoneComments (ambient sim chatter)
    /// </summary>
    internal static class VanillaDumper
    {
        // Next to the mod's own DLL. Identical to the old location on manual
        // installs, and inside the manager's per-mod folder (like
        // plugins\TeamSaltyBois-CustomSimFramework\) on r2modman. Disposable
        // diagnostics belong with the mod, and this stops the dumper from
        // creating a stray unmanaged plugins\CustomSimFramework\ folder.
        internal static string DumpRoot
        {
            get
            {
                string dllDir = Path.GetDirectoryName(typeof(VanillaDumper).Assembly.Location);
                return Path.Combine(string.IsNullOrEmpty(dllDir) ? Paths.PluginPath : dllDir, "VanillaDump");
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Entry points (called from patches / plugin)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Runs as a PREFIX on SimPlayerMngr.Start, the exact moment sim injection would happen.</summary>
        internal static void DumpAtManagerStart(SimPlayerMngr mngr)
        {
            DumpEnvironment(mngr);
            DumpManagerPools(mngr);
            DumpGlobalLanguage(mngr);
            DumpActualSims(mngr);
            DumpGuildPools();
        }

        /// <summary>Runs once the manager reports LoadedSimplayers == true.</summary>
        internal static void DumpPostLoad(SimPlayerMngr mngr)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("== POST-LOAD STATE (LoadedSimplayers == true) ==");
                sb.AppendLine("Captured: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();

                Section(sb, "GameData.SimLang identity", delegate
                {
                    SimPlayerLanguage mgrLang = mngr.GetComponent<SimPlayerLanguage>();
                    sb.AppendLine("GameData.SimLang is null: " + (GameData.SimLang == null));
                    sb.AppendLine("GameData.SimLang == manager's own SimPlayerLanguage component: "
                        + ReferenceEquals(GameData.SimLang, mgrLang));
                });

                Section(sb, "Zone reference (scene name -> display name; scene names are the keys for zones.dialogue.json)", delegate
                {
                    if (ZoneAtlas.Atlas == null)
                    {
                        sb.AppendLine("ZoneAtlas.Atlas is NULL");
                        return;
                    }
                    sb.AppendLine("ZoneAtlas.Atlas entries: " + ZoneAtlas.Atlas.Length);
                    foreach (ZoneAtlasEntry entry in ZoneAtlas.Atlas)
                    {
                        if (entry == null)
                        {
                            sb.AppendLine("  (null entry)");
                            continue;
                        }
                        string displayName = GetCommonTerms.GetZoneTerm(entry.ZoneName);
                        sb.AppendLine("  " + entry.ZoneName
                            + (displayName != entry.ZoneName ? (" -> \"" + displayName + "\"") : "")
                            + " | levels " + entry.LevelRangeLow + "-" + entry.LevelRangeHigh
                            + " | dungeon=" + entry.Dungeon
                            + " | neighbors: " + JoinList(entry.NeighboringZones));
                    }
                    sb.AppendLine();
                    sb.AppendLine("NOTE: each zone's ZoneComments (ambient chatter) are stored inside that");
                    sb.AppendLine("zone's scene file, so they can only be dumped on entering the zone");
                    sb.AppendLine("(see zones/ folder). Zone NAMES above need no visit.");
                });

                Section(sb, "SimPlayerGrouping pools (group-context chat: join/leave/targeting/pain)", delegate
                {
                    SimPlayerGrouping grouping = GameData.SimPlayerGrouping;
                    if (grouping == null)
                    {
                        sb.AppendLine("GameData.SimPlayerGrouping is NULL");
                        return;
                    }
                    DumpAllStringListFields(sb, grouping);
                });

                Section(sb, "Guilds (RecruitmentStrings are shouted by members as ambient chatter)", delegate
                {
                    GuildManager gm = GameData.GuildManager;
                    if (gm == null || gm.Guilds == null)
                    {
                        sb.AppendLine("GuildManager/Guilds unavailable");
                        return;
                    }
                    sb.AppendLine("Guilds: " + gm.Guilds.Count);
                    foreach (LiveGuildData guild in gm.Guilds)
                    {
                        if (guild == null)
                        {
                            continue;
                        }
                        sb.AppendLine("  " + guild.GuildName + " | id=" + guild.Id
                            + " | leader=" + guild.GuildLeader
                            + " | members=" + (guild.GuildMembers == null ? 0 : guild.GuildMembers.Count)
                            + " | skill=" + guild.GuildSkill);
                        if (guild.RecruitmentStrings != null && guild.RecruitmentStrings.Count > 0)
                        {
                            foreach (string line in guild.RecruitmentStrings)
                            {
                                sb.AppendLine("      recruit: " + line);
                            }
                        }
                    }
                });

                Section(sb, "Sim roster (" + mngr.Sims.Count + " sims)", delegate
                {
                    sb.AppendLine("name | class | level | gender | scene | personality | bioIndex | rival | tiedToSlot | gearScore | guild");
                    foreach (SimPlayerTracking sim in mngr.Sims)
                    {
                        if (sim == null)
                        {
                            sb.AppendLine("(null tracking entry)");
                            continue;
                        }
                        sb.AppendLine(sim.SimName
                            + " | " + sim.ClassName
                            + " | " + sim.Level
                            + " | " + sim.Gender
                            + " | " + sim.CurScene
                            + " | " + sim.Personality
                            + " | " + sim.BioIndex
                            + " | " + sim.Rival
                            + " | " + sim.TiedToSlot
                            + " | " + sim.GearScore
                            + " | " + sim.GuildID);
                    }
                });

                Write("05_post_load.txt", sb.ToString());
                DumpGuildTopics();
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Dump] post-load dump failed: " + ex);
            }
        }

        /// <summary>Runs as a PREFIX on ZoneAnnounce.Start every time a zone loads.</summary>
        internal static void DumpZone(ZoneAnnounce zone)
        {
            StringBuilder sb = new StringBuilder();
            string ownScene = zone.gameObject.scene.IsValid() ? zone.gameObject.scene.name : "(no scene)";
            sb.AppendLine("== ZONE ==");
            sb.AppendLine("ZoneName (display): " + zone.ZoneName);
            sb.AppendLine("Scene (of ZoneAnnounce object): " + ownScene);
            sb.AppendLine("Active scene: " + SceneManager.GetActiveScene().name);
            sb.AppendLine("isDungeon: " + zone.isDungeon + "  RaidCapable: " + zone.RaidCapable);
            sb.AppendLine();

            Section(sb, "ZoneComments (ambient sim chatter pool)", delegate
            {
                if (zone.ZoneComments == null)
                {
                    sb.AppendLine("NULL");
                    return;
                }
                sb.AppendLine("count: " + zone.ZoneComments.Count);
                foreach (string line in zone.ZoneComments)
                {
                    sb.AppendLine("  " + line);
                }
            });

            Write(Path.Combine("zones", SanitizeFileName(ownScene) + ".txt"), sb.ToString());
        }

        // ────────────────────────────────────────────────────────────────────
        // Sections
        // ────────────────────────────────────────────────────────────────────

        private static void DumpEnvironment(SimPlayerMngr mngr)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== ENVIRONMENT / TIMING CHECKS (captured in SimPlayerMngr.Start PREFIX) ==");
            sb.AppendLine("Captured: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();

            Section(sb, "Versions", delegate
            {
                sb.AppendLine("Unity version: " + Application.unityVersion);
                sb.AppendLine("Game version (Application.version): " + Application.version);
                sb.AppendLine("Active scene: " + SceneManager.GetActiveScene().name);
            });

            Section(sb, "Timing: is GameData ready at our injection point?", delegate
            {
                sb.AppendLine("GameData.ClassDB is null: " + (GameData.ClassDB == null));
                if (GameData.ClassDB != null)
                {
                    sb.AppendLine("  Paladin: " + ClassNameOf(GameData.ClassDB.Paladin));
                    sb.AppendLine("  Arcanist: " + ClassNameOf(GameData.ClassDB.Arcanist));
                    sb.AppendLine("  Druid: " + ClassNameOf(GameData.ClassDB.Druid));
                    sb.AppendLine("  Duelist: " + ClassNameOf(GameData.ClassDB.Duelist));
                    sb.AppendLine("  Stormcaller: " + ClassNameOf(GameData.ClassDB.Stormcaller));
                    sb.AppendLine("  Reaver: " + ClassNameOf(GameData.ClassDB.Reaver));
                }
                sb.AppendLine("GameData.SimLang is null: " + (GameData.SimLang == null));
                sb.AppendLine("GameData.ServerPop: " + GameData.ServerPop);
                sb.AppendLine("mngr.ServerPop (pre-Start value): " + mngr.ServerPop);
                sb.AppendLine("ZoneAtlas.Atlas: " + (ZoneAtlas.Atlas == null
                    ? "NULL"
                    : ZoneAtlas.Atlas.Length + " entries"));
            });

            Section(sb, "Appearance pools", delegate
            {
                sb.AppendLine("HairColors: " + CountOf(mngr.HairColors) + " -> " + JoinColors(mngr.HairColors));
                sb.AppendLine("SkinColors: " + CountOf(mngr.SkinColors) + " -> " + JoinColors(mngr.SkinColors));
            });

            Section(sb, "Emoji pool (appended by LovesEmojis quirk)", delegate
            {
                FieldInfo emojiField = typeof(SimPlayerMngr).GetField("emojis",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (emojiField == null)
                {
                    sb.AppendLine("private field 'emojis' not found (renamed in a game update?)");
                    return;
                }
                List<string> emojis = emojiField.GetValue(null) as List<string>;
                sb.AppendLine(emojis == null ? "NULL" : JoinList(emojis));
            });

            Section(sb, "Hair style names (objects under BlankSPTemplate; valid HairName values)", delegate
            {
                if (mngr.BlankSPTemplate == null)
                {
                    sb.AppendLine("BlankSPTemplate is NULL");
                    return;
                }
                List<string> hairNames = new List<string>();
                foreach (Transform child in mngr.BlankSPTemplate.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.StartsWith("Chr_Hair") && !hairNames.Contains(child.name))
                    {
                        hairNames.Add(child.name);
                    }
                }
                hairNames.Sort();
                sb.AppendLine(hairNames.Count == 0
                    ? "none found (hair may live on a different object; Chr_Hair_01..38 observed in vanilla data)"
                    : hairNames.Count + " styles: " + string.Join(", ", hairNames.ToArray()));
            });

            Section(sb, "BlankSPTemplate (spawn template for every sim body)", delegate
            {
                DescribeGameObject(sb, mngr.BlankSPTemplate, "BlankSPTemplate");
                if (mngr.BlankSPTemplate != null)
                {
                    // Two residual unknowns from the 2026-07-19 dead-list audit.
                    // Public=true here would let spawned bodies hijack GameData.SimLang,
                    // and serialized LootWanted entries would make the trade-gratitude
                    // block reachable after all.
                    SimPlayer blankSp = mngr.BlankSPTemplate.GetComponent<SimPlayer>();
                    sb.AppendLine("  SimPlayer.LootWanted: " + (blankSp == null ? "no SimPlayer"
                        : (blankSp.LootWanted == null ? "NULL" : blankSp.LootWanted.Count + " item(s)")));
                    SimPlayerLanguage lang = mngr.BlankSPTemplate.GetComponent<SimPlayerLanguage>();
                    sb.AppendLine("  has SimPlayerLanguage: " + (lang != null));
                    if (lang != null)
                    {
                        sb.AppendLine("  SimPlayerLanguage.Public: " + lang.Public);
                        sb.AppendLine("  language list counts: " + SummarizeStringListCounts(lang));
                        sb.AppendLine();
                        sb.AppendLine("  FULL CONTENTS - these serialized lists are the LIVE lines for any");
                        sb.AppendLine("  list LoadSPChat does not overwrite on generic sims (Aggro, Denials,");
                        sb.AppendLine("  LFGPublic, Goodnight, WantsDrop, ...), and the framework's final");
                        sb.AppendLine("  fill fallback for custom sims:");
                        DumpAllStringListFields(sb, lang);
                    }
                }
            });

            Section(sb, "SPTemplate list", delegate
            {
                if (mngr.SPTemplate == null)
                {
                    sb.AppendLine("NULL");
                    return;
                }
                sb.AppendLine("count: " + mngr.SPTemplate.Count);
                foreach (GameObject go in mngr.SPTemplate)
                {
                    DescribeGameObject(sb, go, "  entry");
                }
            });

            Section(sb, "ActualSims overview (pre-authored sims)", delegate
            {
                if (mngr.ActualSims == null)
                {
                    sb.AppendLine("NULL");
                    return;
                }
                sb.AppendLine("count: " + mngr.ActualSims.Count);
                foreach (GameObject go in mngr.ActualSims)
                {
                    sb.AppendLine("  " + (go == null ? "(null)" : go.name));
                }
                sb.AppendLine("(full per-sim detail in 03_actual_sims/)");
            });

            Write("00_environment.txt", sb.ToString());
        }

        private static void DumpManagerPools(SimPlayerMngr mngr)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== SimPlayerMngr GLOBAL POOLS ==");
            sb.AppendLine("Every public List<string> field on the manager, via reflection,");
            sb.AppendLine("so nothing is missed and names match the shipped game exactly.");
            sb.AppendLine();
            DumpAllStringListFields(sb, mngr);
            Write("01_manager_pools.txt", sb.ToString());
        }

        private static void DumpGuildPools()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== GuildManager DIALOGUE POOLS ==");
            sb.AppendLine("Every public List<string> field on GameData.GuildManager, via reflection.");
            sb.AppendLine("Guild chat questions/asks, topic-conversation support lines, membership");
            sb.AppendLine("and recruitment reactions. Several are fragments composed with live data");
            sb.AppendLine("(see Packs/CATEGORY_REFERENCE.md).");
            sb.AppendLine();
            GuildManager guildMngr = GameData.GuildManager;
            if (guildMngr == null)
            {
                sb.AppendLine("GameData.GuildManager is NULL at SimPlayerMngr.Start prefix time.");
            }
            else
            {
                DumpAllStringListFields(sb, guildMngr);
            }
            Write("06_guild_pools.txt", sb.ToString());
        }

        /// <summary>Dumps every GuildTopic asset (keyword-activated guild conversations).</summary>
        private static void DumpGuildTopics()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== GuildTopic ASSETS (Resources/GuildConvoTopics) ==");
            sb.AppendLine("Keyword-activated guild-chat conversations, shared by all guilds.");
            sb.AppendLine("ActivationWords: matched against guild-chat input (yours or a sim's own");
            sb.AppendLine("SimPlayerActivations opener). Responses: verbatim replies, optionally");
            sb.AppendLine("wrapped '<Preceed> <response>' or '<response> <End>'. RelevantScene gates");
            sb.AppendLine("answers to that zone (elsewhere: OutOfZoneAnswers + scene name).");
            sb.AppendLine();
            // InitGuilds loads BOTH folders (GuildManager.cs:133-134).
            AppendTopicFolder(sb, "GuildConvoTopics");
            AppendTopicFolder(sb, "GuildTutorialTopics");
            Write("07_guild_topics.txt", sb.ToString());
        }

        private static void AppendTopicFolder(StringBuilder sb, string folder)
        {
            GuildTopic[] topics = Resources.LoadAll<GuildTopic>(folder);
            sb.AppendLine("---- Resources/" + folder + ": " + topics.Length + " topic(s) ----");
            sb.AppendLine();
            foreach (GuildTopic topic in topics)
            {
                if (topic == null)
                {
                    continue;
                }
                Section(sb, "Topic: " + topic.name + " (" + folder + ")", delegate
                {
                    sb.AppendLine("RequiredLevelToKnow: " + topic.RequiredLevelToKnow
                        + "  MaxLevelToAsk: " + topic.MaxLevelToAsk
                        + "  forceLotsOfResponses: " + topic.forceLotsOfResponses);
                    DumpAllStringListFields(sb, topic);
                });
            }
        }

        private static void DumpGlobalLanguage(SimPlayerMngr mngr)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== GLOBAL SimPlayerLanguage (component on the manager object) ==");
            SimPlayerLanguage lang = mngr.GetComponent<SimPlayerLanguage>();
            if (lang == null)
            {
                sb.AppendLine("NOT PRESENT on the manager GameObject!");
            }
            else
            {
                sb.AppendLine("Public flag: " + lang.Public);
                sb.AppendLine();
                DumpAllStringListFields(sb, lang);
            }
            Write("02_global_language.txt", sb.ToString());
        }

        private static void DumpActualSims(SimPlayerMngr mngr)
        {
            if (mngr.ActualSims == null)
            {
                return;
            }
            foreach (GameObject go in mngr.ActualSims)
            {
                if (go == null)
                {
                    continue;
                }
                try
                {
                    StringBuilder sb = new StringBuilder();
                    DumpOneActualSim(sb, go);
                    Write(Path.Combine("03_actual_sims", SanitizeFileName(go.name) + ".txt"), sb.ToString());
                }
                catch (Exception ex)
                {
                    CustomSimFrameworkPlugin.Log.LogError("[Dump] failed for actual sim '" + go.name + "': " + ex);
                }
            }
        }

        private static void DumpOneActualSim(StringBuilder sb, GameObject go)
        {
            sb.AppendLine("== PRE-AUTHORED SIM: " + go.name + " ==");
            sb.AppendLine();

            Section(sb, "GameObject", delegate
            {
                DescribeGameObject(sb, go, go.name);
            });

            Section(sb, "NPC", delegate
            {
                NPC npc = go.GetComponent<NPC>();
                sb.AppendLine(npc == null ? "no NPC component" : "NPCName: " + npc.NPCName);
            });

            Section(sb, "Stats", delegate
            {
                Stats stats = go.GetComponent<Stats>();
                if (stats == null)
                {
                    sb.AppendLine("no Stats component");
                    return;
                }
                sb.AppendLine("Level: " + stats.Level);
                sb.AppendLine("CharacterClass: " + (stats.CharacterClass == null ? "NULL" : stats.CharacterClass.ClassName));
            });

            Section(sb, "Inventory", delegate
            {
                Inventory inv = go.GetComponent<Inventory>();
                if (inv == null)
                {
                    sb.AppendLine("no Inventory component");
                    return;
                }
                sb.AppendLine("isMale: " + inv.isMale);
                if (inv.EquippedItems == null)
                {
                    sb.AppendLine("EquippedItems: NULL");
                }
                else
                {
                    sb.AppendLine("EquippedItems (" + inv.EquippedItems.Count + "):");
                    foreach (Item item in inv.EquippedItems)
                    {
                        sb.AppendLine("  " + (item == null ? "(null)" : item.Id + " : " + item.ItemName));
                    }
                }
            });

            Section(sb, "SimPlayer (personality / quirks)", delegate
            {
                SimPlayer sp = go.GetComponent<SimPlayer>();
                if (sp == null)
                {
                    sb.AppendLine("no SimPlayer component");
                    return;
                }
                sb.AppendLine("SkillLevel: " + sp.SkillLevel);
                sb.AppendLine("Bio: " + sp.Bio);
                sb.AppendLine("PersonalityType: " + sp.PersonalityType + "  BioIndex: " + sp.BioIndex);
                sb.AppendLine("HairName: " + sp.HairName + "  HairColor(idx): " + sp.HairColor + "  SkinColor(idx): " + sp.SkinColor);
                sb.AppendLine("TypesInAllCaps: " + sp.TypesInAllCaps);
                sb.AppendLine("TypesInAllLowers: " + sp.TypesInAllLowers);
                sb.AppendLine("TypesInThirdPerson: " + sp.TypesInThirdPerson);
                sb.AppendLine("RefersToSelfAs: '" + sp.RefersToSelfAs + "'");
                sb.AppendLine("LovesEmojis: " + sp.LovesEmojis);
                sb.AppendLine("Abbreviates: " + sp.Abbreviates);
                sb.AppendLine("TypoRate: " + sp.TypoRate + "  TypoChance: " + sp.TypoChance);
                sb.AppendLine("SignOffLine: " + JoinList(sp.SignOffLine));
                sb.AppendLine("Greed: " + sp.Greed + "  Patience: " + sp.Patience);
                sb.AppendLine("LoreChase: " + sp.LoreChase + "  GearChase: " + sp.GearChase
                    + "  SocialChase: " + sp.SocialChase + "  Troublemaker: " + sp.Troublemaker);
                sb.AppendLine("DedicationLevel: " + sp.DedicationLevel + "  Dedication: " + sp.Dedication);
                sb.AppendLine("TiedToSlot: " + sp.TiedToSlot);
                sb.AppendLine("Rival: " + sp.Rival + "  IsGMCharacter: " + sp.IsGMCharacter + "  InTutorial: " + sp.InTutorial);
                sb.AppendLine("LootWanted: " + (sp.LootWanted == null ? "NULL" : sp.LootWanted.Count + " item(s)"));
            });

            Section(sb, "Per-sim SimPlayerLanguage (custom dialogue)", delegate
            {
                SimPlayerLanguage lang = go.GetComponent<SimPlayerLanguage>();
                if (lang == null)
                {
                    sb.AppendLine("no SimPlayerLanguage component -> uses randomized global dialogue");
                    return;
                }
                sb.AppendLine("Public flag: " + lang.Public);
                sb.AppendLine();
                DumpAllStringListFields(sb, lang);
            });
        }

        // ────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Runs a dump section, writing any exception into the output instead of aborting the file.</summary>
        private static void Section(StringBuilder sb, string title, Action body)
        {
            sb.AppendLine("---- " + title + " ----");
            try
            {
                body();
            }
            catch (Exception ex)
            {
                sb.AppendLine("!! SECTION FAILED: " + ex.GetType().Name + ": " + ex.Message);
            }
            sb.AppendLine();
        }

        /// <summary>Dumps every public instance List&lt;string&gt; field of the target object with full contents.</summary>
        private static void DumpAllStringListFields(StringBuilder sb, object target)
        {
            FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(List<string>))
                {
                    continue;
                }
                List<string> list = (List<string>)field.GetValue(target);
                sb.AppendLine("### " + field.Name + " (" + (list == null ? "NULL" : list.Count.ToString()) + ")");
                if (list != null)
                {
                    foreach (string line in list)
                    {
                        sb.AppendLine("  " + line);
                    }
                }
                sb.AppendLine();
            }
        }

        /// <summary>One line of counts per list, for quick comparisons.</summary>
        private static string SummarizeStringListCounts(object target)
        {
            StringBuilder sb = new StringBuilder();
            FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(List<string>))
                {
                    continue;
                }
                List<string> list = (List<string>)field.GetValue(target);
                sb.Append(field.Name + "=" + (list == null ? "NULL" : list.Count.ToString()) + " ");
            }
            return sb.ToString();
        }

        private static void DescribeGameObject(StringBuilder sb, GameObject go, string label)
        {
            if (go == null)
            {
                sb.AppendLine(label + ": NULL");
                return;
            }
            sb.AppendLine(label + ": name='" + go.name + "'"
                + " activeSelf=" + go.activeSelf
                + " activeInHierarchy=" + go.activeInHierarchy
                + " scene=" + (go.scene.IsValid() ? go.scene.name : "(none - prefab asset)"));

            Component[] components = go.GetComponents<Component>();
            StringBuilder comps = new StringBuilder();
            foreach (Component c in components)
            {
                comps.Append(c == null ? "(null/missing script)" : c.GetType().Name);
                comps.Append(", ");
            }
            sb.AppendLine("  components: " + comps);

            ModularPar modular = go.GetComponentInChildren<ModularPar>(true);
            sb.AppendLine("  ModularPar in children: " + (modular != null));
        }

        private static string ClassNameOf(Class cls)
        {
            return cls == null ? "NULL" : cls.ClassName;
        }

        private static string CountOf<T>(List<T> list)
        {
            return list == null ? "NULL" : list.Count.ToString();
        }

        private static string JoinList(List<string> list)
        {
            if (list == null)
            {
                return "NULL";
            }
            return "[" + string.Join(" | ", list.ToArray()) + "]";
        }

        private static string JoinColors(List<Color> list)
        {
            if (list == null)
            {
                return "NULL";
            }
            StringBuilder sb = new StringBuilder();
            foreach (Color c in list)
            {
                sb.Append("#" + ColorUtility.ToHtmlStringRGBA(c) + " ");
            }
            return sb.ToString();
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "unnamed";
            }
            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }
            return name;
        }

        private static void Write(string relativePath, string content)
        {
            string fullPath = Path.Combine(DumpRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content);
            CustomSimFrameworkPlugin.Log.LogInfo("[Dump] wrote " + fullPath);
        }
    }
}
