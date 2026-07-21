# Making your own content pack

A **pack** is just a folder inside `BepInEx/config/CustomSimFramework/Packs/`.
No coding needed, everything is plain JSON. Copy the `Paul` folder and edit away.
(Pack Studio, the GUI editor that ships with the mod, edits these same files.
This guide is for anyone who prefers doing it by hand.)

```
Packs/
  MyPack/
    Bob.sim.json           <- a custom sim (any number of *.sim.json files)
    global.dialogue.json   <- optional: extra lines for ALL generic sims
    zones.dialogue.json    <- optional: extra ambient chatter per zone
    topics.dialogue.json   <- optional: guild topic conversations
```

The shipped `Paul` pack is the full coverage reference. **Paul** defines
every live dialogue list, **Marla** shows the minimal effort path (a few
signature lists, the rest autofills), and **Vex** shows the Rival rules.
Start from whichever matches your ambition.

Changes take effect the next time you launch the game and log in.
Check `BepInEx/LogOutput.log` for lines starting with `[Packs]` / `[Inject]` to
confirm your files loaded. Parse errors are reported there too.

**Comments:** JSON has no comment syntax, so the framework treats any key
starting with `_` as a comment and ignores it. Feel free to add
`"_Greetings": "explanation..."` style entries. Empty lines (`""`) in any
list are removed automatically, since they'd otherwise make sims literally
say "lol".

---

## Custom sims (`*.sim.json`)

Only `"Name"` is required. Everything you leave out gets a sensible default, and
any dialogue list you leave out is filled from the game's normal dialogue pools.
You can customize three lists or all thirty five.

Your sim is a full simulated player. The game gives them a save file, they level
up over time, travel between zones, join groups and guilds, answer whispers, use
the auction house, everything the built in named sims do.

### Identity

| Field | Default | Notes |
|---|---|---|
| `Name` | *(required)* | Must be unique, including ignoring letter case. If it matches an existing sim save in a different casing (`paul` vs `Paul`), your sim is skipped, because Windows treats those save files as one. Exact name matches with an existing generic sim take that sim over instead. |
| `Gender` | `"Male"` | `"Male"` or `"Female"` |
| `Class` | `"Duelist"` | `Paladin`, `Arcanist`, `Druid`, `Duelist`, `Stormcaller`, `Reaver`. Note the game displays Duelist as "Windblade" but the JSON must say Duelist. |
| `Level` | `5` | Starting level (1-35). **Only applies on first creation.** After that the sim's own save file owns their progression, like any sim. |
| `SkillLevel` | `40` | How well they play (0-65 observed in vanilla). |
| `Rival` | `false` | Rivals act antagonistic (like the Friends Club guild). |
| `TiedToSlot` | `-1` | Progression tie. `0`-`10` follows that character slot's level. `12` is an occasional daily bump. `99` is independent (rival rate catchup, faster gear progression). `-1` keeps the game default (`0`, same as the built in named sims). Ignored for `Rival` sims, which are always `99`. With SimPassiveLevelingOverhaul installed, slot tied sims gain XP only while you play that slot and `99` gains continuously. |

### Bio (inspect window)

- `Bio` is free text shown when a player inspects your sim. Use `\n` for line
  breaks. If set, it always wins over personality bios.
- `PersonalityType` `0` (the default) gives the game's standard personality for
  preauthored sims: **Nice**. The personality "roll" only exists for generated
  sims, custom sims never roll. Set `1` (nice), `2` (tryhard) or `3` (mean) to
  choose. If your `Bio` is empty, the inspect window then shows a bio of that
  flavor, picked once per session.

### Appearance

- `HairName`: `"Chr_Hair_01"` through `"Chr_Hair_38"`, **except
  `Chr_Hair_03` which doesn't exist** (the game would render a bald head).
  Invalid names warn and reroll. Empty = random.
