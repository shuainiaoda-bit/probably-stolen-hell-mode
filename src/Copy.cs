using System;

namespace ProbablyStolenHellMode
{
    internal static class Copy
    {
        internal static string Text(string chinese, string english) =>
            (LocHelper.GetCurrentLocaleCode() ?? "").StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? chinese : english;

        internal static string Title() => Text("这就是地狱吗？", "Is This Hell?");
        internal static string Subtitle() => Text("生存挑战", "Survival Challenge");

        internal static string[] Introduction() => new[]
        {
            Text("基础房租为250", "Base rent is 250"),
            Text("供货商加价150%", "Supplier markup is 150%"),
            Text($"每晚消耗{Rations.DailyFood} kcal、{Rations.DailyWater} mL", $"Use {Rations.DailyFood} kcal and {Rations.DailyWater} mL each night"),
            Text("右键进食或饮用基础水及以上", "Right-click to eat or drink Basewater or better")
        };
    }
}
