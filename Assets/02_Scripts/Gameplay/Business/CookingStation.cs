using System.Collections.Generic;
using HamPocha.Audio;
using HamPocha.Data;
using HamPocha.Core;
using HamPocha.UI;
using UnityEngine;
using Unity.Profiling;

namespace HamPocha.Gameplay
{
    public enum RecipeId { CarrotSoup = 0, Toast = 1, CarrotSandwich = 2, TomatoBreadPizza = 3, MeatTaco = 4, CornSalad = 5, MeatBurger = 6 }

    public class CookingStation : MonoBehaviour
    {
        private static readonly ProfilerMarker _cancelOrdersMarker = new ProfilerMarker("Business.Close.CancelOrders");
        [SerializeField] private Inventory _inventory;
        [SerializeField] private GameSessionState _session;
        [SerializeField] private WorldClock _clock;
        [SerializeField] private ContentCatalog _catalog;
        // 정의 SO 연결 전의 기존 합성 검사/기존 설정 호환용이다.
        [SerializeField, HideInInspector] private int _acornGold = 10;
        [SerializeField, HideInInspector] private int _mushroomGold = 15;
        [SerializeField, HideInInspector] private int _saleXp = 1;
        private BusinessSaveData _business = new BusinessSaveData();
        private GrowthSaveData _growth = new GrowthSaveData
        {
            Tutorial = new TutorialSaveData { Enabled = true, FirstDayRound = 1 },
            SkewerLevels = new int[OutdoorMenu.RecipeCount]
        };
        private readonly List<TableOrder> _orders = new List<TableOrder>();
        private readonly List<TableOrder> _occupiedTables = new List<TableOrder>();
        private readonly List<TableOrder> _knownTables = new List<TableOrder>();
        public void RegisterTable(TableOrder table) { if (!_knownTables.Contains(table)) _knownTables.Add(table); }
        private TableOrder _selectedOrder;
        private TableOrder _selectedFoodOrder;
        private bool _hasSelection;
        private RecipeId _selectedRecipe;
        private bool _hasFoodSelection;
        private RecipeId _selectedFood;
        // 현재 선택 메뉴의 준비 수량이다. 손님 주문의 생존 여부와 분리한다.
        private int _insertedCount;
        private bool _wasRunning;
        private int _statisticsDay = -1;
        private int _nextOrderSequence;
        private readonly List<CookJobSaveData> _jobs = new List<CookJobSaveData>(ContentCatalog.MaxCookingSlots);
        private SceneLoadManager _loader;
        private PlayerWorldInteraction _playerInteraction;
        private MarketInteractable _market;
        public int JobCount { get { return _jobs.Count; } }
        public int CookingCapacity { get { return _growth.CookingSlots; } }
        public bool CanUseWorldActions { get { return CanRun && _loader != null && _loader.CanUseWorldInteraction && !_loader.IsUiOpen; } }
        public bool HasCarriedFood { get { return _hasFoodSelection; } }
        public RecipeId CarriedFood { get { return _selectedFood; } }
        public int TakeawayCapacity { get { return _growth.Markets[1] ? 4 : 3; } }
        public CookJobSaveData GetJob(int index) { return index >= 0 && index < _jobs.Count ? _jobs[index] : null; }
        public string CloseNotice { get { return "영업을 종료할까요?\n들고 있는 음식은 서빙/지정 장소 폐기 후 종료할 수 있습니다.\n조리 중인 음식은 폐기하며 재료를 반환하지 않습니다.\n완성 음식은 공용창고에 보관하고 공간이 부족한 음식은 폐기합니다.\n식사 중 손님은 즉시 정산 후 퇴장합니다."; } }

        public long Gold { get { return _business.Gold; } }

        internal void ApplyAdGold(long gold)
        {
            _business.Gold = gold;
            Version++;
        }
        internal bool GrantTutorialSleepReward()
        {
            TutorialSaveData tutorial = _growth.Tutorial;
            if (tutorial == null || !tutorial.Enabled || tutorial.Step != 20 ||
                tutorial.ClosingLesson != 6 || tutorial.CompletedTutorialSleep) return false;
            tutorial.CompletedTutorialSleep = true;
            tutorial.TutorialRewardQueued = true;
            Version++;
            Debug.Log("[Gameplay] event=tutorial_reward result=queued gold=300", this);
            return true;
        }

