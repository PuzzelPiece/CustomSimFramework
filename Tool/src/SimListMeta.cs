using System;
using System.Collections.Generic;

namespace PackStudio
{
    internal enum ChatChannel { Say, Shout, Guild, Whisper }

    internal sealed class ListMeta
    {
        internal string Key;         // schema field name
        internal string Title;       // plain-language title
        internal string Guidance;    // one-sentence role + shape hint
        internal string Category;    // editor section header
        internal ChatChannel Channel = ChatChannel.Say;

        internal ListMeta(string key, string title, string category, ChatChannel channel, string guidance)
        {
            Key = key;
            Title = title;
            Category = category;
            Channel = channel;
            Guidance = guidance;
        }
    }

    /// <summary>Editor metadata for the 30 live per-sim dialogue lists
    /// (order = editor order, guidance wording follows PACK_TOOL_REFERENCE).</summary>
    internal static class SimListMeta
    {
        internal static readonly ListMeta[] All =
        {
            new ListMeta("Greetings", "Greetings", "Greetings & memory", ChatChannel.Shout,
                "The building block for greet shouts, guild question prefixes, hail replies and the"
                + " first hello of a session. NN becomes your name. Safe here, though strangers see"
                + " it stripped on shouts."),
            new ListMeta("ReturnGreeting", "Repeat hellos", "Greetings & memory", ChatChannel.Whisper,
                "The whisper greeting once you've already talked this session. NN is safe here."
                + " Rivals answer every greeting from this list, so write it hostile for a rival."),
            new ListMeta("LocalFriendHello", "Friend hellos", "Greetings & memory", ChatChannel.Say,
                "A greeting for a player they know. NN is safe here."),
            new ListMeta("BeenAWhile", "After days apart", "Greetings & memory", ChatChannel.Whisper,
                "The greeting after 4 or more days apart. NN is safe here, EXCEPT on Rival sims."
                + " A rival's whisper replies all come raw from this list, so for a rival write it"
                + " as taunts with no NN."),
            new ListMeta("ReturnToZone", "\"Let's go back to...\"", "Greetings & memory", ChatChannel.Whisper,
                "A FRAGMENT in the memory greeting. The zone name follows your line, as in"
                + " \"we should go back to\" plus the zone."),
            new ListMeta("GoodLastOuting", "Good-run memories", "Greetings & memory", ChatChannel.Whisper,
                "Spliced into the memory greeting after a good shared run. NN is safe here."),
            new ListMeta("BadLastOuting", "Bad-run memories", "Greetings & memory", ChatChannel.Whisper,
                "Spliced into the memory greeting after a rough shared run. NN is safe here."),
            new ListMeta("GotAnItemLastOuting", "Loot memories", "Greetings & memory", ChatChannel.Whisper,
                "A FRAGMENT. The remembered item's name follows your line, as in"
                + " \"and i still cherish my\" plus the item."),
            new ListMeta("Invites", "Zone invites", "Invites & grouping", ChatChannel.Whisper,
                "A FRAGMENT. The zone name follows, as in \"i'm over in\" plus the zone."
                + " End on words like in or at. NN is safe here."),
            new ListMeta("Justifications", "Invite reasons", "Invites & grouping", ChatChannel.Whisper,
                "A FRAGMENT that follows the zone name in invites, like \"if you want easy company\""
                + " coming after \"...Faerie's Brake\"."),
            new ListMeta("Confirms", "Yes-words", "Invites & grouping", ChatChannel.Say,
                "Short agreement lines. No NN."),
            new ListMeta("Affirms", "Agreements", "Invites & grouping", ChatChannel.Say,
                "Acknowledgements, used for guild invite accepts and yes-shouts. No NN."),
            new ListMeta("Denials", "Refusals", "Invites & grouping", ChatChannel.Whisper,
                "Full refusal lines like \"already asked someone else\". No NN."),
            new ListMeta("Negative", "No-answers", "Invites & grouping", ChatChannel.Shout,
                "Short \"no\" answer shouts to other sims' questions. No NN."),
            new ListMeta("DeclineGroup", "Declining groups", "Invites & grouping", ChatChannel.Shout,
                "Turning down group requests. Avoid NN. Shout paths replace it, but whisper replies"
                + " show it literally."),
            new ListMeta("OTW", "On my way", "Invites & grouping", ChatChannel.Shout,
                "Coming-to-help lines. Avoid NN, since whisper replies show it literally."),
            new ListMeta("Aggro", "Combat panic", "Combat", ChatChannel.Say,
                "The group-tell when a mob hits them mid-pull. Full lines, no NN."),
            new ListMeta("Died", "Death lines", "Combat", ChatChannel.Say,
                "The group or raid tell on death. Full lines, no NN."),
            new ListMeta("InsultsFun", "Banter jabs", "Banter", ChatChannel.Shout,
                "Renders as the target's name, an exclamation, then your jab. Write jabs addressed"
                + " at \"you\" that read right after a name. No NN."),
            new ListMeta("RetortsFun", "Comebacks", "Banter", ChatChannel.Shout,
                "Standalone comebacks that end a banter chain. No NN."),
            new ListMeta("Exclamations", "Exclamations", "Banter", ChatChannel.Shout,
                "BARE WORDS like \"lol\" or \"oof\". They get glued straight onto greetings with no"
                + " space, and spliced into banter. Keep them short and avoid NN."),
            new ListMeta("LFGPublic", "LFG shouts", "Public shouts", ChatChannel.Shout,
                "A FRAGMENT. The game appends the area name and \"you can lead.\" End on at or to."),
            new ListMeta("Goodnight", "Goodnights", "Public shouts", ChatChannel.Say,
                "The reply when YOU say goodnight (sims never log off themselves). NN is safe here"
                + " and always becomes your name."),
            new ListMeta("UnsureResponse", "Confusion", "Reactions", ChatChannel.Whisper,
                "The reply to whispers they don't understand. No NN."),
            new ListMeta("AngerResponse", "Hurt feelings", "Reactions", ChatChannel.Whisper,
                "The reaction to rudeness. No NN."),
            new ListMeta("AcknowledgeGratitude", "\"np\" replies", "Reactions", ChatChannel.Whisper,
                "Replies to your thanks. No NN."),
            new ListMeta("WantsDrop", "Loot requests", "Loot & gear", ChatChannel.Say,
                "II becomes the item's name, as in \"can i have the II\". This is the only list"
                + " where II works."),
            new ListMeta("Impressed", "Gear compliments", "Loot & gear", ChatChannel.Say,
                "A FRAGMENT that sandwiches a gear word, like \"whoa that's a really nice\" cape"
                + " and then a closer. End before a noun."),
            new ListMeta("ImpressedEnd", "Compliment closers", "Loot & gear", ChatChannel.Say,
                "A FRAGMENT that closes the gear compliment after the gear word."),
            new ListMeta("LevelUpCelebration", "Ding grats", "Loot & gear", ChatChannel.Guild,
                "Congratulating level-ups. Avoid NN (guild waves substitute it, shouts show it literally)."),
        };

