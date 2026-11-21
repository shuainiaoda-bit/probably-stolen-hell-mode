using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProbablyStolenHellMode
{
    public sealed class SurvivalHud : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const string PanelName = "HellModeSurvival";
        private const string XKey = "survival_hud_x";
        private const string YKey = "survival_hud_y";
        private const float DefaultY = -42f;
        private const float EdgePadding = 6f;
        private const float MeterWidth = 205f;
        private const float MeterGap = 15f;
        private const float HorizontalPadding = 10f;
        private const float PanelHeight = 43f;
        private const float TrackHeight = 8f;
        private static readonly Color Ink = new Color32(236, 230, 212, 255);
        private static readonly Color Panel = new Color32(22, 24, 27, 241);
        private static readonly Color Track = new Color32(51, 55, 58, 255);
        private static readonly Color FoodColor = new Color32(244, 179, 94, 255);
        private static readonly Color WaterColor = new Color32(99, 185, 218, 255);
        private static readonly Color Low = new Color32(239, 115, 102, 255);

        private CanvasGroup visibility;
        private RectTransform foodFill;
        private RectTransform waterFill;
        private TextMeshProUGUI foodLabel;
        private TextMeshProUGUI waterLabel;
        private int shownFood = -1;
        private int shownWater = -1;
        private RectTransform panel;
        private RectTransform viewport;
        private Vector2 viewportSize;
        private Vector2 preferredPosition;
        private Vector2 dragOffset;
        private int? dragPointer;

        internal static void Attach(Transform canvas)
        {
            if (canvas.Find(PanelName) != null) return;
            float width = 2 * (HorizontalPadding + MeterWidth) + MeterGap;
            var panel = Rect(canvas, PanelName, 0.5f, 1f, 0.5f, 1f, 0, DefaultY, width, PanelHeight);
            panel.SetAsLastSibling();
            panel.gameObject.AddComponent<SurvivalHud>().Build(panel);
        }

        private void Build(RectTransform panel)
        {
            this.panel = panel;
            viewport = (RectTransform)panel.parent;
            preferredPosition = new Vector2(ReadPosition(XKey, 0), ReadPosition(YKey, DefaultY));
            ApplyPosition();
            visibility = panel.gameObject.AddComponent<CanvasGroup>();
            Image(panel, Panel, raycast: true);
            (foodLabel, foodFill) = Meter(panel, "Food", HorizontalPadding, FoodColor);
            (waterLabel, waterFill) = Meter(panel, "Water", HorizontalPadding + MeterWidth + MeterGap, WaterColor);
            LateUpdate();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || dragPointer.HasValue ||
                !visibility.blocksRaycasts || UIDragHandler.isDraggingAnyUI || ScratchInput.isScratching ||
                ItemMouseDragHandler.current?.IsDraggingItem == true) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.pressPosition,
                eventData.pressEventCamera, out var press)) return;

            dragPointer = eventData.pointerId;
            dragOffset = panel.anchoredPosition - press;
            UIDragHandler.isDraggingAnyUI = true;
            UIDragHandler.draggingID = PanelName;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragPointer != eventData.pointerId || eventData.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                eventData.pressEventCamera, out var point)) return;

            preferredPosition = ClampPosition(point + dragOffset);
            ApplyPosition();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragPointer == eventData.pointerId && eventData.button == PointerEventData.InputButton.Left)
                FinishDrag();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) FinishDrag();
        }

        private void OnDisable() => FinishDrag();

        private void FinishDrag()
        {
            if (!dragPointer.HasValue) return;
            dragPointer = null;
            if (UIDragHandler.draggingID == PanelName)
            {
                UIDragHandler.draggingID = "";
                UIDragHandler.isDraggingAnyUI = false;
            }
            if (!Rations.Active) return;
            ModHelper.SetModDataFloat(Entry.ModId, XKey, preferredPosition.x);
            ModHelper.SetModDataFloat(Entry.ModId, YKey, preferredPosition.y);
        }

        private static float ReadPosition(string key, float fallback)
        {
            float value = ModHelper.GetModDataFloat(Entry.ModId, key, fallback);
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private void ApplyPosition()
        {
            viewportSize = viewport.rect.size;
            panel.anchoredPosition = ClampPosition(preferredPosition);
        }

        private Vector2 ClampPosition(Vector2 position)
        {
            var size = viewport.rect.size;
            float xLimit = Mathf.Max(0, (size.x - panel.rect.width) / 2 - EdgePadding);
            float bottom = Mathf.Min(-EdgePadding, panel.rect.height + EdgePadding - size.y);
            return new Vector2(Mathf.Clamp(position.x, -xLimit, xLimit), Mathf.Clamp(position.y, bottom, -EdgePadding));
        }

        private static (TextMeshProUGUI label, RectTransform fill) Meter(RectTransform panel, string name, float x, Color color)
        {
            var text = Rect(panel, name + "Value", 0, 1, 0, 1, x, -4, MeterWidth, 23).gameObject.AddComponent<TextMeshProUGUI>();
            text.color = Ink;
            text.fontSize = 17f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            var font = FontLoader.Instance;
            var locale = ModHelper.GetCurrentLocale();
            if (font != null && locale != null) text.font = font.GetFontByRole(FontLoader.FontRole.Noto_Sans, locale);

            var track = Rect(panel, name + "Track", 0, 1, 0, 1, x, -29, MeterWidth, TrackHeight);
            Image(track, Track);
            var fill = Rect(track, name + "Charge", 0, 0.5f, 0, 0.5f, 0, 0, MeterWidth, TrackHeight);
            Image(fill, color);
            return (text, fill);
        }

        private void LateUpdate()
        {
            if (visibility == null) return;
            bool visible = Rations.Active && GameOverUIManager.Instance?.IsShowing != true;
            visibility.alpha = visible ? 1 : 0;
            visibility.blocksRaycasts = visible && StoreUIManager.Instance?.escapeUIManager?.menuPanel?.activeSelf != true;
            if (!visibility.blocksRaycasts) FinishDrag();
            if (!visible) return;
            if (viewportSize != viewport.rect.size) ApplyPosition();
            int food = Rations.Food;
            int water = Rations.Water;
            if (food != shownFood)
            {
                shownFood = food;
                UpdateMeter(foodLabel, foodFill, Copy.Text("食物", "Food"), food, Rations.FoodCapacity, Rations.DailyFood, "kcal", FoodColor);
            }
            if (water != shownWater)
            {
                shownWater = water;
                UpdateMeter(waterLabel, waterFill, Copy.Text("饮水", "Water"), water, Rations.WaterCapacity, Rations.DailyWater, "mL", WaterColor);
            }
        }

        private static void UpdateMeter(TextMeshProUGUI label, RectTransform fill, string name, int amount,
            int capacity, int dailyUse, string unit, Color color)
        {
            label.text = $"{name}  {amount:N0}/{capacity:N0} {unit}";
            label.color = amount <= dailyUse ? Low : Ink;
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MeterWidth * amount / capacity);
            fill.GetComponent<Image>().color = amount <= dailyUse ? Low : color;
        }

        private static RectTransform Rect(Transform parent, string name, float ax, float ay, float px, float py,
            float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(ax, ay);
            rect.pivot = new Vector2(px, py);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Image(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
        }
    }
}
