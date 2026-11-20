using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyStolenHellMode
{
    internal static class BranchCompatibility
    {
        private static readonly Type StockPages = ModReflect.FindType("StockPageHelper");
        private static bool hasEndingRegistry;

        internal static void RegisterEndings()
        {
            try { ModReflect.Target(typeof(ModHelper), "AddEnding"); hasEndingRegistry = true; }
            catch (ModReflectAccessException) { hasEndingRegistry = false; }
            if (!hasEndingRegistry) return;
            ModReflect.CallStatic(typeof(ModHelper), "AddEnding", Entry.ModId, Entry.Starvation,
                (Func<string>)(() => Copy.Text("饿死", "Starved")),
                (Func<string>)(() => Copy.Text("你的食物储备耗尽了。", "You ran out of food.")), false, null);
            ModReflect.CallStatic(typeof(ModHelper), "AddEnding", Entry.ModId, Entry.Dehydration,
                (Func<string>)(() => Copy.Text("渴死", "Died of thirst")),
                (Func<string>)(() => Copy.Text("你的饮水储备耗尽了。", "You ran out of drinking water.")), false, null);
        }

        internal static void RemoveEndings()
        {
            if (hasEndingRegistry) ModReflect.CallStatic(typeof(ModHelper), "RemoveEndings", Entry.ModId);
            hasEndingRegistry = false;
        }

        internal static List<GameItem> HeldItems(StoreClient seller) => StockPages == null
            ? new List<GameItem>()
            : (List<GameItem>)ModReflect.CallStatic(StockPages, "GetHeldItems", seller);

        internal static void PurchaseDone(StoreClient seller)
        {
            if (StockPages != null) ModReflect.CallStatic(StockPages, "OnPurchaseDone", seller);
        }

        internal static bool EndingText(string ending, out string title, out string description)
        {
            title = description = null;
            if (ending == Entry.Starvation) {
                title = Copy.Text("饿死", "Starved");
                description = Copy.Text("你的食物储备耗尽了。", "You ran out of food.");
            } else if (ending == Entry.Dehydration) {
                title = Copy.Text("渴死", "Died of thirst");
                description = Copy.Text("你的饮水储备耗尽了。", "You ran out of drinking water.");
            }
            return title != null;
        }

        [HarmonyPatch(typeof(GameOverUIManager), nameof(GameOverUIManager.DisplayUI))]
        private static class LegacyEndingText
        {
            private static void Prefix(ref string givenReason, ref string givenDescription)
            {
                if (!Entry.Enabled || hasEndingRegistry || !Rations.HasPendingEnding) return;
                if (EndingText(Rations.PendingEnding, out var title, out var description)) {
                    givenReason = title;
                    givenDescription = description;
                }
            }
        }

        [HarmonyPatch(typeof(RunSummaryUI), "EndingTitle")]
        private static class LegacyEndingSummary
        {
            private static bool Prefix(string endingId, ref string __result)
            {
                if (!Entry.Enabled || hasEndingRegistry || !EndingText(endingId, out var title, out _)) return true;
                __result = title;
                return false;
            }
        }
    }
}
