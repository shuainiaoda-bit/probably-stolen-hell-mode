using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyStolenHellMode
{
    internal static class PerkRestrictions
    {
        private static readonly HashSet<string> ScheduledSuppliers = new HashSet<string>
        {
            "supplier", "supplierIntro", "hypertech_smuggler", "inventor", "inventorStorage",
            "retired_gunsmith", "retired_farmer", "retired_aquafarmer", "retired_rancher", "retired_water_merchant",
            "retired_winemaker", "retired_junker", "retired_junker_intro", "retired_chemist",
            "lower_level_rare_merchant", "secSurplusMerchant", "revRaider", "revQuartermaster", Provisioner.ClientId
        };

        private static readonly HashSet<string> GameplayUnlocks = new HashSet<string>
        {
            "BREWER", "CHEMIST", "PHARMA", "MINER", "BREWER_SHAREHOLDER", "CHEMIST_SHAREHOLDER",
            "PHARMA_SHAREHOLDER", "MINER_SHAREHOLDER", "GUTTERFLOW_TAP", "RUSTWATER_TAP", "RUINED_MACHINE_UNLOCK",
            "RETIRED_FARMER", "RETIRED_GUNSMITH", "RETIRED_CHEMIST", "RETIRED_RANCHER", "WATER_TRADER",
            "GUN_PERMIT_I", "GUN_PERMIT_II", "GUN_PERMIT_III", "PLANT_DNA", "VENDING_MACHINE", "VENDING_FOUNTAIN",
            "AQUACULTURE", "AQUACULTURE_II", "WINEMAKER", "MARKETPLACE_REQUESTS", "HYPERTECH_SMUGGLER"
        };

        private static bool UnlockBlocked(NetworkUpgrade upgrade) => upgrade != null && !upgrade.IsUnlocked() &&
            HellPerks.Has(HellPerks.NoUnlocks) && GameplayUnlocks.Contains(upgrade.id);

        [HarmonyPatch]
        private static class BlockFaucet
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(GameItem), nameof(GameItem.MayTarget));
                yield return AccessTools.Method(typeof(GameItem), nameof(GameItem.CanTarget));
                yield return AccessTools.Method(typeof(GameItem), nameof(GameItem.Target));
            }

            private static bool Prefix(GameItem targetItem) => targetItem?.identifier != "faucet" || !HellPerks.Has(HellPerks.NoTap);
        }

        [HarmonyPatch]
        private static class FilterVisitors
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(StoreClientManager), nameof(StoreClientManager.AddClient));
                yield return AccessTools.Method(typeof(StoreClientManager), nameof(StoreClientManager.AddNextClient));
            }

            private static bool Prefix(StoreClient storeClient) => storeClient == null || !HellPerks.Has(HellPerks.WalkInsOnly) ||
                !ScheduledSuppliers.Contains(storeClient.identifier);
        }

        [HarmonyPatch(typeof(NetworkUpgrade), nameof(NetworkUpgrade.IsReady))]
        private static class BlockPurchase
        {
            private static void Postfix(NetworkUpgrade __instance, ref bool __result)
            {
                if (__result && UnlockBlocked(__instance)) __result = false;
            }
        }

        [HarmonyPatch(typeof(WildNetworkUI), "ShowInfo")]
        private static class ExplainLock
        {
            private static void Postfix(string id, WildNetworkFrame ___frame)
            {
                if (id == null || !PlayerStore.Instance.networkUpgrade.TryGetValue(id, out var upgrade) || !UnlockBlocked(upgrade)) return;
                ___frame.infoMessage.text = Copy.Text("担保人已注销", "Guarantor Struck Off");
                ___frame.infoMessage.gameObject.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(WildUIManager), nameof(WildUIManager.OnUnlockClicked))]
        private static class BlockLegacyPurchase
        {
            private static bool Prefix(string ___currentSelectedId) => ___currentSelectedId == null ||
                !PlayerStore.Instance.networkUpgrade.TryGetValue(___currentSelectedId, out var upgrade) || !UnlockBlocked(upgrade);
        }
    }
}
