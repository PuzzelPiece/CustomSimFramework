using System;
using System.Collections.Generic;

namespace Erenshor.CustomSimFramework.Data
{
    /// <summary>
    /// JSON schema for a pack's "global.dialogue.json": lines APPENDED to the pools
    /// that generic (procedurally generated) sims draw from. Field names match the
    /// game's fields exactly. Application is by reflection name-match with dedup.
    /// Category roles (LISTEN / SPEAK / FILL / DEAD) are documented per field and in
    /// Packs/CATEGORY_REFERENCE.md with decompile evidence.
    /// </summary>
    [Serializable]
    public class GlobalDialogueDefinition
    {
        public ManagerPoolAdditions ManagerPools;
        public GlobalLanguageAdditions GlobalLanguage;
        public GroupingPoolAdditions GroupingPools;
        public GuildPoolAdditions GuildPools;
        public TemplateLanguageAdditions TemplateLanguage;

        // Extra names for generated sims (picked randomly at world creation).
        public List<string> MaleNames;
        public List<string> FemaleNames;

        // Extra inspect-window bios for generated sims, by personality flavor.
        // Append-only, so existing saved BioIndex values stay valid.
        public List<string> NiceBios;
        public List<string> TryhardBios;
        public List<string> MeanBios;

        [NonSerialized] public string SourceFile = "";
    }

    /// <summary>
    /// Pools on SimPlayerMngr. Roles:
    ///   LISTEN: phrases the game matches against what the PLAYER types/shouts.
    ///           Adding lines teaches sims to understand more phrasings.
    ///   SPEAK:  lines sims say directly from this pool.
    ///   DUAL:   both of the above.
    ///   FILL:   source pools that seed every generic sim's personal dialogue
    ///           lists at spawn (LoadSPChat). Adding lines enriches all sims.
    ///   DEAD:   never read by the game. Kept for compatibility, the loader warns.
    /// Deliberately NOT exposed: Obscenities (a LISTEN pool that triggers GM
    /// warnings) and GMWarningsObscenities (an ORDERED escalation list, not a
    /// random pool). Modifying either one changes moderation behavior.
    /// </summary>
    [Serializable]
    public class ManagerPoolAdditions
    {
        // ── LISTEN: teach sims to understand more player phrasings ──
        public List<string> GroupRequest;       // "want to group?", "xp"
        public List<string> LFGs;               // your LFG shouts sims respond to
        public List<string> JoinMyGuild;        // you inviting a sim to your guild
        public List<string> JoinYourGuild;      // you asking to join their guild
        public List<string> WhereDidYouGet;     // gear questions
        public List<string> InfoWanted;         // item/NPC/quest knowledge questions
        public List<string> HelpReq;            // asking a sim to come help
        public List<string> InvisReq;           // asking for invisibility
        public List<string> LocReq;             // "where are you"
        public List<string> LevelCheck;         // "what level are you"
        public List<string> Gratitude;          // your thank-you phrases
        public List<string> Apologies;          // your apology phrases
        public List<string> WhatsUp;            // "what are you up to"
        public List<string> Goodnight;          // your log-off farewells
        public List<string> Declinations;       // no-words: recognized from you. (Its DeclineGroup seeding
                                                // is dead, LoadSPChat immediately overwrites it with
                                                // DenyGroup. SimPlayerMngr.cs:3361-3375.)

        // ── DUAL (listen + speak/fill) ──
        public List<string> GenericGreeting;    // greetings: recognized from you AND seeds sim Greetings
        public List<string> Affirmations;       // yes-words: recognized AND seeds sim Confirms
        public List<string> LevelUpCelebrations;// "Ding!!": recognized from you AND shouted by leveling sims

        // ── SPEAK: sims say these directly from the pool ──
        public List<string> ApologyResponses;   // reply to your apology
        public List<string> GroupedAlreadyAccept; // leaving their group to join yours
        public List<string> DidNotUnderstand;   // whisper parser fallback
        public List<string> XPLossMsg;          // group sims after a death/wipe
        public List<string> CongratsForWorldEvent;  // world event / server-first whispers
        public List<string> FriendsClubResponseToWorldEvent; // rival taunts on your wins

        // ── FILL: seeds every generic sim's personal lists at spawn ──
        public List<string> Invites;            // -> sim Invites ("hey i'm over in" + zone)
        public List<string> InviteEnd;          // -> sim Justifications
        public List<string> DenyGroup;          // -> sim DeclineGroup
        public List<string> OTW;                // -> sim OTW
        public List<string> Died;               // -> sim Died
        public List<string> AcknowledgeGratitude; // -> sim AcknowledgeGratitude
        public List<string> Impressed;          // -> sim Impressed
        public List<string> ImpressedEnd;       // -> sim ImpressedEnd
        public List<string> LevelUpCongratulations; // -> sim LevelUpCelebration. ALSO read directly
                                                    // for /say ding replies (ShoutParse 886/890), so DUAL.

