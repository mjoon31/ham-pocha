using System;
using System.Collections.Generic;
using HamPocha.Audio;
using HamPocha.Data;
using UnityEngine;

namespace HamPocha.Gameplay
{
    [Serializable]
    public class KitchenCook
    {
        public int Menu = -1;
        // 0은 선택 기록 없음, 양수는 메뉴 번호에 1을 더한 값이다.
        public int LastRecipe;
        public int[] Ingredients = new int[Inventory.IngredientCount];
        public float Remaining;
        public bool Started;
        public bool Ready { get { return Started && Remaining <= 0; } }
    }

    [Serializable]
    public class OutdoorCook
    {
        // 0은 선택 없음이며 양수는 야외 메뉴 ID + 1이다.
        public int Selection;
        public bool Started;
        public float Remaining;
        // 진행 중인 1개와 별도로 재료를 차감한 대기 수량이다. 구버전 저장은 0으로 읽는다.
        public int Queued;
        // 완성 뒤에는 아이콘용 메뉴를 남겨 두고, 다음 회차의 선택을 기다린다.
        public bool NeedsSelection;
    }

    [Serializable]
    public class KitchenOrder
    {
        // 0 빈 자리, 1 이동, 2 접수 대기, 3 음식 대기, 4 퇴장
        public int Phase;
        public int Menu;
        public float Remaining;
        public bool ReplacementPending;
        public int Sequence;
        public int WarnedPhase;
        public int ResidentTicket;
        public OrderOutcome ExitOutcome;
        public int Gold;
        public int Xp;
    }

    [Serializable]
    public class KitchenSaveData
    {
        public int Hand = -1;
        // 이전 저장에도 안전하도록 0을 선택 기록 없음으로 둔다.
        public int LastFridgeIngredient;
        // 저장 키를 유지한다. -1 빈 칸, 0~6 음식, 7~12 재료.
        public int[] Food = { -1, -1, -1, -1, -1 };
        public List<int> FoodStorageOrder = new List<int>();
        // 이전 실내 메뉴로 만든 야외 재고는 판매가 끝날 때까지 보존한다.
        public int[] Display = new int[7];
        public int[] SkewerDisplay = new int[OutdoorMenu.RecipeCount];
        // 꼬치 번호 다음에 이전 실내 음식 번호를 이어 붙여 마지막 진열 순서를 기록한다.
        public List<int> DisplayOrder = new List<int>();
        public OutdoorCook[] SkewerCooks = { new OutdoorCook(), new OutdoorCook(), new OutdoorCook() };
        public KitchenCook[] Inside = { new KitchenCook(), new KitchenCook(), new KitchenCook() };
        public KitchenCook[] Outside = { new KitchenCook(), new KitchenCook(), new KitchenCook() };
        public KitchenOrder[] Orders = { new KitchenOrder(), new KitchenOrder(), new KitchenOrder() };
        public int Sequence;
        public int NextIndoorResident;
        internal const float VisitIntervalSeconds = 1f;
        public float NextVisit = VisitIntervalSeconds;
        public bool InteriorView;
        public bool HasInteriorPosition;
        public Vector3 InteriorPosition;
    }

    // 물건은 냉장고, 손, 조리대, 음식보관대 중 한 곳에서만 소유한다.
    public sealed class KitchenStock
    {
        public KitchenSaveData State { get; private set; } = new KitchenSaveData();
        public int DisplayVersion { get; private set; }
        private readonly Inventory _inventory;
        private readonly CookingStation _sales;
        public const int OutdoorDisplayLimit = 100;
        public const int OutdoorCookLimit = 5;
        private readonly float[] _outdoorCompletion = new float[3];
        private readonly int[] _outdoorWait = new int[3];

        public KitchenStock(Inventory inventory, CookingStation sales)
        {
            _inventory = inventory;
            _sales = sales;
        }

