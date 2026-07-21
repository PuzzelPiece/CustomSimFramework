using System;
using System.Collections.Generic;

namespace PackStudio
{
    internal struct VanillaTopic
    {
        internal readonly string Name;
        internal readonly string Folder;
        internal readonly string[] Triggers;

        internal VanillaTopic(string name, string folder, string[] triggers)
        {
            Name = name;
            Folder = folder;
            Triggers = triggers;
        }
    }

    internal struct ZoneEntry
    {
        internal readonly string Scene;
        internal readonly string AtlasDisplay; // GetZoneTerm-based (13 are wrong at runtime)
        internal readonly int LevelLow;
        internal readonly int LevelHigh;
        internal readonly bool Dungeon;

        internal ZoneEntry(string scene, string display, int low, int high, bool dungeon)
        {
            Scene = scene;
            AtlasDisplay = display;
            LevelLow = low;
            LevelHigh = high;
            Dungeon = dungeon;
        }
    }

    internal enum NnTier { Safe, Mixed, No }

    /// <summary>
    /// Hand-maintained knowledge tables (sources: PACK_TOOL_REFERENCE.md and
    /// the 2026-07-19/20 audits). Everything derived mechanically from dumps
    /// lives in RefData.Generated.cs instead.
    /// </summary>
    internal static class RefData
    {
        // ── Zone display names ──────────────────────────────────────
        // The game shows ZoneAnnounce.ZoneName, which for 13 zones differs
        // from the atlas/GetZoneTerm name embedded in RefGen.Atlas. These are
        // the dump-verified runtime names (topic RelevantScene compares
        // against THESE, case-sensitively).
        private static readonly Dictionary<string, string> RuntimeDisplayOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Blight", "The Blight" },
            { "Duskenlight", "The Duskenlight Coast" },
            { "Elderstone", "The Elderstone Mines" },
            { "FernallaField", "Fernalla's Revival Plains" },
            { "Krakengard", "Old Krakengard" },
            { "Loomingwood", "Loomingwood Forest" },
            { "Malaroth", "Malaroth's Nesting Grounds" },
            { "Silkengrass", "Silkengrass Meadowlands" },
            { "Undercity", "Lost Cellar" },
            { "Underspine", "Underspine Hollow" },
            { "Willowwatch", "Willowwatch Ridge" },
            { "Windwashed", "Windwashed Pass" },
        };

        // Display names not yet verified by a zone visit (endgame dungeons;
        // atlas name is the best guess until the dump tour reaches them).
        internal static readonly HashSet<string> DisplayNameUnverified =
            new HashSet<string>(StringComparer.Ordinal)
        {
            "VitheosEnd", "Rockshade", "PrielPlateau", "Braxonia", "AzynthiClear",
        };

        // Zones that ship with ZERO vanilla ambient chatter (pack lines are
        // their entire ambient voice).
        internal static readonly HashSet<string> EmptyChatterScenes =
            new HashSet<string>(StringComparer.Ordinal)
        {
            "FernallaField", "Bonepits", "Underspine", "Abyssal",
            "Elderstone", "Krakengard", "Undercity",
        };

        internal static string RuntimeDisplayName(string scene)
        {
            string overridden;
            if (RuntimeDisplayOverrides.TryGetValue(scene, out overridden))
            {
                return overridden;
            }
            foreach (ZoneEntry z in RefGen.Atlas)
            {
                if (z.Scene == scene)
                {
                    return z.AtlasDisplay;
                }
            }
            return scene;
        }

