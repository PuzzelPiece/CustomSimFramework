using System;
using System.Collections.Generic;

namespace Erenshor.CustomSimFramework.Data
{
    /// <summary>
    /// JSON schema for one custom sim (a "*.sim.json" file inside a pack).
    /// Field names mirror the game's components 1:1 so values transfer directly:
    ///   - identity/stats -> NPC / Stats / Inventory on the template GameObject
    ///   - quirks         -> SimPlayer fields (applied to template + re-applied on spawn)
    ///   - dialogue lists -> SimPlayerLanguage lists (same names as the game class)
    /// Lists left null/empty in the JSON are filled from the game's global pools at
    /// template build time (same source pools vanilla's LoadSPChat uses), so a pack
    /// can define only what it cares about.
    /// Loaded with JsonUtility: keep everything flat, no dictionaries.
    /// </summary>
    [Serializable]
    public class SimDefinition
    {
        // ── Identity ────────────────────────────────────────────────
        public string Name = "";
        public string Gender = "Male";           // "Male" or "Female". Validated + trimmed since v0.9.1,
                                                 // anything else warns and defaults to Male.
        public string Class = "Duelist";         // Paladin | Arcanist | Druid | Duelist | Stormcaller | Reaver
                                                 // NOTE: the game DISPLAYS Duelist as "Windblade" but the
                                                 // internal data still says Duelist, so the JSON must say
                                                 // Duelist. Pack Studio shows the Windblade name and writes
                                                 // Duelist for you.
        public int Level = 5;                    // starting level; save file owns progression afterwards
        public float SkillLevel = 40f;           // observed vanilla range ~0-65
        public bool Rival = false;               // rival sims act antagonistic (vanilla: Friends Club)
        public int TiedToSlot = -1;              // -1 = game default (0). 0-10 = progression follows that
                                                 // character slot. 12 = light daily bump. 99 = independent
                                                 // (rival-rate catch-up + faster gear chase). Ignored for
                                                 // Rival sims, the game forces those to 99 every login.
                                                 // NOTE: /friend (and unfriend) sets the sim's slot-tie to the
                                                 // current character slot. A pinned value here re-asserts
                                                 // itself every login, overriding that.
                                                 // TUTORIAL: a sim tied to slot 0-10 is marked InTutorial each
                                                 // login on that slot. It staffs a NEW character's tutorial
                                                 // (a feature!) and is excluded from world spawns until the
                                                 // first landing in a normal zone. That happens instantly for
                                                 // established characters, so it's invisible in normal play.

        // ── Bio / inspect window ────────────────────────────────────
        public string Bio = "";                  // shown in the inspect window; overrides personality bios
        public int PersonalityType = 0;          // 0 = game default: sims on the premade pipeline never roll
                                                 // and resolve to 1 (Nice). Set 1 nice / 2 tryhard / 3 mean
                                                 // to choose (4-5 = plain, no bio pool).
                                                 // NOTE: 0 + empty Bio = a FIXED nice-pool bio (always index 1,
                                                 // same for every such sim). Set 1-3 for a per-session roll.

        // ── Appearance ──────────────────────────────────────────────
        public string HairName = "";             // "Chr_Hair_01".."Chr_Hair_38" EXCEPT 03, which doesn't exist
                                                 // and renders bald. Invalid names warn + reroll. Empty = random.
        public int HairColorIndex = -1;          // index into the game's 20 hair colors; -1 = random
        public int SkinColorIndex = -1;          // index into the game's 11 skin colors; -1 = random

        // ── Typing quirks (how their chat text is rendered) ─────────
        public bool TypesInAllCaps = false;
        public bool TypesInAllLowers = false;
        public bool TypesInThirdPerson = false;
        public string RefersToSelfAs = "";       // e.g. "paul" -> "paul thinks this is great"
        public bool LovesEmojis = false;
        public bool Abbreviates = false;         // DEAD: the game declares this quirk but never reads it
        public float TypoRate = 0f;              // per-word roll vs 150 (vanilla sims use 0-10); the LIVE typo dial
        public float TypoChance = 0f;            // DEAD: gates only no-op garble loops (string.Insert result
                                                 // discarded, SimPlayerMngr.cs:2997). Typos come from TypoRate.
        public List<string> SignOffLines;        // occasionally appended to messages (~10% chance)

