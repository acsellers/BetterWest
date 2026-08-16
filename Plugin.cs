using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using HarmonyLib;

namespace BetterWest;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGUID = "com.asellers.betterwest";
    public const string PluginName = "Better West";
    public const string PluginVersion = "1.0.0";

    internal static new ManualLogSource Logger;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"{PluginName} v{PluginVersion} initialized!");
        Harmony.CreateAndPatchAll(typeof(ResourcesSystemPatch));
        Harmony.CreateAndPatchAll(typeof(SkillsManagerPatch));
        Harmony.CreateAndPatchAll(typeof(PlayerControllerPatch));
        Harmony.CreateAndPatchAll(typeof(BedsManagerPatch));
    }

    private void Update()
    {
        // Unity Legacy Input System (KeyCode.F12)
        // If the game uses the new Input System package, use: Keyboard.current.f12Key.wasPressedThisFrame
        if (Input.GetKeyDown(KeyCode.F12))
        {
            GetSkillValues();
        }
    }

    private void GetSkillValues()
    {
        // 1. Ensure PlayerToolController instance is loaded in the scene
        if (SkillsManager.Instance == null)
        {
            Logger.LogWarning("[F12 Pressed] SkillsManager.Instance is null. Are you in an active game session?");
            return;
        }

        // 2. Direct read if 'DigToolId' is public
        var speedLevel = StaticInstance<SkillsManager>.Instance.GetCurrentSkillLevel(SkillType.Speed);
        Logger.LogInfo($"SpeedLevel: {speedLevel}");
        var speedValue = StaticInstance<SkillsManager>.Instance.GetCurrentSkillValue(SkillType.Speed, true);
        Logger.LogInfo($"SpeedValue: {speedValue}");

        var bed = StaticInstance<BedsManager>.Instance.GetCurrentBedData();
        Logger.LogInfo($"BedData: {bed.EnergySkillLevel}");
        // list tool data from 0 to 4
        var runManager = StaticInstance<PlayerRunningController>.Instance;
        var tr = Traverse.Create(runManager);
        Logger.LogInfo($"Current Run Points: {tr.Field("maxRunPoints").GetValue()}");
    }

}

[HarmonyPatch(typeof(ResourcesSystem), nameof(ResourcesSystem.GetToolDataById))]
public static class ResourcesSystemPatch
{
    // Option A: POSTFIX — Let the original run, then alter or override the return value
    [HarmonyPostfix]
    public static void Postfix(int toolId, ref ToolDataSO __result)
    {
        // If the game didn't find a tool, or for specific target IDs:
        if (__result != null)
        {
            Plugin.Logger.LogInfo($"Modifying stats for tool ID: {toolId}");
            var tr = Traverse.Create(__result);

            tr.Field("radius").SetValue(0.4f + toolId * 0.15f);
            tr.Field("dropRate").SetValue(10+toolId * 4);
        }
    }
}

[HarmonyPatch(typeof(SkillsManager), nameof(SkillsManager.GetCurrentSkillValue))]
public static class SkillsManagerPatch
{
    // Option A: POSTFIX — Let the original run, then alter or override the return value
    [HarmonyPostfix]
    public static void Postfix(SkillType skillType, bool secondValue, ref float __result)
    {
        if (skillType == SkillType.Speed)
        {
            // add in bed energy
            var bed = StaticInstance<BedsManager>.Instance.GetCurrentBedData();
           __result = __result + (bed.EnergySkillLevel - 1) * 0.3f;
           Plugin.Logger.LogInfo($"SkillSystem.GetCurrentSkillValue({skillType}, {secondValue}) = {__result}");
        }
    }
}

[HarmonyPatch(typeof(PlayerController), nameof(PlayerController.SetAsLoaded))]
public static class PlayerControllerPatch {

    [HarmonyPrefix]
    public static void Prefix(PlayerController __instance) {
        Plugin.Logger.LogInfo("PlayerController.SetAsLoaded called");
        var skillLevel = StaticInstance<SkillsManager>.Instance.GetCurrentSkillValue(SkillType.Speed, secondValue: true);
        StaticInstance<PlayerRunningController>.Instance.UpdateMaxRunPoints(skillLevel);
    }
}

[HarmonyPatch(typeof(BedsManager), nameof(BedsManager.BuyBed))]
public static class BedsManagerPatch {
    [HarmonyPostfix]
    public static void Postfix(BedDataSO bedData) {
        Plugin.Logger.LogInfo("BedsManager.BuyBed called");
        var skillLevel = StaticInstance<SkillsManager>.Instance.GetCurrentSkillValue(SkillType.Speed, secondValue: true);
        StaticInstance<PlayerRunningController>.Instance.UpdateMaxRunPoints(skillLevel);
    }
}