- `HairColorIndex`: `0`-`19` (the game's hair color palette). `-1` = random.
- `SkinColorIndex`: `0`-`10`. `-1` = random.

### Typing quirks

These shape every message the sim sends:

- `TypesInAllCaps` / `TypesInAllLowers`: LIKE THIS / like this
- `TypesInThirdPerson`: "Paul is on his way" instead of "I'm on my way"
- `RefersToSelfAs`: replaces "I"/"me" with a nickname (like `"paul"`)
- `LovesEmojis`: sprinkles `:)` `:D` `8)` after punctuation
- `TypoRate`: per word typo chance. Vanilla sims use `0`-`10` (10 is very
  sloppy). **This is the only typo dial that works.**
- `TypoChance`: **no effect**. It gates code whose garbling result is
  discarded (checked in the decompile and by live A/B test).
- `SignOffLines`: list of lines occasionally appended (about 10% of messages)
- `Abbreviates`: **no effect**. The game declares this quirk but never reads
  it anywhere. Kept in the schema for forward compatibility.

**Behavior dials, and which ones actually work:** `GearChase` and `Patience`
are live (copied to the spawned body). `Greed` **cannot be set from JSON**.
The game reloads it from the sim's save at every spawn and rerolls fresh saves
to 1.85-2.9, and its real effect is auction house pricing, not loot begging.
`LoreChase`, `SocialChase`, `DedicationLevel` and `Troublemaker` are stored
but never read by any live code path.

### Dialogue lists

Every list is optional. Tokens: **`NN`** becomes the player's name and **`II`**
becomes an item name (in `WantsDrop` lines).

| List | When it's used |
|---|---|
| `Greetings` | general greeting building block (shouts, hellos, guild chat) |
| `ReturnGreeting` | replying to your hello |
| `LocalFriendHello` | greeting a player they know |
| `BeenAWhile` | greeting after days apart |
| `Invites` / `Justifications` | inviting you to a zone ("hey i'm over in X" / "if you wanna join") |
| `Confirms` / `Affirms` / `Denials` / `Negative` | yes/no responses |
| `DeclineGroup` | turning down a group invite |
| `OTW` | agreeing to come to you |
| `Aggro` | pulling a monster in combat |
| `Died` | after dying |
| `InsultsFun` / `RetortsFun` | playful trash talk between sims |
| `Exclamations` | "lol", "omg" (appended to greetings as bare words, see composition below) |
| `LFGPublic` | shouting looking for group (a **fragment**, see composition below) |
| `Goodnight` | replying when YOU say/shout goodnight, and guild chat goodnight waves (sims never log off on their own) |
| `UnsureResponse` | didn't understand your whisper |
| `AngerResponse` | you were rude to them |
| `WantsDrop` | asking for a loot drop (use `II`) |
| `AcknowledgeGratitude` | "np" replies |
| `Impressed` / `ImpressedEnd` | admiring your gear (item name goes between them) |
| `LevelUpCelebration` | congratulating a levelup |
| `GoodLastOuting` / `BadLastOuting` / `GotAnItemLastOuting` / `ReturnToZone` | reminiscing about your last group session together |

> **Ambient small talk goes in `zones.dialogue.json`, not here.** Idle chatter
> comes from each zone's comment pool. Five per sim lists exist in the game's
> data but are **never read** (`GenericLines`, `Hello`, `Unsure`, `EnvDmg`,
> `Gratitude`). The framework warns you if you populate one and names the live
> alternative. Full evidence based map: [CATEGORY_REFERENCE.md](CATEGORY_REFERENCE.md).

### How your lines get composed

Most lists are **complete sentences**. A few are **fragments** the game splices
with live data, so write those to read correctly with the appended part:

| List(s) | The game builds | So write lines like |
|---|---|---|
| `Invites` + `Justifications` | `<Invite> <zone name> <Justification>` (or `<Invite> <zone>. We'll meet up here and then head somewhere cool!`) | Invite: "hey i'm over in" · Justification: "if you wanna join" |
| `LFGPublic` | `<line> <area name> you can lead.` | "need more people to group at" (end on *at*/*to*, like vanilla) |
| `Impressed` + `ImpressedEnd` | `<Impressed> <gear word> <ImpressedEnd>` | "whoa that's a really nice" + "cape" + "how do i get one of those" |
| `Greetings` (first hello of a session) | `<Greeting>! ` then optional memory segments: `<BeenAWhile>` or `<ReturnToZone> <zone>! <Good/BadLastOuting> <GotAnItemLastOuting> <item>.` | Greeting: "hey hey" · ReturnToZone: "we should go back to" |
| `ReturnGreeting` (location/level whispers) | `<ReturnGreeting> I'm in <zone> right now` / `<ReturnGreeting> I'm level N.` | "oh hey NN" |
| `WantsDrop` | `II` replaced by the item's name | "can i have the II if nobody wants it" |
| `Greetings` (greet replies) | `<Greeting> <full first hello greeting>`, so two greetings can STACK back to back ("yo sup NN! ...") | keep greetings short so stacking reads naturally |
| `InsultsFun` | banter opener: `[<greeting> ]<other sim's name> <Exclamation> <InsultsFun>` ("Hey Forragan hehe!! Are you role playing a noob...") · callout reply: `<your name> <InsultsFun>` | full standalone jabs addressed at "you", they must read correctly right after a name |
| `RetortsFun` | the comeback ending a reaction chain. Several sims react to an insult, each but the last shouts a bare `Exclamation`, and the LAST delivers `<RetortsFun>` standalone | "ok but at least i'm having fun" |
| `Affirms` | guild invite accept: `<line> I'll join up! I'll come to you for an invite now.` (also standalone yes shouts) | short agreement: "sounds good" |
| `Targeting` (grouping pool, global file) | `<line> <target name>` | "switch targets to" |
| `ItemSearch` / `NPCSearch` (guild pool) | `<sim's greeting> <line> <item> drops?` / `<line> <npc> spawns?` | "anybody know where" |
| `LevelAdvice` (guild pool) | `<line>  level N?` | "what should i be grinding at" |
| `QuestAskItem` + `QuestAskEnding` + `Signoff` (guild pool) | `<ask> <item>? <ending>. <signoff>` | "anyone have a spare" + "i can trade for it" + "thanks!!" |
| `OutOfZoneAnswers` (guild pool) | `<line> <zone name>` | "pretty sure that's over in" |
| `NewPlayerWelcome` (guild pool) | `<line> <new member name>` | "o7 welcome" |

**Tokens and styling:**

- `NN` becomes the player's name, **but only in some lists.** The game
  substitutes NN per call site, not globally, so in most lists a literal "NN"
  appears in chat. Where NN works and where it doesn't:

  | Tier | Lists |
  |---|---|
  | **Replaced (safe)** | `Greetings` (Hail hotkey replies always use your name, even strangers. Shout greets strip it for strangers, so "sup NN" can appear as "sup". Guild question prefixes always strip it) · `ReturnGreeting`, `Invites` · `Goodnight` (**always** your name, even strangers) · `LocalFriendHello`, `BeenAWhile` · `ReturnToZone`/`GoodLastOuting`/`BadLastOuting`/`GotAnItemLastOuting` (the whole memory greeting is name substituted, all live tested 2026-07-19) |
  | **Mixed, avoid NN** | `LevelUpCelebration` (guild ding waves substitute the name, shout dings show literal NN) · `Exclamations` · `InsultsFun` · `OTW` and `DeclineGroup` (shout paths replace, **whisper replies show literal NN**) |
  | **Literal, never use NN** | `Died`, `Aggro`, `Confirms`, `Justifications`, `UnsureResponse`, `AngerResponse`, `AcknowledgeGratitude`, `Denials`, `Negative`, `Impressed`/`ImpressedEnd`, `WantsDrop`, `RetortsFun` · all `GroupingPools` lists · all `GuildPools` lists (stripped in questions, literal in whispers) · zone chatter (`zones.dialogue.json`, replaced in one path and literal in another) |

  **Guild waves (the `ReplaceName` rule):** guild greeting/goodnight/ding
  waves substitute NN like this. A sim that knows you uses your name. A
  stranger has a 50% chance of using your name anyway, otherwise NN is
  deleted. A wave triggered by another sim uses **that sim's** name. NN never
  shows literally in guild waves, but write lines that still read naturally
  with the name missing.

- **Rival sims:** every whisper a Rival answers draws a raw line from their
  `BeenAWhile` list. For a Rival, `BeenAWhile` *is* the taunt pool. Write it
  antagonistic, and don't use NN in it (never replaced on that path).
  One nuance for **GlobalLanguage.BeenAWhile** additions: that pool also seeds
  GENERIC sims' BeenAWhile lists at spawn, and generic Friends Club rivals
  read those raw, so a global NN line can appear as a literal "NN" in a
  rival's whisper. Vanilla's own global pool takes the same risk (25 of its
  27 lines use NN), so NN there is vanilla parity rather than an error. Just
  know the edge exists.
- `II` becomes an item name (`WantsDrop` only).
- `Exclamations` appear in TWO shapes: glued **directly after** greeting text
  with no separator, AND space separated inside banter/reaction chains
  (`<target> <Exclamation> <insult>`, reaction choruses). Bare words ("lol",
  "omg") read correctly in both, which is why the rule is bare words only.
  Variety tip: every generic sim shares the template's Exclamations list and
  vanilla ships only FOUR lines ("hehe!!", "yikes!", "oh wowwwww",
  "omg lolllll :)"). That's why reaction choruses repeat the same line.
  A few `TemplateLanguage.Exclamations` additions visibly declone them.
- Every spoken line passes through the quirk system **after** composition
  (caps/lowercase, third person, typos, emojis, sign offs). Write plain,
  naturally cased text and let the quirks do the styling.

## Global dialogue (`global.dialogue.json`)

Lines here are **added to** (never replace) the pools that all generic sims draw
from. See `Paul/global.dialogue.json` for the shape. Sections:

- `ManagerPools`: three kinds of pools (full map in
  [CATEGORY_REFERENCE.md](CATEGORY_REFERENCE.md)):
  - **Speech**: lines sims say. `LevelUpCongratulations`, `XPLossMsg`,
    `DidNotUnderstand`, `GroupedAlreadyAccept`, `CongratsForWorldEvent`, ...
  - **Listen**: phrases sims *understand from you*. Add to `GroupRequest`,
    `LFGs`, `Gratitude`, `LocReq`, `LevelCheck` and friends to teach sims more
    ways of recognizing what you mean. Write **bare words** ("wanna duo", not
    "wanna duo?") because the game's word boundary matching can never match a
    line that starts or ends with punctuation. The loader warns about these.
  - **Fill**: pools that seed every generated sim's personal dialogue at spawn.
    `Invites`, `Died`, `OTW`, `Impressed`, ...
- `GlobalLanguage`: the same 35 list names as a sim's dialogue lists. These are
  the shared fallback lines any sim can use. (`EnvDmg` only works here, not
  per sim. `Gratitude` turns out to be dead everywhere, its only read site
  is unreachable vanilla code. See CATEGORY_REFERENCE.md.)
- `GroupingPools`: group context chat used by any sim in your party.
  `Hellos` (joining), `Goodbyes` (leaving), `Affirms` (following orders),
  `Targeting` (combat calls, target name appended), `Angry`, `Lost`.
- `GuildPools`: guild chat and recruitment dialogue shared by every guild's
  sims. `ItemSearch`/`NPCSearch`/`LevelAdvice` (guild questions),
  `QuestAskItem`/`QuestAskEnding`/`Signoff` (guild quest asks),
  `OutOfZoneAnswers`/`LowLevelAnswers` (topic conversation replies),
  `NewPlayerWelcome`, `InterestMSG`/`FCAntiRecruitMSG` (recruitment
  reactions), `GuildDeletedResponses`/`GuildRemoveResponses`. Several are
  fragments, see the composition table above and the example file.
  **`NN` does not work in guild pools** (stripped to nothing in guild chat
  questions, shown literally in whispers), so write guild lines without it.
- `TemplateLanguage`: **the shared voice of every generic (randomly
  generated) sim.** Ten of a sim's dialogue lists are never refilled from the
  manager pools. What generic sims say for those comes from the spawn
  template, and this section is the only way to add to it: `Aggro`,
  `Exclamations`, `Denials`, `Negative`, `LFGPublic`, `Goodnight`,
  `LocalFriendHello`, `Affirms`, `WantsDrop`, `ReturnToZone`. Also reaches
  generic sims' whisper replies. Named/custom sims are unaffected (their own
  lists win). Composition rules are the same as the per sim lists of the same
  name (see the table above).
- `MaleNames` / `FemaleNames`: extra names for generated sims. Same
  character rules as sim names (letters, digits and hyphens, since they become
  save filenames and whisper targets, and invalid entries are removed with a
  warning). Note: the name databases **override a sim's saved gender** at
  every login, so adding an existing sim's name to the opposite gender list
  flips them.
- `NiceBios` / `TryhardBios` / `MeanBios`: extra inspect window bios.

## Guild topics (`topics.dialogue.json`)

Opener plus on topic responses **conversations in guild chat**, exactly like
vanilla's Discord/Bonepits topics (full vanilla set with the same field
names: `VanillaDump/07_guild_topics.txt`). See `Paul/topics.dialogue.json`
for a worked example. The short rules:

- A guild sim posts one of your `SimPlayerActivations` openers on the guild
  chat timer (about every 2-4 min, tunable via the `GuildChatFrequency`
  config), then 2-4 guildmates reply with `Responses` lines. **Or** any guild
  message containing ALL words of an `ActivationWords` phrase (word boundary,
  any order) triggers the responses, including messages you type.
- `Responses` is **required** (write 3 or more), and a topic needs at least
  one of openers/triggers to exist.
- **Keep triggers specific.** A matched topic swallows the message before
  knowledge answers and the greet/goodnight/ding waves. The loader warns
  when a trigger collides with vanilla topics or wave phrases.
- `Preceed`/`End` wrap responses only in sim started conversations.
  `RelevantScene` (zone display names) gates only keyword triggered replies.
  `RequiredLevelToKnow` is the **level coherence dial**. Sims can only
  OPEN a topic if they're within `ReqLevel-2 .. MaxLevelToAsk`, so a
  ReqLevel-30 raid topic never fires in a beginner guild at all, and
  responders below ReqLevel answer with vanilla "I'm too low to know"
  lines, which reads exactly right when a player asks anyway. Set it to
  the level where a sim could plausibly know the thing: world news = 1,
  mid game gear = around 13 (clears the beginner guild, which caps around
  10), raid talk = 28-30. See topics.dialogue.json for one of each.
- `NN` does not work in topics. Quirks apply after composition.
- In a guild you FOUND mid session, only pack topics work until the next
  game restart (vanilla bug: the game loads its own topics once per launch).

## Zone chatter (`zones.dialogue.json`)

Ambient lines sims shout while hanging out in a zone. `AllZones` lines apply
everywhere. `Zones` entries match one zone by scene name (`"Brake"`) or display
name (`"Faerie's Brake"`), case insensitive.

Zone scene names: `Azure` (Port Azure), `Stowaway`, `Willowwatch`, `Hidden`,
`Brake`, `Vitheo`, `FernallaField`, `Bonepits`, `Duskenlight`, `Krakengard`,
`SaltedStrand`, `Rottenfoot`, `Underspine`, `Braxonian`, `Silkengrass`,
`Loomingwood`, `Elderstone`, `Windwashed`, `Malaroth`, `Undercity`,
`VitheosEnd`, `Rockshade`, `Soluna`, `Blight`, `Ripper`, `Abyssal`,
`PrielPlateau`, `Braxonia`, `AzynthiClear`. The full list with display names
and level ranges is in the vanilla dump (`05_post_load.txt`).

---

### Tips

- Write lines the way MMO players type: short, lowercase ish, imperfect.
  Look at the vanilla lines for tone.
- Don't add periods religiously. The quirk system handles typos and caps.
- Contractions on quirked sims follow TWO different rules, because the game's
  two quirk blocks apply their regexes in opposite orders:
  - `TypesInThirdPerson`: ASCII `I'm` and all curly forms convert correctly.
    ASCII `I'd`/`I'll`/`I've` garble ("paul'll be there").
  - `RefersToSelfAs`: **NO first person contraction converts.** The bare I
    replacement runs first and eats the I inside every contraction, so
    `I'm busy` renders "Vex'm busy". Write contraction free, or use the
    apostropheless MMO style (`im`/`dont`) which passes through untouched.
- JSON gotchas: escape quotes as `\"`, no trailing commas, no comments.
- If your sim already existed as a random generic sim (name collision with a
  previous save), delete their save file `Sims<Name>` in the game's
  `ESSaveData` folder to start them fresh as your custom sim.

### Debugging your pack

Everything logs to `BepInEx/LogOutput.log`:

- `[Packs]` lines confirm each file loaded, or say exactly why it didn't.
- **Misspelled a key?** The loader warns: `unknown key "Greeting" is ignored by
  the game - did you mean "Greetings"?` The game would otherwise silently
  ignore it.
- Set `VerboseLogging = true` in `BepInEx/config/erenshor.customsimframework.cfg`
  to also see per sim language statistics (how many lists are custom vs. filled
  from the game's pools) and a line every time your sim spawns into a zone.
- Set `DumpVanillaData = true` (one session, then turn it off) to dump every
  vanilla dialogue pool, all preauthored sims, the full zone list with display
  names, and guild data to a `VanillaDump` folder next to the mod DLL. The
  best reference for tone and valid values.
