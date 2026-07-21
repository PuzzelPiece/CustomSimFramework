# Category Reference: where every sim line actually comes from

Evidence based map of every dialogue category, produced by tracing every read of
every list through the decompiled game code (citations are `File.cs:line` in the
decompile). Sim visible text comes from three tiers:

1. **List driven**: customizable by packs. This is most of it.
2. **Hardcoded templates**: string literals in code, composed with live data.
   Packs cannot touch these without dedicated patches (inventory below).
3. **Live game data**: item names, zone names and levels spliced into tiers 1 and 2.

Legend: **SPEAK** = sims say lines from it · **LISTEN** = matched against what
*you* type/shout (adding lines teaches sims to understand more phrasings) ·
**FILL** = seeds every generic sim's personal lists at spawn · **DEAD** = never
read by the game (the framework warns if a pack populates one).

(Guild TOPIC conversations, `topics.dialogue.json` since v0.9.0, are a separate
channel not covered by this map. See `Packs/README.md` "Guild topics" and
PACK_TOOL_REFERENCE section 9. The vanilla topic set is dumped in
`VanillaDump/07_guild_topics.txt`.)

---

## Per sim lists (`*.sim.json` → `SimPlayerLanguage`)

| List | Status | Read at |
|---|---|---|
| `Greetings` | SPEAK | shout greetings, small talk, hello builder, guild chat (`SimPlayerMngr.cs:3085`, `SimPlayerShoutParse.cs:605`, `GuildManager.cs:1609`, `NPCAggroArea.cs:45`) |
| `ReturnGreeting` | SPEAK | whisper replies (`SimPlayerMngr.cs:2407,2518,2550`) |
| `Invites` / `Justifications` | SPEAK | zone invites (`SimPlayerMngr.cs:3130`) |
| `Confirms` | SPEAK | agreement shouts/whispers (`1836, 2141`) |
| `GenericLines` | **DEAD** | written by `LoadSPChat`, copied at spawn, **never read**. Ambient small talk is `ZoneComments` |
| `Aggro` | SPEAK | combat group tell (`Character.cs:1434`) |
| `Died` | SPEAK | death group/raid tell (`SimPlayer.cs:634,778`) |
| `InsultsFun` / `RetortsFun` | SPEAK | sim vs sim banter, shout responses (`3093, 1866`, ShoutParse) |
| `Exclamations` | SPEAK | many paths (`SimPlayer.cs:833`, `3018`) |
| `Denials` | SPEAK | whisper refusals (`2215, 2238`) |
| `DeclineGroup` | SPEAK | declining invites (`2589, 2702`, ShoutParse `325,681`) |
| `Negative` | SPEAK | negative shouts (`1844`) |
| `LFGPublic` | SPEAK | LFG shouts. A **fragment**, composed as `<line> <area name> you can lead.` (`SimPlayer.cs:1531,1682`). Vanilla lines end in "at"/"to" |
| `OTW` | SPEAK | "on my way" (`2690`, ShoutParse `318,674`) |
| `Goodnight` | SPEAK | REPLY to the player's goodnight shout/say + guild chat goodnight waves (ShoutParse `644,943`, `GuildManager.cs:916`). Sims never self log off (`tracking.online` is only ever written to 1) |
| `Hello` | **DEAD** | `GetHello()` has no callers. Targeted hellos use `HelloBuilder` (→ `LocalFriendHello`, `Greetings`, memory lists) |
| `LocalFriendHello` | SPEAK | greeting a known player (`HelloBuilder`, login greet) |
| `UnsureResponse` | SPEAK | confused replies (`3317` via `GetUnsure`) |
| `AngerResponse` | SPEAK | reaction to rudeness (`2375`) |
| `Affirms` | SPEAK | guild join acceptance etc. (`3940`) |
| `EnvDmg` | **DEAD (per sim)** | game reads only `GameData.SimLang.GetEnvDmg()` (`NPC.cs:5011,5030`) → use `GlobalLanguage.EnvDmg` |
| `WantsDrop` | SPEAK | loot requests, `II` = item name (`ItemIcon.cs:2001+`) |
| `Gratitude` | **DEAD (per sim AND global)** | the only read site (`SimTradeWindow.cs:168`) sits behind `LootWanted.Contains(...)`, and `LootWanted` is never populated anywhere. The whole trade gratitude block is unreachable vanilla dead code (found 2026-07-19) |
| `Impressed` / `ImpressedEnd` | SPEAK | admiring your gear (`SimPlayer.cs:5132`) |
| `AcknowledgeGratitude` | SPEAK | "np" replies (`2300`) |
| `LevelUpCelebration` | SPEAK | congratulating dings (`1824`, ShoutParse `350,354`) |
| `GoodLastOuting` / `BadLastOuting` / `GotAnItemLastOuting` / `ReturnToZone` / `BeenAWhile` | SPEAK | the memory system, greetings that reference your last group session (`HelloBuilder`, many whisper paths) |
| `Unsure` | **DEAD** | `UnsureResponse` is the live twin |

