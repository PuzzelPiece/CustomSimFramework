using System;
using System.Collections.Generic;

namespace Erenshor.CustomSimFramework.Data
{
    /// <summary>
    /// JSON schema for a pack's "topics.dialogue.json": guild TOPIC CONVERSATIONS
    /// (opener → on-topic responses, like vanilla's Discord/Bonepits topics).
    /// Injected as runtime GuildTopic ScriptableObjects into every guild's
    /// Conversations list (after vanilla's InitGuilds builds it).
    /// Field names mirror the game's GuildTopic class 1:1. The vanilla dump
    /// (VanillaDump/07_guild_topics.txt) is the authoring reference.
    /// </summary>
    [Serializable]
    public class TopicsDefinition
    {
        public List<TopicDefinition> Topics;

        [NonSerialized] public string SourceFile = "";
    }

    /// <summary>
    /// One guild conversation topic. Semantics traced through GuildManager.cs:
    ///  - SimPlayerActivations: openers a sim posts in guild chat on the guild
    ///    chat timer (roughly 2-4 min steady state, coin-flip vs guild
    ///    questions). Empty means the topic is keyword-triggered only. Vanilla
    ///    has precedent both ways, the Mechanics topic has no ActivationWords
    ///    and others have no openers.
    ///  - ActivationWords: keyword phrases matched against EVERY guild-chat
    ///    message, yours or a sim's. ALL words of a phrase must appear,
    ///    word-boundary, any order. First matching topic wins and SKIPS the
    ///    rest of the guild pipeline (knowledge answers, greet/goodnight/ding
    ///    waves), so keep phrases specific.
    ///  - Responses: the on-topic replies. 2-4 sims respond, so write 3 or
    ///    more lines. REQUIRED, because the keyword path indexes this list
    ///    unguarded and a matched topic with no responses would throw inside
    ///    a game coroutine.
    ///  - Preceed / End: optional wrappers, applied ONLY in sim-started
    ///    conversations ("&lt;Preceed&gt; &lt;response&gt;" or "&lt;response&gt; &lt;End&gt;",
    ///    coin-flip when both exist). Keyword-triggered replies are raw.
    ///  - RelevantScene: zone DISPLAY names (like "Stowaway's Step"). Gates
    ///    ONLY keyword-triggered replies. Asked elsewhere, sims answer with
    ///    OutOfZoneAnswers plus the first listed zone. Sim-started
    ///    conversations ignore it.
    ///  - RequiredLevelToKnow: on PLAYER-KEYWORD triggers, responders below it
    ///    answer with LowLevelAnswers (GuildManager.cs:802-804). Sim-started
    ///    conversations do NOT level-check responders. Only the opener is
    ///    gated, sims more than 2 levels below never open the topic.
    ///  - MaxLevelToAsk: sims above it never open the topic.
    /// NN is NOT replaced anywhere in topic paths, it shows literally.
    /// Quirks (PersonalizeString) apply to openers and responses.
    /// </summary>
    [Serializable]
    public class TopicDefinition
    {
        public string Name = "";                    // required; used in logs
        public List<string> SimPlayerActivations;   // openers sims post
        public List<string> ActivationWords;        // keyword trigger phrases
        public List<string> Responses;              // on-topic replies (required)
        public List<string> Preceed;                // optional response prefix pool
        public List<string> End;                    // optional response suffix pool
        public List<string> RelevantScene;          // zone display names (keyword gate)
        public int RequiredLevelToKnow = 1;
        public int MaxLevelToAsk = 35;
        public bool forceLotsOfResponses;           // 5..whole-guild responders
    }
}