        public RecipeData Recipe(int menu) { return _sales.Catalog.Recipes[menu]; }
        public OutdoorRecipe Skewer(int menu) { return _sales.Catalog?.Outdoor?.GetRecipe(menu); }

        public int SkewerForestNumber(OutdoorRecipe recipe) =>
            (int)recipe.RequiredForest - (int)HamPocha.Core.SceneType.Forest01 + 1;

        public bool IsSkewerUnlocked(int menu)
        {
            OutdoorRecipe recipe = Skewer(menu);
            return recipe != null && _sales.Progress.Markets[1] && _sales.IsForestOwned(SkewerForestNumber(recipe) - 1);
        }

        public int FirstLegacyDisplay()
        {
            for (int i = 0; i < State.Display.Length; i++)
                if (State.Display[i] > 0) return i;
            return -1;
        }

        public bool HasDisplayStock()
        {
            if (FirstLegacyDisplay() >= 0) return true;
            foreach (int count in State.SkewerDisplay) if (count > 0) return true;
            return false;
        }

        internal int DisplayStockCount()
        {
            int count = 0;
            foreach (int amount in State.Display) count += amount;
            foreach (int amount in State.SkewerDisplay) count += amount;
            return count;
        }

        internal bool HasUnstoredIngredients()
        {
            for (int index = 0; index < Inventory.IngredientTypeCount; index++)
                if (_inventory.GetCount((IngredientType)index) > 0) return true;
            return false;
        }

        internal int LatestDisplayDish()
        {
            List<int> order = State.DisplayOrder;
            return order.Count > 0 ? order[order.Count - 1] : -1;
        }

        private bool HasDisplayedDish(int dish)
        {
            if (dish < 0) return false;
            if (dish < OutdoorMenu.RecipeCount) return State.SkewerDisplay[dish] > 0;
            int legacy = dish - OutdoorMenu.RecipeCount;
            return legacy < State.Display.Length && State.Display[legacy] > 0;
        }

        private void RestoreDisplayOrder()
        {
            List<int> saved = State.DisplayOrder;
            State.DisplayOrder = new List<int>();
            if (saved != null)
                foreach (int dish in saved)
                    if (HasDisplayedDish(dish) && !State.DisplayOrder.Contains(dish))
                        State.DisplayOrder.Add(dish);

            // 진열 순서가 없는 기존 저장은 메뉴 번호 순으로 표시한다.
            for (int dish = 0; dish < OutdoorMenu.RecipeCount + State.Display.Length; dish++)
                if (HasDisplayedDish(dish) && !State.DisplayOrder.Contains(dish))
                    State.DisplayOrder.Insert(0, dish);
        }

        private void MarkDisplayDish(int dish)
        {
            State.DisplayOrder.Remove(dish);
            State.DisplayOrder.Add(dish);
        }

        public bool TryRemoveDisplayFood(out int skewer, out RecipeId legacy)
        {
            skewer = -1;
            int legacyMenu = FirstLegacyDisplay();
            legacy = legacyMenu < 0 ? default : (RecipeId)legacyMenu;
            if (legacyMenu >= 0)
            {
                State.Display[legacyMenu]--;
                if (State.Display[legacyMenu] == 0) State.DisplayOrder.Remove(OutdoorMenu.RecipeCount + legacyMenu);
                DisplayVersion++;
                return true;
            }

            for (int i = 0; i < State.SkewerDisplay.Length; i++)
            {
                if (State.SkewerDisplay[i] <= 0 || Skewer(i) == null) continue;
                State.SkewerDisplay[i]--;
                if (State.SkewerDisplay[i] == 0) State.DisplayOrder.Remove(i);
                DisplayVersion++;
                skewer = i;
                return true;
            }
            return false;
        }
        public bool IsUnlocked(int menu) { return menu >= 0 && menu < 7 && _sales.Progress.Foods[menu]; }

