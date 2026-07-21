using System.Collections.Generic;

namespace PackStudio
{
    internal sealed class PoolMeta
    {
        internal string Key;
        internal string Title;
        internal string Guidance;
        internal bool Listen;     // matched against what YOU type - no spoken preview

        internal PoolMeta(string key, string title, string guidance, bool listen = false)
        {
            Key = key;
            Title = title;
            Guidance = guidance;
            Listen = listen;
        }
    }

    internal sealed class SectionMeta
    {
        internal string Key;         // JSON section key ("" = file root: names/bios)
        internal string Title;
        internal string Blurb;       // one-liner under the tab
        internal string SizePrefix;  // RefGen.PoolSizes / ExampleLines prefix
        internal PoolMeta[] Pools;

        internal SectionMeta(string key, string title, string blurb, string sizePrefix, PoolMeta[] pools)
        {
            Key = key;
            Title = title;
            Blurb = blurb;
            SizePrefix = sizePrefix;
            Pools = pools;
        }
    }

    /// <summary>
    /// Editor metadata for global.dialogue.json (Server Chatter). Dead pools
    /// are simply not offered. LISTEN pools carry the bare-words rules in
    /// their guidance and get no spoken preview.
    /// </summary>
    internal static class GlobalMeta
    {
        private const string ListenRule = " LISTEN rules: bare words, 2 or more words per line, no"
            + " punctuation at the edges. Only add phrasings vanilla misses.";