        /// <summary>All names a zones.dialogue "Zone" key may use (scene OR display).</summary>
        internal static HashSet<string> AllZoneKeys()
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZoneEntry z in RefGen.Atlas)
            {
                keys.Add(z.Scene);
                keys.Add(z.AtlasDisplay);
                keys.Add(RuntimeDisplayName(z.Scene));
            }
            return keys;
        }

        // ── NN three-tier model (per-sim SimPlayerLanguage lists) ──
        internal static readonly HashSet<string> NnSafeLists = new HashSet<string>(StringComparer.Ordinal)
        {
            "Greetings", "ReturnGreeting", "Invites", "Goodnight", "LocalFriendHello",
            "BeenAWhile", "ReturnToZone", "GoodLastOuting", "BadLastOuting", "GotAnItemLastOuting",
        };

        internal static readonly HashSet<string> NnMixedLists = new HashSet<string>(StringComparer.Ordinal)
        {
            "LevelUpCelebration", "Exclamations", "InsultsFun", "OTW", "DeclineGroup",
        };

        internal static NnTier NnTierOf(string listName)
        {
            if (NnSafeLists.Contains(listName)) { return NnTier.Safe; }
            if (NnMixedLists.Contains(listName)) { return NnTier.Mixed; }
            return NnTier.No;
        }

        // ── Dead surfaces (never offered by the editor UI) ─────────
        internal static readonly HashSet<string> DeadSimLists = new HashSet<string>(StringComparer.Ordinal)
        {
            "GenericLines", "Hello", "Unsure", "EnvDmg", "Gratitude",
        };

        internal static readonly HashSet<string> DeadGlobalLangLists = new HashSet<string>(StringComparer.Ordinal)
        {
            "GenericLines", "Hello", "Unsure", "Gratitude",
        };

        internal static readonly HashSet<string> DeadManagerPools = new HashSet<string>(StringComparer.Ordinal)
        {
            "SmallTalk", "WTBs",
        };

        internal static readonly string[] DeadSimDials =
        {
            "Abbreviates", "TypoChance", "Greed", "Troublemaker",
            "LoreChase", "SocialChase", "DedicationLevel",
        };

        // ── The 34 per-sim dialogue lists, in editor order ─────────
        internal static readonly string[] SimListNames =
        {
            "Greetings", "ReturnGreeting", "LocalFriendHello", "BeenAWhile",
            "ReturnToZone", "GoodLastOuting", "BadLastOuting", "GotAnItemLastOuting",
            "Invites", "Justifications",
            "Confirms", "Affirms", "Denials", "Negative", "DeclineGroup", "OTW",
            "Aggro", "Died",
            "InsultsFun", "RetortsFun", "Exclamations",
            "LFGPublic", "Goodnight",
            "UnsureResponse", "AngerResponse", "AcknowledgeGratitude",
            "WantsDrop", "Impressed", "ImpressedEnd", "LevelUpCelebration",
        };

        // Fragment lists (the game appends a name/zone/item after the line).
        internal static readonly HashSet<string> FragmentLists = new HashSet<string>(StringComparer.Ordinal)
        {
            "Invites", "ReturnToZone", "GotAnItemLastOuting", "LFGPublic", "Impressed",
        };

        // Manager pools that seed each generic sim's personal lists
        // (LoadSPChat mapping), used for "auto-filled from X" notes.
        internal static readonly Dictionary<string, string> ManagerPoolFillMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Greetings", "GenericGreeting" },
            { "ReturnGreeting", "GenericGreeting" },
            { "Invites", "Invites" },
            { "Justifications", "InviteEnd" },
            { "Confirms", "Affirmations" },
            { "DeclineGroup", "DenyGroup" },
            { "OTW", "OTW" },
            { "AcknowledgeGratitude", "AcknowledgeGratitude" },
            { "Died", "Died" },
            { "Impressed", "Impressed" },
            { "ImpressedEnd", "ImpressedEnd" },
            { "LevelUpCelebration", "LevelUpCongratulations" },
        };

        // ── Whisper / say parse orders (interception checks) ───────
        // A message meant for a later pool is EATEN by any earlier pool whose
        // line appears word-boundary inside it. Names = manager pool fields.
        internal static readonly string[] WhisperParseOrder =
        {
            "Obscenities", "JoinYourGuild", "JoinMyGuild", "HelpReq", "Affirmations",
            "Declinations", "Gratitude", "WhereDidYouGet", "InfoWanted", "LocReq",
            "LevelCheck", "Apologies", "WhatsUp", "GenericGreeting",
        };

        internal static readonly string[] SayParseOrder =
        {
            "Obscenities", "Affirmations", "Declinations", "LocReq",
            "WhereDidYouGet", "HelpReq", "Apologies", "WhatsUp", "GenericGreeting",
        };

        // LISTEN + DUAL manager pools (edge-punctuation / bare-words rules).
        internal static readonly HashSet<string> ListenManagerPools = new HashSet<string>(StringComparer.Ordinal)
        {
            "GroupRequest", "LFGs", "JoinMyGuild", "JoinYourGuild", "WhereDidYouGet",
            "InfoWanted", "HelpReq", "InvisReq", "LocReq", "LevelCheck", "Gratitude",
            "Apologies", "WhatsUp", "Goodnight", "Declinations", "GenericGreeting",
            "Affirmations", "LevelUpCelebrations",
        };

        // ── Schema keys per file type (unknown-key warnings) ───────
        internal static readonly string[] SimKeys =
        {
            "Name", "Gender", "Class", "Level", "SkillLevel", "Rival", "TiedToSlot",
            "Bio", "PersonalityType", "HairName", "HairColorIndex", "SkinColorIndex",
            "TypesInAllCaps", "TypesInAllLowers", "TypesInThirdPerson", "RefersToSelfAs",
            "LovesEmojis", "Abbreviates", "TypoRate", "TypoChance", "SignOffLines",
            "Greed", "Patience", "GearChase", "LoreChase", "SocialChase", "Troublemaker",
            "DedicationLevel",
            "Greetings", "ReturnGreeting", "Invites", "Justifications", "Confirms",
            "GenericLines", "Aggro", "Died", "InsultsFun", "RetortsFun", "Exclamations",
            "Denials", "DeclineGroup", "Negative", "LFGPublic", "OTW", "Goodnight",
            "Hello", "LocalFriendHello", "UnsureResponse", "AngerResponse", "Affirms",
            "EnvDmg", "WantsDrop", "Gratitude", "Impressed", "ImpressedEnd",
            "AcknowledgeGratitude", "LevelUpCelebration", "GoodLastOuting",
            "BadLastOuting", "GotAnItemLastOuting", "ReturnToZone", "BeenAWhile", "Unsure",
        };

        internal static readonly string[] Classes =
        {
            "Paladin", "Arcanist", "Druid", "Duelist", "Stormcaller", "Reaver",
        };

        // The game DISPLAYS the internal class "Duelist" as "Windblade"; pack
        // JSON must carry internal names (the mod's ResolveClass only accepts
        // those). The editor shows players the names they know.
        internal static readonly string[] ClassDisplayNames =
        {
            "Arcanist", "Druid", "Paladin", "Reaver", "Stormcaller", "Windblade",
        };

        internal static string ClassToInternal(string display)
        {
            return display == "Windblade" ? "Duelist" : display;
        }

        internal static string ClassToDisplay(string internalName)
        {
            return internalName.Equals("Duelist", StringComparison.OrdinalIgnoreCase)
                ? "Windblade" : internalName;
        }

        internal const string NamePattern = "^[A-Za-z0-9-]+$";

        internal static bool IsValidHairName(string name)
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
    }
}