        public KitchenSaveData Capture()
        {
            return JsonUtility.FromJson<KitchenSaveData>(JsonUtility.ToJson(State));
        }

        public void Restore(KitchenSaveData data)
        {
            State = data == null ? new KitchenSaveData() : JsonUtility.FromJson<KitchenSaveData>(JsonUtility.ToJson(data));
            State.NextVisit = Mathf.Min(State.NextVisit, KitchenSaveData.VisitIntervalSeconds);
            RestoreFoodStorageOrder();
            DisplayVersion++;
            // 구버전 화로·진열 배열은 실내 음식 번호로 그대로 소진한다.
            if (State.SkewerDisplay == null) State.SkewerDisplay = new int[OutdoorMenu.RecipeCount];
            RestoreDisplayOrder();
            if (State.SkewerCooks == null)
                State.SkewerCooks = new[] { new OutdoorCook(), new OutdoorCook(), new OutdoorCook() };
            Array.Clear(_outdoorCompletion, 0, _outdoorCompletion.Length);
            Array.Clear(_outdoorWait, 0, _outdoorWait.Length);
            RestoreIngredients(State.Inside);
            RestoreIngredients(State.Outside);
        }

        private void RestoreIngredients(KitchenCook[] cooks)
        {
            foreach (KitchenCook cook in cooks)
            {
                if (cook.Ingredients.Length == Inventory.IngredientCount) continue;
                Array.Resize(ref cook.Ingredients, Inventory.IngredientCount);
                if (cook.Started || cook.Menu < 0) continue;

                for (int i = 0; i < cook.Ingredients.Length; i++)
                {
                    int extra = cook.Ingredients[i] - Need(cook.Menu, i);
                    if (extra <= 0 || !_inventory.TryReturnKitchenIngredient((IngredientType)i, extra)) continue;
                    cook.Ingredients[i] -= extra;
                    Debug.Log($"[Save] event=kitchen_ingredient_restore result=returned menu={cook.Menu} ingredient={(IngredientType)i} amount={extra}");
                }
            }
        }

        public static bool IsValid(KitchenSaveData data, bool requireSkewers = false)
        {
            if (data == null) return true;
            if (data.Hand < -1 || data.Hand >= Inventory.IndoorItemKeyCount || data.Food == null || data.Food.Length != 5 ||
                data.Display == null || data.Display.Length != 7 || data.Orders == null || data.Orders.Length != 3 ||
                data.LastFridgeIngredient < 0 || data.LastFridgeIngredient > Inventory.IngredientCount ||
                data.Sequence < 0 || data.NextIndoorResident < 0 ||
                data.NextIndoorResident >= (int)CustomerKind.Child || !IsSeconds(data.NextVisit)) return false;
            foreach (int item in data.Food) if (item < -1 || item >= Inventory.IndoorItemKeyCount) return false;
            foreach (int count in data.Display) if (count < 0 || count > 100) return false;
            foreach (KitchenOrder order in data.Orders)
                if (order == null || order.Phase < 0 || order.Phase > 4 || order.Menu < 0 || order.Menu >= 7 ||
                    !IsSeconds(order.Remaining) || (order.ReplacementPending && order.Phase != 0) ||
                    order.Sequence < 0 || order.Gold < 0 || order.Xp < 0 ||
                    (order.WarnedPhase != 0 && order.WarnedPhase != 2 && order.WarnedPhase != 3) ||
                    order.ResidentTicket < 0 || order.ResidentTicket > (int)CustomerKind.Child) return false;
            Vector3 pos = data.InteriorPosition;
            if (data.HasInteriorPosition && (!(Mathf.Abs(pos.x) < 1000) || !(Mathf.Abs(pos.y) < 1000) ||
                !(Mathf.Abs(pos.z) < 1000))) return false;
            return IsCookArray(data.Inside) && IsCookArray(data.Outside) &&
                IsSkewerSaveValid(data, requireSkewers);
        }

