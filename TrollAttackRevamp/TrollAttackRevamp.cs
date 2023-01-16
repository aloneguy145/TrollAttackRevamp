using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;

namespace TrollAttackRevamp
{
    [BepInPlugin(modGUID, modName, modVersion)]
    
    public class TrollAttackRevamp : BaseUnityPlugin
    {
        private const string modGUID = "blacks7ar.TrollAttackRevamp";
        private const string modName = "TrollAttackRevamp";
        private const string modVersion = "1.0.0.0";
        private static string configFileName = "blacks7ar.TrollAttackRevamp";
        private static string configFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + configFileName;

        private static readonly ManualLogSource TARLogger = BepInEx.Logging.Logger.CreateLogSource("TrollAttackRevamp");
        
        private static readonly ConfigSync configSync = new ConfigSync(modGUID)
        {
            DisplayName = modName,
            CurrentVersion = modVersion,
            MinimumRequiredVersion = modVersion
        };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        private static ConfigEntry<Toggle> serverConfigLocked = null;
        private static ConfigEntry<Toggle> enableMod = null;
        private static ConfigEntry<int> trollLevel = null;
        private static ConfigEntry<float> intervalDivider = null;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, 
            bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = base.Config.Bind(group, name, value, description);
            configSync.AddConfigEntry(configEntry).SynchronizedConfig = synchronizedSetting;
            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description, 
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description, null), synchronizedSetting);
        }
        
        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, configFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(configFileFullPath)) return;
            try
            {
                TARLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                TARLogger.LogError($"There was an issue loading your {configFileName}");
                TARLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        private void Awake()
        {
            serverConfigLocked = config("1 - ServerSync", "Lock Configuration", Toggle.On,
                new ConfigDescription("If On, the configuration is locked and can be changed by server admins only.",
                    null));
            configSync.AddLockingConfigEntry(serverConfigLocked);
            enableMod = config("2 - General", "Enable Mod", Toggle.On,
                new ConfigDescription("Enable/Disable mod.", null));
            trollLevel = config("3 - Troll.Config", "Number of Stars", 1,
                new ConfigDescription("At what level the troll starts attacking more.",
                    new AcceptableValueRange<int>(1, 3)));
            intervalDivider = config("3 - Troll.Config", "Attack Interval Divider", 2f,
                new ConfigDescription("Attack interval divider. Value of 2 makes the troll attack 2x faster",
                    new AcceptableValueRange<float>(2f, 5f)));
            
            Assembly assembly = Assembly.GetExecutingAssembly();
            new Harmony(modGUID).PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Harmony.UnpatchID(modGUID);
        }
        
        [HarmonyPatch(typeof(Character), "ApplyDamage")]
        public class CharacterAwake_Patch
        {
            private static void Postfix(Character __instance, HitData hit, bool showDamageText, bool triggerEffects,
                HitData.DamageModifier mod = HitData.DamageModifier.Normal)
            {
                if (!(__instance != null) || hit == null || __instance.IsBoss() || __instance.IsPlayer() ||
                    !ZNet.instance.IsServer())
                {
                    return;
                }

                int level = __instance.GetLevel();
                if (__instance.m_name == "$enemy_troll" && level >= trollLevel.Value)
                {
                    if (enableMod.Value == Toggle.On)
                    {
                        MonsterAI component = __instance.GetComponent<MonsterAI>();
                        intervalDivider.SettingChanged += delegate
                        {
                            component.m_minAttackInterval /= intervalDivider.Value;
                            __instance.m_speed *= intervalDivider.Value;
                            __instance.m_runSpeed *= intervalDivider.Value;
                            __instance.m_turnSpeed *= intervalDivider.Value;
                            __instance.m_runTurnSpeed *= intervalDivider.Value;
                        };
                        component.m_minAttackInterval /= intervalDivider.Value;
                        __instance.m_speed *= intervalDivider.Value;
                        __instance.m_runSpeed *= intervalDivider.Value;
                        __instance.m_turnSpeed *= intervalDivider.Value;
                        __instance.m_runTurnSpeed *= intervalDivider.Value;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }
    }
}