using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ProbablyStolenHellMode
{
    public sealed class Entry : IMod
    {
        internal const string ModId = "probably_stolen_hell_mode";
        internal const string DifficultyId = ModId + ".hell";
        internal const string Starvation = ModId + ".starvation";
        internal const string Dehydration = ModId + ".dehydration";
        internal static bool Enabled { get; private set; }

        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            if (Enabled) return;
            Enabled = true;
            ModHelper.AddDifficulty(ModId, DifficultyId, 500, Copy.Title, Copy.Subtitle,
                configure: rules => rules.supplierMarkupMultiplier = 3f,
                basedOn: Difficulty.CHALLENGE);
            BranchCompatibility.RegisterEndings();
            HellPerks.Register();
            ModHook.OnNewGameStarted += OnNewRun;
            ModHook.OnGameLoadedLate += OnRunLoaded;
            ModHook.OnGoingSleepLate += Rations.SettleNight;
            ModHook.OnGenerateCustomerVeryEarly += Provisioner.Queue;
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHook.OnNewGameStarted -= OnNewRun;
            ModHook.OnGameLoadedLate -= OnRunLoaded;
            ModHook.OnGoingSleepLate -= Rations.SettleNight;
            ModHook.OnGenerateCustomerVeryEarly -= Provisioner.Queue;
            ModHelper.RemoveDifficulties(ModId);
            BranchCompatibility.RemoveEndings();
            ModHelper.RemoveStartingPerks(ModId);
            Rations.ResetRuntime();
        }

        private static void OnNewRun(string _) { if (Rations.Active) Rations.Initialize(); }
        private static void OnRunLoaded() => Rations.ResetRuntime();
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.StartNewGame))]
    internal static class NewRunRent
    {
        private static void Prefix(PlayerStore __instance)
        {
            Rations.ResetRuntime();
            HellPerks.NormalizeSelection();
            if (Entry.Enabled && NewGameData.Instance?.difficultyId == Entry.DifficultyId)
                __instance.startingRent = 250;
        }
    }

    [HarmonyPatch(typeof(Difficulty), nameof(Difficulty.GetEffectLines))]
    internal static class DifficultyIntroduction
    {
        private static void Postfix(Difficulty difficulty, List<string> __result)
        {
            if (!Entry.Enabled || difficulty?.id != Entry.DifficultyId) return;

            __result.Remove(LocHelper.GetLocalizedMainMenu("difficulty_effect_supplier_pricier"));
            __result.InsertRange(0, Copy.Introduction());
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BeginDay))]
    internal static class RationEndingAtNight
    {
        private static bool Prefix(PlayerStore __instance)
        {
            if (!Rations.HasPendingEnding) return true;
            if (GameOverUIManager.Instance != null && !GameOverUIManager.Instance.IsShowing)
                __instance.ExecuteGameOver(Rations.PendingEnding);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.ExecuteGameOver))]
    internal static class PreserveRationEnding
    {
        private static bool Prefix(ref string ending)
        {
            if (!Rations.HasPendingEnding) return true;
            if (GameOverUIManager.Instance == null || GameOverUIManager.Instance.IsShowing) return false;
            ending = Rations.PendingEnding;
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class DoNotSaveFatalNight
    {
        private static bool Prefix() => !Rations.HasPendingEnding;
    }

    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.ResetData))]
    internal static class ResetRun
    {
        private static void Prefix() => Rations.ResetRuntime();
    }

    [HarmonyPatch(typeof(EmporiumEntry), "Start")]
    internal static class AttachSurvivalHud
    {
        private static void Postfix()
        {
            var storeUI = StoreUIManager.Instance;
            if (Rations.Active && storeUI != null && storeUI.primaryCanvas != null)
                SurvivalHud.Attach(storeUI.primaryCanvas.transform);
        }
    }
}
