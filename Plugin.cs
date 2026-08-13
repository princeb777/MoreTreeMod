using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace RhiaShopRefresh
{
    [BepInPlugin("com.osn.rhiashoprefresh", "Rhia Shop Refresh", "2.4.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static FieldInfo currentDateField;
        private static GameDate savedDate;
        private static bool isMocking = false;
        private static int lastRefreshedDay = -1;

        private void Awake()
        {
            currentDateField = AccessTools.Field(typeof(WorldTime), "currentGameDate");
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo("Rhia Shop Refresh mod has loaded! (Version 2.4 - Minor Patch Update)");
        }

        // Reset the tracker when a new game session/save is loaded
        [HarmonyPatch(typeof(RhiaNPC), "Start")]
        [HarmonyPostfix]
        public static void RhiaNPC_Start_Postfix()
        {
            lastRefreshedDay = -1;
        }

        // 1. Hook CheckShops to ensure Rhia's updateDays contains every day of the week.
        // This handles the native refresh when you sleep and wake up on a new day.
        [HarmonyPatch(typeof(ShopDatabaseAccessor), nameof(ShopDatabaseAccessor.CheckShops))]
        [HarmonyPrefix]
        static void Prefix_CheckShops()
        {
            List<Shop> shops = ShopDatabaseAccessor.GetAllShops();
            if (shops == null) return;

            foreach (Shop shop in shops)
            {
                if (shop.shopType == ShopType.Rhia)
                {
                    if (shop.updateDays.Count < 7)
                    {
                        shop.updateDays.Clear();
                        shop.updateDays.Add(Day.Mon);
                        shop.updateDays.Add(Day.Tue);
                        shop.updateDays.Add(Day.Wed);
                        shop.updateDays.Add(Day.Thurs);
                        shop.updateDays.Add(Day.Fri);
                        shop.updateDays.Add(Day.Sat);
                        shop.updateDays.Add(Day.Sun);
                    }
                }
            }
        }

        // 2. Intercept opening the shop UI. 
        // This solves the "Same Day" issue! When you load a save, the game overwrites the shop dictionary with the saved data.
        // By intercepting right as you open the UI, we can force a fresh generation specifically for that same day.
        [HarmonyPatch(typeof(ShopUI), nameof(ShopUI.OpenUI))]
        [HarmonyPrefix]
        static void Prefix_ShopUI_OpenUI(ShopUI __instance)
        {
            if (__instance.shop != null && __instance.shop.shopType == ShopType.Rhia)
            {
                if (WorldTime.GetInstance() == null) return;
                
                int currentDay = (int)WorldTime.BCOGAGJCFNP.day;
                
                // If we haven't refreshed her shop yet in this session for today, force it right before the UI opens!
                if (lastRefreshedDay != currentDay)
                {
                    ShopDatabaseAccessor.CreateNewShopList(__instance.shop, false);
                }
            }
        }

        // 3. Hook JIHDBELJHNI (generates Rhia's special item).
        // Uses Reflection to physically spoof the date to Monday to bypass the hardcoded weekly check.
        [HarmonyPatch(typeof(Shop), "JIHDBELJHNI")]
        [HarmonyPrefix]
        static void Prefix_JIHDBELJHNI(Shop __instance)
        {
            if (__instance.shopType == ShopType.Rhia)
            {
                WorldTime instance = WorldTime.GetInstance();
                if (instance != null && currentDateField != null && !isMocking)
                {
                    // Update tracker so we don't double-refresh if the UI is opened later
                    lastRefreshedDay = (int)WorldTime.BCOGAGJCFNP.day;

                    savedDate = (GameDate)currentDateField.GetValue(instance);
                    GameDate fakeDate = savedDate;
                    fakeDate.day = Day.Mon;
                    currentDateField.SetValue(instance, fakeDate);
                    isMocking = true;
                }
            }
        }

        // Ensure the date is always restored perfectly even if an exception occurs during generation.
        [HarmonyPatch(typeof(Shop), "JIHDBELJHNI")]
        [HarmonyFinalizer]
        static void Finalizer_JIHDBELJHNI(Shop __instance)
        {
            if (__instance.shopType == ShopType.Rhia)
            {
                WorldTime instance = WorldTime.GetInstance();
                if (instance != null && currentDateField != null && isMocking)
                {
                    currentDateField.SetValue(instance, savedDate);
                    isMocking = false;
                }
            }
        }
    }
}
