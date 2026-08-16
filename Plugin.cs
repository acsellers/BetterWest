using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using HarmonyLib;
using System.Reflection;

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
        Harmony.CreateAndPatchAll(typeof(PetroleumMachinePatch));
        Harmony.CreateAndPatchAll(typeof(PetroleumMachineUpdateFuelPatch));
    }

    private void Update()
    {
        // Unity Legacy Input System (KeyCode.F11)
        // If the game uses the new Input System package, use: Keyboard.current.f11Key.wasPressedThisFrame
        if (Input.GetKeyDown(KeyCode.F11))
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
    private static readonly FieldInfo dropRateField = AccessTools.Field(typeof(ToolDataSO), "dropRate");
    private static readonly FieldInfo radiusField = AccessTools.Field(typeof(ToolDataSO), "radius");
    // Option A: POSTFIX — Let the original run, then alter or override the return value
    [HarmonyPostfix]
    public static void Postfix(int toolId, ref ToolDataSO __result)
    {
        if (__result == null) return;
        
        dropRateField.SetValue(__result, 10+toolId * 4);
        radiusField.SetValue(__result, 0.4f + toolId * 0.15f);
    }
}

[HarmonyPatch(typeof(SkillsManager), nameof(SkillsManager.GetCurrentSkillValue))]
public static class SkillsManagerPatch
{
    // Option A: POSTFIX — Let the original run, then alter or override the return value
    [HarmonyPostfix]
    public static void Postfix(SkillType skillType, bool secondValue, ref float __result)
    {
        if (skillType == SkillType.Speed && secondValue)
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

[HarmonyPatch(typeof(PetroleumMachine), nameof(PetroleumMachine.ResetTime))]
public static class PetroleumMachinePatch {
    [HarmonyPrefix]
    public static void Prefix(PetroleumMachine __instance) {
        Plugin.Logger.LogInfo("PetroleumMachine.ResetTime called");
        var tr = Traverse.Create(StaticInstance<FurnacesManager>.Instance);
        tr.Field("petroleumMachineWorkTime").SetValue(100f);
    }
}

[HarmonyPatch(typeof(PetroleumMachine), "UpdateFuel")]
public static class PetroleumMachineUpdateFuelPatch {
    [HarmonyPrefix]
    static bool Prefix(PetroleumMachine __instance, FurnaceDatabase ___database, bool ___isWorkerSet, FuelStateInfo ___fuelStateInfo, float ___fuelCapacity) {
        if (___database.SmeltTimeLeft <= 0f || !___isWorkerSet || ___database.HasFullBarrel == 1)
        {
            ___fuelStateInfo.Set(___database.Fuel / ___fuelCapacity);
            return false;
        }
        float num = ___fuelCapacity / 500f;
        float num2 = ___database.Fuel - num * Time.deltaTime;
        if (num2 < 0f)
        {
            num2 = 0f;
        }
        ___database.SetFuel(num2);
        ___fuelStateInfo.Set(___database.Fuel / ___fuelCapacity);

        return false;
    }
}
