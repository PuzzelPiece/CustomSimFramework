using System;
using System.Collections.Generic;
using Erenshor.CustomSimFramework.Data;
using UnityEngine;

namespace Erenshor.CustomSimFramework
{
    /// <summary>
    /// Injects pack-defined guild topic conversations. Builds runtime GuildTopic
    /// ScriptableObjects (once per launch) and makes sure every guild's
    /// Conversations list contains them. Vanilla's InitGuilds CLEARS and rebuilds
    /// Conversations (once per app launch, GuildManager.cs:154), so application
    /// happens as a POSTFIX on InitGuilds plus an idempotent top-up from every
    /// zone entry. The top-up also covers guilds the player founds mid-session,
    /// since GuildManagerUI.cs:371 adds those to Guilds with an empty topic list.
    /// Vanilla's own topics stay missing there until restart. That's vanilla
    /// bug #6 and we deliberately don't fix it here.
    ///
    /// Safety rules from the design audit, do not relax these:
    ///  - every List field on a created topic must be NON-NULL. The game
    ///    dereferences Preceed/End/ActivationWords/RelevantScene unconditionally
    ///    (GuildManager.cs:680-691, 788, 794).
    ///  - topics with empty Responses must never be injected. The keyword
    ///    reply path indexes Responses unguarded (GuildManager.cs:810).
    ///    The loader already refuses them but the builder re-checks anyway.
    /// </summary>
    internal static class GuildTopicInjector
    {
        private static List<GuildTopic> _built;      // created once per launch
        private static bool _warningsDone;           // shadowing warnings, once per launch
        private static bool _scenesValidated;        // RelevantScene check, once per launch

        internal static void ApplyAll()
        {
            if (PackLoader.TopicPacks.Count == 0)
            {
                return;
            }
            GuildManager gm = GameData.GuildManager;
            if (gm == null || gm.Guilds == null)
            {
                return; // retried from the next zone entry / InitGuilds
            }
            EnsureBuilt();
            if (_built.Count == 0)
            {
                return;
            }
            int added = 0;
            int guildsTouched = 0;
            foreach (LiveGuildData guild in gm.Guilds)
            {
                if (guild == null || guild.Conversations == null)
                {
                    continue; // InitGuilds owns creating the list, never race it
                }
                bool touched = false;
                foreach (GuildTopic topic in _built)
                {
                    if (!guild.Conversations.Contains(topic))
                    {
                        guild.Conversations.Add(topic);
                        added++;
                        touched = true;
                    }
                }
                if (touched)
                {
                    guildsTouched++;
                }
            }
            if (added > 0)
            {
                CustomSimFrameworkPlugin.Log.LogInfo("[Topics] " + _built.Count + " pack topic(s) ensured in "
                    + guildsTouched + " guild(s) (" + added + " list insert(s)).");
            }
            TryWarnShadowingOnce(gm);
            TryValidateScenesOnce();
        }

        /// <summary>Builds the runtime GuildTopic instances from all loaded packs (once per launch).</summary>
        private static void EnsureBuilt()
        {
            if (_built != null)
            {
                return;
            }
            _built = new List<GuildTopic>();
            foreach (TopicsDefinition pack in PackLoader.TopicPacks)
            {
                if (pack.Topics == null)
                {
                    continue;
                }
                foreach (TopicDefinition def in pack.Topics)
                {
                    if (def == null || def.Responses == null || def.Responses.Count == 0)
                    {
                        continue; // loader already warned; never inject a crash-capable topic
                    }
                    GuildTopic topic = ScriptableObject.CreateInstance<GuildTopic>();
                    topic.name = "CSF_" + def.Name;
                    // Every list MUST be non-null (see class summary).
                    topic.SimPlayerActivations = CopyList(def.SimPlayerActivations);
                    topic.ActivationWords = CopyList(def.ActivationWords);
                    topic.Responses = CopyList(def.Responses);
                    topic.Preceed = CopyList(def.Preceed);
                    topic.End = CopyList(def.End);
                    topic.RelevantScene = CopyList(def.RelevantScene);
                    topic.RequiredLevelToKnow = def.RequiredLevelToKnow;
                    topic.MaxLevelToAsk = def.MaxLevelToAsk;
                    topic.forceLotsOfResponses = def.forceLotsOfResponses;
                    _built.Add(topic);
                    CustomSimFrameworkPlugin.LogDebug("Built guild topic '" + def.Name + "' ("
                        + topic.SimPlayerActivations.Count + " opener(s), "
                        + topic.ActivationWords.Count + " trigger(s), "
                        + topic.Responses.Count + " response(s)) from " + pack.SourceFile);
                }
            }
            WarnPackTopicCrossChecks();
        }