        internal static readonly SectionMeta[] Sections =
        {
            new SectionMeta("ManagerPools", "Understanding & replies",
                "What sims RECOGNIZE from you, plus shared replies and the pools that seed every generated sim.",
                "Manager", new[]
            {
                new PoolMeta("GroupRequest", "Group requests (listen)", "Ways of asking a sim to group." + ListenRule, true),
                new PoolMeta("LFGs", "LFG shouts (listen)", "Your looking-for-group shouts sims respond to." + ListenRule, true),
                new PoolMeta("JoinMyGuild", "\"Join my guild\" (listen)", "You inviting a sim to your guild." + ListenRule, true),
                new PoolMeta("JoinYourGuild", "\"Invite me\" (listen)", "You asking to join their guild." + ListenRule, true),
                new PoolMeta("WhereDidYouGet", "Gear questions (listen)", "Asking where their equipment came from."
                    + " Lines containing \"where \" only work via whisper (a location answer intercepts says)." + ListenRule, true),
                new PoolMeta("InfoWanted", "Knowledge questions (listen)", "Item/NPC/quest questions." + ListenRule, true),
                new PoolMeta("HelpReq", "Help requests (listen)", "Asking a sim to come help you." + ListenRule, true),
                new PoolMeta("InvisReq", "Invisibility requests (listen)", "Asking a caster for invisibility." + ListenRule, true),
                new PoolMeta("LocReq", "\"Where are you\" (listen)", "Asking a sim's location." + ListenRule, true),
                new PoolMeta("LevelCheck", "\"What level\" (listen)", "Asking a sim's level." + ListenRule, true),
                new PoolMeta("Gratitude", "Thanks (listen)", "Your thank-you phrasings." + ListenRule, true),
                new PoolMeta("Apologies", "Apologies (listen)", "Your apology phrasings." + ListenRule, true),
                new PoolMeta("WhatsUp", "\"What are you up to\" (listen)", "Small-talk check-ins." + ListenRule, true),
                new PoolMeta("Goodnight", "Log-off farewells (listen)", "Your goodnight phrasings (triggers reply waves)." + ListenRule, true),
                new PoolMeta("Declinations", "No-words (listen)", "Ways of saying no that sims recognize." + ListenRule, true),
                new PoolMeta("GenericGreeting", "Greetings (listen + seed)",
                    "Recognized from you AND seeds every generated sim's own greetings. Single words"
                    + " are fine here since this pool is checked last. These also open every guild"
                    + " item, NPC and level question, since the game always puts a greeting in front"
                    + " of those."),
                new PoolMeta("Affirmations", "Yes-words (listen + seed)", "Recognized from you AND seeds sim agreements."),
                new PoolMeta("LevelUpCelebrations", "Ding words (listen + speak)",
                    "Recognized from you (fires grats waves) AND shouted by leveling sims."),
                new PoolMeta("ApologyResponses", "Apology replies", "Replies to your apology. Vanilla ships only THREE, so additions kill fast repeats."),
                new PoolMeta("GroupedAlreadyAccept", "Leaving-their-group lines", "Said when a sim ditches their group to join yours."),
                new PoolMeta("DidNotUnderstand", "Confusion replies", "The whisper-parser fallback when a sim doesn't understand you."),
                new PoolMeta("XPLossMsg", "Wipe laments", "Group sims after a death/wipe."),
                new PoolMeta("CongratsForWorldEvent", "World-event congrats", "Whispers after your server-first/world-event wins."),
                new PoolMeta("FriendsClubResponseToWorldEvent", "Rival taunts", "The rival guild's response to your wins."),
                new PoolMeta("Invites", "Invite fragments (seed)", "Seeds sims' zone invites - FRAGMENT, ends before a zone name (\"found a decent camp in\")."),
                new PoolMeta("InviteEnd", "Invite closers (seed)", "Seeds invite justifications - follows the zone name (\"if you feel like tagging along\")."),
                new PoolMeta("DenyGroup", "Group declines (seed)", "Seeds sims' decline-group lines."),
                new PoolMeta("OTW", "On-my-way (seed)", "Seeds sims' coming-to-help lines."),
                new PoolMeta("Died", "Death lines (seed)", "Seeds sims' death lines."),
                new PoolMeta("AcknowledgeGratitude", "\"np\" replies (seed)", "Seeds sims' you're-welcome replies."),
                new PoolMeta("Impressed", "Gear compliments (seed)", "Seeds gear compliments - FRAGMENT, ends before a gear word."),
                new PoolMeta("ImpressedEnd", "Compliment closers (seed)", "Seeds the closer after the gear word."),
                new PoolMeta("LevelUpCongratulations", "Ding grats (seed)", "Seeds sims' level-up congratulations."),
            }),

            new SectionMeta("GlobalLanguage", "Shared fallback voice",
                "The fallback lists ANY sim can draw from, with the same list names as a sim file."
                + " ReturnGreeting, AcknowledgeGratitude and LevelUpCelebration ship EMPTY in"
                + " vanilla, and EnvDmg lives only here.",
                "Global", BuildGlobalLanguage()),

            new SectionMeta("GroupingPools", "Party chat",
                "Group-context lines shared by EVERY sim grouped with you.",
                "Grouping", new[]
            {
                new PoolMeta("Hellos", "Joining your group", "Said when a sim joins."),
                new PoolMeta("Goodbyes", "Leaving your group", "Said when a sim leaves."),
                new PoolMeta("Affirms", "Order acknowledgements", "Acknowledging assist calls and orders."),
                new PoolMeta("Targeting", "Target calls", "FRAGMENT - the target's name follows (\"burn down\" + name)."),
                new PoolMeta("Angry", "Upset lines", "Being dismissed (especially while dead)."),
                new PoolMeta("Lost", "Lost & confused", "Can't find or reach the target."),
            }),

            new SectionMeta("GuildPools", "Guild chat",
                "Questions, quest asks and reactions shared by every guild's sims. Several are"
                + " FRAGMENTS composed with live item, NPC, zone or member names, so end them where"
                + " the name goes. NN does not work here.",
                "Guild", new[]
            {
                new PoolMeta("ItemSearch", "Item questions",
                    "FRAGMENT -> \"<greeting> <line> <item name> drops?\" (\"anyone know where\")."
                    + " The game ALWAYS opens with a random greeting from the asking sim and that"
                    + " cannot be turned off, so write a fragment that reads naturally after a greeting."),
                new PoolMeta("NPCSearch", "NPC questions",
                    "FRAGMENT -> \"<greeting> <line> <npc name> spawns?\". Same forced greeting"
                    + " in front as item questions."),
                new PoolMeta("LevelAdvice", "Level questions",
                    "FRAGMENT -> \"<greeting> <line>  level N?\" (\"whats efficient at\"). Same"
                    + " forced greeting in front, and yes, the game really puts two spaces before"
                    + " \"level\"."),
                new PoolMeta("QuestAskItem", "Quest asks",
                    "FRAGMENT before the item: \"<line> <item>? <closer>. <sign-off>\""
                    + " (\"anyone have a spare\"). Quest asks get NO greeting in front."),
                new PoolMeta("QuestAskEnding", "Quest-ask closers",
                    "The clause after the \"?\" (\"i can trade for it\"). The game adds the period"
                    + " itself, so don't end these with one."),
                new PoolMeta("Signoff", "Sign-offs", "The closer after quest asks (\"thanks!!\")."),
                new PoolMeta("OutOfZoneAnswers", "Out-of-zone answers", "FRAGMENT -> \"<line> <zone name>\" (\"pretty sure that's over in\")."),
                new PoolMeta("LowLevelAnswers", "Too-low-to-know answers", "Full lines low-level sims answer gated topics with."),
                new PoolMeta("NewPlayerWelcome", "New-member welcomes", "FRAGMENT -> \"<line> <new member name>\"."),
                new PoolMeta("GuildDeletedResponses", "Disband reactions", "Whispers after you disband your guild."),
                new PoolMeta("GuildRemoveResponses", "Kick reactions", "Whispers after you kick a member."),
                new PoolMeta("InterestMSG", "Recruit interest", "Whispers after your recruit shouts (\"is the guild taking casuals?\")."),
                new PoolMeta("FCAntiRecruitMSG", "Rival anti-recruit taunts", "The rival guild mocking your recruiting."),
            }),

            new SectionMeta("TemplateLanguage", "Generic-sim voice",
                "The shared voice of every randomly generated sim - the 10 lists the game never refills."
                + " These ship TINY in vanilla (several have TWO lines), so additions here are the single"
                + " biggest repeat-killer in the game.",
                "Template", new[]
            {
                new PoolMeta("Aggro", "Combat panic", "Group-tell when a generic sim pulls a mob."),
                new PoolMeta("Exclamations", "Exclamations", "BARE WORDS - glued onto greetings with no space, spliced into banter. Highest-visibility list in the game."),
                new PoolMeta("Denials", "Refusals", "Full-line whisper refusals."),
                new PoolMeta("Negative", "No-answers", "\"No\" answer-shouts to other sims' questions."),
                new PoolMeta("LFGPublic", "LFG shouts", "FRAGMENT -> \"<line> <area name> you can lead.\""),
                new PoolMeta("Goodnight", "Goodnights", "Replies to goodnight shouts and guild goodnights."),
                new PoolMeta("LocalFriendHello", "Friend hellos", "Greeting a player they know (NN-safe)."),
                new PoolMeta("Affirms", "Agreements", "Guild-invite accepts and yes-shouts."),
                new PoolMeta("WantsDrop", "Loot requests", "II = the item's name."),
                new PoolMeta("ReturnToZone", "\"Back to...\" fragments", "FRAGMENT -> \"<line> <zone>! \" in memory greetings."),
            }),

            new SectionMeta("", "Names & bios",
                "Extra names for brand-new generated sims (letters/digits/hyphen only) and extra"
                + " inspect-window bios by personality flavor.",
                null, new[]
            {
                new PoolMeta("MaleNames", "Male names", "Avoid names in the female list - dual-listed names always resolve male.", true),
                new PoolMeta("FemaleNames", "Female names", "Avoid names in the male list.", true),
                new PoolMeta("NiceBios", "Nice bios", "Inspect-window bios for nice sims.", true),
                new PoolMeta("TryhardBios", "Tryhard bios", "Bios for tryhard sims.", true),
                new PoolMeta("MeanBios", "Mean bios", "Bios for mean sims.", true),
            }),
        };

