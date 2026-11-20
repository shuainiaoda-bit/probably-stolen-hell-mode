using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ProbablyStolenHellMode
{
    internal static class HellPerks
    {
        private const string Prefix = Entry.ModId + ".perk.";
        internal const string FridayRent = Prefix + "friday_rent";
        internal const string NoTap = Prefix + "no_tap";
        internal const string OnePolicy = Prefix + "one_policy";
        internal const string NoUnlocks = Prefix + "no_unlocks";
        internal const string WalkInsOnly = Prefix + "walk_ins_only";
        internal const string RentReserve = Prefix + "rent_reserve";
        internal const string FullLot = Prefix + "full_lot";

        internal static bool IsModPerk(StartingPerk perk) => perk?.id?.StartsWith(Prefix, StringComparison.Ordinal) == true;
        internal static bool Has(string id) => Entry.Enabled && PlayerStore.HasInstance &&
            PlayerStore.Instance.startingPerks?.Exists(perk => perk?.id == id) == true;

        internal static void Register()
        {
            Add(FridayRent, -1, "周末不办公", "Closed on Weekends", "从第二次收租起，房东改在周五上门。", "From the second rent collection, the landlord visits on Fridays.", StartingPerkList.BAD_LEASE);
            Add(NoTap, -2, "不准玩水商", "No Tap Water", "水龙头禁用。", "The faucet is disabled.", StartingPerkList.WORKING_PIPES);
            Add(OnePolicy, -1, "保单只认一栏", "Single Coverage", "每周只能为设备、普通货或贵重货中的一类投保。", "Insure one category each week: equipment, ordinary goods, or valuables.", StartingPerkList.ALERT);
            Add(NoUnlocks, -3, "担保人已注销", "Guarantor Struck Off", "无法购买解锁玩法的人脉服务。专精自带的服务仍可用。", "You cannot buy gameplay unlocks. Services included with your specialization still work.", StartingPerkList.WILD_BAD_SIDE);
            Add(WalkInsOnly, -1, "只收散户", "Walk-ins Only", "固定供货商不再来访。普通顾客仍会出售货物。", "Scheduled suppliers stop visiting. Walk-ins still sell goods.", StartingPerkList.SIDE_STREET, FullLot);
            Add(RentReserve, -2, "先扣房租", "Rent First", "普通销售收入的30%存作下期房租，存满即停。", "30% of ordinary sales goes toward the next rent bill, up to the amount due.", StartingPerkList.SAVING);
            Add(FullLot, 0, "整箱价", "Whole Lot", "只能整批买入，可再打九折。夜间拜访博士时照常购买。", "Buy the full lot for an extra 10% off. Night visits to Dr. Jackson work as usual.", StartingPerkList.HOARDER, WalkInsOnly);
        }

        private static void Add(string id, int points, string zh, string en, string zhEffect, string enEffect, string icon, params string[] incompatible)
        {
            ModHelper.AddStartingPerk(Entry.ModId, id, points,
                points < 0 ? StartingPerk.StartingPerkType.NEGATIVE : StartingPerk.StartingPerkType.NEUTRAL,
                () => Copy.Text(zh, en), () => Copy.Text(zhEffect, enEffect), icon, incompatible);
        }

        internal static void NormalizeSelection()
        {
            var settings = NewGameData.Instance;
            if (settings?.startingPerks == null) return;
            if (!Entry.Enabled)
                settings.startingPerks.RemoveAll(IsModPerk);
            else if (settings.startingPerks.Exists(perk => perk?.id == WalkInsOnly))
                settings.startingPerks.RemoveAll(perk => perk?.id == FullLot);
        }
    }

    internal static class StartingPerkPresentation
    {
        [HarmonyPatch(typeof(StartingPerkIconLoader), nameof(StartingPerkIconLoader.GetIcon))]
        private static class TraitIcon
        {
            private static void Postfix(StartingPerk perk, ref UnityEngine.Sprite __result)
            {
                if (!Entry.Enabled || !HellPerks.IsModPerk(perk)) return;
                string key = perk.iconKey ?? StartingPerkList.GetModPerk(perk.id)?.iconKey;
                if (key != null && StartingPerkIconLoader.perkIcons.TryGetValue(key, out var icon)) __result = icon;
            }
        }

        [HarmonyPatch(typeof(NewGamePerksUI), "Rebuild")]
        private static class PlaceNewTraitsFirst
        {
            private static void Prefix(List<StartingPerk> ___sorted)
            {
                if (!Entry.Enabled) return;
                var perks = ___sorted.FindAll(HellPerks.IsModPerk);
                ___sorted.RemoveAll(HellPerks.IsModPerk);
                ___sorted.InsertRange(0, perks);
            }
        }
    }
}
