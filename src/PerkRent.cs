using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyStolenHellMode
{
    internal static class PerkRent
    {
        private const string ReserveKey = "rent_reserve";
        private const string RemainderKey = "rent_reserve_tenths";

        private static bool IsRenting(PlayerStore store) => !store.IsPropertyPaid && !store.isPayingMortgage;
        private static bool ReserveActive(PlayerStore store) => HellPerks.Has(HellPerks.RentReserve) && IsRenting(store);
        internal static int Reserved => ModHelper.GetModDataInt(Entry.ModId, ReserveKey, 0);

        private static void SaveReserve(int amount, int remainder = 0)
        {
            ModHelper.SetModDataInt(Entry.ModId, ReserveKey, amount);
            ModHelper.SetModDataInt(Entry.ModId, RemainderKey, remainder);
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.GetRentDayCounter))]
        private static class FridaySchedule
        {
            private static void Postfix(PlayerStore __instance, ref int __result)
            {
                if (!HellPerks.Has(HellPerks.FridayRent) || !IsRenting(__instance)) return;
                int day = StoreStation.GetDayCounter();
                if (day > 7) __result = (12 - day % 7) % 7;
            }
        }

        [HarmonyPatch(typeof(RunStats), nameof(RunStats.OnItemSold))]
        private static class ReserveFromSale
        {
            private static void Prefix(int cost, StoreClient client)
            {
                if (cost <= 0 || client == null || !client.IsClientBuying() || !PlayerStore.HasInstance) return;
                var store = PlayerStore.Instance;
                if (!ReserveActive(store)) return;

                int reserved = Reserved;
                int remaining = store.GetRentValue() - reserved;
                if (remaining <= 0) return;

                long tenths = cost * 3L + ModHelper.GetModDataInt(Entry.ModId, RemainderKey, 0);
                int deposit = (int)Math.Min(tenths / 10, Math.Min(remaining, Math.Max(0, store.playerCash)));
                store.playerCash -= deposit;
                SaveReserve(reserved + deposit, deposit < remaining ? (int)(tenths % 10) : 0);
            }
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.CanPayRent))]
        private static class IncludeHeldCash
        {
            private static void Postfix(PlayerStore __instance, ref bool __result)
            {
                if (!__result && ReserveActive(__instance))
                    __result = (long)__instance.playerCash + Reserved >= __instance.GetRentValue();
            }
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.OnRentPaid))]
        private static class SettleRent
        {
            private static void Prefix(PlayerStore __instance)
            {
                if (!ReserveActive(__instance)) return;
                __instance.playerCash += Reserved;
                SaveReserve(0);
            }
        }

        [HarmonyPatch]
        private static class RefundReserve
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(PlayerStore), nameof(PlayerStore.BuyOutStore));
                yield return AccessTools.Method(typeof(PlayerStore), nameof(PlayerStore.TakeMortgage));
            }

            private static void Postfix(PlayerStore __instance)
            {
                if (!HellPerks.Has(HellPerks.RentReserve) || IsRenting(__instance)) return;
                __instance.playerCash += Reserved;
                SaveReserve(0);
            }
        }

        [HarmonyPatch(typeof(AdvCalendarUIManager), nameof(AdvCalendarUIManager.OnCalendarButtonClicked))]
        private static class Calendar
        {
            private static void Prefix()
            {
                var store = PlayerStore.Instance;
                if (HellPerks.Has(HellPerks.FridayRent) && IsRenting(store)) store.dayUntilRent = store.GetRentDayCounter();
            }

            private static void Postfix(AdvCalendarUIManager __instance)
            {
                var store = PlayerStore.Instance;
                if (!ReserveActive(store)) return;
                __instance.landlordTMP.text += "\n" + string.Format(
                    Copy.Text("已预存 {0}，还需 {1}", "Reserved {0}, {1} still due"), Reserved, Math.Max(0, store.GetRentValue() - Reserved));
            }
        }
    }
}