        private static bool IsSkewerSaveValid(KitchenSaveData data, bool required)
        {
            if (data.SkewerCooks == null && data.SkewerDisplay == null) return !required;
            if (data.SkewerDisplay == null || data.SkewerDisplay.Length != OutdoorMenu.RecipeCount ||
                data.SkewerCooks == null || data.SkewerCooks.Length != 3) return false;

            foreach (int count in data.SkewerDisplay)
                if (count < 0 || count > OutdoorDisplayLimit) return false;

            foreach (OutdoorCook cook in data.SkewerCooks)
            {
                if (cook == null || cook.Selection < 0 || cook.Selection > OutdoorMenu.RecipeCount ||
                    !IsSeconds(cook.Remaining) || (cook.Started && (cook.Selection == 0 || cook.NeedsSelection)) ||
                    cook.Queued < 0 || cook.Queued >= OutdoorCookLimit ||
                    (!cook.Started && (cook.Remaining != 0 || cook.Queued != 0))) return false;
            }
            return true;
        }

        private static bool IsCookArray(KitchenCook[] cooks)
        {
            if (cooks == null || cooks.Length != 3) return false;
            foreach (KitchenCook cook in cooks)
            {
                if (cook == null || cook.Menu < -1 || cook.Menu >= 7 || cook.LastRecipe < 0 || cook.LastRecipe > 7 || !IsSeconds(cook.Remaining) ||
                    cook.Ingredients == null || (cook.Ingredients.Length != 5 && cook.Ingredients.Length != Inventory.IngredientCount) ||
                    (cook.Menu < 0 && (cook.Started || cook.Remaining > 0))) return false;
                foreach (int count in cook.Ingredients) if (count < 0 || count > 100) return false;
            }
            return true;
        }

        private static bool IsSeconds(float seconds) { return seconds >= 0 && seconds <= 3600; }

        public int DepositBag()
        {
            int moved = _inventory.DepositAll();
            if (moved > 0 && State.LastFridgeIngredient == 0)
            {
                for (int i = 0; i < Inventory.IngredientCount; i++)
                {
                    if (_inventory.GetStorageCount((IngredientType)i) == 0) continue;
                    State.LastFridgeIngredient = i + 1;
                    break;
                }
            }
            return moved;
        }

        public bool TakeIngredient(int key)
        {
            if (State.Hand != -1 || key >= Inventory.IndoorItemKeyCount ||
                !Inventory.IsIngredientKey(key) || !_inventory.TryTakeFridge(Inventory.KeyIngredient(key))) return false;
            State.Hand = key;
            State.LastFridgeIngredient = (int)Inventory.KeyIngredient(key) + 1;
            Sfx.Play(SfxCue.Pickup);
            return true;
        }

        public bool PutIngredient()
        {
            if (!Inventory.IsIngredientKey(State.Hand) || !_inventory.TryPutFridge(Inventory.KeyIngredient(State.Hand))) return false;
            State.Hand = -1;
            Sfx.Play(SfxCue.Store);
            return true;
        }

        public int StoredItem(int slot)
        {
            int saved = State.Food[slot];
            if (saved < 0) return -1;
            return saved < 7 ? saved + 5 : Inventory.IngredientKey((IngredientType)(saved - 7));
        }

        internal int LatestStoredItem()
        {
            List<int> order = State.FoodStorageOrder;
            return order.Count > 0 ? StoredItem(order[order.Count - 1]) : -1;
        }