        internal void ApplyMailboxReward(CookingSaveData saved)
        {
            _business.Gold = saved.Business.Gold;
            if (_growth.Tutorial != null && saved.Growth.Tutorial != null)
                _growth.Tutorial.TutorialFurnitureGranted = saved.Growth.Tutorial.TutorialFurnitureGranted;
            Version++;
        }
        public long SalesXp { get { return ToExperience(_business.SalesXp); } }
        public int Level { get { return Growth.Level(_catalog, SalesXp); } }
        public ContentCatalog Catalog { get { return _catalog; } }
        public GrowthSaveData Progress { get { return _growth; } }
        public bool SummaryPending { get { return _business.SummaryPending; } }
        internal bool ClosedManually => _business.ClosedManually;
        public long SummaryGold { get { return _business.SummaryGold; } }
        public int ServedCount { get; private set; }
        public int WrongFoodCount { get; private set; }
        public int MissedCount { get; private set; }
        public int Version { get; private set; }
        public string LastMessage { get; private set; } = "원하는 메뉴를 선택하고 재료를 넣어 미리 조리할 수 있습니다.";
        public RecipeId SelectedRecipe { get { return _selectedRecipe; } }
        public bool HasSelection { get { return _hasSelection; } }
        public bool HasFoodSelection { get { return _hasFoodSelection; } }
        public RecipeId SelectedFood { get { return _selectedFood; } }
        public TableOrder SelectedFoodOrder { get { return _selectedFoodOrder; } }
        public TableOrder SelectedOrder { get { return _selectedOrder; } }
        public int InsertedCount { get { return _insertedCount; } }
        public int RequiredCount { get { return _catalog == null ? 1 : _catalog.Recipes[(int)_selectedRecipe].TotalCount; } }
        public bool CanRun { get { return _clock.CanAdvance && _session.CurrentActivity == PlayerActivity.Business; } }
        public string SummaryText { get { return $"영업 {_business.LastSummaryDay}회 결산\n정상 판매 {_business.SummaryServed} · 조리 실수 {_business.SummaryWrong} · 접수 누락 {_business.SummaryMissed}\n매출 {_business.SummaryGold} 골드 · 판매 경험치 {ToExperience(_business.SummaryXp)}"; } }

        private long ToExperience(long salePoints)
        {
            // 기존 저장과 진행 중 주문의 판매 점수는 원래 단위로 읽고 쓴다.
            if (_catalog == null) return salePoints;
            return Growth.ExperienceFromSalePoints(salePoints);
        }

        private void Awake()
        {
            _loader = FindObjectOfType<SceneLoadManager>();
            _playerInteraction = FindObjectOfType<PlayerWorldInteraction>();

            if (_inventory == null || _session == null || _clock == null || (_catalog != null && !_catalog.IsValid()))
            {
                Debug.LogError($"[Gameplay] frame={Time.frameCount} event=cooking_initialize result=failed reason=missing_reference", this);
                enabled = false;
            }
        }

        private void Update()
        {
            bool running = _session.CurrentActivity == PlayerActivity.Business;
            if (running) EnsureStatisticsDay();
            if (CanRun && _clock.CanAdvanceTimers)
            {
                foreach (CookJobSaveData job in _jobs)
                {
                    if (job.Elapsed >= job.Duration) continue;
                    job.Elapsed = Mathf.Min(job.Duration, job.Elapsed + Time.deltaTime);
                    if (job.Elapsed >= job.Duration)
                    {
                        Version++;
                        Sfx.Play(SfxCue.CookReady);
                        Debug.Log($"[Gameplay] event=cook_job result=ready recipe={job.Recipe}", this);
                    }
                }
            }
            CompleteBusinessIfNeeded();
            _wasRunning = running;
        }

        public bool HasRawRecipeStock()
        {
            if (_catalog == null) return false;
            for (int i = 0; i < 7; i++) if (CanPrepareRecipe((RecipeId)i)) return true;
            return false;
        }

        public bool IsNear(Vector3 position, float range = 2f)
        {
            if (_loader == null || _loader.PlayerBody == null) return false;
            Vector3 delta = _loader.PlayerBody.position - position;
            delta.y = 0;
            return delta.sqrMagnitude <= range * range;
        }

        public bool IsAtMarket()
        {
            if (_loader == null || _loader.CurrentRegion == null || _loader.PlayerBody == null) return false;
            if (_market == null || !_market.isActiveAndEnabled)
                _market = _loader.CurrentRegion.GetComponentInChildren<MarketInteractable>();

            return _market != null && _market.IsInInteractionRange(_loader.PlayerBody.position, _market.InteractionRange);
        }

        public bool CanInteractWith(TableOrder table)
        {
            return _playerInteraction != null && _playerInteraction.CanInteractWith(table);
        }

        public bool TryPickup(int index)
        {
            CookJobSaveData job = GetJob(index);
            if (!CanRun || !IsAtMarket() || HasCarriedFood || job == null || job.Elapsed < job.Duration)
            {
                Debug.LogWarning($"[Gameplay] event=food_pickup result=rejected reason=unavailable_or_not_ready slot={index} carried={HasCarriedFood}", this);
                return false;
            }
            _selectedFood = job.Recipe;
            _hasFoodSelection = true;
            _jobs.RemoveAt(index);
            Sfx.Play(SfxCue.Pickup);
            _playerInteraction?.PlayInteractionMotion();
            SetMessage(GetRecipeLabel(_selectedFood) + " 운반 중 · 손님에게 직접 이동해 서빙하세요");
            return true;
        }

