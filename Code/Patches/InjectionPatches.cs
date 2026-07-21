using System;
using System.Collections.Generic;
using Erenshor.CustomSimFramework.Data;
using HarmonyLib;

namespace Erenshor.CustomSimFramework.Patches
{
    /// <summary>
    /// PREFIX on SimPlayerMngr.Start (runs AFTER the vanilla-dump prefix, which has
    /// higher priority, so dumps capture unmodified state). Registers custom sim
    /// templates into ActualSims and appends global dialogue additions, all before
    /// the manager's own body kicks off LoadSimPlayersIntoGame, which then treats our
    /// sims exactly like the game's pre-authored ones.
    /// </summary>
    [HarmonyPatch(typeof(SimPlayerMngr), "Start")]
    internal static class SimPlayerMngr_Start_Inject
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(SimPlayerMngr __instance)
        {
            CustomSimFrameworkPlugin.OnManagerStart();
            try
            {
                GlobalDialogueInjector.Apply(__instance);
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] Global dialogue injection failed: " + ex);
            }
            try
            {
                SimTemplateBuilder.InjectAll(__instance);
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] Sim injection failed: " + ex);
            }
        }
    }

    /// <summary>
    /// POSTFIX on SimPlayerTracking.SpawnMeInGame. The vanilla premade-copy transfers
    /// most quirks to the spawned body but misses TypesInAllLowers, RefersToSelfAs and
    /// Abbreviates. Checked in the decompile: SpawnMeInGame copies only AllCaps,
    /// emojis, third-person and typo fields, and LoadSpawnTemplateFromPremade adds
    /// bio and sign-offs. For our sims we re-apply the complete quirk set to the
    /// live instance.
    /// </summary>
    [HarmonyPatch(typeof(SimPlayerTracking), nameof(SimPlayerTracking.SpawnMeInGame))]
    internal static class SpawnMeInGame_QuirkFix
    {
        [HarmonyPostfix]
        private static void Postfix(SimPlayer __result, SimPlayerTracking _sim)
        {
            if (__result == null || _sim == null || string.IsNullOrEmpty(_sim.SimName))
            {
                return;
            }
            SimDefinition def;
            if (!SimTemplateBuilder.BuiltSims.TryGetValue(_sim.SimName, out def))
            {
                return;
            }
            try
            {
                SimTemplateBuilder.ApplyQuirks(__result, def);
                __result.SkillLevel = def.SkillLevel;

                // Vanilla overwrites the spawned body's PersonalityType from tracking
                // (default 1) in LoadSimPlayerTemplate, ignoring the template value.
                // Honor an explicit pack setting. 0 keeps the game's default, which
                // for premade-pipeline sims never rolls and resolves to 1 (Nice),
                // with BioIndex fixed at 1 when Bio is empty.
                if (def.PersonalityType != 0)
                {
                    __result.PersonalityType = def.PersonalityType;
                    if (string.IsNullOrEmpty(def.Bio))
                    {
                        // Roll the bio once per session, not per spawn, so the
                        // inspect bio doesn't change every time the sim zones in.
                        if (def.RolledBioIndex < 0)
                        {
                            List<string> bioPool = GetBioPool(def.PersonalityType);
                            if (bioPool != null && bioPool.Count > 0)
                            {
                                def.RolledBioIndex = UnityEngine.Random.Range(0, bioPool.Count);
                            }
                        }
                        if (def.RolledBioIndex >= 0)
                        {
                            __result.BioIndex = def.RolledBioIndex;
                        }
                    }
                }
                CustomSimFrameworkPlugin.LogDebug("Spawn fix-up applied to '" + _sim.SimName
                    + "' (personality " + __result.PersonalityType + ", zone " + _sim.CurScene + ")");
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] Quirk fix-up failed for '" + _sim.SimName + "': " + ex);
            }
        }

        private static List<string> GetBioPool(int personalityType)
        {
            SimPlayerMngr mngr = GameData.SimMngr;
            if (mngr == null)
            {
                return null;
            }
            switch (personalityType)
            {
                case 1: return mngr.NiceDesciptions;
                case 2: return mngr.TryhardDescriptions;
                case 3: return mngr.MeanDescriptions;
                default: return null;
            }
        }
    }

    /// <summary>
    /// PREFIX on ZoneAnnounce.Start (after the dump prefix). Appends pack-provided
    /// zone chatter to this zone's ZoneComments, the pool ambient sim shouts draw
    /// from (SimPlayerLanguage.GetGeneric reads GameData.CurrentZoneAnnounce).
    /// Matches by scene name or display name, case-insensitive. "AllZones" lines go
    /// everywhere. Dedup makes revisits harmless.
    /// Also retries the post-load roster dump here, since zone entry is a reliable
    /// main-thread moment well after the sim roster has loaded.
    /// </summary>
    [HarmonyPatch(typeof(ZoneAnnounce), "Start")]
    internal static class ZoneAnnounce_Start_Inject
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(ZoneAnnounce __instance)
        {
            // Post-load work FIRST. It contains the once-per-launch vanilla
            // dump, and the pool appends below would contaminate it. The
            // grouping pools only become available at zone entry, so this is
            // exactly where the dump usually fires. Round 2 of the v0.9.4 fix,
            // a captured dump proved the appends were running first here.
            try
            {
                CustomSimFrameworkPlugin.TryPostLoadWork();
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] post-load work attempt failed: " + ex);
            }
            try
            {
                ApplyZoneChatter(__instance);
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] Zone chatter injection failed: " + ex);
            }
            // Guaranteed-late retry for the grouping/guild pool appends. The
            // vanilla Start body dereferences GameData.SimPlayerGrouping
            // (ZoneAnnounce.cs:140), so by every zone entry both managers
            // exist. Latched internally, a no-op once applied this login.
            try
            {
                GlobalDialogueInjector.ApplyGroupingPools();
                GlobalDialogueInjector.ApplyGuildPools();
                GlobalDialogueInjector.ValidateZoneTargets();
                // Guild topics: idempotent top-up (per-guild Contains check).
                // Primary application is the InitGuilds postfix. This covers
                // guilds founded mid-session and any late manager state.
                GuildTopicInjector.ApplyAll();
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Inject] pool append retry failed: " + ex);
            }
        }

        private static void ApplyZoneChatter(ZoneAnnounce zone)
        {
            if (PackLoader.ZoneDialogues.Count == 0)
            {
                return;
            }
            if (zone.ZoneComments == null)
            {
                zone.ZoneComments = new List<string>();
            }
            string sceneName = zone.gameObject.scene.IsValid() ? zone.gameObject.scene.name : "";
            int added = 0;

            foreach (ZoneDialogueDefinition def in PackLoader.ZoneDialogues)
            {
                added += GlobalDialogueInjector.AppendLines(zone.ZoneComments, def.AllZones);
                if (def.Zones == null)
                {
                    continue;
                }
                foreach (ZoneLines entry in def.Zones)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Zone))
                    {
                        continue;
                    }
                    bool matches = string.Equals(entry.Zone, sceneName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.Zone, zone.ZoneName, StringComparison.OrdinalIgnoreCase);
                    if (matches)
                    {
                        added += GlobalDialogueInjector.AppendLines(zone.ZoneComments, entry.Lines);
                    }
                }
            }
            if (added > 0)
            {
                CustomSimFrameworkPlugin.Log.LogInfo("[Inject] " + added + " chatter line(s) added to zone '"
                    + (string.IsNullOrEmpty(sceneName) ? zone.ZoneName : sceneName) + "'.");
            }
        }
    }
}