        private void RestoreFoodStorageOrder()
        {
            int occupied = 0;
            for (int i = 0; i < State.Food.Length; i++)
                if (State.Food[i] >= 0) occupied |= 1 << i;

            List<int> order = State.FoodStorageOrder;
            if (order != null)
            {
                int seen = 0;
                foreach (int slot in order)
                {
                    if (slot < 0 || slot >= State.Food.Length || (seen & (1 << slot)) != 0)
                    {
                        seen = -1;
                        break;
                    }
                    seen |= 1 << slot;
                }
                if (seen == occupied) return;
            }

            // 보관 순서가 없던 저장은 기존 칸 순서로 초기화한다.
            State.FoodStorageOrder = new List<int>();
            for (int i = 0; i < State.Food.Length; i++)
                if (State.Food[i] >= 0) State.FoodStorageOrder.Add(i);
        }

        public bool StoreCarriedItem()
        {
            if (State.Hand < 0 || !TryStoreItem(State.Hand)) return false;
            State.Hand = -1;
            Sfx.Play(SfxCue.Store);
            return true;
        }

        private bool TryStoreItem(int key)
        {
            for (int i = 0; i < State.Food.Length; i++)
            {
                if (State.Food[i] >= 0) continue;
                State.Food[i] = Inventory.IsIngredientKey(key) ? (int)Inventory.KeyIngredient(key) + 7 : key - 5;
                State.FoodStorageOrder.Add(i);
                return true;
            }
            return false;
        }

        internal void SettleIndoorFood(out int stored, out int discarded)
        {
            stored = 0;
            discarded = 0;
            if (Inventory.IsFoodKey(State.Hand))
            {
                if (TryStoreItem(State.Hand)) stored++;
                else discarded++;
                State.Hand = -1;
            }

            for (int i = 0; i < State.Inside.Length; i++)
            {
                KitchenCook cook = State.Inside[i];
                if (CanChangeMenu(i)) continue;
                if (cook.Ready && TryStoreItem(cook.Menu + 5)) stored++;
                else discarded++;
                State.Inside[i] = new KitchenCook { Menu = cook.Menu, LastRecipe = cook.LastRecipe };
            }
        }

        public bool TakeStoredItem(int slot)
        {
            if (State.Hand != -1 || slot < 0 || slot >= State.Food.Length || State.Food[slot] < 0) return false;
            State.Hand = StoredItem(slot);
            State.Food[slot] = -1;
            State.FoodStorageOrder.Remove(slot);
            Sfx.Play(SfxCue.Pickup);
            return true;
        }

        public bool SelectMenu(int slot, int menu)
        {
            if (_sales.IsClosing && !HasAcceptedOrder(menu)) return false;
            if (!IsUnlocked(menu) || slot < 0 || slot >= State.Inside.Length || !CanChangeMenu(slot)) return false;
            KitchenCook cook = State.Inside[slot];
            cook.Menu = menu;
            cook.LastRecipe = menu + 1;
            return true;
        }

        internal int OutdoorCookCount(int slot)
        {
            OutdoorCook cook = State.SkewerCooks[slot];
            return cook.Started ? 1 + cook.Queued : 0;
        }

        internal bool TryQueueSkewer(int slot, int menu, out string message, out bool missingIngredients)
        {
            missingIngredients = false;
            message = "먼저 조리할 꼬치를 지정하세요";
            if (slot < 0 || slot >= Growth.OutdoorCookers(_sales.Progress))
                return false;

            OutdoorCook cook = State.SkewerCooks[slot];
            if (State.Outside[slot].Started || (cook.Started && cook.Selection != menu + 1))
            {
                message = "화로가 이미 조리 중이에요";
                return false;
            }

            if (OutdoorCookCount(slot) >= OutdoorCookLimit)
            {
                message = $"화로마다 최대 {OutdoorCookLimit}개까지 조리할 수 있어요";
                return false;
            }
            if (!IsSkewerUnlocked(menu))
            {
                message = "필요한 숲을 먼저 해금하세요";
                return false;
            }
            if (State.SkewerDisplay[menu] >= OutdoorDisplayLimit)
            {
                message = "진열대가 가득 찼어요";
                return false;
            }
            OutdoorRecipe recipe = Skewer(menu);
            if (!_inventory.TryConsumeFridgeIngredients(recipe.Ingredients))
            {
                missingIngredients = true;
                message = "재료가 부족합니다.";
                return false;
            }

            if (cook.Started)
            {
                cook.Queued++;
                message = $"꼬치를 예약했어요. ({OutdoorCookCount(slot)}/{OutdoorCookLimit})";
                return true;
            }

            cook.Selection = menu + 1;
            cook.NeedsSelection = false;
            StartSkewer(slot, cook, recipe);
            message = "꼬치 조리를 시작했어요";
            return true;
        }