        // ── Behavior dials ──────────────────────────────────────────
        public float Greed = 1.2f;               // DEAD as a JSON dial: the body always loads Greed from the
                                                 // save, and fresh saves (Greed 1.0) reroll to 1.85-2.9 at
                                                 // first spawn (SimPlayer.cs:2899). Greed's real meaning is
                                                 // auction-house pricing, not loot appetite.
        public int Patience = 1000;              // LIVE (dead-in-group nag interval etc.)
        public int GearChase = 4;                // LIVE (copied to the body at spawn)
        public int LoreChase = 0;                // DEAD: stored in tracking, never read by the game
        public int SocialChase = 0;              // DEAD: stored in tracking, never read by the game
        public int Troublemaker = 0;             // DEAD: read only on live bodies, but never copied to them
        public int DedicationLevel = 0;          // DEAD: stored in tracking, never read by the game

        // ── Dialogue lists (names identical to SimPlayerLanguage) ───
        // Tokens: "II" is replaced with an item name in WantsDrop lines.
        //         "NN" (player name) is replaced ONLY in some lists. The game
        //         does replacement per call site, not globally (all re-traced
        //         and live-verified 2026-07-19). Safe: Greetings,
        //         ReturnGreeting, Invites, Goodnight (always the name, even
        //         strangers), LocalFriendHello, BeenAWhile, and the memory
        //         lists (the whole memory greeting is name-substituted).
        //         MIXED, meaning some paths replace and others show literal
        //         "NN", so avoid it in: LevelUpCelebration (guild ding wave
        //         substitutes, shout dings literal), Exclamations, InsultsFun,
        //         and OTW/DeclineGroup (shout paths replace, WHISPER replies
        //         literal). NOT replaced, always literal "NN": Died, Aggro,
        //         Confirms, Justifications, UnsureResponse, AngerResponse,
        //         AcknowledgeGratitude, Denials, Negative, Impressed(+End),
        //         WantsDrop, RetortsFun.
        //         Full table + guild-wave name rule: Packs/README.md.
        // Rival sims: MOST whisper responses draw raw lines from BeenAWhile.
        // For a Rival, write BeenAWhile as their taunt pool (no NN).
        // Exceptions, all checked in the decompile: greetings answer from
        // ReturnGreeting, obscenities from AngerResponse, and knowledge
        // questions ("where does X drop") get normal helpful answers because
        // no rival branch exists there. So flavor those lists antagonistic too.
        public List<string> Greetings;
        public List<string> ReturnGreeting;
        public List<string> Invites;
        public List<string> Justifications;
        public List<string> Confirms;
        public List<string> GenericLines;
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
        public List<string> Hello;
        public List<string> LocalFriendHello;
        public List<string> UnsureResponse;
        public List<string> AngerResponse;
        public List<string> Affirms;
        public List<string> EnvDmg;
        public List<string> WantsDrop;
        public List<string> Gratitude;
        public List<string> Impressed;
        public List<string> ImpressedEnd;
        public List<string> AcknowledgeGratitude;
        public List<string> LevelUpCelebration;
        public List<string> GoodLastOuting;
        public List<string> BadLastOuting;
        public List<string> GotAnItemLastOuting;
        public List<string> ReturnToZone;
        public List<string> BeenAWhile;
        public List<string> Unsure;

        // Set by the loader for log messages, not part of the JSON.
        [NonSerialized] public string SourceFile = "";

        // One-time bio roll cached by the spawn postfix so the inspect bio
        // stays the same across respawns within a session. Not part of the JSON.
        [NonSerialized] public int RolledBioIndex = -1;
    }
}