        public bool TryDiscardCarried()
        {
            if (!CanRun || !HasCarriedFood) return false;
            _hasFoodSelection = false;
            Sfx.Play(SfxCue.MenuClose);
            _playerInteraction?.PlayInteractionMotion();
            SetMessage("들고 있던 음식을 폐기했습니다. 재료 반환 없음");
            return true;
        }

        public bool TryCancelCooking(CookJobSaveData job)
        {
            // UI를 연 뒤 칸이 정렬돼도 다른 작업을 취소하지 않도록 작업 자체를 확인한다.
            if (!CanRun || !IsAtMarket() || job == null || !_jobs.Contains(job) || job.Elapsed >= job.Duration)
            {
                Debug.LogWarning($"[Gameplay] event=cook_cancel result=rejected reason=unavailable_or_already_ready", this);

                return false;
            }

            _jobs.Remove(job);
            SetMessage(GetRecipeLabel(job.Recipe) + " 조리를 취소했습니다. 사용한 재료는 반환하지 않습니다.");
            Debug.Log($"[Gameplay] event=cook_cancel result=discarded recipe={job.Recipe} refund=0", this);

            return true;
        }

        public bool IsPlaceUnlocked(TableOrder table)
        {
            if (table.IsTakeaway) return table.MarketIndex == 0 && table.SlotIndex < TakeawayCapacity;
            return table.MarketIndex < 2 && _growth.Markets[table.MarketIndex] && table.SlotIndex < _growth.Tables[table.MarketIndex];
        }

        private static readonly string[] RecipeNames = { "당근 수프", "바삭 토스트", "당근 샌드위치", "토마토 빵피자", "고기 타코", "옥수수 샐러드", "고기 버거" };

        public static string GetRecipeName(RecipeId recipe) { return RecipeNames[(int)recipe]; }
        public string GetRecipeLabel(RecipeId recipe) { return _catalog == null ? GetRecipeName(recipe) : _catalog.Recipes[(int)recipe].DisplayName; }

        public static IngredientType GetIngredient(RecipeId recipe)
        {
            return recipe == RecipeId.CarrotSoup ? IngredientType.Carrot : IngredientType.Bread;
        }

        public int GetSaleGold(RecipeId recipe)
        {
            return _catalog == null ? (recipe == RecipeId.CarrotSoup ? _acornGold : _mushroomGold) : Growth.Price(_catalog, _growth, (int)recipe);
        }

        public int GetSaleXp(RecipeId recipe) { return _catalog == null ? _saleXp : _catalog.Recipes[(int)recipe].SalesXp; }
        public bool IsForestOwned(int index) { return Growth.IsForestOwned(_growth, index); }
        public bool IsMarketOwned(int index)
        {
            return _growth != null && _growth.Markets != null && index >= 0 && index < _growth.Markets.Length && _growth.Markets[index];
        }

        public string PurchaseInfo(PurchaseType type, int index)
        {
            string reason;
            bool allowed = CanPurchase(type, index, out reason);
            return Growth.Cost(_catalog, _growth, type, index) + " 골드 · " + (allowed ? "구매 가능" : reason);
        }

        public bool CanPurchase(PurchaseType type, int index, out string reason)
        {
            return Growth.CanBuy(_catalog, _growth, _business, type, index, out reason);
        }

        public bool TryBuy(PurchaseType type, int index)
        {
            if (!_clock.CanAdvance || (_session.CurrentActivity != PlayerActivity.FreeRoam && _session.CurrentActivity != PlayerActivity.Business))
            {
                SetMessage("현재 상태에서는 구매할 수 없습니다.");
                return false;
            }

            string reason;
            bool success = Growth.TryBuy(_catalog, _growth, _business, type, index, out reason);
            if (success) Sfx.Play(SfxCue.Growth);
            SetMessage(reason);
            Debug.Log($"[Gameplay] frame={Time.frameCount} event=growth_purchase result={success} type={type} index={index} reason={reason}", this);

            return success;
        }

        public int AvailableOrderCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < (_catalog == null ? 2 : 7); i++) total += GetAvailableOrderCount((RecipeId)i);