        private void StartSkewer(int slot, OutdoorCook cook, OutdoorRecipe recipe)
        {
            cook.Started = true;
            cook.Remaining = recipe.CookingSeconds;
            _outdoorWait[slot] = 0;
            _outdoorCompletion[slot] = 0f;
            Sfx.Play(SfxCue.Cook);
        }

        internal void CancelSkewerCooks()
        {
            for (int slot = 0; slot < State.SkewerCooks.Length; slot++)
            {
                OutdoorCook cook = State.SkewerCooks[slot];
                if (!cook.Started) continue;

                int menu = cook.Selection - 1;
                int count = OutdoorCookCount(slot);
                OutdoorRecipe recipe = Skewer(menu);
                if (recipe == null || !_inventory.TryRefundFridgeRecipe(recipe.Ingredients, count))
                {
                    Debug.LogError($"[Gameplay] event=outdoor_cooking result=refund_failed slot={slot + 1} menu={menu} count={count}");
                    continue;
                }

                cook.Started = false;
                cook.Remaining = 0f;
                cook.Queued = 0;
                cook.NeedsSelection = true;
                _outdoorWait[slot] = 0;
                _outdoorCompletion[slot] = 0f;
                Debug.Log($"[Gameplay] event=outdoor_cooking result=canceled_and_refunded slot={slot + 1} menu={menu} count={count}");
            }
        }

        internal float CookingProgress(int slot, bool outdoors)
        {
            if (outdoors && !State.Outside[slot].Started)
            {
                OutdoorCook current = State.SkewerCooks[slot];
                if (_outdoorCompletion[slot] > 0f) return 1f;
                OutdoorRecipe skewer = current.Started ? Skewer(current.Selection - 1) : null;
                return skewer == null ? 0f : 1f - Mathf.Clamp01(current.Remaining / skewer.CookingSeconds);
            }
            KitchenCook cook = outdoors ? State.Outside[slot] : State.Inside[slot];
            if (!cook.Started) return 0f;
            float duration = Recipe(cook.Menu).GetCookingSeconds(outdoors);
            return 1f - Mathf.Clamp01(cook.Remaining / Mathf.Max(.01f, duration));
        }

        public bool Insert(int slot)
        {
            if (!CanInsert(slot)) return false;

            KitchenCook cook = State.Inside[slot];
            int ingredient = (int)Inventory.KeyIngredient(State.Hand);
            cook.Menu = SelectedIndoorMenu(cook);
            cook.Ingredients[ingredient]++;
            State.Hand = -1;
            foreach (RecipeIngredient item in Recipe(cook.Menu).Ingredients)
            {
                if (cook.Ingredients[(int)item.Ingredient] >= item.Count) continue;
                Sfx.Play(SfxCue.Cook);
                return true;
            }
            StartCooking(cook);
            return true;
        }

        public bool CanInsert(int slot)
        {
            KitchenCook cook = State.Inside[slot];
            if (cook.Started || !Inventory.IsIngredientKey(State.Hand)) return false;

            int menu = SelectedIndoorMenu(cook);
            int ingredient = (int)Inventory.KeyIngredient(State.Hand);
            return menu >= 0 && (!_sales.IsClosing || HasAcceptedOrder(menu)) &&
                cook.Ingredients[ingredient] < Need(menu, ingredient);
        }