## Manager pools (`global.dialogue.json` → `ManagerPools`)

| Pool | Status | Evidence |
|---|---|---|
| `GroupRequest`, `LFGs`, `JoinMyGuild`, `JoinYourGuild`, `WhereDidYouGet`, `InfoWanted`, `HelpReq`, `InvisReq`, `LocReq`, `LevelCheck`, `Gratitude`, `Apologies`, `WhatsUp`, `Goodnight` | LISTEN | whisper parser + shout parser (`ProcessWhisper` checks, `SimPlayerShoutParse.cs:241,279`, `GuildManager.cs:1048,1074`) |
| `GenericGreeting`, `Affirmations` | DUAL | listen (`2392, 2095`) + fill (`LoadSPChat`) |
| `Declinations` | LISTEN | listen (`2109`) only. Its `DeclineGroup` seeding is dead: `LoadSPChat` fills `DeclineGroup` from `Declinations`, then immediately clears and refills it from `DenyGroup` (`SimPlayerMngr.cs:3361-3375`) |
| `LevelUpCelebrations` | DUAL | listen for your dings (ShoutParse `291`) + shouted by leveling sims (`Stats.cs:914`) |
| `ApologyResponses`, `GroupedAlreadyAccept`, `DidNotUnderstand`, `XPLossMsg`, `CongratsForWorldEvent`, `FriendsClubResponseToWorldEvent` | SPEAK | `2480, 2708, 2002`, `Respawn.cs:102`, congrats system (`QueueCongrats`, `Character.cs:1968`) |
| `Invites`, `InviteEnd`, `DenyGroup`, `OTW`, `Died`, `AcknowledgeGratitude`, `Impressed`, `ImpressedEnd`, `LevelUpCongratulations` | FILL | seed generic sims' personal lists (`LoadSPChat`). `LevelUpCongratulations` feeds per sim `LevelUpCelebration` AND is read directly for /say ding replies (ShoutParse 886/890), so it's effectively DUAL |
| `SmallTalk` | **DEAD** | only read feeds dead `GenericLines` (`3354`) |
| `WTBs` | **DEAD** | WTB spam is hardcoded (below) |
| `Obscenities`, `GMWarningsObscenities` | *not exposed* | live, but they drive the GM moderation system (ordered escalation, not a random pool). Modifying them changes moderation behavior |

## Grouping pools (`global.dialogue.json` → `GroupingPools`)

A second dialogue component, `SimPlayerGrouping`, drives group context chat for
ALL sims in your party. Per sim lists are not consulted here, though quirks
still apply via `PersonalizeString`:

| Pool | Status | Read at |
|---|---|---|
| `Hellos` | SPEAK | sim joins your group (`SimPlayerGrouping.cs:771+`) |
| `Goodbyes` | SPEAK | sim leaves your group, the "adios" family (`903+`) |
| `Angry` | SPEAK | upset variant on leaving/dismissal (`907+`) |
| `Affirms` | SPEAK | acknowledging assist calls / orders (`1221+`) |
| `Targeting` | SPEAK | combat target calls, target name appended (`NPC.cs:3317`) |
| `Lost` | SPEAK | can't find/reach target (`1373`) |
| `Ow` | **DEAD** | declared (`SimPlayerGrouping.cs:60`) but never read. Not exposed by the framework |

Related hardcoded group lines (tier 2): `"You're a lot higher than me, I'll try
to keep up..."` (`SimPlayerGrouping.cs:774`), `"I am the new Main Assist, assist
me!"` (`NPC.cs:4672`), `"Main assist will be me again, assist me!"` (`NPC.cs:4700`),
`"I'm here, joining your group now."` (`SimPlayer.cs:1119`), `"I'm here, heading
your way."` (`SimPlayerMngr.cs:1623`), mana percentage reports (`SimPlayerGrouping.cs:689+`).

## Template language (`global.dialogue.json` → `TemplateLanguage`)

Every spawned sim body is a clone of `BlankSPTemplate`. `LoadSPChat` refills
about 22 of its 35 language lists from the manager pools at spawn, but 10 live
lists keep the template's own content. For generic sims, that content IS
their dialogue for: `Aggro`, `Exclamations`, `Denials`, `Negative`,
`LFGPublic`, `Goodnight`, `LocalFriendHello`, `Affirms`, `WantsDrop`,
`ReturnToZone`. `TemplateLanguage` appends to the template component
(pack exposed since v0.7.0). Nothing in the game writes these lists (the only
template language writer is `LoadSPChat(blank, loadAll)` at
`SimPlayerMngr.cs:254`, which touches only the refilled ones), and
`FindSimPlayer` returns the template for sims that aren't premades, so additions also reach
generic sims' **whisper replies**. Premades and custom sims are unaffected
(`LoadLanguageFromPremade` overwrites all 35 on their bodies).

## Other live channels

