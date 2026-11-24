using System;
using UnityEngine;

namespace ProbablyStolenHellMode
{
    internal enum RationAction { None, Eat, Drink }

    internal static class Rations
    {
        internal const int FoodCapacity = 2500;
        internal const int WaterCapacity = 1500;
        internal const int DailyFood = 350;
        internal const int DailyWater = 300;
        private const string FoodKey = "food_kcal";
        private const string WaterKey = "water_ml";
        private const string NightKey = "last_night";

        internal static string PendingEnding { get; private set; }
        internal static bool HasPendingEnding => Active && PendingEnding != null;
        internal static bool Active => Entry.Enabled && PlayerStore.HasInstance &&
            ModHelper.GetDifficultyId() == Entry.DifficultyId;

        internal static int Food => Mathf.Clamp(ModHelper.GetModDataInt(Entry.ModId, FoodKey, FoodCapacity), 0, FoodCapacity);
        internal static int Water => Mathf.Clamp(ModHelper.GetModDataInt(Entry.ModId, WaterKey, WaterCapacity), 0, WaterCapacity);

        internal static void ResetRuntime() => PendingEnding = null;

        internal static void Initialize()
        {
            ResetRuntime();
            SetFood(FoodCapacity);
            SetWater(WaterCapacity);
            ModHelper.SetModDataInt(Entry.ModId, NightKey, 0);
        }

        private static void SetFood(int value) => ModHelper.SetModDataInt(Entry.ModId, FoodKey, value);
        private static void SetWater(int value) => ModHelper.SetModDataInt(Entry.ModId, WaterKey, value);

        internal static void SettleNight()
        {
            if (!Active || PendingEnding != null || GameOverUIManager.Instance?.IsShowing == true) return;
            int day = StoreStation.GetDayCounter();
            if (day <= 0 || ModHelper.GetModDataInt(Entry.ModId, NightKey, 0) >= day) return;
            int food = Math.Max(0, Food - DailyFood);
            int water = Math.Max(0, Water - DailyWater);
            SetFood(food);
            SetWater(water);
            ModHelper.SetModDataInt(Entry.ModId, NightKey, day);
            if (water == 0) PendingEnding = Entry.Dehydration;
            else if (food == 0) PendingEnding = Entry.Starvation;
        }

        internal static RationAction ActionFor(GameItem item)
        {
            if (!Active || item == null || item.parentInventory == null || item.unitCount <= 0 ||
                !GeneralHelper.IsItemOwned(item) || GeneralHelper.IsDisplayOnly(item)) return RationAction.None;
            if (SealedVolume(item) > 0 || (item.IsTag("LIQUID_CONTAINER_TAG") && !item.IsTag("WINE_CONTAINER_TAG") &&
                WaterHelper.GetTotalVolume(item) >= 1000)) return RationAction.Drink;
            if (item.IsGameItemType("FOOD") && !item.IsTag("FISH_FERTILIZED_TAG") && Calories(item) > 0)
                return RationAction.Eat;
            return RationAction.None;
        }

        internal static bool Apply(GameItem item, RationAction action)
        {
            if (!Active || PendingEnding != null || ActionFor(item) != action) return false;
            switch (action)
            {
                case RationAction.Eat: return Eat(item);
                case RationAction.Drink: return Drink(item);
                default: return false;
            }
        }

        private static int Calories(GameItem item) => item.IsTag("CALORIE_VALUE_TAG")
            ? item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt() : 0;

        private static int SealedVolume(GameItem item)
        {
            if (item.identifier == "water_ration_small") return 375;
            if (item.identifier == "water_ration") return 750;
            return 0;
        }

        private static bool Eat(GameItem item)
        {
            int current = Food;
            if (current == FoodCapacity) return Notice("食物储备已满。", "Your food reserve is full.");
            int amount = Math.Min(FoodCapacity - current, Calories(item));
            ConsumeOne(item);
            SetFood(current + amount);
            Notice($"补充了 {amount} kcal。", $"Restored {amount} kcal.");
            return true;
        }

        private static bool Drink(GameItem item)
        {
            int current = Water;
            int missing = WaterCapacity - current;
            if (missing == 0) return Notice("饮水储备已满。", "Your drinking water reserve is full.");
            int amount = SealedVolume(item);
            if (amount > 0)
            {
                if (amount > missing) return Notice("饮水储备装不下一整包。可先把水倒进容器。",
                    "There is no room for the whole ration. Pour it into a container first.");
                ConsumeOne(item);
            }
            else
            {
                if (WaterHelper.GetWaterPurity(item) < WaterFeatureHelper.BASE_WATER_PURITY)
                    return Notice("只能饮用基础水或更干净的水。", "Only Basewater or better is drinkable.");
                amount = Math.Min(missing, WaterHelper.GetTotalVolume(item) / 1000);
                if (amount <= 0) return false;
                WaterHelper.Remove(item, amount * 1000);
                WaterHelper.UpdateContainerGoodType(item);
                WaterHelper.UpdateWaterBottleSound(item);
                item.Validate();
            }
            SetWater(current + amount);
            Notice($"补充了 {amount} mL。", $"Restored {amount} mL.");
            return true;
        }

        private static void ConsumeOne(GameItem item)
        {
            if (item.unitCount == 1) item.Destroy();
            else item.GetSplitItem(1).Destroy();
        }

        private static bool Notice(string chinese, string english)
        {
            StoreUIManager.Instance?.Notify(Copy.Text(chinese, english));
            return false;
        }
    }
}
