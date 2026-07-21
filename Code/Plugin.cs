using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Erenshor.CustomSimFramework
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CustomSimFrameworkPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "erenshor.customsimframework";
        public const string PluginName = "Custom Sim Framework";
        public const string PluginVersion = "0.9.5";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> CfgDumpVanillaData;
        internal static ConfigEntry<bool> CfgVerboseLogging;
        internal static ConfigEntry<float> CfgGuildChatFrequency;
        internal static ConfigEntry<float> CfgGuildQuestFrequency;

        // Cached so patch code pays a single static read instead of a
        // ConfigEntry property access. Kept in sync via SettingChanged.
        internal static bool DumpEnabled;
        internal static bool Verbose;
        internal static float GuildChatFactor = 1f;   // clamped [0.1, 10]
        internal static float GuildQuestFactor = 1f;  // clamped [0.1, 10]

        private Harmony _harmony;
        private static CustomSimFrameworkPlugin _instance;
        private static bool _postLoadDumped;       // once per game launch
        private static bool _postLoadFixupsDone;   // once per login

        private void Awake()
        {
            _instance = this;
            Log = Logger;

            CfgDumpVanillaData = Config.Bind(
                "Diagnostics",
                "DumpVanillaData",
                false,
                "Writes the vanilla sim data (global dialogue pools, pre-authored sims, zone " +
                "comments, timing checks) to BepInEx/plugins/CustomSimFramework/VanillaDump. " +
                "Captured BEFORE framework injection, on the FIRST login of the app run, so " +
                "it reflects the unmodified game. Only needed for development / pack authoring.");

            CfgVerboseLogging = Config.Bind(
                "Diagnostics",
                "VerboseLogging",
                false,
                "Logs detailed framework activity: per-sim language fill statistics, spawn " +
                "fix-ups, zone chatter matching. Useful when developing a pack or hunting a " +
                "bug; noisy otherwise. Zero cost when disabled.");

            CfgGuildChatFrequency = Config.Bind(
                "Dialogue",
                "GuildChatFrequency",
                1f,
                new ConfigDescription(
                    "How often guild sims start QUESTIONS and TOPIC CONVERSATIONS in guild chat. " +
                    "1 = vanilla (roughly every 2-4 minutes, with occasional quick follow-ups); " +
                    "2 = twice as often; 0.5 = half as often. Takes effect from the next timer " +
                    "cycle; also applies (rarely, harmlessly) while at character select.",
                    new AcceptableValueRange<float>(0.1f, 10f)));

            CfgGuildQuestFrequency = Config.Bind(
                "Dialogue",
                "GuildQuestFrequency",
                1f,
                new ConfigDescription(
                    "How often guild sims post fetch-quest asks (\"anyone have a spare X?\"). " +
                    "1 = vanilla (roughly every 5-27 minutes); 2 = twice as often; 0.5 = half.",
                    new AcceptableValueRange<float>(0.1f, 10f)));

            DumpEnabled = CfgDumpVanillaData.Value;
            Verbose = CfgVerboseLogging.Value;
            GuildChatFactor = ClampFactor(CfgGuildChatFrequency.Value);
            GuildQuestFactor = ClampFactor(CfgGuildQuestFrequency.Value);
            CfgDumpVanillaData.SettingChanged += delegate
            {
                DumpEnabled = CfgDumpVanillaData.Value;
                enabled = true; // Update() re-evaluates and self-disables when idle
            };
            CfgVerboseLogging.SettingChanged += delegate
            {
                Verbose = CfgVerboseLogging.Value;
            };
            CfgGuildChatFrequency.SettingChanged += delegate
            {
                GuildChatFactor = ClampFactor(CfgGuildChatFrequency.Value);
            };
            CfgGuildQuestFrequency.SettingChanged += delegate
            {
                GuildQuestFactor = ClampFactor(CfgGuildQuestFrequency.Value);
            };

            PackLoader.LoadAll();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(CustomSimFrameworkPlugin).Assembly);

            Log.LogInfo(PluginName + " " + PluginVersion + " loaded. Sims=" + PackLoader.Sims.Count
                + " DumpVanillaData=" + DumpEnabled + " VerboseLogging=" + Verbose);
        }

        /// <summary>
        /// Called from the SimPlayerMngr.Start prefix each login. Re-arms the
        /// once-per-login work (save fixups, grouping pool injection) and wakes
        /// the Update poll. Update self-disables once everything is done, so
        /// steady-state gameplay has zero per-frame cost.
        /// </summary>
        internal static void OnManagerStart()
        {
            _postLoadFixupsDone = false;
            GlobalDialogueInjector.ResetPerLogin();
            if (_instance != null)
            {
                _instance.enabled = true;
            }
        }

        private void Update()
        {
            TryPostLoadWork();
            if (_postLoadFixupsDone && (_postLoadDumped || !DumpEnabled))
            {
                enabled = false;
            }
        }

        /// <summary>Frequency factors must stay in the vanilla-sane band even if
        /// the config file is hand-edited outside the AcceptableValueRange.</summary>
        private static float ClampFactor(float value)
        {
            if (value < 0.1f)
            {
                return 0.1f;
            }
            if (value > 10f)
            {
                return 10f;
            }
            return value;
        }

        /// <summary>Detail logging that compiles to a single static bool check when off.</summary>
        internal static void LogDebug(string msg)
        {
            if (Verbose)
            {
                Log.LogInfo("[Debug] " + msg);
            }
        }

        /// <summary>Runs once-per-login fixups and the optional post-load dump as
        /// soon as the sim roster has finished loading. Also invoked from zone
        /// entry as a fallback trigger.</summary>
        internal static void TryPostLoadWork()
        {
            bool fixupsPending = !_postLoadFixupsDone;
            bool dumpPending = DumpEnabled && !_postLoadDumped;
            if (!fixupsPending && !dumpPending)
            {
                return;
            }
            SimPlayerMngr mngr = GameData.SimMngr;
            if (mngr == null || !mngr.LoadedSimplayers)
            {
                return;
            }
            // The dump has to run first. It captures the vanilla grouping and
            // guild pools plus the roster before any fixups, and the injections
            // below would leak pack lines into it. Learned that the hard way in
            // v0.9.4 when our own shipped dump turned out to be contaminated.
            if (dumpPending)
            {
                _postLoadDumped = true;
                VanillaDumper.DumpPostLoad(mngr);
            }
            if (fixupsPending)
            {
                _postLoadFixupsDone = true;
                SimTemplateBuilder.ApplyPostLoadFixups(mngr);
                try
                {
                    GlobalDialogueInjector.ApplyGroupingPools();
                }
                catch (System.Exception ex)
                {
                    Log.LogError("[Global] Grouping pool injection failed: " + ex);
                }
                try
                {
                    GlobalDialogueInjector.ApplyGuildPools();
                }
                catch (System.Exception ex)
                {
                    Log.LogError("[Global] Guild pool injection failed: " + ex);
                }
                try
                {
                    GlobalDialogueInjector.ValidateZoneTargets();
                }
                catch (System.Exception ex)
                {
                    Log.LogError("[Packs] Zone-target validation failed: " + ex);
                }
            }
        }
    }
}