        private int SelectedIndoorMenu(KitchenCook cook)
        {
            if (cook.Menu >= 0) return cook.Menu;

            // 이전 저장은 완성 음식을 꺼내면 선택을 LastRecipe에만 남겼다.
            int lastMenu = cook.LastRecipe - 1;
            return IsUnlocked(lastMenu) ? lastMenu : -1;
        }

        public bool CanChangeMenu(int slot)
        {
            KitchenCook cook = State.Inside[slot];
            if (cook.Started) return false;
            foreach (int count in cook.Ingredients) if (count > 0) return false;
            return true;
        }

        public bool TakeCooked(int slot)
        {
            KitchenCook cook = State.Inside[slot];
            if (State.Hand != -1 || !cook.Ready) return false;
            State.Hand = cook.Menu + 5;
            State.Inside[slot] = new KitchenCook { Menu = cook.Menu, LastRecipe = cook.Menu + 1 };
            Sfx.Play(SfxCue.Pickup);
            return true;
        }

        private bool HasAcceptedOrder(int menu)
        {
            foreach (KitchenOrder order in State.Orders)
                if (order.Phase == 3 && order.Remaining > 0f && order.Menu == menu) return true;
            return false;
        }

        public void UpdateCooks(float seconds, bool outdoorOpen)
        {
            for (int i = 0; i < 3; i++)
            {
                _outdoorCompletion[i] = Mathf.Max(0f, _outdoorCompletion[i] - seconds);
                KitchenCook indoor = State.Inside[i];
                if (!_sales.IsClosing || HasAcceptedOrder(indoor.Menu)) Advance(indoor, seconds, SfxCue.IndoorCookReady);
                if (!outdoorOpen) continue;
                KitchenCook legacy = State.Outside[i];
                if (legacy.Started)
                {
                    Advance(legacy, seconds, SfxCue.CookReady);
                    if (legacy.Ready && State.Display[legacy.Menu] < OutdoorDisplayLimit)
                    {
                        State.Display[legacy.Menu]++;
                        MarkDisplayDish(OutdoorMenu.RecipeCount + legacy.Menu);
                        DisplayVersion++;
                        State.Outside[i] = new KitchenCook { LastRecipe = legacy.Menu + 1 };
                        _outdoorCompletion[i] = 1f;
                        Debug.Log($"[Gameplay] event=outdoor_display result=legacy_stocked menu={legacy.Menu}");
                    }
                    continue;
                }

                if (i < Growth.OutdoorCookers(_sales.Progress)) UpdateSkewer(i, seconds);
            }
        }

        private void UpdateSkewer(int slot, float seconds)
        {
            OutdoorCook cook = State.SkewerCooks[slot];
            int menu = cook.Selection - 1;
            if (!cook.Started || !IsSkewerUnlocked(menu)) return;
            if (State.SkewerDisplay[menu] >= OutdoorDisplayLimit)
            {
                LogOutdoorWait(slot, menu, 1, "display_full");
                return;
            }

            if (_outdoorWait[slot] != 0)
            {
                _outdoorWait[slot] = 0;
                Debug.Log($"[Gameplay] event=outdoor_cooking result=resumed slot={slot} menu={menu}");
            }

            cook.Remaining = Mathf.Max(0f, cook.Remaining - seconds);
            if (cook.Remaining > 0f) return;

            State.SkewerDisplay[menu]++;
            MarkDisplayDish(menu);
            DisplayVersion++;
            Sfx.Play(SfxCue.CookReady);
            Debug.Log($"[Gameplay] event=outdoor_display result=stocked slot={slot} menu={menu} count={State.SkewerDisplay[menu]}");
            if (cook.Queued > 0)
            {
                cook.Queued--;
                StartSkewer(slot, cook, Skewer(menu));
                Debug.Log($"[Gameplay] event=outdoor_cooking result=started_from_queue slot={slot + 1} menu={menu} count={OutdoorCookCount(slot)}");
                return;
            }

            cook.Started = false;
            cook.NeedsSelection = true;
            _outdoorCompletion[slot] = 1f;
        }

