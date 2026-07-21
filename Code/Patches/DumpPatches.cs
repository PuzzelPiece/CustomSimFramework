using System;
using HarmonyLib;

namespace Erenshor.CustomSimFramework.Patches
{
    /// <summary>
    /// PREFIX on SimPlayerMngr.Start. This is the exact hook point where injection
    /// registers custom sims into ActualSims and appends lines to the global dialogue
    /// pools, so the dump captures game state precisely as injection will see it.
    /// Runs before the manager's own body, which kicks off LoadSimPlayersIntoGame.
    /// </summary>
    [HarmonyPatch(typeof(SimPlayerMngr), "Start")]
    internal static class SimPlayerMngr_Start_Dump
    {
        // The blank sim template is a shared prefab ASSET: pack appends and
        // the game's own per-login refill persist on it for the whole app
        // run, so only the FIRST login of a launch sees it vanilla (v0.9.4).
        private static bool _dumpedThisLaunch;

        // High priority: runs before the injection prefix so dumps capture vanilla state.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static void Prefix(SimPlayerMngr __instance)
        {
            if (!CustomSimFrameworkPlugin.DumpEnabled || _dumpedThisLaunch)
            {
                return;
            }
            _dumpedThisLaunch = true;
            try
            {
                VanillaDumper.DumpAtManagerStart(__instance);
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Dump] manager dump failed: " + ex);
            }
        }
    }

    /// <summary>
    /// PREFIX on ZoneAnnounce.Start. Captures each zone's ZoneComments (the ambient
    /// sim chatter pool) as we enter it. Prefix rather than postfix so the dump still
    /// happens even if something in the zone's own Start body throws.
    /// The zone chatter injection uses this same hook.
    /// </summary>
    [HarmonyPatch(typeof(ZoneAnnounce), "Start")]
    internal static class ZoneAnnounce_Start_Dump
    {
        // High priority: runs before the zone-chatter injection prefix.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static void Prefix(ZoneAnnounce __instance)
        {
            if (!CustomSimFrameworkPlugin.DumpEnabled)
            {
                return;
            }
            try
            {
                VanillaDumper.DumpZone(__instance);
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Dump] zone dump failed: " + ex);
            }
        }
    }
}
