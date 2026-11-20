using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyStolenHellMode
{
    internal static class ItemMenu
    {
        private static ItemContextHandler owner;
        private static RichTextElement row;
        private static GridPixelElement layout;
        private static RectTransform hitArea;
        private static RationAction action;
        private static bool highlighted;
        private static string caption;

        internal static void Show(ItemContextHandler handler, GameItem item, PixelWindow window, GridPixelElement original)
        {
            action = Rations.ActionFor(item);
            if (action == RationAction.None)
            {
                if (owner == handler && row != null && row.handler != null && row.handler.gameObject.activeSelf)
                {
                    row.handler.gameObject.SetActive(false);
                    layout.Validate();
                }
                return;
            }
            if (owner != handler || row == null || row.handler == null)
            {
                owner = handler;
                layout = new GridPixelElement(1, 2);
                row = new RichTextElement();
                row.handler.gameObject.name = "HellModeConsume";
                foreach (var graphic in row.handler.GetComponentsInChildren<Image>(true))
                {
                    if (!graphic.raycastTarget) continue;
                    hitArea = graphic.rectTransform;
                    break;
                }
                window.Detach(original);
                layout.Attach(original);
                layout.Attach(row);
                window.Attach(layout);
            }
            row.handler.gameObject.SetActive(true);
            caption = action == RationAction.Eat ? Copy.Text("进食", "Eat") : Copy.Text("饮水", "Drink");
            SetColor(false);
            foreach (var child in row.handler.GetComponentsInChildren<Transform>(true))
                child.gameObject.tag = "ContextMenuNode";
            layout.handler.gameObject.tag = "ContextMenuNode";
            layout.Validate();
        }

        internal static void Hover(ItemContextHandler handler, RichTextElement selected)
        {
            if (owner != handler || row == null || row.handler == null || !row.handler.gameObject.activeSelf) return;
            bool isSelected = selected == row;
            if (isSelected != highlighted) SetColor(isSelected);
        }

        private static void SetColor(bool selected)
        {
            highlighted = selected;
            row.SetText(caption, 10000, selected ? RenderHandler.ColorPalette.VeryLightGray : RenderHandler.ColorPalette.LightGray);
        }

        internal static bool Handle(ItemContextHandler handler, RichTextElement button, GameItem item)
        {
            if (owner != handler || row == null || button != row) return false;
            handler.Stop();
            Rations.Apply(item, action);
            return true;
        }

        internal static RichTextElement At(ItemContextHandler handler, Vector2 position, Canvas canvas)
        {
            if (owner != handler || !Rations.Active || hitArea == null || !hitArea.gameObject.activeInHierarchy || canvas == null)
                return null;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(hitArea, position, camera) ? row : null;
        }
    }

    [HarmonyPatch(typeof(ItemContextHandler), "UpdateConditions")]
    internal static class ItemMenuActionRow
    {
        private static void Postfix(ItemContextHandler __instance, GameItem ___currentItem,
            PixelWindow ___contextWindow, GridPixelElement ___contextButtons) =>
            ItemMenu.Show(__instance, ___currentItem, ___contextWindow, ___contextButtons);
    }

    [HarmonyPatch(typeof(ItemContextHandler), "UpdateSelectButtonColor")]
    internal static class ItemMenuHover
    {
        private static void Postfix(ItemContextHandler __instance, RichTextElement selectedButton) => ItemMenu.Hover(__instance, selectedButton);
    }

    [HarmonyPatch(typeof(ItemContextHandler), "TriggerButton")]
    internal static class ItemMenuConsume
    {
        private static bool Prefix(ItemContextHandler __instance, RichTextElement button, GameItem ___currentItem) =>
            !ItemMenu.Handle(__instance, button, ___currentItem);
    }

    [HarmonyPatch(typeof(ItemContextHandler), nameof(ItemContextHandler.FindRichTextNode))]
    internal static class ItemMenuFindAction
    {
        private static bool Prefix(ItemContextHandler __instance, Vector2 eventDataPosition, Canvas canvas, ref RichTextElement __result)
        {
            var hit = ItemMenu.At(__instance, eventDataPosition, canvas);
            if (hit == null) return true;
            __result = hit;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemContextHandler), nameof(ItemContextHandler.FindContextMenuNode))]
    internal static class ItemMenuKeepOpen
    {
        private static bool Prefix(ItemContextHandler __instance, Vector2 eventDataPosition, Canvas canvas, ref GameObject __result)
        {
            var hit = ItemMenu.At(__instance, eventDataPosition, canvas);
            if (hit == null) return true;
            __result = hit.handler.gameObject;
            return false;
        }
    }
}