        /// <summary>
        /// v0.9.1 cross-checks across ALL loaded packs, once per launch.
        /// Covers duplicate topic names (the parse-time check is per-file
        /// only), pack-topic-vs-pack-topic trigger shadowing (first-injected
        /// wins), and triggers containing the game's sanitized knowledge
        /// tokens. Sim questions are re-fed through topic matching as
        /// "&lt;Item&gt; item " / "&lt;NPC&gt; npc " / " level N", so a trigger with a
        /// bare item/npc/level word can eat knowledge answers.
        /// </summary>
        private static void WarnPackTopicCrossChecks()
        {
            HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GuildTopic topic in _built)
            {
                if (!seenNames.Add(topic.name))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Topics] duplicate topic name '" + topic.name
                        + "' across packs - both are injected, but logs and warnings will be ambiguous.");
                }
                foreach (string phrase in topic.ActivationWords)
                {
                    foreach (string word in phrase.Split(' '))
                    {
                        if (string.Equals(word, "item", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(word, "npc", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(word, "level", StringComparison.OrdinalIgnoreCase))
                        {
                            CustomSimFrameworkPlugin.Log.LogWarning("[Topics] '" + topic.name + "' trigger \""
                                + phrase + "\" contains the word \"" + word + "\" - the game routes sanitized"
                                + " knowledge questions (\"<name> item \", \"<name> npc \", \" level N\")"
                                + " through topic matching, so this trigger may eat item/NPC/level answers.");
                            break;
                        }
                    }
                }
            }
            // Shadowing among our own topics: earlier-injected topics match
            // first, so a LATER topic whose canonical trigger message also
            // matches an EARLIER topic's trigger never fires for it.
            for (int earlier = 0; earlier < _built.Count; earlier++)
            {
                for (int later = earlier + 1; later < _built.Count; later++)
                {
                    foreach (string laterPhrase in _built[later].ActivationWords)
                    {
                        foreach (string earlierPhrase in _built[earlier].ActivationWords)
                        {
                            if (GuildManager.ActivationMatches(laterPhrase, earlierPhrase))
                            {
                                CustomSimFrameworkPlugin.Log.LogWarning("[Topics] '" + _built[later].name
                                    + "' trigger \"" + laterPhrase + "\" is shadowed by '"
                                    + _built[earlier].name + "' (\"" + earlierPhrase
                                    + "\") - earlier-injected topics match first, so this trigger may never"
                                    + " reach its own topic.");
                            }
                        }
                    }
                }
            }
        }

        private static List<string> CopyList(List<string> source)
        {
            return (source != null) ? new List<string>(source) : new List<string>();
        }

        /// <summary>
        /// Warns (once per launch) about activation phrases that get shadowed by
        /// vanilla topics, or that would themselves eat the guild greet/goodnight/
        /// ding waves and knowledge answers (topic matching runs FIRST in the
        /// guild-chat pipeline and a match skips everything after it).
        /// Uses the game's own ActivationMatches for exact semantics.
        /// </summary>
        private static void TryWarnShadowingOnce(GuildManager gm)
        {
            if (_warningsDone)
            {
                return;
            }
            SimPlayerMngr mngr = GameData.SimMngr;
            List<GuildTopic> existing = FindVanillaTopics(gm);
            if (mngr == null || existing == null)
            {
                return; // retried on a later apply
            }
            _warningsDone = true;
            foreach (GuildTopic ours in _built)
            {
                foreach (string phrase in ours.ActivationWords)
                {
                    // 1) A vanilla topic whose phrase matches ours' canonical
                    //    message answers first (first-match-wins + yield break).
                    foreach (GuildTopic vanilla in existing)
                    {
                        foreach (string theirs in vanilla.ActivationWords)
                        {
                            if (GuildManager.ActivationMatches(phrase, theirs))
                            {
                                CustomSimFrameworkPlugin.Log.LogWarning("[Topics] '" + ours.name + "' trigger \""
                                    + phrase + "\" is shadowed by the vanilla topic '" + vanilla.name
                                    + "' (\"" + theirs + "\") - vanilla topics match first, so this trigger"
                                    + " may never reach your topic.");
                            }
                        }
                    }
                    // 2) A wave-trigger phrase that also matches our topic would
                    //    fire the topic INSTEAD of the greet/goodnight/ding wave.
                    WarnWaveOverlap(phrase, ours.name, mngr.GenericGreeting, "greeting");
                    WarnWaveOverlap(phrase, ours.name, mngr.Goodnight, "goodnight");
                    WarnWaveOverlap(phrase, ours.name, mngr.LevelUpCelebrations, "level-up (ding)");
                }
            }
        }

        private static void WarnWaveOverlap(string phrase, string topicName, List<string> listenPool, string waveName)
        {
            if (listenPool == null)
            {
                return;
            }
            foreach (string listenLine in listenPool)
            {
                if (GuildManager.ActivationMatches(listenLine, phrase))
                {
                    CustomSimFrameworkPlugin.Log.LogWarning("[Topics] '" + topicName + "' trigger \"" + phrase
                        + "\" also matches the " + waveName + " listen phrase \"" + listenLine
                        + "\" - guild messages like that will fire your topic INSTEAD of the "
                        + waveName + " wave. Use a more specific trigger.");
                    return; // one warning per phrase/pool is enough
                }
            }
        }

        /// <summary>Vanilla (non-CSF) topics from the first populated guild list.</summary>
        private static List<GuildTopic> FindVanillaTopics(GuildManager gm)
        {
            foreach (LiveGuildData guild in gm.Guilds)
            {
                if (guild == null || guild.Conversations == null || guild.Conversations.Count == 0)
                {
                    continue;
                }
                List<GuildTopic> vanilla = new List<GuildTopic>();
                foreach (GuildTopic topic in guild.Conversations)
                {
                    if (topic != null && !_built.Contains(topic))
                    {
                        vanilla.Add(topic);
                    }
                }
                if (vanilla.Count > 0)
                {
                    return vanilla;
                }
            }
            return null; // InitGuilds hasn't populated anything yet
        }

        /// <summary>
        /// RelevantScene entries must be zone DISPLAY names (the game compares
        /// them against GameData.SceneName). Warn-only, once per launch, when
        /// the zone atlas is available.
        /// </summary>
        private static void TryValidateScenesOnce()
        {
            if (_scenesValidated)
            {
                return;
            }
            ZoneAtlasEntry[] atlas = ZoneAtlas.Atlas;
            if (atlas == null)
            {
                return; // retried on a later apply
            }
            _scenesValidated = true;
            // The RUNTIME gate is the game's ordinal, case-sensitive
            // List.Contains (GuildManager.cs:794). So the exact-case check
            // comes first, and a case-insensitive-only match gets its own
            // did-you-mean warning. Added in v0.9.1 because the v0.9.0
            // validator was case-insensitive and silently passed wrong casings.
            HashSet<string> exact = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> byLower = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZoneAtlasEntry entry in atlas)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ZoneName))
                {
                    continue;
                }
                string display = GetCommonTerms.GetZoneTerm(entry.ZoneName);
                exact.Add(display);
                byLower[display] = display;
            }
            foreach (GuildTopic topic in _built)
            {
                foreach (string scene in topic.RelevantScene)
                {
                    if (exact.Contains(scene))
                    {
                        continue;
                    }
                    string properCasing;
                    if (byLower.TryGetValue(scene, out properCasing))
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Topics] '" + topic.name + "': RelevantScene \""
                            + scene + "\" differs from the atlas display name \"" + properCasing
                            + "\" only by casing - the game compares CASE-SENSITIVELY, so this gate would"
                            + " treat every zone (including the right one) as out-of-zone. Use \""
                            + properCasing + "\".");
                    }
                    else
                    {
                        CustomSimFrameworkPlugin.Log.LogWarning("[Topics] '" + topic.name + "': RelevantScene \""
                            + scene + "\" matches no zone DISPLAY name in the game's atlas (write e.g."
                            + " \"Stowaway's Step\", not the scene name; special/event scenes may not be"
                            + " listed) - the zone gate would treat every zone as out-of-zone.");
                    }
                }
            }
        }
    }
}
