using System.Linq;

namespace ProbablyStolenHellMode
{
    internal static class Provisioner
    {
        internal const string ClientId = Entry.ModId + ".provisioner";
        private const string VisitKey = "provisioner_queued_day";

        internal static void Queue(StoreClientManager manager)
        {
            if (!Rations.Active) return;
            int day = StoreStation.GetDayCounter();
            if (day <= 0 || day % 7 != 4 || ModHelper.GetModDataInt(Entry.ModId, VisitKey, 0) == day ||
                manager.clientStack.Any(client => client?.identifier == ClientId)) return;

            var merchant = new StoreClient
            {
                identifier = ClientId,
                displayName = Copy.Text("下层区补给商", "Lower Levels Provisioner"),
                spriteName = SpriteDict.GetRandomMerchantSpriteName(),
                clientIntent = StoreClient.ClientIntent.SELL
            };
            StoreClientFactionSetup.InitLowerLevelCitizen(merchant);
            merchant.mainDialogue.SetText(merchant.displayName,
                Copy.Text("周四送到。今天有饭，也有干净水。", "Thursday delivery. I have food and clean water."))
                .SetEndAction(StockCounter);
            merchant.AddBasicDialog();
            merchant.CompleteClientCreation();
            manager.AddClient(merchant);
            if (manager.clientStack.Contains(merchant)) ModHelper.SetModDataInt(Entry.ModId, VisitKey, day);
        }

        private static void StockCounter()
        {
            Add("water_ration", 4, 24);
            Add("water_ration_small", 2, 12);
            Add("nutrifruit", 3);
            Add("processed_milk", 2);
            Add("processed_juice", 1);
            Add("processed_meat", 1);
            Add("cup_noodle", 1);
        }

        private static void Add(string id, int count, int rationPrice = 0)
        {
            for (int i = 0; i < count; i++)
            {
                var item = DirectoryMaster.Item(id);
                if (item == null) continue;
                if (rationPrice != 0) item.unitValue = rationPrice;
                PlayerStore.Instance.TryAddDirectSellingItemToTable(item);
            }
        }
    }
}