        // Non-sim surfaces that still need a chat channel / placeholder.
        internal static readonly ListMeta[] Extra =
        {
            new ListMeta("ZoneComments", "Ambient chatter", "Zone Chatter", ChatChannel.Shout,
                "Full-line ambient shouts sims make while idling in this zone."),
            new ListMeta("AllZones", "Everywhere lines", "Zone Chatter", ChatChannel.Shout,
                "Ambient shouts added to EVERY zone's pool."),
            new ListMeta("SimPlayerActivations", "Conversation openers", "Guild Topics", ChatChannel.Guild,
                "Sims post these in guild chat to start the topic, roughly every few minutes when"
                + " the dice allow."),
            new ListMeta("Responses", "Responses", "Guild Topics", ChatChannel.Guild,
                "The on-topic replies that 2 to 4 responders answer with. REQUIRED, and 3 or more"
                + " lines are recommended."),
            new ListMeta("Preceed", "Response openers", "Guild Topics", ChatChannel.Guild,
                "An optional wrapper spoken before a response. Sim-started conversations only."),
            new ListMeta("End", "Response closers", "Guild Topics", ChatChannel.Guild,
                "An optional wrapper spoken after a response. Sim-started conversations only."),
        };

        internal static ListMeta Get(string key)
        {
            foreach (ListMeta meta in All)
            {
                if (meta.Key == key) { return meta; }
            }
            foreach (ListMeta meta in Extra)
            {
                if (meta.Key == key) { return meta; }
            }
            return null;
        }

        // Hand-picked placeholders for lists whose dump sources are empty or
        // unhelpful. Everything else resolves from RefGen.ExampleLines.
        private static readonly Dictionary<string, string> HandExamples = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ReturnToZone", "we should go back to" },
            { "GotAnItemLastOuting", "i still cherish my" },
            { "Justifications", "if you want easy company" },
            { "Impressed", "whoa that's a really nice" },
            { "ImpressedEnd", "how do i get one of those" },
            { "LFGPublic", "group forming at" },
            { "WantsDrop", "can i have the II if nobody wants it" },
            { "ZoneComments", "anyone else just vibing today" },
            { "AllZones", "anyone else just vibing today" },
            { "MaleNames", "Borvik" },
            { "FemaleNames", "Maplewood" },
            { "NiceBios", "I wave at every player I pass." },
            { "TryhardBios", "My gear score is a lifestyle." },
            { "MeanBios", "Yes I ninja'd it. No I don't feel bad." },
            { "SimPlayerActivations", "Does anyone else think Tinkles holds a grudge?" },
            { "ActivationWords", "tinkles grudge" },
            { "Responses", "She remembers. Faeries always remember." },
            { "Preceed", "ok so" },
            { "End", "anyway, watch your back out there." },
            { "RelevantScene", "Abyssal Lake" },
        };

        /// <summary>Ghost-placeholder example for one list (display-only).</summary>
        internal static string ExampleFor(string listKey)
        {
            string hand;
            if (HandExamples.TryGetValue(listKey, out hand)) { return hand; }
            string[] found;
            if (RefGen.ExampleLines.TryGetValue("Global." + listKey, out found) && found.Length > 0)
            {
                return found[0];
            }
            string pool;
            if (RefData.ManagerPoolFillMap.TryGetValue(listKey, out pool)
                && RefGen.ExampleLines.TryGetValue("Manager." + pool, out found) && found.Length > 0)
            {
                return found[0];
            }
            if (RefGen.ExampleLines.TryGetValue("Template." + listKey, out found) && found.Length > 0)
            {
                return found[0];
            }
            return "type a line...";
        }

        /// <summary>Vanilla pool size for the dilution note (0 if unknown).</summary>
        internal static int VanillaPoolSize(string listKey)
        {
            int n;
            if (RefGen.PoolSizes.TryGetValue("Global." + listKey, out n)) { return n; }
            return 0;
        }
    }
}
