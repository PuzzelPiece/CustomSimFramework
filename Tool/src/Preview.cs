using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PackStudio
{
    internal sealed class PreviewQuirks
    {
        internal bool AllCaps;
        internal bool AllLowers;
        internal bool ThirdPerson;
        internal string RefersToSelfAs = "";
        internal bool LovesEmojis;
        internal double TypoRate;
        internal List<string> SignOffLines = new List<string>();
        internal string SimName = "Sim";

        internal static PreviewQuirks Of(SimFile sim)
        {
            return new PreviewQuirks
            {
                AllCaps = sim.GetBool("TypesInAllCaps", false),
                AllLowers = sim.GetBool("TypesInAllLowers", false),
                ThirdPerson = sim.GetBool("TypesInThirdPerson", false),
                RefersToSelfAs = sim.GetString("RefersToSelfAs", "").Trim(),
                LovesEmojis = sim.GetBool("LovesEmojis", false),
                TypoRate = sim.GetDouble("TypoRate", 0),
                SignOffLines = sim.GetLines("SignOffLines"),
                SimName = sim.SimName,
            };
        }
    }

    /// <summary>
    /// Render engine: an EXACT port of the game's PersonalizeString
    /// (SimPlayerMngr.cs:3597-3674) plus the composition templates. The port
    /// deliberately reproduces the game's quirks, including the
    /// RefersToSelfAs block applying bare-I BEFORE its contraction rules
    /// (making those rules dead code), proven 2026-07-20. The preview must
    /// show the truth, not the idealized model.
    /// </summary>
    internal static class Preview
    {
        /// <summary>Deterministic render: no typo/sign-off/emoji dice (pass a
        /// Random to roll them like the game does).</summary>
        internal static string Personalize(string text, PreviewQuirks q, Random dice)
        {
            if (q == null || string.IsNullOrEmpty(text))
            {
                return string.IsNullOrEmpty(text) ? "lol" : text;
            }
            if (q.ThirdPerson)
            {
                string n = q.SimName;
                text = RepI(text, "\\bI'm\\b", n + " is");
                text = RepI(text, "\\bI’m\\b", n + " is");
                text = RepI(text, "\\bI’d\\b", n + " would");
                text = RepI(text, "\\bI’ll\\b", n + " will");
                text = RepI(text, "\\bI’ve\\b", n + " has");
                text = RepI(text, "\\bI\\b", n);
                text = RepI(text, "\\bme\\b", n);
                text = RepI(text, "\\bmy\\b", n + "'s");
                text = RepI(text, "\\bmine\\b", n + "'s");
                text = RepI(text, "\\bmyself\\b", "themselves");
            }
            if (!string.IsNullOrEmpty(q.RefersToSelfAs))
            {
                string r = q.RefersToSelfAs;
                // Game order: bare I first. The contraction rules below it
                // can never fire. Kept verbatim for fidelity.
                text = RepI(text, "\\bI\\b", r);
                text = RepI(text, "\\bme\\b", r);
                text = RepI(text, "\\bmy\\b", r + "'s");
                text = RepI(text, "\\bmine\\b", r + "'s");
                text = RepI(text, "\\bmyself\\b", "themselves");
                text = RepI(text, "\\bI'm\\b", r + " is");
                text = RepI(text, "\\bI’m\\b", r + " is");
                text = RepI(text, "\\bI’d\\b", r + " would");
                text = RepI(text, "\\bI’ll\\b", r + " will");
                text = RepI(text, "\\bI’ve\\b", r + " has");
            }
            if (dice != null && q.SignOffLines.Count > 0 && dice.Next(0, 10) == 0)
            {
                text = text + " " + q.SignOffLines[dice.Next(0, q.SignOffLines.Count)];
            }
            if (dice != null && q.TypoRate > 0)
            {
                string[] words = text.Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    if (dice.NextDouble() * 150.0 < q.TypoRate)
                    {
                        words[i] = ApplyRandomTypo(words[i], dice);
                    }
                }
                text = string.Join(" ", words);
            }
            if (q.AllCaps) { text = text.ToUpperInvariant(); }
            if (q.AllLowers) { text = text.ToLowerInvariant(); }
            if (dice != null && q.LovesEmojis && RefGen.Emojis.Length > 0)
            {
                text = Regex.Replace(text, "([.!?])", delegate(Match m)
                {
                    if (dice.Next(0, 10) == 0)
                    {
                        return m.Value + " " + RefGen.Emojis[dice.Next(0, RefGen.Emojis.Length)];
                    }
                    return m.Value;
                });
            }
            return text;
        }

        private static string RepI(string text, string pattern, string replacement)
        {
            return Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
        }

        private static string ApplyRandomTypo(string word, Random dice)
        {
            if (string.IsNullOrEmpty(word)) { return word; }
            foreach (char c in word)
            {
                if (char.IsDigit(c)) { return word; }
            }
            string tail = "";
            while (word.Length > 0 && char.IsPunctuation(word[word.Length - 1]))
            {
                tail = word[word.Length - 1] + tail;
                word = word.Substring(0, word.Length - 1);
            }
            if (word.Length <= 1) { return word + tail; }
            switch (dice.Next(0, 3))
            {
                case 0:
                {
                    int i = dice.Next(0, word.Length - 1);
                    char[] chars = word.ToCharArray();
                    char t = chars[i];
                    chars[i] = chars[i + 1];
                    chars[i + 1] = t;
                    word = new string(chars);
                    break;
                }
                case 1:
                    word = word.Remove(dice.Next(0, word.Length), 1);
                    break;
                default:
                    word = word.Insert(dice.Next(0, word.Length + 1), ((char)dice.Next(97, 123)).ToString());
                    break;
            }
            return word + tail;
        }

        // ── Composition templates ───────────────────────────────────
        // Sample context values used by the preview pane.
        internal const string SamplePlayer = "Alden";
        internal const string SampleItem = "Funeral Shroud";
        internal const string SampleNpc = "Overseer Orlok";
        internal const string SampleZoneDisplay = "Faerie's Brake";
        internal const string SampleTarget = "A Brittle Skeleton";
        internal const string SampleGreeting = "hey hey";
        internal const string SampleTopicResponse = "The ripples are too big for fish. Just saying.";
        // Vanilla-flavored filler for the guild quest-ask sentence
        // (GuildManager.cs:1491: "<ask> <item>? <ending>. <signoff>").
        internal const string SampleQuestAsk = "Hey guys, anyone up for helping me get";
        internal const string SampleQuestEnding = "I have all afternoon";
        internal const string SampleQuestSignoff = "Just hit me up in a whisper.";
        internal const string SampleLevel = "12";

        /// <summary>
        /// "In-game this can look like:" examples for one line of one list.
        /// Labeled (label, rendered) pairs, quirks applied.
        /// </summary>
        internal static List<KeyValuePair<string, string>> RenderExamples(string listName, string line, PreviewQuirks q)
        {
            List<KeyValuePair<string, string>> output = new List<KeyValuePair<string, string>>();
            switch (listName)
            {
                case "Greetings":
                    Example(output, q, "hail reply (always your name)", NN(line, SamplePlayer));
                    Example(output, q, "shout-greet to a stranger", NN(line, "").Trim());
                    Example(output, q, "guild question prefix (NN stripped)",
                        NN(line, "").Trim() + " anyone know where " + SampleItem + " drops?");
                    break;
                case "ReturnGreeting":
                case "Goodnight":
                case "LocalFriendHello":
                case "BeenAWhile":
                    Example(output, q, "to you", NN(line, SamplePlayer));
                    break;
                case "Invites":
                    Example(output, q, "zone invite whisper",
                        NN(line, SamplePlayer) + " " + SampleZoneDisplay + " if you want easy company");
                    break;
                case "Justifications":
                    Example(output, q, "after the zone name",
                        "hey i'm over in " + SampleZoneDisplay + " " + line);
                    break;
                case "ReturnToZone":
                    Example(output, q, "memory greeting",
                        SampleGreeting + "! " + NN(line, SamplePlayer) + " " + SampleZoneDisplay + "! ");
                    break;
                case "GotAnItemLastOuting":
                    Example(output, q, "memory greeting",
                        NN(line, SamplePlayer) + " " + SampleItem + ".");
                    break;
                case "LFGPublic":
                    Example(output, q, "LFG shout",
                        line + " " + SampleZoneDisplay + " you can lead.");
                    break;
                case "WantsDrop":
                    Example(output, q, "loot request",
                        Regex.Replace(line, "\\bII\\b", SampleItem));
                    break;
                case "Impressed":
                    Example(output, q, "gear compliment", line + " cape " + "how do i get one of those");
                    break;
                case "InsultsFun":
                    Example(output, q, "banter opener", SampleGreeting + " Vorn lol " + line);
                    Example(output, q, "call-out reply", SamplePlayer + " " + line);
                    break;
                case "Exclamations":
                    Example(output, q, "glued to a greeting (no space!)", SampleGreeting + line);
                    Example(output, q, "banter splice", SampleGreeting + " Vorn " + line + " you fight like a merchant");
                    break;
                case "LevelUpCelebration":
                    Example(output, q, "guild ding wave (name substituted or dropped)", NN(line, SamplePlayer));
                    Example(output, q, "shout ding (NN literal!)", line);
                    break;
                case "SimPlayerActivations":
                    Example(output, q, "posted in guild chat on the chat timer", line);
                    break;
                case "Responses":
                    Example(output, q, "reply to a player trigger (raw)", line);
                    Example(output, q, "in a sim-started conversation (may be wrapped)",
                        "ok so " + line);
                    break;
                case "Preceed":
                    Example(output, q, "wrapping a response (sim-started only)",
                        line + " " + SampleTopicResponse);
                    break;
                case "End":
                    Example(output, q, "closing a response (sim-started only)",
                        SampleTopicResponse + " " + line);
                    break;
                case "SignOffLines":
                    Example(output, q, "appended to the end of a message (~10% chance)",
                        "omw, save me a spot " + line);
                    Example(output, q, "on a whisper reply",
                        "np, that's what groups are for " + line);
                    break;
                // ── Guild chat compositions (GuildManager.cs:1491,1608-1630,706,742,2216) ──
                case "ItemSearch":
                    Example(output, q, "guild question - the asker's greeting is ALWAYS put in front",
                        SampleGreeting + " " + line + " " + SampleItem + " drops?");
                    break;
                case "NPCSearch":
                    Example(output, q, "guild question - the asker's greeting is ALWAYS put in front",
                        SampleGreeting + " " + line + " " + SampleNpc + " spawns?");
                    break;
                case "LevelAdvice":
                    Example(output, q, "guild question - greeting in front, and the game's own double space",
                        SampleGreeting + " " + line + "  level " + SampleLevel + "?");
                    break;
                case "QuestAskItem":
                    Example(output, q, "guild quest ask (these get no greeting)",
                        line + " " + SampleItem + "? " + SampleQuestEnding + ". " + SampleQuestSignoff);
                    break;
                case "QuestAskEnding":
                    Example(output, q, "middle of the quest ask - the game adds the period itself",
                        SampleQuestAsk + " " + SampleItem + "? " + line + ". " + SampleQuestSignoff);
                    break;
                case "Signoff":
                    Example(output, q, "closing the quest ask",
                        SampleQuestAsk + " " + SampleItem + "? " + SampleQuestEnding + ". " + line);
                    break;
                case "OutOfZoneAnswers":
                    Example(output, q, "the topic's home zone is appended",
                        line + " " + SampleZoneDisplay);
                    break;
                case "NewPlayerWelcome":
                    Example(output, q, "the new member's name is appended",
                        line + " " + SamplePlayer);
                    break;
                case "InterestMSG":
                    Example(output, q, "whisper after your recruit shout", line);
                    Example(output, q, "if they are already grouped with you",
                        line + " I'm right here in your group!");
                    break;
                // ── Party / manager / memory compositions ──
                case "Targeting":
                    Example(output, q, "the target's name is appended",
                        line + " " + SampleTarget);
                    break;
                case "InviteEnd":
                    Example(output, q, "after the zone name in a sim's invite",
                        "hey i'm over in " + SampleZoneDisplay + " " + line);
                    break;
                case "ImpressedEnd":
                    Example(output, q, "closing a gear compliment",
                        "whoa that's a really nice cape " + line);
                    break;
                case "LevelUpCongratulations":
                    Example(output, q, "reply to your /say ding (read straight from this pool)", line);
                    Example(output, q, "seeds sims' grats (guild wave uses the name, shout dings leave NN literal)",
                        NN(line, SamplePlayer));
                    break;
                case "GoodLastOuting":
                    Example(output, q, "inside the memory greeting (a good run remembered)",
                        SampleGreeting + "! " + NN(line, SamplePlayer));
                    break;
                case "BadLastOuting":
                    Example(output, q, "inside the memory greeting - the game adds the period itself",
                        SampleGreeting + "! " + NN(line, SamplePlayer) + ".");
                    break;
                default:
                    Example(output, q, "as spoken", line);
                    break;
            }
            return output;
        }

        private static void Example(List<KeyValuePair<string, string>> output, PreviewQuirks q, string label, string text)
        {
            output.Add(new KeyValuePair<string, string>(label, Personalize(text, q, null)));
        }

        private static string NN(string line, string playerName)
        {
            return Regex.Replace(line, "\\bNN\\b", playerName);
        }
    }
}