                return total;
            }
        }

        private int GetSupplyCount(RecipeId recipe)
        {
            int count = _inventory.GetFoodCount(recipe);
            foreach (CookJobSaveData job in _jobs) if (job.Recipe == recipe) count++;
            if (HasCarriedFood && CarriedFood == recipe) count++;
            return count;
        }

        public int GetReservedCount(IngredientType ingredient)
        {
            int reserved = 0;
            if (_catalog == null) return 0;
            for (int i = 0; i < 7; i++)
            {
                RecipeId recipe = (RecipeId)i;
                int deficit = Mathf.Max(0, GetWaitingCount(recipe) - GetSupplyCount(recipe));
                foreach (RecipeIngredient item in _catalog.Recipes[i].Ingredients)
                    if (item.Ingredient == ingredient) reserved += deficit * item.Count;
            }
            return reserved;
        }

        public void DepositToStorage()
        {
            int moved = _inventory.DepositAll();
            if (moved > 0) _playerInteraction?.PlayInteractionMotion();
            SetMessage("입고 " + moved + "개 · 공간 부족분은 가방에 유지합니다.");
        }

        public int GetUnassignedFood(RecipeId recipe)
        {
            return Mathf.Max(0, GetSupplyCount(recipe) - GetWaitingCount(recipe));
        }

        private int GetAvailableOrderCount(RecipeId recipe)
        {
            if (_catalog == null || !_growth.Foods[(int)recipe]) return 0;
            int count = int.MaxValue;
            foreach (RecipeIngredient item in _catalog.Recipes[(int)recipe].Ingredients)
            {
                int available = Mathf.Max(0, _inventory.GetTotalCount(item.Ingredient) - GetReservedCount(item.Ingredient));
                count = Mathf.Min(count, available / item.Count);
            }
            return count + GetUnassignedFood(recipe);
        }

        public bool CanCreateOrder()
        {
            return CanRun && _session.CanTakeNewOrders && HasRawRecipeStock() && AvailableOrderCount > 0;
        }

        internal bool IsClosing => _session.CurrentActivity == PlayerActivity.Business && !_session.CanTakeNewOrders;
        internal bool CanAcceptOrders => _session.CurrentActivity == PlayerActivity.Business;

        internal bool HasWaitingCustomers()
        {
            if (PochaKitchen.Current != null) return PochaKitchen.Current.HasGuestsAtClose;
            foreach (TableOrder order in _orders)
                if (order.HasArrived && order.HasOrder && order.WaitRemaining > 0f) return true;
            return false;
        }

        internal void ReleaseArrivingOrders()
        {
            if (PochaKitchen.Current != null)
            {
                PochaKitchen.Current.ReleaseArrivingOrders();
                return;
            }
            for (int i = _orders.Count - 1; i >= 0; i--)
                if (!_orders[i].HasArrived) _orders[i].CancelForBusinessEnd();
        }

        public bool TryReservePlace(TableOrder table)
        {
            if (table == null || _occupiedTables.Contains(table)) return false;
            if (PochaKitchen.Current != null)
            {
                if (!PochaKitchen.Current.HasDisplayFood || table.IsTakeaway || table.MarketIndex != 0) return false;
            }
            else if (!CanCreateOrder()) return false;
            _occupiedTables.Add(table);
            if (table.IsTakeaway) RefreshTakeawayLine(table.MarketIndex, false);
            Version++;
            return true;
        }

        private void RefreshTakeawayLine(int market, bool restoring)
        {
            int position = 0;
            foreach (TableOrder place in _occupiedTables)
            {
                if (place.IsTakeaway && place.MarketIndex == market) place.SetQueuePosition(position++, restoring);
            }
        }

        public bool TryCreateOrder(TableOrder table, out RecipeId recipe)
        {
            recipe = RecipeId.CarrotSoup;
            if (!CanCreateOrder() || table == null || !_occupiedTables.Contains(table) || _orders.Contains(table) ||
                !table.HasArrived || (table.IsTakeaway && !IsFirstTakeaway(table))) return false;

            int candidates = 0;
            for (int i = 0; i < (_catalog == null ? 2 : 7); i++)
            {
                if (GetAvailableOrderCount((RecipeId)i) <= 0) continue;

                // 후보 메뉴마다 한 번만 추첨한다. 완성 음식이나 재료 수량에 가중치를 주지 않는다.
                candidates++;
                if (Random.Range(0, candidates) == 0) recipe = (RecipeId)i;
            }

            if (candidates == 0) return false;
            _orders.Add(table);
            EnsureStatisticsDay();
            Version++;

            return true;
        }

        public int NextAdmissionSequence() { return ++_nextOrderSequence; }
        public int GetTakeawayCount(int market)
        {
            int count = 0;
            foreach (TableOrder order in _occupiedTables) if (order.IsTakeaway && order.MarketIndex == market) count++;

            return count;
        }

        public bool IsFirstTakeaway(TableOrder table)
        {
            if (table == null || !table.IsTakeaway || !_occupiedTables.Contains(table)) return false;

            foreach (TableOrder order in _occupiedTables)
            {
                if (order.IsTakeaway && order.MarketIndex == table.MarketIndex && order.AdmissionSequence < table.AdmissionSequence) return false;
            }

            return true;
        }

        public int GetWaitingCount(RecipeId recipe)
        {
            int count = 0;
            foreach (TableOrder order in _orders) if (order.Recipe == recipe) count++;

            return count;
        }

        // 과거 호출 호환. 음식은 어느 손님에게도 귀속하지 않는다.
        public bool TryAssignStoredFood(TableOrder table) { return false; }
        private void AssignStoredFood() { }
        private void ReturnPreparedFood(TableOrder table) { table.SetPrepared(false); }
        public void SnapPlayerToMarketFront(Rigidbody player) { }

        public int GetPreparedCount(RecipeId recipe)
        {
            int count = _inventory.GetFoodCount(recipe);
            foreach (CookJobSaveData job in _jobs) if (job.Recipe == recipe && job.Elapsed >= job.Duration) count++;
            return count;
        }

        public void SelectOrder(TableOrder table)
        {
            // 이전 호출부도 주문 자체가 아닌 해당 메뉴를 조리 대상으로 선택한다.
            if (table == null || !_orders.Contains(table)) return;

            SelectRecipe(table.Recipe);
        }

        public void SelectRecipe(RecipeId recipe)
        {
            int index = (int)recipe;
            if (!CanRun || index < 0 || index >= _growth.Foods.Length || !_growth.Foods[index])
            {
                SetMessage("현재 조리할 수 없는 메뉴입니다.");
                return;
            }

            if (!_hasSelection || _selectedRecipe != recipe) _insertedCount = 0;
            _selectedOrder = null;
            _selectedRecipe = recipe;
            _hasSelection = true;
            SetMessage(GetRecipeLabel(recipe) + " 조리 선택 · 주문 없이도 만들 수 있습니다.");
        }

        private TableOrder FindUnpreparedOrder(RecipeId recipe)
        {
            foreach (TableOrder order in _orders)
            {
                if (order.HasOrder && order.Recipe == recipe && !order.HasPreparedFood) return order;
            }

            return null;
        }

        public bool CanPrepareRecipe(RecipeId recipe)
        {
            int index = (int)recipe;
            if (_catalog == null || index < 0 || index >= 7 || !_growth.Foods[index]) return false;
            // 예약은 신규 주문 메뉴 선정에만 적용한다. 자유 조리는 실제 재료를 소모할 수 있다.
            foreach (RecipeIngredient item in _catalog.Recipes[index].Ingredients)
                if (_inventory.GetTotalCount(item.Ingredient) < item.Count) return false;
            return true;
        }

        public void SelectPreparedOrder(TableOrder table) { if (table != null) SelectPreparedFood(table.Recipe); }

        public void SelectPreparedFood(RecipeId recipe)
        {
            if (!CanRun || HasCarriedFood) return;
            for (int i = 0; i < _jobs.Count; i++)
            {
                if (_jobs[i].Recipe == recipe && _jobs[i].Elapsed >= _jobs[i].Duration)
                {
                    TryPickup(i);
                    return;
                }
            }
            TryPickupStored(recipe);
        }

        public bool TryPickupStored(RecipeId recipe)
        {
            if (!CanRun || !IsAtMarket() || HasCarriedFood || !_inventory.TryTakeFood(recipe))
            {
                Debug.LogWarning($"[Gameplay] event=food_pickup result=rejected reason=stored_food_unavailable recipe={recipe} carried={HasCarriedFood}", this);
                return false;
            }
            _selectedFood = recipe;
            _hasFoodSelection = true;
            Sfx.Play(SfxCue.Pickup);
            _playerInteraction?.PlayInteractionMotion();
            SetMessage(GetRecipeLabel(recipe) + " 보관분1개 운반");
            return true;
        }

        public void OnButtonIngredientOrCook()
        {
            TutorialSaveData tutorial = _growth.Tutorial;
            if (tutorial != null && tutorial.Enabled && tutorial.Step == 14)
            {
                foreach (CookJobSaveData job in _jobs)
                {
                    if ((int)job.Recipe != tutorial.Recipe) continue;
                    Debug.LogWarning("[Gameplay] event=cook_start result=rejected reason=tutorial_wait_for_completion", this);

                    return;
                }
            }

            if (!CanRun || !IsAtMarket() || !_hasSelection || _jobs.Count >= CookingCapacity || !CanPrepareRecipe(_selectedRecipe))
            {
                SetMessage("조리할 메뉴·원재료·빈 조리대를 확인하세요");
                return;
            }
            RecipeData recipe = _catalog.Recipes[(int)_selectedRecipe];
            if (!_inventory.TryConsumeRecipe(recipe)) return;
            _jobs.Add(new CookJobSaveData { Recipe = _selectedRecipe, Duration = recipe.CookingSeconds, Elapsed = 0 });
            Sfx.Play(SfxCue.Cook);
            _playerInteraction?.PlayInteractionMotion();
            _insertedCount = 0;
            SetMessage(GetRecipeLabel(_selectedRecipe) + " 조리 시작 · UI를 닫아도 진행됩니다.");
        }

        public bool TryServe(TableOrder table)
        {
            if (!CanUseWorldActions || !HasCarriedFood || table == null || !table.HasOrder || !table.Accepted || !table.HasArrived ||
                table.Customer == null || table.WaitRemaining <= 0 || !_orders.Contains(table) ||
                (table.IsTakeaway && !IsFirstTakeaway(table)) || !CanInteractWith(table))
            {
                SetMessage("수락한 주문의 테이블/손님 가까이에서 서빙하세요. 대상이 겹치면 원하는 쪽을 바라보세요");
                return false;
            }
            RecipeId delivered = _selectedFood;
            bool correct = delivered == table.Recipe;
            if (correct && (_business.Gold > long.MaxValue - int.MaxValue || _business.SalesXp > long.MaxValue - int.MaxValue)) return false;
            if (correct && !table.Customer.TryReceiveFood()) return false;
            _hasFoodSelection = false;
            _orders.Remove(table);
            if (!correct)
            {
                table.MarkWrongFood(delivered);
                table.Customer.LeaveTable();
                SetMessage("오배송 · 음식 소진/골드·경험치 0");
                TutorialUi.Current?.OnWrongDelivery();
                return true;
            }
            Sfx.Play(SfxCue.Happy);
            if (_growth.Tutorial != null && _growth.Tutorial.Enabled && _growth.Tutorial.Step < 21)
            {
                if (table.IsTakeaway) _growth.Tutorial.TakeawayServed = true;
                else _growth.Tutorial.TableServed = true;
            }
            if (table.IsTakeaway)
            {
                table.SettleMeal();
                table.Customer.LeaveTable();
            }
            SetMessage(table.IsTakeaway ? "포장 즉시 정산 완료" : "서빙 완료 ·5초 식사 후 정산");
            return true;
        }

        public void CancelOrder(TableOrder table)
        {
            if (table == null) return;
            ReturnPreparedFood(table);
            _occupiedTables.Remove(table);
            if (table.IsTakeaway) RefreshTakeawayLine(table.MarketIndex, false);
            if (_orders.Remove(table)) SetMessage("주문 취소 · 예약 해제, 남은 완성 음식은 재사용할 수 있습니다.");
            if (CanRun) AssignStoredFood();
        }

        private bool HasPanelSales => SummaryPending || _session.HasOpenedBusinessToday;
        internal int TodayServedCount => SummaryPending ? _business.SummaryServed : HasPanelSales ? ServedCount : 0;
        internal long TodaySalesGold => SummaryPending ? _business.SummaryGold : HasPanelSales ? _business.DailyGold : 0;
        internal int PanelMissedCount => SummaryPending ? _business.SummaryMissed : HasPanelSales ? MissedCount : 0;
        internal int PanelWrongCount => SummaryPending ? _business.SummaryWrong : HasPanelSales ? WrongFoodCount : 0;
        internal string PanelExperience => ToExperience(SummaryPending ? _business.SummaryXp : HasPanelSales ? _business.DailyXp : 0).ToString("N0");

        internal int GetTodayMenuCount(int menu)
        {
            return HasPanelSales && _business.DailyMenuCounts != null &&
                menu >= 0 && menu < _business.DailyMenuCounts.Length ? _business.DailyMenuCounts[menu] : 0;
        }

        internal long GetTodayMenuGold(int menu)
        {
            return HasPanelSales && _business.DailyMenuGold != null &&
                menu >= 0 && menu < _business.DailyMenuGold.Length ? _business.DailyMenuGold[menu] : 0;
        }

        public void RecordOrderOutcome(OrderOutcome outcome, int gold = 0, int xp = 0, int menu = -1)
        {
            EnsureStatisticsDay();
            Version++;
            switch (outcome)
            {
                case OrderOutcome.Served:
                    ServedCount++;
                    _business.Gold += gold;
                    _business.SalesXp += xp;
                    _business.DailyGold += gold;
                    _business.DailyXp += xp;
                    if (menu >= 0 && menu < BusinessSaveData.MenuCount)
                    {
                        if (_business.DailyMenuCounts == null || _business.DailyMenuCounts.Length != BusinessSaveData.MenuCount)
                            _business.DailyMenuCounts = new int[BusinessSaveData.MenuCount];
                        if (_business.DailyMenuGold == null || _business.DailyMenuGold.Length != BusinessSaveData.MenuCount)
                            _business.DailyMenuGold = new long[BusinessSaveData.MenuCount];
                        _business.DailyMenuCounts[menu]++;
                        _business.DailyMenuGold[menu] += gold;
                    }
                    if (gold > 0) Sfx.Play(SfxCue.Gold);
                    Debug.Log($"[Gameplay] frame={Time.frameCount} event=sale_reward result=granted gold={gold} xp={ToExperience(xp)} level={Level}", this);
                    break;
                case OrderOutcome.WrongFood:
                    WrongFoodCount++;
                    Sfx.Play(SfxCue.Angry);
                    break;
                case OrderOutcome.Missed:
                    MissedCount++;
                    Sfx.Play(SfxCue.Angry);
                    break;
                case OrderOutcome.BusinessClosed:
                    MissedCount++;
                    break;
            }
        }

        public void CompleteBusinessIfNeeded(bool midnight = false, bool manual = false)
        {
            if (_session.CurrentActivity == PlayerActivity.Business || _session.BusinessState != DailyBusinessState.Closed) return;
            if (_statisticsDay <= _business.LastSummaryDay) return;
            _wasRunning = false;
            if (PochaKitchen.Current != null) PochaKitchen.Current.SettleBusinessClose();
            // 식사 정산과 퇴장/슬롯 정리는 summary 확정 전에1회 수행한다.
            using (_cancelOrdersMarker.Auto())
            {
                for (int i = _occupiedTables.Count - 1; i >= 0; i--)
                {
                    TableOrder table = _occupiedTables[i];
                    if (PochaKitchen.Current != null && table.Outcome == OrderOutcome.Served) continue;
                    table.CancelForBusinessEnd();
                }
            }
            int stored = 0;
            int discarded = 0;
            if (midnight)
            {
                PochaKitchen.Current?.Stock?.SettleIndoorFood(out stored, out discarded);
                if (HasCarriedFood)
                {
                    if (_inventory.TryStoreFood(CarriedFood)) stored++;
                    else discarded++;
                    _hasFoodSelection = false;
                }
            }
            foreach (CookJobSaveData job in _jobs)
            {
                if (job.Elapsed >= job.Duration && _inventory.TryStoreFood(job.Recipe)) stored++;
                else discarded++;
            }
            _jobs.Clear();
            _hasSelection = false;
            _insertedCount = 0;
            _business.LastSummaryDay = _statisticsDay;
            _business.SummaryServed = ServedCount;
            _business.SummaryWrong = WrongFoodCount;
            _business.SummaryMissed = MissedCount;
            _business.SummaryGold = _business.DailyGold;
            _business.SummaryXp = _business.DailyXp;
            _business.SummaryPending = true;
            _business.ClosedManually = manual;
            SetMessage($"결산 완료 · 음식 보관{stored}/폐기{discarded} · 보상은1회 정산했습니다.");
            Debug.Log($"[Gameplay] event=business_summary result=completed sequence={_statisticsDay} stored={stored} discarded={discarded}", this);
        }

        public void ConfirmSummary()
        {
            if (!SummaryPending) return;
            _business.SummaryPending = false;
            Version++;
            GameSave save = FindObjectOfType<GameSave>();
            if (save != null && save.IsReady) save.RequestSave();
            Sfx.Play(SfxCue.MenuClose);
            Debug.Log($"[UI] frame={Time.frameCount} event=business_summary result=confirmed day={_business.LastSummaryDay}", this);
        }

        public void BeginBusiness() { EnsureStatisticsDay(); _wasRunning = true; }

        private void EnsureStatisticsDay()
        {
            if (_session.CurrentActivity != PlayerActivity.Business || _statisticsDay == _session.BusinessSequence) return;
            _statisticsDay = _session.BusinessSequence;
            ServedCount = 0;
            WrongFoodCount = 0;
            MissedCount = 0;
            _business.DailyGold = 0;
            _business.DailyXp = 0;
            _business.DailyMenuCounts = null;
            _business.DailyMenuGold = null;
        }

        public CookingSaveData Capture()
        {
            CompleteBusinessIfNeeded();
            CookingSaveData data = new CookingSaveData { Kitchen = PochaKitchen.Current == null ? null : PochaKitchen.Current.Capture(),
                OperationVersion = 1, HasCarriedFood = HasCarriedFood, CarriedFood = CarriedFood, HasSelection = _hasSelection, SelectedRecipe = _selectedRecipe,
                HasFoodSelection = _hasFoodSelection, SelectedFood = _selectedFood, InsertedCount = InsertedCount,
                WasRunning = _wasRunning, StatisticsDay = _statisticsDay, ServedCount = ServedCount, WrongFoodCount = WrongFoodCount,
                MissedCount = MissedCount, Business = JsonUtility.FromJson<BusinessSaveData>(JsonUtility.ToJson(_business)),
                Growth = JsonUtility.FromJson<GrowthSaveData>(JsonUtility.ToJson(_growth)), NextOrderSequence = _nextOrderSequence,
                SelectedOrderId = SaveKey.Get(_selectedOrder), SelectedFoodOrderId = SaveKey.Get(_selectedFoodOrder),
                OrderTableIds = new List<string>(), OccupiedTableIds = new List<string>() };
            foreach (CookJobSaveData job in _jobs) data.Jobs.Add(new CookJobSaveData { Recipe = job.Recipe, Elapsed = job.Elapsed, Duration = job.Duration });
            foreach (TableOrder table in _orders) data.OrderTableIds.Add(SaveKey.Get(table));
            foreach (TableOrder table in _occupiedTables) data.OccupiedTableIds.Add(SaveKey.Get(table));

            return data;
        }

        public void Restore(CookingSaveData data, Dictionary<string, TableOrder> tables,
            bool upgradeContent = false, bool preserveStarterShrimp = false)
        {
            if (PochaKitchen.Current != null) PochaKitchen.Current.Restore(data.Kitchen);
            _business = data.Business == null ? new BusinessSaveData() : JsonUtility.FromJson<BusinessSaveData>(JsonUtility.ToJson(data.Business));
            if (!AreMenuSalesValid(_business, data.ServedCount))
            {
                _business.DailyMenuCounts = null;
                _business.DailyMenuGold = null;
                Debug.LogWarning("[Save] event=business_menu_sales result=fallback reason=invalid_breakdown", this);
            }
            _growth = data.Growth == null ? new GrowthSaveData() : JsonUtility.FromJson<GrowthSaveData>(JsonUtility.ToJson(data.Growth));
            for (int i = 0; i < _growth.FoodLevels.Length; i++)
            {
                if (_growth.FoodLevels[i] <= Growth.MaxFoodLevel) continue;
                Debug.Log($"[Save] event=food_level_restore result=capped menu={i} old_level={_growth.FoodLevels[i]} max_level={Growth.MaxFoodLevel}", this);
                _growth.FoodLevels[i] = Growth.MaxFoodLevel;
            }
            Growth.UnlockForests(_catalog, _growth);
            Growth.RestoreSkewerLevels(_catalog, _growth, preserveStarterShrimp);
            _hasSelection = data.HasSelection;
            _selectedRecipe = data.SelectedRecipe;
            _jobs.Clear();
            foreach (CookJobSaveData job in data.Jobs) _jobs.Add(new CookJobSaveData { Recipe = job.Recipe, Elapsed = job.Elapsed, Duration = job.Duration });
            _hasFoodSelection = data.HasCarriedFood;
            _selectedFood = data.CarriedFood;
            _insertedCount = data.InsertedCount;
            _wasRunning = data.WasRunning;
            _statisticsDay = data.StatisticsDay;
            _nextOrderSequence = data.NextOrderSequence;
            ServedCount = data.ServedCount;
            WrongFoodCount = data.WrongFoodCount;
            MissedCount = data.MissedCount;
            _selectedOrder = !string.IsNullOrEmpty(data.SelectedOrderId) ? tables[data.SelectedOrderId] : null;
            _selectedFoodOrder = !string.IsNullOrEmpty(data.SelectedFoodOrderId) ? tables[data.SelectedFoodOrderId] : null;
            _orders.Clear();
            _occupiedTables.Clear();
            foreach (string id in data.OrderTableIds) _orders.Add(tables[id]);
            foreach (string id in data.OccupiedTableIds) _occupiedTables.Add(tables[id]);
            if (upgradeContent)
            {
                foreach (TableOrder table in _orders)
                {
                    if (GetUnassignedFood(table.Recipe) > 0) table.SetPrepared(true);
                    if (_selectedOrder == null && data.HasSelection && table.Recipe == data.SelectedRecipe && !table.HasPreparedFood)
                    {
                        _selectedOrder = table;
                        for (int i = 0; i < data.InsertedCount; i++) table.InsertOne();
                    }
                    if (_selectedFoodOrder == null && data.HasFoodSelection && table.Recipe == data.SelectedFood && table.HasPreparedFood) _selectedFoodOrder = table;
                }
            }
            if (_hasSelection && _selectedOrder != null)
                _insertedCount = _selectedOrder.HasPreparedFood ? 0 : _selectedOrder.InsertedCount;
            _selectedOrder = null;
            if (_catalog != null)
                _insertedCount = Mathf.Clamp(_insertedCount, 0, _catalog.Recipes[(int)_selectedRecipe].TotalCount);

            foreach (TableOrder place in _occupiedTables)
                _nextOrderSequence = Mathf.Max(_nextOrderSequence, place.AdmissionSequence);
            foreach (TableOrder place in _occupiedTables)
                if (place.AdmissionSequence <= 0) place.SetAdmissionSequence(NextAdmissionSequence());
            _occupiedTables.Sort((left, right) => left.AdmissionSequence.CompareTo(right.AdmissionSequence));
            foreach (TableOrder order in _orders.ToArray())
            {
                if (!order.HasArrived || (order.IsTakeaway && !IsFirstTakeaway(order)))
                {
                    ReturnPreparedFood(order);
                    _orders.Remove(order);
                    order.DeferOrder();
                    Debug.Log($"[Save] frame={Time.frameCount} event=customer_visit_restore result=deferred target={order.DisplayName}", this);
                }
            }
            for (int market = 0; market < 1; market++) RefreshTakeawayLine(market, true);
            _selectedFoodOrder = null;
            foreach (TableOrder table in _knownTables)
                if (table != null && !table.HasOrder) ReturnPreparedFood(table);
            AssignStoredFood();
            Version++;
        }

        internal static bool AreMenuSalesValid(BusinessSaveData business, int servedCount)
        {
            int[] counts = business.DailyMenuCounts;
            long[] proceeds = business.DailyMenuGold;
            if (counts == null && proceeds == null) return true;
            if (counts == null || proceeds == null || counts.Length != BusinessSaveData.MenuCount ||
                proceeds.Length != BusinessSaveData.MenuCount) return false;

            int remainingCount = servedCount;
            long remainingGold = business.DailyGold;
            for (int i = 0; i < BusinessSaveData.MenuCount; i++)
            {
                if (counts[i] < 0 || counts[i] > remainingCount || proceeds[i] < 0 || proceeds[i] > remainingGold ||
                    (counts[i] == 0 && proceeds[i] != 0)) return false;
                remainingCount -= counts[i];
                remainingGold -= proceeds[i];
            }
            return true;
        }

        private void SetMessage(string message)
        {
            LastMessage = message;
            Version++;
            Debug.Log($"[Gameplay] frame={Time.frameCount} event=cooking result=changed message={message}", this);
        }
    }
}