        // ── DEAD: never read by the game (loader warns if populated) ──
        public List<string> SmallTalk;          // only feeds sim GenericLines, which nothing reads
        public List<string> WTBs;               // WTB spam is hardcoded (SimPlayer.DoWTBSpam)
    }

    /// <summary>
    /// Lists on the global SimPlayerLanguage component. Most are live fallbacks any
    /// sim can draw from. DEAD at the global level too: GenericLines, Hello, Unsure
    /// (loader warns). EnvDmg and Gratitude are LIVE here (the game reads only the
    /// global copies) even though the per-sim versions are dead.
    /// </summary>
    [Serializable]
    public class GlobalLanguageAdditions
    {
        public List<string> Greetings;
        public List<string> ReturnGreeting;
        public List<string> Invites;
        public List<string> Justifications;
        public List<string> Confirms;
        public List<string> GenericLines;      // DEAD
        public List<string> Aggro;
        public List<string> Died;
        public List<string> InsultsFun;
        public List<string> RetortsFun;
        public List<string> Exclamations;
        public List<string> Denials;
        public List<string> DeclineGroup;
        public List<string> Negative;
        public List<string> LFGPublic;
        public List<string> OTW;
        public List<string> Goodnight;
        public List<string> Hello;             // DEAD
        public List<string> LocalFriendHello;
        public List<string> UnsureResponse;
        public List<string> AngerResponse;
        public List<string> Affirms;
        public List<string> EnvDmg;            // LIVE here (global-only category)
        public List<string> WantsDrop;
        public List<string> Gratitude;         // DEAD even here (found 2026-07-19). Its only read site
                                               // (SimTradeWindow.cs:168) is gated on LootWanted, which the
                                               // game never populates. Unreachable vanilla code.
        public List<string> Impressed;
        public List<string> ImpressedEnd;
        public List<string> AcknowledgeGratitude;
        public List<string> LevelUpCelebration;
        public List<string> GoodLastOuting;
        public List<string> BadLastOuting;
        public List<string> GotAnItemLastOuting;
        public List<string> ReturnToZone;
        public List<string> BeenAWhile;
        public List<string> Unsure;            // DEAD (UnsureResponse is the live one)
    }

    /// <summary>
    /// Pools on SimPlayerGrouping (GameData.SimPlayerGrouping). Group-context
    /// chat lines all group sims draw from. All SPEAK, all confirmed live
    /// (SimPlayerGrouping.cs:771,903,907,1221,1373 and NPC.cs:3317).
    /// SimPlayerGrouping also declares an "Ow" list, but nothing ever reads
    /// it, so it isn't exposed here.
    /// </summary>
    [Serializable]
    public class GroupingPoolAdditions
    {
        public List<string> Hellos;     // sim joins your group
        public List<string> Goodbyes;   // sim leaves your group ("adios")
        public List<string> Affirms;    // acknowledging assist calls / orders
        public List<string> Targeting;  // combat target calls (target name appended)
        public List<string> Angry;      // upset lines (e.g. being dismissed)
        public List<string> Lost;       // can't find / reach the target
    }

    /// <summary>
    /// The shared voice of every GENERIC (procedurally generated) sim: lines
    /// appended to BlankSPTemplate's SimPlayerLanguage, the component every
    /// spawned sim body is cloned from. Only the lists LoadSPChat does NOT
    /// refill are exposed (the refilled ones belong in ManagerPools); nothing
    /// in the game ever writes these, so appends survive the whole login.
    /// Also reaches generic sims' whisper replies (FindSimPlayer returns the
    /// template for non-premades). Premades and custom sims are unaffected
    /// (their own language overwrites the clone's).
    /// </summary>
    [Serializable]
    public class TemplateLanguageAdditions
    {
        public List<string> Aggro;              // full line, group-tell when the sim pulls a mob
        public List<string> Exclamations;       // appended to greeting shouts with NO space, so bare words ("lol")
        public List<string> Denials;            // full-line whisper refusal ("I already invited you")
        public List<string> Negative;           // full-line "no" answer-shout to another sim's question
        public List<string> LFGPublic;          // FRAGMENT -> "<line> <area name> you can lead."
        public List<string> Goodnight;          // full line, REPLY to the player's goodnight shout/say and
                                                // guild-chat goodnights (sims never log off on their own)
        public List<string> LocalFriendHello;   // full line, greeting a player they know (NN)
        public List<string> Affirms;            // guild-invite accept: "<line> I'll join up!..." + yes-shouts
        public List<string> WantsDrop;          // full line, loot request (II = item name)
        public List<string> ReturnToZone;       // FRAGMENT -> "<line> <zone>! " in memory greetings
    }

