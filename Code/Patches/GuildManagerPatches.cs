using System;
using HarmonyLib;

namespace Erenshor.CustomSimFramework.Patches
{
    /// <summary>
    /// POSTFIX on GuildManager.InitGuilds. Vanilla clears and rebuilds every
    /// guild's Conversations list inside this method (GuildManager.cs:154), so
    /// pack topics get (re)applied immediately after. InitGuilds is latched to
    /// one body-run per app launch (instance `init` flag on a persistent
    /// manager), but the postfix runs on every call. ApplyAll's per-guild
    /// Contains check makes that free. The zone-entry retry in
    /// InjectionPatches is the second application point and covers guilds
    /// founded mid-session.
    /// </summary>
    [HarmonyPatch(typeof(GuildManager), "InitGuilds")]
    internal static class GuildManager_InitGuilds_Topics
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                GuildTopicInjector.ApplyAll();
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Topics] Topic injection failed: " + ex);
            }
        }
    }

    /// <summary>
    /// Guild chat frequency dials. Two timers drive everything here:
    ///   GuildBanterDel -> SimPlayerChatInput = guild QUESTIONS + TOPIC
    ///     conversations (steady state Random(6000,15000), about 2-4 min, plus
    ///     a 20% fast retry of Random(100,600). GuildManager.cs:190-204)
    ///   GuildQuestDel -> GenerateGuildGuildQuest = fetch-quest asks
    ///     (Random(20000,100000), about 5.5-27 min. Lines 206-216)
    /// Both get re-armed inside the fire branch, so scaling works by
    /// JUMP-DETECTION. Outside of a re-arm the timers only ever decrease,
    /// which means a value that went UP was re-armed this frame, and we divide
    /// the fresh roll by the configured factor once. The Awake postfix scales
    /// the initial rolls and resets the trackers, which also guards the
    /// stale-tracker double-scale race if the manager is ever recreated.
    /// </summary>
    [HarmonyPatch(typeof(GuildManager), "Awake")]
    internal static class GuildManager_Awake_Dials
    {
        [HarmonyPostfix]
        private static void Postfix(GuildManager __instance)
        {
            try
            {
                float chat = CustomSimFrameworkPlugin.GuildChatFactor;
                float quest = CustomSimFrameworkPlugin.GuildQuestFactor;
                if (chat != 1f)
                {
                    __instance.GuildBanterDel /= chat;
                }
                if (quest != 1f)
                {
                    __instance.GuildQuestDel /= quest;
                }
                GuildManager_Update_Dials.ResetTrackers(__instance);
            }
            catch (Exception ex)
            {
                CustomSimFrameworkPlugin.Log.LogError("[Dials] Awake scaling failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(GuildManager), "Update")]
    internal static class GuildManager_Update_Dials
    {
        private static float _lastBanter;
        private static float _lastQuest;

        internal static void ResetTrackers(GuildManager gm)
        {
            _lastBanter = gm.GuildBanterDel;
            _lastQuest = gm.GuildQuestDel;
        }

        [HarmonyPostfix]
        private static void Postfix(GuildManager __instance)
        {
            // Always track, so a factor enabled mid-session never misreads a
            // mid-countdown value as a re-arm. Scale only on a detected jump.
            float banter = __instance.GuildBanterDel;
            if (banter > _lastBanter)
            {
                float factor = CustomSimFrameworkPlugin.GuildChatFactor;
                if (factor != 1f)
                {
                    banter /= factor;
                    __instance.GuildBanterDel = banter;
                    CustomSimFrameworkPlugin.LogDebug("[Dials] guild chat timer re-armed -> " + banter.ToString("F0"));
                }
            }
            _lastBanter = banter;

            float quest = __instance.GuildQuestDel;
            if (quest > _lastQuest)
            {
                float factor = CustomSimFrameworkPlugin.GuildQuestFactor;
                if (factor != 1f)
                {
                    quest /= factor;
                    __instance.GuildQuestDel = quest;
                    CustomSimFrameworkPlugin.LogDebug("[Dials] guild quest timer re-armed -> " + quest.ToString("F0"));
                }
            }
            _lastQuest = quest;
        }
    }
}
