using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProbablyStolenHellMode
{
    internal static class PerkWholesale
    {
        private static StoreClient CurrentSeller => PlayerStore.HasInstance ? PlayerStore.Instance.currentClientInstance?.GetClientBlueprint() : null;

        internal static bool Applies(StoreClient seller) => seller != null && seller.IsClientSelling() &&
            HellPerks.Has(HellPerks.FullLot) && PlayerStore.Instance.playerLocation != PlayerStore.PlayerLocation.INVENTOR;

        private static bool CanTrade(StoreClient seller) => !seller.isBusinessComplete &&
            (!seller.isRegularSellDisable || seller.isOfferWholesale) && !NegociationUIManager.IsTradeLocked();

        private static List<GameItem> Stock(StoreClient seller)
        {
            var stock = EmporiumEntry.Instance.GetAllNonOwnedItem();
            stock.AddRange(BranchCompatibility.HeldItems(seller).Where(item => !GeneralHelper.IsItemOwned(item)));
            return stock;
        }

        internal static long Quote(StoreClient seller, long subtotal)
        {
            if (seller.isOfferWholesale) subtotal += subtotal * seller.wholesaleDiscount / 100;
            return subtotal - subtotal / 10;
        }

        [HarmonyPatch(typeof(NegociationUIManager), "BuildSelling")]
        private static class LotOfferUI
        {
            private static void Postfix(NegociationUIManager __instance, StoreClient storeClient, bool locked,
                NegotiationTabletFrame ___frame, ref TabletButton ___primaryButton, ref TabletButton ___secondaryButton)
            {
                if (!Applies(storeClient)) return;

                var stock = Stock(storeClient);
                long total = Quote(storeClient, stock.Sum(item => item.GetNegociatedValue()));
                var store = PlayerStore.Instance;
                TabletUI.SubHeader(___frame, ___frame.content,
                    string.Format(Copy.Text("整箱价 · {0}组 · 再打九折", "Whole Lot · {0} lots · extra 10% off"), stock.Count), 38f).SetAsFirstSibling();
                foreach (var item in stock.Where(item => item.parentInventory != EmporiumEntry.Instance.backInvinvElement))
                    TabletUI.ListRow(___frame, ___frame.content, item.GetDisplayName(), TabletUI.Credits(item.GetNegociatedValue()), false, null, null);
                TabletUI.ClearChildren(___frame.footer);
                TabletUI.ClearChildren(___frame.buttonHost);
                ___secondaryButton = null;
                TabletUI.FooterLine(___frame, ___frame.footer, 0f, LocHelper.GetLocalizedUI("ui_nego_your_funds"), TabletUI.Credits(store.playerCash));
                TabletUI.FooterLine(___frame, ___frame.footer, 35f, LocHelper.GetLocalizedUI("ui_nego_total"), TabletUI.Credits(total),
                    total > store.playerCash ? TabletStyle.RED : (UnityEngine.Color?)null);
                ___primaryButton = TabletUI.BigButton(___frame, ___frame.buttonHost, 153f, 0f,
                    Copy.Text("整批买入", "Buy the whole lot"), __instance.OnWholesaleClick);
                ___primaryButton.SetInteractable(!locked && CanTrade(storeClient) && stock.Count > 0 && total <= store.playerCash);
            }
        }

        [HarmonyPatch(typeof(NegociationUIManager), "BuildSignature")]
        private static class RefreshLotTotal
        {
            private static void Postfix(StoreClient storeClient, ref string __result)
            {
                if (!Applies(storeClient)) return;
                var stock = Stock(storeClient);
                __result += "|" + stock.Count + "|" + Quote(storeClient, stock.Sum(item => item.GetNegociatedValue()));
            }
        }

        [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.OnWholesaleClick))]
        private static class LotPurchase
        {
            private static bool Prefix(NegociationUIManager __instance, NegotiationTabletFrame ___frame)
            {
                var seller = CurrentSeller;
                if (!Applies(seller)) return true;
                if (CanTrade(seller)) Buy(__instance, ___frame, seller);
                return false;
            }
        }

        [HarmonyPatch]
        private static class BlockPartialOrders
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(NegociationUIManager), nameof(NegociationUIManager.OnBuyAllClick));
                yield return AccessTools.Method(typeof(NegociationUIManager), nameof(NegociationUIManager.BuyItem));
                yield return AccessTools.Method(typeof(NegociationUIManager), nameof(NegociationUIManager.BuyMultipleItem));
                yield return AccessTools.Method(typeof(PlayerStore), nameof(PlayerStore.BuyItem));
                yield return AccessTools.Method(typeof(PlayerStore), nameof(PlayerStore.BuyAllItemFromTable));
            }

            private static bool Prefix() => !Applies(CurrentSeller);
        }

        private static void Buy(NegociationUIManager ui, NegotiationTabletFrame frame, StoreClient seller)
        {
            var store = PlayerStore.Instance;
            EmporiumEntry.Instance.DeselectDragItem(nonOwnedOnly: true);
            var stock = Stock(seller);
            if (stock.Count == 0) return;
            var prices = stock.Select(item => item.GetNegociatedValue()).ToArray();
            long total = Quote(seller, prices.Sum());
            if (total < 0 || total > store.playerCash) return;

            var arrival = PlanArrival(BranchCompatibility.HeldItems(seller));
            if (arrival == null || !MoveArrivals(arrival))
            {
                if (frame != null) frame.sectionHeader.text = Copy.Text("腾出空间再整批买入", "Make room for the whole lot");
                return;
            }

            store.playerCash -= (int)total;
            long subtotal = 0;
            for (int index = 0; index < stock.Count; index++)
            {
                long previous = Quote(seller, subtotal);
                subtotal += prices[index];
                store.OnItemBought(stock[index], (int)(Quote(seller, subtotal) - previous));
            }

            foreach (var item in stock) store.MoveDealItemToInventory(item);
            seller.isOfferWholesale = false;
            BranchCompatibility.PurchaseDone(seller);
            TutorialUIManager.Instance.BuyDealed();
            EmporiumAudioPlayer.Instance.PlayRegister();
            ui.RefreshUI();
            if (seller.acceptDealDialogue != null && !seller.acceptDealDialoguePlayed &&
                DialogUIManager.Instance.StartDialogue(seller.acceptDealDialogue))
                seller.acceptDealDialoguePlayed = true;
            else
                seller.OnDealAccepted();
        }

        private static List<SlotMarker> PlanArrival(List<GameItem> held)
        {
            var emporium = EmporiumEntry.Instance;
            var grids = MapUIManager.Instance.isOutside
                ? new[] { emporium.afterhourInventory, emporium.backInvinvElement, emporium.frontInvinvElement }
                : new[] { emporium.invElement, emporium.backInvinvElement, emporium.frontInvinvElement };
            var plan = new List<SlotMarker>();
            var ignored = new List<GameItem>();
            foreach (var item in held.Where(item => !GeneralHelper.IsItemOwned(item)))
            {
                SlotMarker slot = null;
                foreach (var grid in grids)
                {
                    if (grid == null || grid.IsInsertLocked()) continue;
                    slot = FindSlot(grid, item, ignored, plan);
                    if (slot != null) break;
                }
                if (slot == null) return null;
                plan.Add(slot);
            }
            return plan;
        }

        private static SlotMarker FindSlot(GameGridInventory grid, GameItem item, List<GameItem> ignored, List<SlotMarker> plan)
        {
            if (item.MaxNumRemove() < item.unitCount) return null;
            var shape = new GridShapeBuilder(item.modifiedShape);
            for (int rotation = 0; rotation < 4; rotation++)
            for (int y = 0; y < grid.inventoryShape.height; y++)
            for (int x = 0; x < grid.inventoryShape.width; x++)
            {
                shape.SetTransform(x, y, false, rotation);
                if (!grid.CheckShapePlacement(item, shape, ignored) ||
                    plan.Any(slot => slot.inventory == grid && slot.itemGridShape.Intersects(shape))) continue;
                return new SlotMarker { item = item, inventory = grid, itemGridShape = shape.Build(), numTransfer = item.unitCount };
            }
            return null;
        }

        private static bool MoveArrivals(List<SlotMarker> plan)
        {
            var from = new Stack<SlotMarker>();
            foreach (var slot in plan)
            {
                var item = slot.item;
                from.Push(new SlotMarker { item = item, inventory = item.parentInventory, itemGridShape = item.modifiedShape.Clone(), numTransfer = item.unitCount });
                if (slot.TryAcceptOnce() == slot.numTransfer) continue;

                while (from.Count > 0)
                {
                    var original = from.Pop();
                    if (original.item.parentInventory != original.inventory) original.TryAcceptOnce();
                }
                return false;
            }
            return true;
        }
    }
}
