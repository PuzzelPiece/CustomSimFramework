using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PackStudio
{
    internal enum Severity { Blocker, Warning, Tip }

    internal sealed class Finding
    {
        internal Severity Severity;
        internal string File;     // e.g. "Paul.sim.json"
        internal string Where;    // e.g. "Greetings, line 2" / "topic 'TinklesGrudge'"
        internal string Message;

        public override string ToString()
        {
            string tag = Severity == Severity.Blocker ? "BLOCKER" : (Severity == Severity.Warning ? "warning" : "tip");
            return "[" + tag + "] " + File + " (" + Where + "): " + Message;
        }
    }

    /// <summary>
    /// The rule engine. Pure functions over the pack model; message wording
    /// mirrors the mod loader's actual log lines where an equivalent exists
    /// (PACK_TOOL_REFERENCE.md section 13), so the tool and the game agree.
    /// </summary>
    internal static class Validation
    {
        private static readonly Regex NameRegex = new Regex(RefData.NamePattern, RegexOptions.Compiled);

        internal static List<Finding> ValidatePack(Pack pack)
        {
            List<Finding> findings = new List<Finding>();
            foreach (PackJsonFile file in pack.AllFiles())
            {
                if (file.LoadError != null)
                {
                    Add(findings, Severity.Blocker, file.FileName, "file",
                        "could not be parsed as JSON: " + file.LoadError);
                }
            }
            foreach (PackJsonFile file in pack.AllFiles())
            {
                CheckLineBreaks(file, findings);
            }
            ValidateSims(pack, findings);
            if (pack.Global != null) { ValidateGlobal(pack.Global, findings); }
            if (pack.Zones != null) { ValidateZones(pack.Zones, findings); }
            if (pack.Topics != null) { ValidateTopics(pack, findings); }
            return findings;
        }

        private static void Add(List<Finding> list, Severity severity, string file, string where, string message)
        {
            list.Add(new Finding { Severity = severity, File = file, Where = where, Message = message });
        }

        /// <summary>
        /// A dialogue line containing an embedded line break makes the sim
        /// shout the whole block as ONE message (the vanilla "paste blob" bug,
        /// TESTING.md #9). Checks every string inside every array; root-level
        /// strings (Bio, where \n is legitimate) and "_" comment keys are
        /// exempt by construction.
        /// </summary>
        private static void CheckLineBreaks(PackJsonFile file, List<Finding> findings)
        {
            WalkArrayStrings(file.Root, "", delegate(string where, string value)
            {
                if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                {
                    Add(findings, Severity.Warning, file.FileName, where,
                        "contains an embedded line break - the sim would shout the whole block as one"
                        + " message (the vanilla \"paste blob\" bug). Split it into separate lines.");
                }
            });
        }

        private static void WalkArrayStrings(JNode node, string path, Action<string, string> visit)
        {
            JObj obj = node as JObj;
            if (obj != null)
            {
                foreach (string key in obj.Keys)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) { continue; }
                    WalkArrayStrings(obj[key], path.Length == 0 ? key : path + "." + key, visit);
                }
                return;
            }
            JArr arr = node as JArr;
            if (arr != null)
            {
                for (int i = 0; i < arr.Items.Count; i++)
                {
                    JStr str = arr.Items[i] as JStr;
                    if (str != null)
                    {
                        visit(path + ", line " + (i + 1), str.Value);
                    }
                    else
                    {
                        WalkArrayStrings(arr.Items[i], path + " entry " + (i + 1), visit);
                    }
                }
            }
        }

        // ── Sims ────────────────────────────────────────────────────

        private static void ValidateSims(Pack pack, List<Finding> findings)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SimFile sim in pack.Sims)
            {
                string f = sim.FileName;
                string name = sim.GetString("Name", "").Trim();
                if (name.Length == 0)
                {
                    Add(findings, Severity.Blocker, f, "Name", "has no Name - the game would skip this sim.");
                }
                else if (!NameRegex.IsMatch(name))
                {
                    Add(findings, Severity.Blocker, f, "Name", "'" + name
                        + "' contains unsupported characters (allowed: A-Z, a-z, 0-9, hyphen; no spaces).");
                }
                else if (!seen.Add(name))
                {
                    Add(findings, Severity.Blocker, f, "Name", "duplicate sim name '" + name
                        + "' - the game keeps only the first.");
                }

                string gender = sim.GetString("Gender", "Male").Trim();
                if (!gender.Equals("Male", StringComparison.OrdinalIgnoreCase)
                    && !gender.Equals("Female", StringComparison.OrdinalIgnoreCase))
                {
                    Add(findings, Severity.Warning, f, "Gender", "'" + gender
                        + "' is not 'Male' or 'Female' - the game would default to Male.");
                }

                string cls = sim.GetString("Class", "Duelist").Trim();
                bool classOk = false;
                foreach (string known in RefData.Classes)
                {
                    if (known.Equals(cls, StringComparison.OrdinalIgnoreCase)) { classOk = true; }
                }
                if (!classOk)
                {
                    Add(findings, Severity.Warning, f, "Class", "unknown class '" + cls
                        + "' - the game would default to Duelist.");
                }

                int level = sim.GetInt("Level", 5);
                if (level < 1 || level > 35)
                {
                    Add(findings, Severity.Warning, f, "Level", level
                        + " is outside 1-35 - the game clamps to that range.");
                }

                int slot = sim.GetInt("TiedToSlot", -1);
                bool rival = sim.GetBool("Rival", false);
                if (slot != -1 && (slot < 0 || slot > 10) && slot != 12 && slot != 99)
                {
                    Add(findings, Severity.Blocker, f, "TiedToSlot", slot
                        + " is not valid (-1, 0-10, 12 or 99).");
                }
                else if (slot != -1 && rival)
                {
                    Add(findings, Severity.Warning, f, "TiedToSlot",
                        "ignored for Rival sims (the game forces rivals to 99).");
                }

                int pt = sim.GetInt("PersonalityType", 0);
                if (pt < 0 || pt > 5)
                {
                    Add(findings, Severity.Warning, f, "PersonalityType", pt + " is outside 0-5.");
                }

                string hair = sim.GetString("HairName", "");
                if (hair.Length > 0 && !RefData.IsValidHairName(hair))
                {
                    Add(findings, Severity.Warning, f, "HairName", "'" + hair
                        + "' is not a real style (valid: Chr_Hair_01..38 except 03) - the game would reroll it.");
                }
                CheckIndex(findings, f, "HairColorIndex", sim.GetInt("HairColorIndex", -1), RefGen.HairColors.Length);
                CheckIndex(findings, f, "SkinColorIndex", sim.GetInt("SkinColorIndex", -1), RefGen.SkinColors.Length);

                double typo = sim.GetDouble("TypoRate", 0);
                if (typo > 10)
                {
                    Add(findings, Severity.Tip, f, "TypoRate", typo
                        + " is beyond the vanilla range (0-10) - text gets very mangled.");
                }

                foreach (string dial in RefData.DeadSimDials)
                {
                    if (sim.Root.ContainsKey(dial))
                    {
                        Add(findings, Severity.Warning, f, dial,
                            "is never read by the game (dead dial) - remove it.");
                    }
                }

                Quirks quirks = Quirks.Of(sim);
                foreach (string key in sim.Root.Keys)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) { continue; }
                    if (Array.IndexOf(RefData.SimKeys, key) < 0)
                    {
                        Add(findings, Severity.Warning, f, key,
                            "unknown key - ignored by the game (typo?).");
                    }
                }

                foreach (string list in RefData.SimListNames)
                {
                    ValidateSimList(sim, list, quirks, rival, findings);
                }
                foreach (string dead in RefData.DeadSimLists)
                {
                    if (sim.GetLines(dead).Count > 0)
                    {
                        Add(findings, Severity.Warning, f, dead,
                            "is never read by the game for individual sims - these lines will not appear.");
                    }
                }
                // SignOffLines ride PersonalizeString output; check quirk hazards.
                CheckLineListQuirks(findings, f, "SignOffLines", sim.GetLines("SignOffLines"), quirks);

                if (rival && sim.GetLines("BeenAWhile").Count == 0)
                {
                    Add(findings, Severity.Tip, f, "BeenAWhile",
                        "a Rival answers EVERY recognized whisper from BeenAWhile - without your own lines"
                        + " they draw from the shared pool (which is written friendly).");
                }
                if (rival)
                {
                    // Rival-gated replies that DON'T come from BeenAWhile
                    // (verified: greeting whispers use ReturnGreeting,
                    // SimPlayerMngr.cs:2401). Unspecified lists auto-fill
                    // with FRIENDLY shared lines, so a rival greets warmly.
                    List<string> friendlyFilled = new List<string>();
                    foreach (string social in new[] { "ReturnGreeting", "LocalFriendHello", "Goodnight", "AcknowledgeGratitude", "UnsureResponse", "AngerResponse" })
                    {
                        if (sim.GetLines(social).Count == 0) { friendlyFilled.Add(social); }
                    }
                    if (friendlyFilled.Count > 0)
                    {
                        Add(findings, Severity.Tip, f, string.Join("/", friendlyFilled.ToArray()),
                            "left empty on a RIVAL these auto-fill with cheerful shared lines - the rival"
                            + " will greet and thank players warmly. Write antagonistic ones.");
                    }
                }
            }
        }

        private static void CheckIndex(List<Finding> findings, string file, string key, int value, int count)
        {
            if (value != -1 && (value < 0 || value >= count))
            {
                Add(findings, Severity.Warning, file, key, value + " is out of range (0-"
                    + (count - 1) + ", or -1 for random) - the game would clamp it.");
            }
        }

        private static void ValidateSimList(SimFile sim, string list, Quirks quirks, bool rival, List<Finding> findings)
        {
            List<string> lines = sim.GetLines(list);
            if (lines.Count == 0) { return; }
            string f = sim.FileName;
            NnTier tier = RefData.NnTierOf(list);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string where = list + ", line " + (i + 1);
                if (string.IsNullOrWhiteSpace(line))
                {
                    Add(findings, Severity.Warning, f, where,
                        "empty line - the game would render it as \"lol\" (the loader strips these).");
                    continue;
                }
                if (Regex.IsMatch(line, "\\bNN\\b"))
                {
                    if (tier == NnTier.Mixed)
                    {
                        Add(findings, Severity.Warning, f, where,
                            "NN sometimes works here but often shows literally or vanishes - avoid it in " + list + ".");
                    }
                    else if (tier == NnTier.No)
                    {
                        Add(findings, Severity.Warning, f, where,
                            "NN is never replaced in " + list + " - it would show literally as \"NN\".");
                    }
                    else if (rival && list == "BeenAWhile")
                    {
                        Add(findings, Severity.Warning, f, where,
                            "for a RIVAL, BeenAWhile lines answer whispers raw - NN shows literally there.");
                    }
                }
                if (line.Contains("II") && list != "WantsDrop" && Regex.IsMatch(line, "\\bII\\b"))
                {
                    Add(findings, Severity.Warning, f, where,
                        "II is only replaced in WantsDrop lines - it would show literally here.");
                }
            }
            CheckLineListQuirks(findings, f, list, lines, quirks);
        }

        // ── Quirk-aware contraction / conjugation rules (2026-07-20) ──

        internal sealed class Quirks
        {
            internal bool ThirdPerson;
            internal string RefersToSelfAs = "";

            internal static Quirks Of(SimFile sim)
            {
                return new Quirks
                {
                    ThirdPerson = sim.GetBool("TypesInThirdPerson", false),
                    RefersToSelfAs = sim.GetString("RefersToSelfAs", "").Trim(),
                };
            }
        }

        private static readonly Regex AnyFirstPersonContraction =
            new Regex("\\bI[’'](m|d|ll|ve)\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AsciiHardContraction =
            new Regex("\\bI'(d|ll|ve)\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static void CheckLineListQuirks(List<Finding> findings, string file, string list,
            List<string> lines, Quirks quirks)
        {
            if (quirks == null || (!quirks.ThirdPerson && quirks.RefersToSelfAs.Length == 0))
            {
                return;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) { continue; }
                string where = list + ", line " + (i + 1);
                if (quirks.RefersToSelfAs.Length > 0 && AnyFirstPersonContraction.IsMatch(line))
                {
                    Add(findings, Severity.Warning, file, where,
                        "RefersToSelfAs sims convert NO first-person contraction (the bare-I rule runs first):"
                        + " this renders like \"" + quirks.RefersToSelfAs + "'m\". Write it contraction-free.");
                }
                else if (quirks.ThirdPerson && AsciiHardContraction.IsMatch(line))
                {
                    Add(findings, Severity.Warning, file, where,
                        "third-person sims garble ASCII I'd/I'll/I've (only I'm and curly forms convert)"
                        + " - write \"I would\"/\"I will\"/\"I have\".");
                }
            }
        }

        // ── global.dialogue.json ───────────────────────────────────

        private static void ValidateGlobal(PackJsonFile global, List<Finding> findings)
        {
            string f = global.FileName;
            JObj mgr = global.GetSection("ManagerPools");
            if (mgr != null)
            {
                foreach (string pool in RefData.DeadManagerPools)
                {
                    if (PackJsonFile.LinesOf(mgr[pool] as JArr).Count > 0)
                    {
                        Add(findings, Severity.Warning, f, "ManagerPools." + pool,
                            "is never read by the game - these lines will not appear.");
                    }
                }
                ValidateListenPools(mgr, f, findings);
            }
            JObj lang = global.GetSection("GlobalLanguage");
            if (lang != null)
            {
                foreach (string pool in RefData.DeadGlobalLangLists)
                {
                    if (PackJsonFile.LinesOf(lang[pool] as JArr).Count > 0)
                    {
                        Add(findings, Severity.Warning, f, "GlobalLanguage." + pool,
                            "is dead even on the global component - these lines will not appear.");
                    }
                }
            }
            ValidateNameLists(global, findings);
            CheckSharedSurfaceContractions(global, findings);
        }

        /// <summary>
        /// LISTEN-pool checks: edge punctuation (can never word-boundary
        /// match), single bare words (intercept far too much), and
        /// interception: an EARLIER pool's line appearing word-boundary
        /// inside this line means typing it triggers the earlier pool
        /// instead. Checked for both parse orders against vanilla contents
        /// (RefData.Generated) merged with the pack's own earlier additions.
        /// </summary>
        private static void ValidateListenPools(JObj mgr, string file, List<Finding> findings)
        {
            foreach (string pool in RefData.ListenManagerPools)
            {
                List<string> lines = PackJsonFile.LinesOf(mgr[pool] as JArr);
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0) { continue; }
                    string where = "ManagerPools." + pool + ", line " + (i + 1);
                    if (!char.IsLetterOrDigit(line[0]) || !char.IsLetterOrDigit(line[line.Length - 1]))
                    {
                        Add(findings, Severity.Warning, file, where, "\"" + line
                            + "\" starts/ends with punctuation - the game's word-boundary matching can"
                            + " never match it at message edges (write \"wanna duo\", not \"wanna duo?\").");
                    }
                    if (line.Trim().IndexOf(' ') < 0)
                    {
                        // Severity depends on parse position: pools checked
                        // EARLY (or keyword-shaped ones) eat later categories
                        // with a bare word; the wave/greeting family is
                        // checked last (or lives on the shout path only), so
                        // a single word there just extends recognition -
                        // vanilla's own greeting/goodnight/LFG pools are full
                        // of single words.
                        bool lateOrWavePool = pool == "GenericGreeting" || pool == "Goodnight"
                            || pool == "LFGs" || pool == "LevelUpCelebrations";
                        Add(findings, lateOrWavePool ? Severity.Tip : Severity.Warning, file, where, "\"" + line
                            + "\" is a single word - single-word listen lines intercept far too much"
                            + " (including quest-NPC keywords). Use 2+ words."
                            + (lateOrWavePool ? " (This pool is checked last, so the risk is low here.)" : ""));
                    }
                    CheckInterception(mgr, pool, line, RefData.WhisperParseOrder, "whisper", file, where, findings);
                    CheckInterception(mgr, pool, line, RefData.SayParseOrder, "say", file, where, findings);
                }
            }
        }

        private static void CheckInterception(JObj mgr, string pool, string line, string[] order,
            string orderName, string file, string where, List<Finding> findings)
        {
            int poolIndex = Array.IndexOf(order, pool);
            if (poolIndex < 0) { return; } // pool not on this path
            for (int i = 0; i < poolIndex; i++)
            {
                string earlier = order[i];
                List<string> merged = new List<string>();
                string[] vanilla;
                if (RefGen.VanillaListen.TryGetValue(earlier, out vanilla)) { merged.AddRange(vanilla); }
                merged.AddRange(PackJsonFile.LinesOf(mgr[earlier] as JArr));
                foreach (string candidate in merged)
                {
                    if (candidate.Length == 0) { continue; }
                    if (Regex.IsMatch(line, "\\b" + Regex.Escape(candidate.ToLowerInvariant()) + "\\b",
                        RegexOptions.IgnoreCase))
                    {
                        Severity severity = orderName == "whisper" ? Severity.Warning : Severity.Tip;
                        Add(findings, severity, file, where, "typing \"" + line + "\" would trigger the earlier "
                            + earlier + " pool first on the " + orderName + " path (its line \"" + candidate
                            + "\" appears inside yours)" + (orderName == "say"
                                ? " - vanilla has the same quirk; the line still works via whisper." : "."));
                        return; // one interception per line/order is enough noise
                    }
                }
            }
        }

        private static void ValidateNameLists(PackJsonFile global, List<Finding> findings)
        {
            string f = global.FileName;
            List<string> male = global.GetLines("MaleNames");
            List<string> female = global.GetLines("FemaleNames");
            foreach (string listName in new[] { "MaleNames", "FemaleNames" })
            {
                List<string> names = listName == "MaleNames" ? male : female;
                for (int i = 0; i < names.Count; i++)
                {
                    if (!NameRegex.IsMatch(names[i]))
                    {
                        Add(findings, Severity.Warning, f, listName + ", line " + (i + 1), "\"" + names[i]
                            + "\" has unsupported characters (names become save filenames and whisper"
                            + " targets) - the loader removes it.");
                    }
                    for (int j = i + 1; j < names.Count; j++)
                    {
                        if (names[i].Equals(names[j], StringComparison.OrdinalIgnoreCase))
                        {
                            Add(findings, Severity.Warning, f, listName, "\"" + names[i] + "\" and \"" + names[j]
                                + "\" duplicate or differ only by casing - two minted sims would share one save file.");
                        }
                    }
                }
            }
            foreach (string m in male)
            {
                foreach (string fem in female)
                {
                    if (m.Equals(fem, StringComparison.OrdinalIgnoreCase))
                    {
                        Add(findings, Severity.Warning, f, "MaleNames/FemaleNames", "\"" + m
                            + "\" appears in both lists - sims with this name will ALWAYS resolve male.");
                    }
                }
            }
        }

        /// <summary>
        /// Shared pools are spoken by arbitrary sims, including quirked ones -
        /// a first-person contraction in them garbles for RefersToSelfAs sims
        /// (and ASCII I'd/I'll/I've for the third-person premade). Tip-level:
        /// vanilla's own pools carry the same class of line.
        /// </summary>
        private static void CheckSharedSurfaceContractions(PackJsonFile file, List<Finding> findings)
        {
            foreach (string sectionName in new[] { "ManagerPools", "GlobalLanguage", "GroupingPools", "GuildPools", "TemplateLanguage" })
            {
                JObj section = file.GetSection(sectionName);
                if (section == null) { continue; }
                foreach (string key in section.Keys)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) { continue; }
                    if (sectionName == "ManagerPools" && RefData.ListenManagerPools.Contains(key)) { continue; }
                    List<string> lines = PackJsonFile.LinesOf(section[key] as JArr);
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (AnyFirstPersonContraction.IsMatch(lines[i]) || AsciiHardContraction.IsMatch(lines[i]))
                        {
                            Add(findings, Severity.Tip, file.FileName, sectionName + "." + key + ", line " + (i + 1),
                                "shared pools are spoken by quirked sims too - a first-person contraction here"
                                + " renders garbled for them (\"Vex'm\"). Consider a contraction-free wording.");
                        }
                    }
                }
            }
        }

        // ── zones.dialogue.json ────────────────────────────────────

        private static void ValidateZones(PackJsonFile zones, List<Finding> findings)
        {
            string f = zones.FileName;
            HashSet<string> known = RefData.AllZoneKeys();
            JArr zoneArr = zones.Root["Zones"] as JArr;
            if (zoneArr == null) { return; }
            for (int i = 0; i < zoneArr.Items.Count; i++)
            {
                JObj entry = zoneArr.Items[i] as JObj;
                if (entry == null) { continue; }
                JStr zone = entry["Zone"] as JStr;
                string name = zone != null ? zone.Value.Trim() : "";
                if (name.Length == 0)
                {
                    Add(findings, Severity.Blocker, f, "Zones entry " + (i + 1),
                        "has no \"Zone\" name - its lines can never load.");
                }
                else if (!known.Contains(name))
                {
                    Add(findings, Severity.Warning, f, "Zones entry " + (i + 1), "\"" + name
                        + "\" matches no zone in the game's atlas - check the spelling"
                        + " (special/event scenes may not be listed).");
                }
                List<string> lines = PackJsonFile.LinesOf(entry["Lines"] as JArr);
                for (int j = 0; j < lines.Count; j++)
                {
                    if (Regex.IsMatch(lines[j], "\\bNN\\b"))
                    {
                        Add(findings, Severity.Warning, f, "\"" + name + "\", line " + (j + 1),
                            "NN replacement is unreliable in zone chatter (mixed paths) - avoid it.");
                    }
                }
            }
        }

        // ── topics.dialogue.json ───────────────────────────────────

        private static void ValidateTopics(Pack pack, List<Finding> findings)
        {
            PackJsonFile topics = pack.Topics;
            string f = topics.FileName;
            JArr arr = topics.Root["Topics"] as JArr;
            if (arr == null) { return; }
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<KeyValuePair<string, string>> earlierTriggers = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JObj topic = arr.Items[i] as JObj;
                if (topic == null) { continue; }
                JStr nameNode = topic["Name"] as JStr;
                string name = nameNode != null ? nameNode.Value.Trim() : "";
                string label = name.Length > 0 ? "topic '" + name + "'" : "topic " + (i + 1);
                if (name.Length == 0)
                {
                    Add(findings, Severity.Blocker, f, label, "has no Name - the loader skips it.");
                }
                else if (!names.Add(name))
                {
                    Add(findings, Severity.Warning, f, label, "duplicate topic name - logs become ambiguous.");
                }
                List<string> responses = PackJsonFile.LinesOf(topic["Responses"] as JArr);
                List<string> openers = PackJsonFile.LinesOf(topic["SimPlayerActivations"] as JArr);
                List<string> triggers = PackJsonFile.LinesOf(topic["ActivationWords"] as JArr);
                if (responses.Count == 0)
                {
                    Add(findings, Severity.Blocker, f, label,
                        "has no Responses - a matched topic without responses would CRASH the game"
                        + " (the loader refuses to inject it).");
                }
                else if (responses.Count < 3)
                {
                    Add(findings, Severity.Tip, f, label, "only " + responses.Count
                        + " response(s) - conversations will repeat; 3+ recommended.");
                }
                if (openers.Count == 0 && triggers.Count == 0)
                {
                    Add(findings, Severity.Blocker, f, label,
                        "has neither openers nor trigger phrases - unreachable, the loader skips it.");
                }
                JBool force = topic["forceLotsOfResponses"] as JBool;
                if (force != null && force.Value)
                {
                    Add(findings, Severity.Tip, f, label,
                        "forceLotsOfResponses makes 5..whole-guild sims answer - floods chat, use sparingly.");
                }

                foreach (string phrase in triggers)
                {
                    CheckTrigger(pack, f, label, phrase, earlierTriggers, findings);
                }
                foreach (string phrase in triggers)
                {
                    earlierTriggers.Add(new KeyValuePair<string, string>(name, phrase));
                }

                JArr scenes = topic["RelevantScene"] as JArr;
                if (scenes != null)
                {
                    foreach (string scene in PackJsonFile.LinesOf(scenes))
                    {
                        CheckRelevantScene(f, label, scene, findings);
                    }
                }
            }
        }

        private static void CheckTrigger(Pack pack, string f, string label, string phrase,
            List<KeyValuePair<string, string>> earlierTriggers, List<Finding> findings)
        {
            string where = label + ", trigger \"" + phrase + "\"";
            foreach (string word in phrase.Split(' '))
            {
                if (word.Length > 0 && (!char.IsLetterOrDigit(word[0]) || !char.IsLetterOrDigit(word[word.Length - 1])))
                {
                    Add(findings, Severity.Warning, f, where, "contains a punctuation-edged word (\"" + word
                        + "\") - the game's word-boundary matching can never match it.");
                    break;
                }
            }
            if (phrase.Trim().IndexOf(' ') < 0)
            {
                Add(findings, Severity.Warning, f, where,
                    "single-word triggers swallow every guild message containing that word - use 2+ words.");
            }
            foreach (string word in phrase.Split(' '))
            {
                if (word.Equals("item", StringComparison.OrdinalIgnoreCase)
                    || word.Equals("npc", StringComparison.OrdinalIgnoreCase)
                    || word.Equals("level", StringComparison.OrdinalIgnoreCase))
                {
                    Add(findings, Severity.Warning, f, where, "contains the word \"" + word
                        + "\" - the game routes sanitized knowledge questions through topic matching,"
                        + " so this can eat item/NPC/level answers.");
                    break;
                }
            }
            foreach (VanillaTopic vanilla in RefGen.VanillaTopics)
            {
                foreach (string theirs in vanilla.Triggers)
                {
                    if (ActivationMatches(phrase, theirs))
                    {
                        Add(findings, Severity.Warning, f, where, "shadowed by the vanilla topic '"
                            + vanilla.Name + "' (\"" + theirs + "\") - vanilla topics match first, so this"
                            + " trigger may never reach your topic.");
                    }
                }
            }
            foreach (KeyValuePair<string, string> earlier in earlierTriggers)
            {
                if (ActivationMatches(phrase, earlier.Value))
                {
                    Add(findings, Severity.Warning, f, where, "shadowed by your earlier topic '"
                        + earlier.Key + "' (\"" + earlier.Value + "\") - earlier topics match first.");
                }
            }
            // Wave overlap: a pure greeting/goodnight/ding message that ALSO
            // matches this trigger would fire the topic instead of the wave.
            CheckWaveOverlap(pack, f, where, phrase, "GenericGreeting", "greeting", findings);
            CheckWaveOverlap(pack, f, where, phrase, "Goodnight", "goodnight", findings);
            CheckWaveOverlap(pack, f, where, phrase, "LevelUpCelebrations", "level-up (ding)", findings);
        }

        // Two directions of wave conflict:
        //  1) a wave listen line that matches the trigger (broad trigger
        //     eating pure wave messages). That's the harmful class, so it warns.
        //  2) a listen line CONTAINED in the trigger phrase (the trigger's
        //     own canonical message would also fire the wave). That's usually
        //     intended (the topic is the more specific answer), tip-level.
        private static void CheckWaveOverlap(Pack pack, string f, string where, string phrase,
            string pool, string waveName, List<Finding> findings)
        {
            List<string> listen = new List<string>();
            string[] vanilla;
            if (RefGen.VanillaListen.TryGetValue(pool, out vanilla)) { listen.AddRange(vanilla); }
            if (pack.Global != null)
            {
                JObj mgr = pack.Global.GetSection("ManagerPools");
                if (mgr != null) { listen.AddRange(PackJsonFile.LinesOf(mgr[pool] as JArr)); }
            }
            foreach (string line in listen)
            {
                if (ActivationMatches(line, phrase))
                {
                    Add(findings, Severity.Warning, f, where, "also matches the " + waveName
                        + " listen phrase \"" + line + "\" - such guild messages fire your topic INSTEAD"
                        + " of the " + waveName + " wave. Use a more specific trigger.");
                    return;
                }
            }
            foreach (string line in listen)
            {
                if (line.Length > 0 && Regex.IsMatch(phrase,
                    "\\b" + Regex.Escape(line.ToLowerInvariant()) + "\\b", RegexOptions.IgnoreCase))
                {
                    Add(findings, Severity.Tip, f, where, "contains the " + waveName
                        + " listen phrase \"" + line + "\", so messages matching this trigger skip"
                        + " the " + waveName + " wave and get your topic instead. Usually fine,"
                        + " just be aware.");
                    return;
                }
            }
        }

        private static void CheckRelevantScene(string f, string label, string scene, List<Finding> findings)
        {
            List<string> displays = new List<string>();
            foreach (ZoneEntry z in RefGen.Atlas)
            {
                displays.Add(RefData.RuntimeDisplayName(z.Scene));
            }
            if (displays.Contains(scene))
            {
                foreach (ZoneEntry z in RefGen.Atlas)
                {
                    if (RefData.RuntimeDisplayName(z.Scene) == scene
                        && RefData.DisplayNameUnverified.Contains(z.Scene))
                    {
                        Add(findings, Severity.Tip, f, label, "RelevantScene \"" + scene
                            + "\" is a best-guess display name (zone not yet dump-verified).");
                    }
                }
                return;
            }
            foreach (string display in displays)
            {
                if (display.Equals(scene, StringComparison.OrdinalIgnoreCase))
                {
                    Add(findings, Severity.Warning, f, label, "RelevantScene \"" + scene
                        + "\" differs from the zone's display name \"" + display + "\" only by casing"
                        + " - the game compares CASE-SENSITIVELY; every zone would read as out-of-zone.");
                    return;
                }
            }
            Add(findings, Severity.Warning, f, label, "RelevantScene \"" + scene
                + "\" matches no zone DISPLAY name (write e.g. \"Stowaway's Step\", not the scene"
                + " name) - the zone gate would treat every zone as out-of-zone.");
        }

        /// <summary>Exact port of GuildManager.ActivationMatches.</summary>
        internal static bool ActivationMatches(string incoming, string activation)
        {
            if (string.IsNullOrWhiteSpace(incoming) || string.IsNullOrWhiteSpace(activation))
            {
                return false;
            }
            incoming = incoming.ToLowerInvariant();
            foreach (string word in activation.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Regex.IsMatch(incoming, "\\b" + Regex.Escape(word) + "\\b"))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