- **Zone chatter** (`zones.dialogue.json`) → `ZoneAnnounce.ZoneComments`, read by
  `GetGeneric` (`SimPlayerLanguage.cs:135`). **This is the ambient small talk
  channel.** Most idle shouting in a zone comes from here.
- **Guild recruitment lines** → `LiveGuildData.RecruitmentStrings`, shouted by
  guild members (`SimPlayerMngr.cs:3045`). Not yet pack exposed (future: custom guilds).
- **Names** (`MaleNames`/`FemaleNames`) and **bios** (`NiceBios`/`TryhardBios`/`MeanBios`), live for generated sims.

## Guild pools (`global.dialogue.json` → `GuildPools`)

Pools on `GuildManager` driving guild chat and recruitment. All SPEAK, all
confirmed live, pack exposed since v0.6.0. Guild questions are prefixed with the
asking sim's own `Greetings` line (`NN` stripped):

| Pool | Composition | Read at |
|---|---|---|
| `ItemSearch` / `NPCSearch` | `<greeting> <line> <item> drops?` / `<greeting> <line> <npc> spawns?` | `GuildManager.cs:1608,1617` |
| `LevelAdvice` | `<greeting> <line>  level N?` | `1626` |
| `QuestAskItem` + `QuestAskEnding` + `Signoff` | `<ask> <item>? <ending>. <signoff>` | `1491` |
| `OutOfZoneAnswers` | `<line> <scene name>`, the topic answer when the topic's zone is elsewhere | `706,827` |
| `LowLevelAnswers` | full line, responder below the topic's `RequiredLevelToKnow` | `804` |
| `NewPlayerWelcome` | `<line> <new member name>` | `742` |
| `InterestMSG` | full line whisper after your recruit shout (+ optional `" I'm right here in your group!"`) | `2201-2222` |
| `FCAntiRecruitMSG` | full line rival whisper | `2201` |
| `GuildDeletedResponses` / `GuildRemoveResponses` | full line whispers after disband/kick | `GuildManagerUI.cs:454`, `GuildManager.cs:1355` |

Still not exposed: per guild `RecruitmentStrings`, rival name lists
(`RivalMales`/`RivalFemales`), tutorial name lists, and the emoji pool
(`SimPlayerMngr.emojis`, private). Guild topic conversations got their own
channel in v0.9.0 (`topics.dialogue.json`).

## Hardcoded templates (tier 2, not customizable via packs)

| Message | Source |
|---|---|
| `"WTB <item>, offering <gold> gold. Open a trade with me."` | `SimPlayer.DoWTBSpam` (`SimPlayer.cs:601`), random item from ItemDB |
| Guild recruitment pitch `"Hey NN, have you ever thought about a guild? ..."` | `SimPlayerMngr.cs:3247` |
| Alt character nags (`"You're on an alt?? Jump over onto X..."`, 10 variants) | `SimPlayerLanguage.HelloBuilder` (`SimPlayerLanguage.cs:363-394`) |
| Location/level whisper composition (`"... I'm in <zone> near <POI>"`, `"... I'm level N."`) | `SimPlayerMngr.cs:2518, 2550` |
| Group meetup line `"We'll meet up here and then head somewhere cool!"` | `SimPlayerMngr.cs:3134` |
| GM anti pathing warnings | `PlayerControl.cs:2480,2490` |
| Info request answers (item/NPC/quest knowledge) | `KnowledgeDatabase` (data driven Q&A system, own subsystem) |

Substitution tokens in list lines: **`II`** becomes the item name (`WantsDrop`
loot requests). **`NN`** becomes the player name, but only where a call site
replaces it. `PersonalizeString` itself never touches NN. Replaced:
SimPlayerLanguage getters (`GetReturnGreeting`/`GetInvite`/`GetGoodnight`/
`GetTargetedHello`, and that last one wraps the ENTIRE memory greeting, so the
memory lists are all NN safe), the greeting/goodnight/LFG **shout** paths,
guild `ReplaceName` waves (known player gets the name, stranger gets a 50%
name anyway roll or deletion, sim triggered waves use that sim's name),
`NPCAggroArea` greets, and the Hail hotkey reply (`TypeText.cs:255`, which
replaces unconditionally, even for strangers). MIXED, replaced on some paths
and literal on others, so avoid NN there: the OTW / DeclineGroup
whisper vs shout split, `LevelUpCelebration` (guild wave vs shout ding),
`Exclamations`, `InsultsFun`. NOT replaced, literal "NN" in chat: `Died`,
`Aggro`, `AcknowledgeGratitude`, `Denials`, `UnsureResponse`, `AngerResponse`,
`Impressed(+End)`, `WantsDrop`, `RetortsFun`, all grouping/guild pools, rival
`BeenAWhile` responses, and zone comments in the shout greeting path. All
spoken text passes through `PersonalizeString` (quirks: caps, third person,
typos, emojis, sign offs). Typos come from `TypoRate` only, since `TypoChance`
gates dead garble loops.