        private void LogOutdoorWait(int slot, int menu, int state, string reason)
        {
            if (_outdoorWait[slot] == state) return;
            _outdoorWait[slot] = state;
            Debug.Log($"[Gameplay] event=outdoor_cooking result=waiting slot={slot} menu={menu} reason={reason}");
        }

        public bool HasOrderStock()
        {
            for (int menu = 0; menu < 7; menu++)
                if (IsUnlocked(menu) && (FoodAvailable(menu) > 0 || HasIndoorIngredients(menu))) return true;
            return false;
        }

        private bool HasIndoorIngredients(int menu)
        {
            foreach (RecipeIngredient item in Recipe(menu).Ingredients)
                if (IndoorIngredientCount((int)item.Ingredient) < item.Count) return false;
            return true;
        }

        private int IndoorIngredientCount(int ingredient)
        {
            int count = _inventory.GetStorageCount((IngredientType)ingredient);
            if (State.Hand == Inventory.IngredientKey((IngredientType)ingredient)) count++;
            for (int i = 0; i < State.Food.Length; i++)
                if (State.Food[i] == ingredient + 7) count++;
            return count;
        }

        public bool CanOffer(int menu)
        {
            if (!IsUnlocked(menu)) return false;
            int[] stock = new int[Inventory.IngredientCount];
            int[] dishes = new int[7];
            for (int i = 0; i < stock.Length; i++) stock[i] = IndoorIngredientCount(i);
            for (int i = 0; i < dishes.Length; i++) dishes[i] = FoodAvailable(i);
            foreach (KitchenOrder order in State.Orders)
            {
                if (order.Phase < 1 || order.Phase > 3) continue;
                if (dishes[order.Menu] > 0) dishes[order.Menu]--;
                else foreach (RecipeIngredient item in Recipe(order.Menu).Ingredients) stock[(int)item.Ingredient] -= item.Count;
            }
            if (dishes[menu] > 0) return true;
            foreach (RecipeIngredient item in Recipe(menu).Ingredients)
                if (stock[(int)item.Ingredient] < item.Count) return false;
            return true;
        }

        private int FoodAvailable(int menu)
        {
            int count = State.Hand == menu + 5 ? 1 : 0;
            foreach (int food in State.Food) if (food == menu) count++;
            foreach (KitchenCook cook in State.Inside) if (cook.Menu == menu && cook.Started) count++;
            return count;
        }

        internal bool HasReadyFood(int menu)
        {
            if (State.Hand == menu + 5 || Array.IndexOf(State.Food, menu) >= 0) return true;
            foreach (KitchenCook cook in State.Inside)
                if (cook.Menu == menu && cook.Ready) return true;
            return false;
        }

        private void StartCooking(KitchenCook cook)
        {
            cook.Remaining = Recipe(cook.Menu).CookingSeconds;
            cook.Started = true;
            Sfx.Play(SfxCue.Cook);
            Debug.Log($"[Gameplay] event=kitchen_cooking result=started menu={cook.Menu} seconds={cook.Remaining}");
        }

        private static void Advance(KitchenCook cook, float seconds, SfxCue readyCue)
        {
            if (!cook.Started || cook.Remaining <= 0) return;
            cook.Remaining = Mathf.Max(0, cook.Remaining - seconds);
            if (cook.Ready)
            {
                Sfx.Play(readyCue);
                Debug.Log($"[Gameplay] event=kitchen_cooking result=ready menu={cook.Menu}");
            }
        }

        private int Need(int menu, int ingredient)
        {
            foreach (RecipeIngredient item in Recipe(menu).Ingredients)
                if ((int)item.Ingredient == ingredient) return item.Count;
            return 0;
        }
    }
}
