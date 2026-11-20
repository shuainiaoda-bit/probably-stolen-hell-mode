using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace ProbablyStolenHellMode
{
    internal static class PerkInsurance
    {
        private const string WeekKey = "insurance_week";
        private const string CategoryKey = "insurance_category";

        internal enum Category { Equipment, Ordinary, Valuables }

        private static int Week => Math.Max(0, StoreStation.GetDayCounter() - 1) / 7;
        private static bool Locked => ModHelper.GetModDataInt(Entry.ModId, WeekKey, -1) == Week;
        private static Category Selected => (Category)ModHelper.GetModDataInt(Entry.ModId, CategoryKey, (int)Category.Ordinary);

        internal static Category Classify(GameItem item)
        {
            if (item.IsGameItemType("MACHINE") || item.IsGameItemType("MODULE") || item.IsGameItemType("NODE"))
                return Category.Equipment;
            if (item.IsGameItemType("LUXURY_ITEM") || item.IsGameItemType("RARE") || item.IsGameItemType("HIGHEND"))
                return Category.Valuables;
            return Category.Ordinary;
        }

        private static string Name(Category category)
        {
            switch (category)
            {
                case Category.Equipment: return Copy.Text("设备", "Equipment");
                case Category.Valuables: return Copy.Text("贵重货", "Valuables");
                default: return Copy.Text("普通货", "Ordinary goods");
            }
        }

        [HarmonyPatch(typeof(EndOfDayUIManager), "BuildServiceRow")]
        private static class PolicyPicker
        {
            private static void Postfix(StoreService storeService, EndOfDayTabletFrame ___frame)
            {
                if (storeService.id != StoreService.INSURANCE_SERVICE || !HellPerks.Has(HellPerks.OnePolicy)) return;
                var row = TabletUI.RowRoot(___frame.content, "HellInsuranceCategory", 36f);
                var buttons = new TabletButton[3];
                for (int i = 0; i < buttons.Length; i++)
                {
                    var category = (Category)i;
                    var button = TabletUI.SmallButton(___frame, row, 36 + i * 145, 2, 139, 29, Name(category), () =>
                    {
                        if (Locked) return;
                        ModHelper.SetModDataInt(Entry.ModId, CategoryKey, (int)category);
                        for (int index = 0; index < buttons.Length; index++) buttons[index].SetSelected(index == (int)category);
                    });
                    buttons[i] = button;
                    button.SetSelected(category == Selected);
                    button.SetInteractable(!Locked);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.HandleInsurance))]
        private static class NightCoverage
        {
            private static void Prefix()
            {
                if (HellPerks.Has(HellPerks.OnePolicy) && StoreService.GetInsuranceService().IsActive())
                    ModHelper.SetModDataInt(Entry.ModId, WeekKey, Week);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var nativeTarget = AccessTools.Method(typeof(PlayerStore), nameof(PlayerStore.GetRandomBurglaryEligibleItem));
                var paidTarget = AccessTools.Method(typeof(PerkInsurance), nameof(PaidTarget));
                int calls = 0;
                foreach (var instruction in instructions)
                {
                    if (instruction.Calls(nativeTarget) && ++calls == 2)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = paidTarget;
                    }
                    yield return instruction;
                }
            }
        }

        internal static GameItem PaidTarget(PlayerStore store)
        {
            var item = store.GetRandomBurglaryEligibleItem();
            if (item == null || !HellPerks.Has(HellPerks.OnePolicy) || Classify(item) == Selected) return item;
            store.AddNightLog(string.Format(Copy.Text("{0}不在本周保单内，已被偷走。", "{0} was stolen. This week's policy did not cover it."), item.GetDisplayName()), "9c3738");
            store.StealItem(item);
            return null;
        }
    }
}