    /// <summary>
    /// Pools on GuildManager (GameData.GuildManager). Guild chat and
    /// guild-recruitment dialogue shared by every guild's sims. All SPEAK, all
    /// confirmed live. Several are FRAGMENTS the game composes with live data
    /// (item/NPC/zone/member names), noted per field with in-game examples in
    /// Packs/README.md. Quirks (PersonalizeString) apply after composition.
    /// NOTE: the NN token does NOT work in these pools. The question paths
    /// strip it to nothing and the whisper paths would show it literally.
    /// </summary>
    [Serializable]
    public class GuildPoolAdditions
    {
        // ── Guild-chat questions sims ask (GuildManager.cs:1608-1626) ──
        // Composed: "<sim's Greeting> <line> <name> drops?/spawns?". Write
        // fragments that end naturally before a name ("anyone know where").
        public List<string> ItemSearch;         // -> "<line> <item name> drops?"
        public List<string> NPCSearch;          // -> "<line> <npc name> spawns?"
        public List<string> LevelAdvice;        // -> "<line>  level N?" ("what should i grind at")

        // ── Guild quest asks (GuildManager.cs:1491) ──
        // Composed: "<QuestAskItem> <item>? <QuestAskEnding>. <Signoff>"
        public List<string> QuestAskItem;       // fragment before the item name ("anyone have a spare")
        public List<string> QuestAskEnding;     // clause after the "?" ("I'll pay well")
        public List<string> Signoff;            // closer ("Thanks!")

        // ── Guild topic conversations (GuildManager.cs:706,804,827) ──
        public List<string> OutOfZoneAnswers;   // -> "<line> <zone name>" ("pretty sure that's over in")
        public List<string> LowLevelAnswers;    // full line ("no clue, i'm too low for that")

        // ── Membership events ──
        public List<string> NewPlayerWelcome;   // -> "<line> <new member name>" (GuildManager.cs:742)
        public List<string> GuildDeletedResponses; // whisper after you disband your guild (full line)
        public List<string> GuildRemoveResponses;  // whisper after you kick a member (full line)

        // ── Recruitment reactions (whispers after your guild-recruit shouts) ──
        public List<string> InterestMSG;        // full line; game may append " I'm right here in your group!"
        public List<string> FCAntiRecruitMSG;   // Friends Club rival taunt (full line)
    }

    /// <summary>
    /// JSON schema for a pack's "zones.dialogue.json": lines appended to
    /// ZoneAnnounce.ZoneComments, the pool ambient sim shouts draw from in a zone.
    /// This is THE channel for ambient small talk (per-sim GenericLines and the
    /// manager SmallTalk pool are dead).
    /// </summary>
    [Serializable]
    public class ZoneDialogueDefinition
    {
        public List<ZoneLines> Zones;       // per-zone additions
        public List<string> AllZones;       // appended to every zone

        [NonSerialized] public string SourceFile = "";
    }

    [Serializable]
    public class ZoneLines
    {
        // Matches the zone's scene name (e.g. "Brake") OR display name
        // (e.g. "Faerie's Brake"), case-insensitive.
        public string Zone = "";
        public List<string> Lines;
    }

    /// <summary>
    /// Evidence-based category status, shared by the loader warnings.
    /// See Packs/CATEGORY_REFERENCE.md for the decompile citations.
    /// </summary>
    internal static class CategoryInfo
    {
        /// <summary>Per-sim SimPlayerLanguage lists the game never reads, with the live alternative.</summary>
        internal static readonly Dictionary<string, string> DeadSimListAlternatives = new Dictionary<string, string>
        {
            { "GenericLines", "zone chatter (zones.dialogue.json) - ambient small talk comes from ZoneComments" },
            { "Hello", "LocalFriendHello or Greetings (GetHello is never called)" },
            { "Unsure", "UnsureResponse (the live twin)" },
            { "EnvDmg", "GlobalLanguage.EnvDmg in global.dialogue.json (game reads only the global copy)" },
            { "Gratitude", "nothing - Gratitude is dead everywhere: its only read site is unreachable"
                + " (LootWanted is never populated) and trade thank-yous are hardcoded" }
        };

        /// <summary>Global SimPlayerLanguage lists that are dead even on the global component.</summary>
        internal static readonly HashSet<string> DeadGlobalLanguageLists = new HashSet<string>
        {
            "GenericLines", "Hello", "Unsure",
            "Gratitude" // found 2026-07-19: its only read (SimTradeWindow.cs:168) sits
                        // behind the never-populated LootWanted gate
        };

        /// <summary>Manager pools that are never read.</summary>
        internal static readonly HashSet<string> DeadManagerPools = new HashSet<string>
        {
            "SmallTalk", "WTBs"
        };

        /// <summary>
        /// Manager pools the game matches against PLAYER text with \b-bounded
        /// regex (LISTEN + DUAL). Used to warn about lines with edge
        /// punctuation, which can never match at message boundaries.
        /// </summary>
        internal static readonly HashSet<string> ListenManagerPools = new HashSet<string>
        {
            "GroupRequest", "LFGs", "JoinMyGuild", "JoinYourGuild", "WhereDidYouGet",
            "InfoWanted", "HelpReq", "InvisReq", "LocReq", "LevelCheck", "Gratitude",
            "Apologies", "WhatsUp", "Goodnight", "Declinations", "GenericGreeting",
            "Affirmations", "LevelUpCelebrations"
        };
    }
}