        /// <summary>GlobalLanguage reuses the per-sim list metadata (they ARE
        /// the same lists, shared) + the global-only EnvDmg.</summary>
        private static PoolMeta[] BuildGlobalLanguage()
        {
            List<PoolMeta> pools = new List<PoolMeta>();
            foreach (ListMeta meta in SimListMeta.All)
            {
                pools.Add(new PoolMeta(meta.Key, meta.Title, meta.Guidance));
            }
            pools.Add(new PoolMeta("EnvDmg", "Environment yelps",
                "Sims yelp when the world hurts them - LIVE ONLY in this shared list."));
            return pools.ToArray();
        }

        internal static string ExampleFor(SectionMeta section, string key)
        {
            if (section.SizePrefix != null)
            {
                string[] found;
                if (RefGen.ExampleLines.TryGetValue(section.SizePrefix + "." + key, out found) && found.Length > 0)
                {
                    return found[0];
                }
            }
            return SimListMeta.ExampleFor(key);
        }

        internal static int PoolSize(SectionMeta section, string key)
        {
            if (section.SizePrefix == null)
            {
                // names/bios live on the manager
                string mapped = key == "NiceBios" ? "NiceDesciptions"
                    : key == "TryhardBios" ? "TryhardDescriptions"
                    : key == "MeanBios" ? "MeanDescriptions"
                    : key == "MaleNames" ? "NameDatabaseMale"
                    : key == "FemaleNames" ? "NameDatabaseFemale" : key;
                int n0;
                return RefGen.PoolSizes.TryGetValue("Manager." + mapped, out n0) ? n0 : 0;
            }
            int n;
            return RefGen.PoolSizes.TryGetValue(section.SizePrefix + "." + key, out n) ? n : 0;
        }
    }
}
