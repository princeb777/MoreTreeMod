using BepInEx;
using HarmonyLib;
using System.Collections.Generic;

namespace RhiaShopRefresh
{
    [BepInPlugin("com.osn.rhiashoprefresh", "Rhia Shop Refresh", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo("Rhia Shop Refresh mod has loaded!");
        }

        // 1. Hook CheckShops to ensure Rhia's updateDays contains every day of the week.
        // This ensures the shop refresh logic is evaluated every day for Rhia.
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

        // We use this flag to mock the day only during Rhia's shop generation.
        public static bool forceMondayForRhia = false;

        // 2. Hook GOCFGBOMGMF (the method generating Rhia's special item).
        // Set the flag to true so we can mock WorldTime to return Monday.
        [HarmonyPatch(typeof(Shop), "GOCFGBOMGMF")]
        [HarmonyPrefix]
        static void Prefix_GOCFGBOMGMF(Shop __instance)
        {
            if (__instance.shopType == ShopType.Rhia)
            {
                forceMondayForRhia = true;
            }
        }

        // Ensure the flag is always reset even if an exception occurs during generation.
        [HarmonyPatch(typeof(Shop), "GOCFGBOMGMF")]
        [HarmonyFinalizer]
        static void Finalizer_GOCFGBOMGMF(Shop __instance)
        {
            if (__instance.shopType == ShopType.Rhia)
            {
                forceMondayForRhia = false;
            }
        }

        // 3. Postfix the GameDate property getter to trick the Monday check.
        // GOCFGBOMGMF explicitly checks `WorldTime.NOAOJJLNHJJ.day == Day.Mon`.
        [HarmonyPatch(typeof(WorldTime), nameof(WorldTime.NOAOJJLNHJJ), MethodType.Getter)]
        [HarmonyPostfix]
        static void Postfix_get_NOAOJJLNHJJ(ref GameDate __result)
        {
            if (forceMondayForRhia)
            {
                __result.day = Day.Mon;
            }
        }
    }
}
