using System.Collections;
using System.Collections.Generic;
using HamPocha.Audio;
using HamPocha.Core;
using HamPocha.Data;
using HamPocha.Input;
using HamPocha.UI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HamPocha.Gameplay
{
    public sealed class PochaKitchen : MonoBehaviour
    {
        public const string InteriorPath = "Assets/01_Scenes/PochaInterior.unity";
        public const int DepositSlot = HamPocha.Gameplay.Inventory.ItemKeyCount;
        private const int SkewerKeyStart = DepositSlot + 1;
        private static readonly ProfilerMarker ViewMarker = new ProfilerMarker("Pocha.SwitchView");
        public static PochaKitchen Current { get; private set; }

        public KitchenStock Stock { get; private set; }
        public bool IsInside { get; private set; }
        public bool HasWheel { get { return _wheel != Wheel.None; } }
        public bool IsOpen { get { return _session.CurrentActivity == PlayerActivity.Business; } }
        public bool OutdoorUnlocked { get { return Sales.Progress.Markets[1]; } }
        public CookingStation Sales { get; private set; }
        public Inventory Inventory { get; private set; }
        public int Hand { get { return Stock.State.Hand; } }
        public PlayerMotor InsideMotor { get; private set; }
        public Rigidbody InsideBody { get; private set; }
        public bool CanDrawHud => _loader.CurrentScene == SceneType.Village && _loader.HasActiveScene &&
            !_loader.IsTransitioning && !_loader.IsStartupLoading && !_loader.IsSessionBlocked && !IsIntro;

        public bool CanShow => CanDrawHud && !_loader.IsMenuPaused && !_loader.IsTravelPromptOpen && (!_loader.IsUiOpen || HasWheel);
        internal bool IsTutorialMenuPaused => _loader.IsTutorialMenuPaused;
        internal float OrderDuration(int phase) => phase == 2 ? _timing.AcceptWaitSeconds : _timing.FoodWaitSeconds;

        private const float Range = 1.20f;
        private const float MarketEntryRange = .75f;
        private const float ViewAreaMargin = .75f;
        private const float CounterAisleEdge = -.75f;
        private const float OrderFacingHalfAngle = 22.5f;
        private const double InsideTargetSwitchSeconds = .075d;
        private const float HouseYardBoundaryOffset = 3.15f;
        // 사진의 바닥 격자 기준으로 왼쪽 끝, 두 자리 경계, 오른쪽 끝을 지정한다.
        private static readonly float[] CounterSlotEdges = { -3.82f, -1.51f, .80f, 2.60f };
        // 기존 기본 폭이 두 칸이므로 그 1/4이 좌우에 추가할 반 칸이다. 끝자리는 원래 경계를 유지한다.
        private static readonly float CounterNeighborReach = (CounterSlotEdges[1] - CounterSlotEdges[0]) * .25f;
        private const float WheelSwitchPadding = .22f;
        private const float WheelSwitchMaxDegrees = 12f;
        private const float CookerHalfWidth = .80f;
        private const float CookerNeighborReach = .77f;
        private const float CookerAisleReach = .82f;
        private const float CookerFrontReach = .42f;
        private const float StorageRange = .90f;
        private const float WalkSeconds = 1.3f;
        private const float ReplacementArrivalSeconds = 5f;
        private const float VisitTimerTolerance = .0001f;
        private const float SavedWallTolerance = .25f;
        private const float GuestScaleMultiplier = 1.45f;
        private const float InteriorGuestScale = PlayerMotor.InteriorVisualScale * GuestScaleMultiplier;
        private const float GuestCameraDepth = 2.65f;
        private const float HeldVisualSize = 1.00f;
        private const float VillageGuestSpacing = 1.12f;
        private enum Wheel { None, Fridge, Food, Cook, Outdoor, Display }
        private enum Target { None, Market, Fridge, Food, Cook, Order, Outdoor, Exit, Display }
        private SceneLoadManager _loader;
        private PlayerWorldInteraction _interaction;
        private GameSessionState _session;
        private WorldClock _clock;
        private BusinessTiming _timing;
        private PlayerInputReader _input;
        private KitchenHud _hud;
        private Camera _villageCamera;
        private Camera _insideCamera;
        private float _insideSpawnHeight;
        private Transform _insideRoom;
        private Vector3 _insideSpawn;
        private BoxCollider _insideFloor;
        private float _insideMinX;
        private float _insideMinZ;
        private float _counterContactMinZ;
        private float _insideMaxZ;
        private Bounds _thirdCookerBounds;
        private bool _hasThirdCookerBounds;
        private AudioListener _villageAudio;
        private MarketInteractable _market;
        private BoxCollider[] _marketFootprints = System.Array.Empty<BoxCollider>();
        private Transform _fridge;
        private Transform _food;
        private BoxCollider _exitMat;
        private Transform _outdoorRoot;
        private Transform _counter;
        private float _displaySurface;
        private int _shownDisplayVersion = -1;
        private bool _shownDisplayUnlocked;
        private readonly GameObject[] _displayModels = new GameObject[OutdoorMenu.RecipeCount + 1];
        private readonly TableOrder[] _outdoorTables = new TableOrder[6];
        private readonly int[] _displayModelKeys = { -1, -1, -1, -1, -1, -1 };
        private Transform _displayApproach;
        private Transform _village;
        private readonly Transform[] _cook = new Transform[3];
        private readonly Vector3[] _cookApproaches = new Vector3[3];
        private readonly Transform[] _outdoor = new Transform[3];
        private readonly Transform[] _order = new Transform[3];
        private readonly Transform[] _guestPoints = new Transform[3];
        private readonly Transform[] _visitors = new Transform[3];
        private readonly Vector3[] _visitorBaseScales = new Vector3[3];
        private readonly PoseMotion[] _visitorMotions = new PoseMotion[3];
        private readonly int[] _visitorTickets = new int[3];
        private readonly VillageNpc[] _indoorResidents = new VillageNpc[(int)CustomerKind.Child];
        private readonly Vector3[] _guestRenderPositions = new Vector3[3];
        private bool _guestsMovedForRender;
        private Transform _insideHand, _outsideHand;
        private int _shownHand = -2;
        private GameObject _insideHeldVisual, _outsideHeldVisual;
        private readonly int[] _wheelItems = new int[7];
        private Wheel _wheel;
        private int _wheelCook;
        private int _wheelSize;
        private int _selected = -1;
        private bool _loading;
        private bool _enterWhenReady;
        private bool _interiorLoadFailed;
        private bool _restoreView;
        private Bounds _businessArea, _viewArea;
        private Transform _houseYard;
        private Transform MarketPoint => _market == null ? null : _market.InteractionPoint;
        internal Camera InteriorCamera => _insideCamera;
        internal RectTransform TutorialViewTarget => _hud?.TutorialViewTarget;
        internal RectTransform FirstDayViewControl => _hud?.FirstDayViewControl;
        internal bool IsLockNoticeOpen => _hud != null && _hud.IsLockNoticeOpen;
        internal RectTransform TutorialWheelTarget => HasSelectedTutorialWheelSlot
            ? _hud?.TutorialSelectedSlot(_selected) : _hud?.TutorialWheelTarget(FirstBusinessWheelItem());
        internal bool HasSelectedTutorialWheelSlot => HasWheel && _wheel != Wheel.Display &&
            _selected >= 0 && _selected < _wheelSize && _wheelItems[_selected] >= 0;
        internal bool IsFridgeWheel => _wheel == Wheel.Fridge;
        internal bool HasIndoorSlotWheel => _wheel is Wheel.Fridge or Wheel.Food or Wheel.Cook;
        internal Transform TutorialOutdoorCooker => _outdoor[0];
        internal Transform TutorialExit => _exitMat != null ? _exitMat.transform : null;
        internal RectTransform TutorialBusinessButton => _hud?.TutorialBusinessButton;
        internal RectTransform TutorialBusinessAction => _hud?.TutorialBusinessAction;
        internal RectTransform BusinessPanelCard => _hud?.BusinessPanelCard;
        internal RectTransform FirstDayWheelSlot(int item) => HasWheel ? _hud?.TutorialWheelTarget(item) : null;
        internal bool FirstDaySlotSelected(int item) => HasSelectedTutorialWheelSlot && _wheelItems[_selected] == item;
        internal Transform FirstDayOutdoorArea => _outdoorRoot;
        internal Transform[] FirstDayCookers => _cook;
        internal Camera FirstDayExteriorCamera => _villageCamera;
        internal RectTransform FirstDayRecipeCard => _hud?.FirstDayRecipeCard;
        internal RectTransform FirstDayOrderBadge(int slot) => _hud?.FirstDayOrderBadge(slot);
        internal Transform FirstDayIngredientSource => Stock.TutorialStoredItem() >= 0 ? _food : _fridge;
        internal int FirstDayPracticeCookSlot => Stock.TutorialSoupCookSlot();
        internal Transform FirstDayGuest(int slot) => slot >= 0 ? _visitors[slot] : null;
        internal bool AtFirstDayEntrance()
        {
            if (!CanShow || IsInside || HasWheel) return false;
            FindTarget();
            return !_ambiguous && _near == Target.Market;
        }
        internal int FirstDayOrderSlot(int phase)
        {
            for (int i = 0; i < Stock.State.Orders.Length; i++)
                if (Stock.State.Orders[i].Phase == phase && Stock.State.Orders[i].Menu == (int)RecipeId.CarrotSoup) return i;
            return -1;
        }
        internal bool IsBusinessPanelOpen => _hud != null && _hud.IsBusinessPanelOpen;
        internal bool CanConfirmBusinessPanel => !Sales.SummaryPending &&
            (IsOpen ? _session.CanFinishCurrentBusiness : _session.CanOpenAtCurrentTime);

        internal bool CanFinishFirstDaySales
        {
            get
            {
                if (!IsOpen || Stock == null) return false;
                if (_session.IsClosing) return false;
                for (int i = 0; i < Inventory.IngredientTypeCount; i++)
                    if (Inventory.GetStorageCount((IngredientType)i) > 0) return false;
                foreach (KitchenOrder order in Stock.State.Orders)
                    if (order.Phase is >= 1 and <= 3) return false;
                if (HamPocha.Gameplay.Inventory.IsFoodKey(Hand) || Stock.HasDisplayStock())
                    return false;
                foreach (int item in Stock.State.Food)
                    if (item is >= 0 and < BusinessSaveData.IndoorMenuCount) return false;
                foreach (KitchenCook cook in Stock.State.Inside)
                    if (cook.Started) return false;
                foreach (KitchenCook cook in Stock.State.Outside)
                    if (cook.Started) return false;
                foreach (OutdoorCook cook in Stock.State.SkewerCooks)
                    if (cook.Started) return false;
                return true;
            }
        }

        internal Transform TutorialFacility(int lesson)
        {
            switch (lesson)
            {
                case 1: return _fridge;
                case 2: return _food;
                case 3: return _cook[0];
                default: return null;
            }
        }
        private bool _interiorReady;
        private int _nearSlot;
        private Target _near;
        private bool _ambiguous;
        private (Target type, int slot) _pendingInsideTarget;
        private double _pendingInsideTargetSince;
        private readonly List<PlayerWorldInteraction.InteractionCandidate> _targets =
            new List<PlayerWorldInteraction.InteractionCandidate>(12);
        private readonly List<(Target type, int slot)> _targetUses = new List<(Target, int)>(12);
        private bool _wasOpen;
        private bool IsIntro
        {
            get
            {
                TutorialSaveData tutorial = Sales.Progress.Tutorial;
                return tutorial != null && tutorial.Enabled && tutorial.Step == 0;
            }
        }

        private bool NeedsFirstServe
        {
            get
            {
                TutorialSaveData tutorial = Sales.Progress.Tutorial;
                return tutorial != null && tutorial.Enabled && tutorial.Step >= 11 && tutorial.Step <= 16 && !tutorial.TakeawayServed;
            }
        }

        public void Initialize(SceneLoadManager loader)
        {

            _loader = loader;
            _interaction = loader.PlayerBody.GetComponent<PlayerWorldInteraction>();
            _session = FindObjectOfType<GameSessionState>();
            _clock = FindObjectOfType<WorldClock>();
            _timing = FindObjectOfType<BusinessPacing>().Current;
            _input = FindObjectOfType<PlayerInputReader>();
            Sales = FindObjectOfType<CookingStation>();
            Inventory = FindObjectOfType<Inventory>();
            Stock = new KitchenStock(Inventory, Sales);
            _villageCamera = loader.PlayerBody.GetComponentInChildren<Camera>(true);
            _villageAudio = _villageCamera.GetComponent<AudioListener>();
            _hud = gameObject.AddComponent<KitchenHud>();
            _hud.Initialize(this, FindObjectOfType<InventoryHud>(), _input);
            _loader.BeforeTransition += OnTravel;
            Current = this;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginGuestRendering;
            RenderPipelineManager.endCameraRendering += EndGuestRendering;
        }

        private void OnDisable()
        {
            ClearTarget();
            RenderPipelineManager.beginCameraRendering -= BeginGuestRendering;
            RenderPipelineManager.endCameraRendering -= EndGuestRendering;
            RestoreGuestRenderPositions();
        }

        private void OnDestroy()
        {
            if (_loader != null) _loader.BeforeTransition -= OnTravel;
            if (Current == this) Current = null;
        }

        private void OnTravel()
        {
            CloseWheel();
            CloseBusinessPanel();
            if (IsInside) SetView(false);
            foreach (Transform guest in _visitors)
                if (guest != null) guest.gameObject.SetActive(false);
            FinishEntryWait();
        }

        public KitchenSaveData Capture()
        {
            if (InsideBody != null)
            {
                Stock.State.HasInteriorPosition = true;
                Stock.State.InteriorPosition = _insideRoom.position + _insideRoom.InverseTransformPoint(InsideBody.position);
            }
            Stock.State.InteriorView = IsInside || _restoreView;
            return Stock.Capture();
        }

        public void Restore(KitchenSaveData data)
        {
            ClearTarget();
            Stock.Restore(data);
            _restoreView = Stock.State.InteriorView;
            RestoreInsidePosition();
        }

        private void LateUpdate()
        {
            if (_loader == null) return;
            if (!_loader.IsTransitioning && _loader.CurrentScene == SceneType.Village && _loader.CurrentRegion != null)
            {
                if (_village != _loader.CurrentRegion.transform) BindVillage(_loader.CurrentRegion.transform);
                if (!_loading && !_interiorReady && !_interiorLoadFailed && !_loader.IsStartupLoading && !IsIntro) StartCoroutine(LoadInterior());
                if (_restoreView && _interiorReady && CanShow)
                {
                    _restoreView = false;
                    SetView(true);
                }
            }
            if (HasWheel && _clock.IsApplicationActive && !CanShow && !IsTutorialMenuPaused) CloseWheel();
            if (HasWheel && !IsTutorialMenuPaused)
            {
                if (_wheel == Wheel.Fridge)
                {
                    for (int i = 0; i < HamPocha.Gameplay.Inventory.IngredientCount; i++)
                    {
                        int count = Inventory.GetStorageCount((IngredientType)i);
                        int item = count > 0 ? HamPocha.Gameplay.Inventory.IngredientKey((IngredientType)i) : -1;
                        if (_wheelItems[i] != item)
                        {
                            _wheelItems[i] = item;
                            _hud.UpdateWheelItem(i, item);
                        }
                        _hud.UpdateFridgeCount(i, count);
                    }
                }
                if (_wheel == Wheel.Display)
                {
                    for (int i = 0; i < _wheelSize; i++)
                    {
                        int item = DisplayItem(i);
                        if (_wheelItems[i] == item) continue;
                        _wheelItems[i] = item;
                        _hud.UpdateWheelItem(i, item);
                    }
                }
                ReadWheel();
            }
            RefreshHandVisual();
            if (_loader.CurrentScene != SceneType.Village || _loader.IsTransitioning) return;
            if (_clock.CanAdvanceTimers)
            {
                if (_wasOpen && !IsOpen) CloseOrders();
                _wasOpen = IsOpen;
                Stock.UpdateCooks(Time.deltaTime, _session.CanTakeNewOrders);
                UpdateOrders(Time.deltaTime);
            }
            RefreshDisplayModels();
            if (_interiorReady) UpdateVisitors();
        }

        private void BindVillage(Transform village)
        {
            _village = village;
            _houseYard = village.Find("PlayerHouse/YardPaving");
            _market = village.GetComponentInChildren<MarketInteractable>();
            _marketFootprints = _market != null
                ? _market.GetComponentsInChildren<BoxCollider>()
                : System.Array.Empty<BoxCollider>();
            System.Array.Clear(_indoorResidents, 0, _indoorResidents.Length);
            foreach (VillageNpc resident in village.GetComponentsInChildren<VillageNpc>(true))
                if (resident.IsCustomer) _indoorResidents[(int)resident.Kind] = resident;
            if (MarketPoint == null)
                Debug.LogWarning("[Scene] event=pocha_guest_view result=unavailable reason=missing_market_point", this);
            if (_market != null)
            {
                _viewArea = _market.InteractionBounds;
                if (MarketPoint != null) _viewArea.Encapsulate(MarketPoint.position);
                _viewArea.Expand(new Vector3(ViewAreaMargin * 2f, 0f, ViewAreaMargin * 2f));
                // 마을 영업 화면의 빨간 경계처럼 포차 앞마당의 빈 이동 공간도 포함한다.
                _businessArea = new Bounds(_market.transform.position + new Vector3(.2f, 0f, -2.4f),
                    new Vector3(19f, 0f, 17f));
                _businessArea.Encapsulate(_viewArea);
            }
            _outdoorRoot = village.Find("OutdoorKitchen");
            if (_outdoorRoot == null)
            {
                Debug.LogError("[Scene] event=outdoor_kitchen result=failed reason=missing_root", this);
                return;
            }
            for (int i = 0; i < 3; i++) _outdoor[i] = _outdoorRoot.Find("OutdoorCooker" + (i + 1));
            _counter = _outdoorRoot.Find("OutdoorDisplay");
            BindDisplayModels();
            _displayApproach = _counter == null ? null : _counter.Find("CustomerApproach");
            if (_market == null) return;
            if (_counter != null) AddBusinessPoint(_counter.position);
            foreach (Transform cooker in _outdoor)
                if (cooker != null) AddBusinessPoint(cooker.position);
            foreach (TableOrder table in village.GetComponentsInChildren<TableOrder>(true))
            {
                if (table.IsTakeaway || table.MarketIndex != 0) continue;
                AddBusinessPoint(table.transform.position);
                if (table.SlotIndex >= 0 && table.SlotIndex < _outdoorTables.Length)
                    _outdoorTables[table.SlotIndex] = table;
            }
        }

        private IEnumerator LoadInterior()
        {
            _loading = true;
            Scene scene = SceneManager.GetSceneByPath(InteriorPath);
            if (!scene.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(InteriorPath))
                {
                    LogAction("missing_scene");
                    FailInteriorLoad();
                    yield break;
                }
                yield return SceneManager.LoadSceneAsync(InteriorPath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(InteriorPath);
            }
            if (!scene.isLoaded || scene.rootCount != 1)
            {
                LogAction("invalid_scene");
                FailInteriorLoad();
                yield break;
            }
            Transform root = scene.GetRootGameObjects()[0].transform;
            _insideCamera = root.GetComponentInChildren<Camera>(true);
            _fridge = root.Find("Fridge");
            _food = root.Find("FoodStorage");
            _exitMat = root.Find("Decor/ExitMat")?.GetComponent<BoxCollider>();
            Transform player = root.Find("InteriorPlayer");
            _insideFloor = root.Find("KitchenFloor")?.GetComponent<BoxCollider>();
            BoxCollider boundary = root.Find("CounterBoundary")?.GetComponent<BoxCollider>();
            BoxCollider westWall = root.Find("WestWall")?.GetComponent<BoxCollider>();
            BoxCollider northWall = root.Find("NorthWall")?.GetComponent<BoxCollider>();
            for (int i = 0; i < 3; i++)
            {
                _cook[i] = root.Find("Cooker" + (i + 1));
                _order[i] = root.Find("Order" + (i + 1));
                _guestPoints[i] = root.Find("Order" + (i + 1) + "/GuestPoint");
            }
            if (_insideCamera == null || player == null || _fridge == null || _food == null || _exitMat == null || boundary == null ||
                westWall == null || northWall == null || _insideFloor == null ||
                System.Array.IndexOf(_cook, null) >= 0 || System.Array.IndexOf(_order, null) >= 0 ||
                System.Array.IndexOf(_guestPoints, null) >= 0)
            {
                LogAction("missing_interior_reference");
                FailInteriorLoad();
                yield break;
            }
            yield return null;
            _insideRoom = root;
            _insideSpawn = player.position;
            _insideSpawnHeight = player.position.y;
            InsideMotor = _loader.PlayerBody.GetComponent<PlayerMotor>().CreateInteriorMotor(player);
            InsideBody = player.GetComponent<Rigidbody>();
            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            Vector3 position = root.InverseTransformPoint(InsideBody.position);
            float contactOffset = Physics.defaultContactOffset;
            float radius = capsule.radius * Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z);
            float clearance = radius + contactOffset;
            _insideMinX = GetInteriorFace(westWall, Vector3.right).x + clearance;
            float counterFaceZ = GetInteriorFace(boundary, Vector3.forward).z;
            _insideMinZ = counterFaceZ + clearance;
            // 벽을 따라 움직일 때 캡슐의 접촉 보정으로 생기는 작은 위치 차이를 허용한다.
            _counterContactMinZ = Mathf.Max(counterFaceZ,
                counterFaceZ + radius - boundary.contactOffset - capsule.contactOffset);
            _insideMaxZ = GetInteriorFace(northWall, Vector3.back).z - clearance;
            for (int i = 0; i < _cook.Length; i++)
            {
                Transform cooker = _cook[i];
                BoxCollider footprint = cooker.GetComponent<BoxCollider>();
                if (footprint == null || !footprint.enabled)
                {
                    _cookApproaches[i] = cooker.position - root.right * .80f;
                    Debug.LogWarning($"[Scene] event=pocha_cooker_approach result=fallback reason=missing_collider slot={i + 1}", this);
                    continue;
                }

                Vector3 face = GetInteriorFace(footprint, Vector3.left);
                face.x -= clearance + .04f;
                _cookApproaches[i] = root.TransformPoint(face);
            }
            CacheThirdCookerBounds();
            RestoreInsidePosition();
            for (int i = 0; i < _visitors.Length; i++)
            {
                EnsureGuestVisual(i, GuestTicket(Stock.State.Orders[i], i));
                yield return null;
            }
            DayShadows shadows = root.GetComponent<DayShadows>();
            if (shadows != null) shadows.SetInteriorGuests(_visitors);
            _insideHand = InsideMotor.CreateCarryAnchor();
            _outsideHand = _loader.PlayerBody.GetComponent<PlayerMotor>().CreateCarryAnchor();
            _interiorReady = true;
            _loading = false;
            bool shouldEnter = _enterWhenReady;
            FinishEntryWait();
            if (shouldEnter && CanShow && CanSwitchView) SetView(true, playSound: true);
            Debug.Log($"[Scene] event=pocha_interior result=ready counter_contact_min_z={_counterContactMinZ:F3}", this);
        }

        private Vector3 GetInteriorFace(BoxCollider boundary, Vector3 normal)
        {
            Vector3 face = boundary.center + Vector3.Scale(boundary.size * .5f, normal);
            return _insideRoom.InverseTransformPoint(boundary.transform.TransformPoint(face));
        }

        private void RestoreInsidePosition()
        {
            if (InsideBody == null || !Stock.State.HasInteriorPosition) return;

            // 원래 포차 원점을 더한 실내 좌표를 계속 저장해 회전 전 저장도 그대로 복원한다.
            Vector3 saved = Stock.State.InteriorPosition - _insideRoom.position;
            if (!(saved.x >= _insideMinX - SavedWallTolerance && saved.x <= 2.21f &&
                saved.z >= -1.70f && saved.z <= _insideMaxZ + SavedWallTolerance)) return;

            // 실내 물리 바닥보다 아래에 저장되었더라도 외형이 바닥에 묻히지 않게 복원한다.
            float floorHeight = _insideSpawnHeight - _insideRoom.position.y;
            if (Mathf.Abs(saved.y - floorHeight) > .08f)
                Debug.Log("[Save] event=pocha_position_restore result=adjusted reason=floor_height", this);
            if (saved.z < _insideMinZ)
                Debug.Log("[Save] event=pocha_position_restore result=adjusted reason=serving_boundary", this);
            if (saved.x < _insideMinX || saved.z > _insideMaxZ)
                Debug.Log("[Save] event=pocha_position_restore result=adjusted reason=wall_boundary", this);
            saved.y = floorHeight;
            saved.x = Mathf.Max(saved.x, _insideMinX);
            saved.z = Mathf.Clamp(saved.z, _insideMinZ, _insideMaxZ);
            Vector3 position = _insideRoom.TransformPoint(saved);
            Vector3 clearPosition = SceneRegion.FindRoomRestorePosition(position, _insideSpawn, _insideFloor,
                InsideBody.GetComponent<CapsuleCollider>());
            if (clearPosition != position)
                Debug.LogWarning("[Save] event=pocha_position_restore result=adjusted reason=interior_layout_obstacle", this);
            InsideBody.position = clearPosition;
        }

        private void AddBusinessPoint(Vector3 point)
        {
            Bounds fixtureArea = new Bounds(point, new Vector3(ViewAreaMargin * 2f, 0f, ViewAreaMargin * 2f));
            _viewArea.Encapsulate(fixtureArea);
            _businessArea.Encapsulate(fixtureArea);
        }

        internal Vector3 ClampBusinessPosition(Vector3 position)
        {
            if (_market == null) return position;
            position.x = Mathf.Clamp(position.x, _businessArea.min.x, _businessArea.max.x);
            position.z = Mathf.Clamp(position.z, _businessArea.min.z, _businessArea.max.z);
            if (_houseYard != null)
            {
                Vector3 towardPocha = _houseYard.right;
                float distance = Vector3.Dot(position - _houseYard.position, towardPocha);
                if (distance < HouseYardBoundaryOffset)
                    position += towardPocha * (HouseYardBoundaryOffset - distance);
            }
            return position;
        }

        internal bool CanSwitchView
        {
            get
            {
                if (_market == null || _loader.PlayerBody == null || HasWheel) return false;
                if (IsInside) return true;
                Vector3 position = _loader.PlayerBody.position;
                position.y = _viewArea.center.y;
                return _viewArea.Contains(position);
            }
        }

        private bool IsNearMarketEntrance(Vector3 position, out float distanceSquared)
        {
            distanceSquared = float.PositiveInfinity;
            if (_market == null) return false;

            foreach (BoxCollider footprint in _marketFootprints)
            {
                if (footprint == null || !footprint.enabled || footprint.isTrigger ||
                    !footprint.gameObject.activeInHierarchy) continue;

                // 회전한 차체 발판의 모서리에서 수평 거리를 잰다.
                Transform frame = footprint.transform;
                Vector3 local = frame.InverseTransformPoint(position);
                local.y = footprint.center.y;
                Vector3 closest = new Bounds(footprint.center, footprint.size).ClosestPoint(local);
                Vector3 delta = frame.TransformPoint(closest) - position;
                delta.y = 0f;
                distanceSquared = Mathf.Min(distanceSquared, delta.sqrMagnitude);
            }

            if (float.IsPositiveInfinity(distanceSquared))
                distanceSquared = _market.GetInteractionDistanceSquared(position);

            return distanceSquared <= MarketEntryRange * MarketEntryRange;
        }

        public void ToggleView()
        {
            if (!IsOpen || Sales.SummaryPending || !CanShow || HasWheel) return;
            if (TutorialUi.Current?.BlocksFirstDayExterior == true) return;
            if (IsInside) { SetView(false, playSound: true); return; }
            if (!CanSwitchView)
            {
                LogAction("market_out_of_range");
                return;
            }
            Enter();
        }

        private void Enter()
        {
            if (TutorialUi.Current?.BlockPochaEntrance() == true) return;

            if (_interiorReady) SetView(true, playSound: true);
            else if (_interiorLoadFailed) LogAction("interior_load_failed");
            else if (!_enterWhenReady)
            {
                _enterWhenReady = true;
                _loader.SetUiOpen(this, true);
                _loader.ShowKitchenLoading(true);
                Debug.Log("[Scene] event=pocha_interior result=waiting", this);
            }
        }

        private void FailInteriorLoad()
        {
            _interiorLoadFailed = true;
            _loading = false;
            FinishEntryWait();
        }

        private void FinishEntryWait()
        {
            if (!_enterWhenReady) return;
            _enterWhenReady = false;
            _loader.SetUiOpen(this, false);
            _loader.ShowKitchenLoading(false);
        }

        private void SetView(bool inside, bool playSound = false)
        {
            bool changed = IsInside != inside;
            if (changed) ClearTarget();
            using (ViewMarker.Auto())
            {
                IsInside = inside;
                _villageCamera.enabled = !inside;
                if (_villageAudio != null) _villageAudio.enabled = !inside;
                _insideCamera.enabled = inside;
                AudioListener audio = _insideCamera.GetComponent<AudioListener>();
                if (audio != null) audio.enabled = inside;
                UpdateVisitors();
                _loader.RefreshKitchenControl();
                if (playSound && changed) Sfx.Play(SfxCue.ViewSwitch);
                Debug.Log($"[Scene] event=pocha_view result=switched inside={inside}", this);
            }
        }

        internal void OpenBusinessPanel()
        {
            if (!CanShow || HasWheel || Sales.SummaryPending || !_loader.CanUseWorldInteraction ||
                TutorialUi.Current?.BlocksFirstDayBusinessPanel == true) return;
            _loader.SetUiOpen(this, true);
            _hud.ShowBusinessPanel();
            Debug.Log("[UI] event=business_sales_panel result=opened", this);
        }

        internal void CloseBusinessPanel()
        {
            if (!IsBusinessPanelOpen) return;
            _hud.HideBusinessPanel();
            if (_loader != null) _loader.SetUiOpen(this, false);
            TutorialUi.Current?.OnIntroBusinessPanelUsed();
        }

        internal void ConfirmBusinessPanel()
        {
            if (_loader.IsMenuPaused || !IsBusinessPanelOpen || !CanConfirmBusinessPanel) return;
            CloseBusinessPanel();
            StartOrStop();
        }

        private void StartOrStop()
        {
            if (!CanShow || HasWheel || _market == null) return;
            bool open = IsOpen;
            if (open && !_session.IsClosing && TutorialUi.Current?.RejectFirstDayClosing() == true) return;
            if (!open && !_session.CanOpenAtCurrentTime)
            {
                LogAction("opening_time");
                return;
            }
            if (!open && TryShowUnstoredIngredients(out _)) return;
            if (!open && !HasOpeningStock())
            {
                NotifyLocked("재료가 부족해요. 냉장고에 재료를 보관해 주세요", "opening_ingredients_missing", missingIngredients: true);
                return;
            }
            bool success = open ? _loader.TryStopBusiness(out _) : _loader.TryStartBusiness(_market, out _);
            if (success)
            {
                if (open) CloseOrders();
                else
                {
                    _wasOpen = true;
                    _hud.ShowBusinessOpenNotice();
                }
            }
            LogAction(open ? "close" : "open", success);
        }

        public void AcknowledgeSummary() { Sales.ConfirmSummary(); }

        public void Discard()
        {
            if (!CanShow || HasWheel || Hand < 0) return;
            if (!Stock.TryDiscardHand())
            {
                NotifyLocked("첫 조리 실습에 필요한 마지막 당근이나 수프는 남겨주세요", "tutorial_last_carrot", missingIngredients: true);
                return;
            }
            Sfx.Play(SfxCue.MenuClose);
            PlayInteractionMotion();
            LogAction("discard", true);
        }

        public void ApplyControl(bool canMove, PlayerMotor outsideMotor, Rigidbody outsideBody)
        {
            SetMotion(outsideMotor, outsideBody, canMove && !IsInside && !HasWheel);
            if (InsideMotor != null) SetMotion(InsideMotor, InsideBody, canMove && IsInside && !HasWheel);
        }

        private static void SetMotion(PlayerMotor motor, Rigidbody body, bool move)
        {
            if (move)
            {
                body.isKinematic = false;
                motor.enabled = true;
            }
            else
            {
                motor.enabled = false;
                if (body.isKinematic) return;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        public bool TryInteract()
        {
            if (IsIntro || _loader.CurrentScene != SceneType.Village) return false;
            if (!CanShow) return true;
            if (Sales.SummaryPending)
            {
                Debug.Log("[Input] event=pocha_interact result=rejected reason=summary_pending", this);
                return true;
            }
            if (HasWheel) { ConfirmWheel(); return true; }
            FindTarget();
            if (TutorialUi.Current?.UsesFirstDayRounds == true &&
                !TutorialUi.Current.AllowsFirstDayKitchenInteraction(GetSelectedTarget())) return true;
            if (_ambiguous)
            {
                LogAction("ambiguous_targets");
                return true;
            }
            if (IsInside && _near == Target.Order)
            {
                float angle = GetFacingAngle(_guestPoints[_nearSlot].position,
                    InsideMotor.FacingPosition, InsideMotor.FacingDirection);
                Vector3 model = _insideRoom.InverseTransformPoint(InsideMotor.FacingPosition);
                Debug.Log($"[Input] event=pocha_order_target result=selected slot={_nearSlot + 1} angle={angle:F1} half_angle={OrderFacingHalfAngle:F1} model_x={model.x:F3} model_z={model.z:F3}", this);
            }
            else if (IsInside && _near == Target.None)
                LogAction("no_target_in_range_or_facing");

            switch (_near)
            {
                case Target.Market:
                    Enter();
                    break;
                case Target.Exit:
                    if (TutorialUi.Current?.BlocksFirstDayExterior == true) return true;
                    Debug.Log("[Input] event=pocha_exit result=accepted source=exit_mat", this);
                    SetView(false, playSound: true);
                    break;
                case Target.Fridge:
                    if (Hand == -1) OpenWheel(Wheel.Fridge, 0);
                    else PlayInteractionAndLog(Stock.PutIngredient(), "fridge_return");
                    break;
                case Target.Food:
                    if (Hand == -1) OpenWheel(Wheel.Food, 0);
                    else PlayInteractionAndLog(Stock.StoreCarriedItem(), "shelf_store");
                    break;
                case Target.Cook:
                    InteractWithCooker(_nearSlot);
                    break;
                case Target.Outdoor:
                    if (!OutdoorUnlocked) NotifyLocked("성장 메뉴에서 야시장을 해금하세요", "outdoor_locked", facility: true);
                    else if (_nearSlot >= Growth.OutdoorCookers(Sales.Progress))
                        NotifyLocked("성장 메뉴에서 해당 화로를 해금하세요", "outdoor_cooker_locked", facility: true);
                    else InteractWithOutdoorCooker(_nearSlot);
                    break;
                case Target.Display:
                    if (!OutdoorUnlocked) NotifyLocked("성장 메뉴에서 야시장을 해금하세요", "outdoor_display_locked", facility: true);
                    else OpenWheel(Wheel.Display, 0);
                    break;
                case Target.Order:
                    InteractWithOrder(_nearSlot);
                    break;
                default: break;
            }
            return true;
        }

        private void InteractWithCooker(int slot)
        {
            KitchenCook cook = Stock.State.Inside[slot];
            if (cook.Ready)
                PlayInteractionAndLog(Stock.TakeCooked(slot), "food_pickup");
            else if (cook.Started) LogAction("cooking");
            else if (Stock.CanInsert(slot))
                PlayInteractionAndLog(Stock.Insert(slot), "ingredient_insert");
            else if (Stock.CanChangeMenu(slot)) OpenWheel(Wheel.Cook, slot);
            else LogAction("ingredient_not_required");
        }

        private void InteractWithOutdoorCooker(int slot)
        {
            if (Stock.State.Outside[slot].Started)
            {
                LogAction("cooking");
                return;
            }
            OutdoorCook cook = Stock.State.SkewerCooks[slot];
            if (!cook.Started)
            {
                OpenWheel(Wheel.Outdoor, slot);
                return;
            }
            QueueOutdoorRecipe(slot, cook.Selection - 1);
        }

        private void QueueOutdoorRecipe(int slot, int menu)
        {
            if (!_session.CanTakeNewOrders)
            {
                LogAction("outdoor_closed");
                return;
            }

            bool accepted = Stock.TryQueueSkewer(slot, menu, out string message, out bool missingIngredients);
            if (missingIngredients)
            {
                NotifyLocked(message, "outdoor_ingredients_missing", missingIngredients: true);
                return;
            }
            PlayInteractionAndLog(accepted, "outdoor_cooking_queue");
        }

        public bool HasOpeningStock() { return Stock.HasOrderStock(); }
        internal bool HasUnstoredIngredients => Stock != null && Stock.HasUnstoredIngredients();

        internal bool TryShowUnstoredIngredients(out string message)
        {
            message = null;
            if (!HasUnstoredIngredients) return false;
            message = "수집한 재료를 냉장고에 보관하세요!";
            NotifyLocked(message, "opening_ingredients_not_stored", title: "재료 보관");
            return true;
        }
        public bool UsesLegacyIntro { get { return IsIntro; } }

        public int GetFirstBusinessStep()
        {
            if (!IsInside || !IsOpen) return 11;

            KitchenOrder order = FirstAcceptedOrder();
            if (order == null) return 12;
            if (Hand == order.Menu + 5) return 16;

            KitchenCook cook = FirstOrderCook(order.Menu);
            if (cook == null) return 13;
            return cook.Ready ? 15 : 14;
        }

        // 번호는 확인한 영업 안내를 저장하는 비트 위치다. 새 문구는 배열 끝에 추가한다.
        internal static readonly string[] BusinessTips =
        {
            "표시된 포차로 돌아가세요",
            "포차 가까이에서 상호작용 버튼을 눌러 들어가세요",
            "‘일괄 보관’을 터치하거나 조이스틱으로 가리킨 뒤 상호작용을 누르세요",
            "강조된 재료를 골라 1개를 꺼내세요",
            "강조된 주문 메뉴를 고르세요",
            "슬롯을 터치하거나 조이스틱으로 가리킨 뒤 상호작용을 누르세요",
            "정오가 되면 화면 오른쪽의 영업 버튼에서 영업을 시작할 수 있어요. 개점은 하루 한 번이에요",
            "손에 든 물건을 냉장고나 보관대에 넣으세요",
            "냉장고 가까이에서 상호작용하고 ‘일괄 보관’을 고르세요",
            "완성 음식을 든 채 같은 메뉴 아이콘이 뜬 손님에게 가세요. 상호작용하면 서빙돼요",
            "그 재료가 필요한 조리대로 가서 상호작용으로 넣으세요",
            "다른 물건을 들고 있어요. 냉장고나 보관대에 먼저 넣으세요",
            "좌상단 주문을 보고 조리대 원형 메뉴에서 같은 음식을 고르세요",
            "완성된 조리대 가까이에서 상호작용으로 음식을 꺼내세요",
            "재료를 모두 넣으면 자동으로 조리돼요. 기다리는 동안 다른 손님의 주문도 받을 수 있어요. 조리가 끝나면 조리대에서 음식을 집으세요",
            "좌상단 주문에 표시된 재료를 확인하세요. 냉장고에서 한 개씩 꺼내 같은 조리대에 넣으세요",
            "물음표가 뜬 손님에게 다가가 25초 안에 주문을 받으세요. 수락하고 나면 50초 안에 서빙해야 해요",
            "냉장고에 재료를 일괄 보관해 다음 주문을 준비하세요",
            "실내 주문 포인트에 손님이 올 때까지 기다리세요",
        };

        public int FirstBusinessTipId(int guidedItem)
        {
            if (!NeedsFirstServe || Sales.SummaryPending) return -1;
            if (!IsInside) return 1;
            if (HasWheel)
            {
                if (guidedItem == DepositSlot)
                    return 2;
                if (_wheel == Wheel.Fridge && guidedItem >= 0)
                    return 3;
                if (_wheel == Wheel.Cook && guidedItem >= 0)
                    return 4;
                return 5;
            }

            if (!IsOpen)
            {
                if (HasOpeningStock() && !HasUnstoredIngredients) return 6;
                if (Hand >= 0) return 7;
                return 8;
            }

            KitchenOrder accepted = FirstAcceptedOrder();
            if (accepted != null)
            {
                if (Hand == accepted.Menu + 5) return 9;

                KitchenCook cook = FirstOrderCook(accepted.Menu);
                bool holdingIngredient = HamPocha.Gameplay.Inventory.IsIngredientKey(Hand);
                if (holdingIngredient && cook != null && !cook.Started)
                {
                    int ingredient = (int)HamPocha.Gameplay.Inventory.KeyIngredient(Hand);
                    foreach (RecipeIngredient item in Stock.Recipe(cook.Menu).Ingredients)
                        if ((int)item.Ingredient == ingredient && cook.Ingredients[ingredient] < item.Count)
                            return 10;
                }
                if (Hand >= 0) return 11;
                if (cook == null) return 12;
                if (cook.Ready) return 13;
                if (cook.Started) return 14;
                return 15;
            }

            foreach (KitchenOrder order in Stock.State.Orders)
                if (order.Phase == 2 && order.Remaining > 0) return 16;

            if (!HasOpeningStock()) return 17;
            return 18;
        }

        internal Transform BusinessTipTarget(int tip)
        {
            if (tip == 1) return MarketPoint;
            if (!IsInside || HasWheel || tip < 0) return null;
            if (tip == 8 || tip == 15 || tip == 17) return _fridge;
            if (tip == 7 || tip == 11) return HamPocha.Gameplay.Inventory.IsIngredientKey(Hand) ? _fridge : _food;
            if (tip == 9 || tip == 16)
            {
                int phase = tip == 9 ? 3 : 2;
                for (int i = 0; i < _order.Length; i++)
                    if (Stock.State.Orders[i].Phase == phase && (phase == 2 || Stock.State.Orders[i].Menu + 5 == Hand))
                        return _visitors[i] != null ? _visitors[i] : _guestPoints[i];
            }
            if (tip == 10 || tip == 12 || tip == 13)
            {
                KitchenOrder order = FirstAcceptedOrder();
                if (order == null) return null;
                KitchenCook cook = FirstOrderCook(order.Menu);
                int slot = cook != null ? System.Array.IndexOf(Stock.State.Inside, cook) : System.Array.FindIndex(Stock.State.Inside, item => item.Menu < 0);
                if (slot >= 0) return _cook[slot];
            }
            return null;
        }

        public int FirstBusinessWheelItem()
        {
            if (!NeedsFirstServe) return -1;
            if (_wheel == Wheel.Fridge && !IsOpen && (!HasOpeningStock() || HasUnstoredIngredients)) return DepositSlot;

            KitchenOrder order = FirstAcceptedOrder();
            if (order == null) return -1;
            if (_wheel == Wheel.Cook) return order.Menu + 5;
            if (_wheel != Wheel.Fridge) return -1;

            KitchenCook cook = FirstOrderCook(order.Menu);
            if (cook == null || cook.Started) return -1;
            foreach (RecipeIngredient item in Stock.Recipe(cook.Menu).Ingredients)
                if (cook.Ingredients[(int)item.Ingredient] < item.Count && Inventory.GetStorageCount(item.Ingredient) > 0)
                    return HamPocha.Gameplay.Inventory.IngredientKey(item.Ingredient);

            return -1;
        }

        private KitchenOrder FirstAcceptedOrder()
        {
            KitchenOrder first = null;
            foreach (KitchenOrder order in Stock.State.Orders)
            {
                if (order.Phase != 3 || order.Remaining <= 0) continue;
                if (Hand == order.Menu + 5) return order;
                if (first == null || order.Sequence < first.Sequence) first = order;
            }

            return first;
        }

        private KitchenCook FirstOrderCook(int menu)
        {
            KitchenCook first = null;
            foreach (KitchenCook cook in Stock.State.Inside)
            {
                if (cook.Menu != menu) continue;
                if (cook.Ready) return cook;
                if (first == null || (cook.Started && !first.Started)) first = cook;
            }

            return first;
        }

        internal bool TryGetBadgePoint(int index, out Vector3 screen)
        {
            screen = Vector3.zero;
            if (index >= 3 && index < 6)
            {
                KitchenOrder order = Stock.State.Orders[index - 3];
                return (order.Phase == 2 || order.Phase == 3) && TryGetGuestHead(index - 3, out screen);
            }

            Transform anchor;
            if (index < 3)
            {
                if (!IsInside && index >= Growth.OutdoorCookers(Sales.Progress)) return false;
                anchor = IsInside ? _cook[index] : _outdoor[index];
            }
            else if (index == 6) anchor = IsInside ? _fridge : OutdoorUnlocked ? _counter : null;
            else anchor = IsInside ? _food : null;

            if (anchor == null) return false;
            Camera camera = IsInside ? _insideCamera : _villageCamera;
            Vector3 point = anchor.position + Vector3.up * .92f;
            if (IsInside && index == 6) point += _insideRoom.forward * .35f;
            screen = camera.WorldToViewportPoint(point);
            return screen.z > 0 && screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1;
        }

        internal int EquipmentBadgeItem(int index)
        {
            if (index == 6 && IsInside)
                return Stock.State.LastFridgeIngredient > 0 ? InventoryKey(Stock.State.LastFridgeIngredient - 1) : -1;
            if (index == 7) return Stock.LatestStoredItem();
            if (index == 6)
            {
                int dish = Stock.LatestDisplayDish();
                if (dish < 0) return -1;
                if (dish < OutdoorMenu.RecipeCount) return SkewerKeyStart + dish;
                return dish - OutdoorMenu.RecipeCount + 5;
            }

            KitchenCook cook = IsInside ? Stock.State.Inside[index] : Stock.State.Outside[index];
            if (!IsInside && !cook.Started)
            {
                int selection = Stock.State.SkewerCooks[index].Selection;
                if (selection > 0) return SkewerKeyStart + selection - 1;
            }
            int recipe = cook.Menu >= 0 ? cook.Menu : cook.LastRecipe - 1;
            return recipe >= 0 ? recipe + 5 : -1;
        }

        internal int DisplayItem(int slot)
        {
            if (slot < OutdoorMenu.RecipeCount)
            {
                int index = OutdoorMenu.DisplayRecipeIndex(slot);
                return Stock.State.SkewerDisplay[index] > 0 ? SkewerKeyStart + index : -1;
            }
            if (slot != OutdoorMenu.RecipeCount) return -1;
            int legacy = Stock.FirstLegacyDisplay();
            return legacy < 0 ? -1 : legacy + 5;
        }

        internal bool WheelShowsDisplay => _wheel == Wheel.Display;

        internal bool IsSkewerItem(int key) => key >= SkewerKeyStart && key < SkewerKeyStart + OutdoorMenu.RecipeCount;

        internal Sprite SkewerIcon(int key) => Stock.Skewer(key - SkewerKeyStart)?.Icon;

        internal bool WheelItemLocked(int key)
        {
            if (_wheel == Wheel.Cook) return key < 0;
            return _wheel == Wheel.Outdoor && IsSkewerItem(key) && !Stock.IsSkewerUnlocked(key - SkewerKeyStart);
        }

        internal string WheelCaption(int key)
        {
            if (WheelItemLocked(key)) return "잠김";

            if (IsSkewerItem(key))
            {
                int index = key - SkewerKeyStart;
                OutdoorRecipe recipe = Stock.Skewer(index);
                if (recipe == null) return "메뉴 준비 중";
                if (_wheel == Wheel.Fridge)
                {
                    for (int i = 0; i < HamPocha.Gameplay.Inventory.IngredientCount; i++)
                    {
                        int count = Inventory.GetStorageCount((IngredientType)i);
                        int item = count > 0 ? HamPocha.Gameplay.Inventory.IngredientKey((IngredientType)i) : -1;
                        if (_wheelItems[i] != item)
                        {
                            _wheelItems[i] = item;
                            _hud.UpdateWheelItem(i, item);
                        }
                        _hud.UpdateFridgeCount(i, count);
                    }
                }
                if (_wheel == Wheel.Display) return $"{recipe.DisplayName}\n진열 {Stock.State.SkewerDisplay[index]}개";
                return recipe.DisplayName;
            }

            if (_wheel == Wheel.Display && HamPocha.Gameplay.Inventory.IsFoodKey(key))
                return $"이전 재고 · {Stock.Recipe(key - 5).DisplayName}\n진열 {Stock.State.Display[key - 5]}개";
            return null;
        }

        private static int InventoryKey(int index) => HamPocha.Gameplay.Inventory.IngredientKey((IngredientType)index);

        public bool TryGetGuestHead(int slot, out Vector3 screen)
        {
            screen = Vector3.zero;
            Transform guest = _visitors[slot];
            Camera camera = IsInside ? _insideCamera : _villageCamera;
            if (camera == null || guest == null || !guest.gameObject.activeInHierarchy) return false;

            float height = 1.65f * (IsInside ? GuestScaleMultiplier : _visitorBaseScales[slot].y);
            Vector3 point = guest.position + Vector3.up * height;
            if (IsInside) point += _insideRoom.forward * .44f;
            screen = camera.WorldToViewportPoint(point);
            return screen.z > 0 && screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1;
        }

        internal float GuestBadgeScale => IsInside || _insideCamera == null || _villageCamera == null
            ? 1f
            : _insideCamera.orthographicSize / (_villageCamera.orthographicSize * InteriorGuestScale);

        private void FindTarget()
        {
            _targets.Clear();
            _targetUses.Clear();
            Vector3 pos = IsInside ? InsideBody.position : _loader.PlayerBody.position;
            Vector3 facing = IsInside ? InsideMotor.FacingDirection : _interaction.FacingDirection;
            Vector3 facingPosition = IsInside ? InsideMotor.FacingPosition : pos;

            if (IsInside && IsOnExitMat(pos))
            {
                ClearTarget();
                _near = Target.Exit;
                return;
            }

            if (IsInside)
            {
                Consider(_fridge, pos, Target.Fridge, 0, StorageRange);
                Consider(_food, pos, Target.Food, 0, StorageRange);
                for (int i = 0; i < 3; i++)
                {
                    if (CanInteractWithCooker(i)) ConsiderCooker(i, pos, facingPosition, facing);
                    if (CanInteractWithOrder(i)) ConsiderOrder(i, pos, facingPosition, facing);
                }
            }
            else
            {
                for (int i = 0; i < 3; i++) Consider(_outdoor[i], pos, Target.Outdoor, i);
                Consider(_counter, pos, Target.Display, 0);
                if (MarketPoint != null && IsNearMarketEntrance(pos, out float marketDistanceSquared))
                    AddTarget(MarketPoint, Target.Market, 0, Mathf.Sqrt(marketDistanceSquared));
            }
            int chosen = IsInside ? ChooseFacingOrder(facingPosition, facing) : -1;
            if (chosen >= 0) _ambiguous = false;
            else chosen = _interaction.ChooseInteraction(_targets, pos, facing, out _ambiguous);
            if (IsInside) chosen = StabilizeInsideTarget(chosen);
            if (chosen >= 0) (_near, _nearSlot) = _targetUses[chosen];
            else _near = Target.None;
        }

        private int StabilizeInsideTarget(int chosen)
        {
            int current = _targetUses.IndexOf((_near, _nearSlot));
            if (current < 0 || current == chosen)
            {
                _pendingInsideTarget = default;
                return chosen;
            }

            // 거리나 각도 경합이 잠시 모호해져도 현재 대상이 유효하면 계속 사용할 수 있다.
            if (chosen < 0)
            {
                _pendingInsideTarget = default;
                _ambiguous = false;
                return current;
            }

            // 프레임 수가 아닌 실제 시간으로 연속해서 우선인 대상을 확인한다.
            double now = Time.unscaledTimeAsDouble;
            var next = _targetUses[chosen];
            if (_pendingInsideTarget != next)
            {
                _pendingInsideTarget = next;
                _pendingInsideTargetSince = now;
            }
            if (now - _pendingInsideTargetSince < InsideTargetSwitchSeconds) return current;

            _pendingInsideTarget = default;
            return chosen;
        }

        private void ClearTarget()
        {
            _near = Target.None;
            _ambiguous = false;
            _pendingInsideTarget = default;
        }

        private int ChooseFacingOrder(Vector3 player, Vector3 facing)
        {
            int chosen = -1;
            float bestAngle = float.PositiveInfinity;
            for (int i = 0; i < _targetUses.Count; i++)
            {
                if (_targetUses[i].type != Target.Order) continue;

                float angle = GetFacingAngle(_targets[i].Position, player, facing);
                if (angle > OrderFacingHalfAngle) continue;
                if (angle < bestAngle || chosen >= 0 && Mathf.Approximately(angle, bestAngle) &&
                    _targets[i].Distance < _targets[chosen].Distance)
                {
                    bestAngle = angle;
                    chosen = i;
                }
            }

            if (chosen < 0) return -1;

            // 설비를 더 정면으로 보는 경우 손님 후보가 조리대·보관대 선택을 빼앗지 않는다.
            for (int i = 0; i < _targetUses.Count; i++)
            {
                if (_targetUses[i].type == Target.Order) continue;

                float angle = GetFacingAngle(_targets[i].Position, player, facing);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    chosen = i;
                }
            }

            return chosen;
        }

        private static float GetFacingAngle(Vector3 target, Vector3 player, Vector3 facing)
        {
            Vector3 direction = target - player;
            direction.y = 0f;
            facing.y = 0f;

            return Vector3.Angle(facing, direction);
        }

        internal Transform GetOutlineTarget()
        {
            if (!CanShow || HasWheel) return null;
            FindTarget();
            return GetSelectedTarget();
        }

        private Transform GetSelectedTarget()
        {
            switch (_near)
            {
                case Target.Market: return _market.transform;
                case Target.Exit: return _exitMat.transform;
                case Target.Fridge: return _fridge;
                case Target.Food: return _food;
                case Target.Cook:
                    return CanInteractWithCooker(_nearSlot) ? _cook[_nearSlot] : null;
                case Target.Outdoor: return _outdoor[_nearSlot];
                case Target.Display: return _counter;
                case Target.Order: return _visitors[_nearSlot];
                default: return null;
            }
        }

        private bool IsOnExitMat(Vector3 position)
        {
            if (_exitMat == null || !_exitMat.enabled || !_exitMat.gameObject.activeInHierarchy) return false;

            Vector3 offset = _exitMat.transform.InverseTransformPoint(position) - _exitMat.center;
            Vector3 halfSize = _exitMat.size * .5f;

            return Mathf.Abs(offset.x) <= halfSize.x && Mathf.Abs(offset.z) <= halfSize.z;
        }

        private bool CanInteractWithOrder(int slot)
        {
            KitchenOrder order = Stock.State.Orders[slot];
            Transform counterPoint = _order[slot];
            if (!IsOpen || counterPoint == null || !counterPoint.gameObject.activeInHierarchy || order.Remaining <= 0f)
                return false;

            return order.Phase == 2 || order.Phase == 3 && HamPocha.Gameplay.Inventory.IsFoodKey(Hand);
        }

        private void ConsiderOrder(int slot, Vector3 player, Vector3 facingPosition, Vector3 facing)
        {
            Transform counterPoint = _order[slot];
            if (counterPoint == null || !counterPoint.gameObject.activeInHierarchy) return;

            // 후보 구역과 부채꼴은 모두 화면에 보이는 플레이어 모델 위치를 원점으로 판정한다.
            Vector3 floor = _insideRoom.InverseTransformPoint(facingPosition);
            float aisleEdge = CounterAisleEdge;
            if (slot == 2 && _cook[2] != null)
                aisleEdge = Mathf.Max(aisleEdge, _insideRoom.InverseTransformPoint(_cook[2].position).z);
            float leftEdge = CounterSlotEdges[slot];
            float rightEdge = CounterSlotEdges[slot + 1];
            float bodyZ = _insideRoom.InverseTransformPoint(player).z;
            if (bodyZ < _counterContactMinZ || floor.z > aisleEdge) return;

            // 가판대 건너의 대응 손님을 향하도록 실제 손님 위치를 조준점으로 쓴다.
            Vector3 aim = _guestPoints[slot].position;
            bool inBaseRange = floor.x >= leftEdge && floor.x <= rightEdge;
            if (!inBaseRange)
            {
                // 기본 구역은 기존 선택에 남기고, 좌우로 넓힌 구역에서만 시선을 필수로 한다.
                if (slot > 0) leftEdge -= CounterNeighborReach;
                if (slot < 2) rightEdge += CounterNeighborReach;
                if (floor.x < leftEdge || floor.x > rightEdge) return;
                if (GetFacingAngle(aim, facingPosition, facing) > OrderFacingHalfAngle) return;
            }

            Vector3 delta = counterPoint.position - player;
            delta.y = 0f;
            AddTarget(counterPoint, Target.Order, slot, delta.magnitude, aim);
        }

        private bool CanInteractWithCooker(int slot)
        {
            Transform cooker = _cook[slot];
            if (cooker == null || !cooker.gameObject.activeInHierarchy) return false;

            KitchenCook cook = Stock.State.Inside[slot];
            if (cook.Ready) return Hand == -1;
            if (cook.Started) return false;

            return Stock.CanInsert(slot) || Stock.CanChangeMenu(slot);
        }

        private void GetCookerSideRange(int slot, float halfWidth, out float minAlong, out float maxAlong)
        {
            minAlong = -halfWidth;
            maxAlong = halfWidth;
            // 번호는 줄 안쪽부터 입구 쪽 순서다. 3번의 손님 쪽 경계는 확장하지 않는다.
            if (slot > 0 && !CanInteractWithCooker(slot - 1)) maxAlong += CookerNeighborReach;
            if (slot < 2 && !CanInteractWithCooker(slot + 1)) minAlong -= CookerNeighborReach;
        }

        private void CacheThirdCookerBounds()
        {
            _hasThirdCookerBounds = false;
            foreach (MeshFilter part in _cook[2].GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh == null || !part.TryGetComponent(out Renderer renderer) || !renderer.enabled) continue;

                AddThirdCookerBounds(part.sharedMesh.bounds, part.transform);
            }
            if (_hasThirdCookerBounds)
            {
                Debug.Log($"[Scene] event=pocha_cooker_facing result=ready slot=3 min_x={_thirdCookerBounds.min.x:F3} max_x={_thirdCookerBounds.max.x:F3} min_z={_thirdCookerBounds.min.z:F3} max_z={_thirdCookerBounds.max.z:F3}", this);
                return;
            }

            BoxCollider footprint = _cook[2].GetComponent<BoxCollider>();
            if (footprint != null) AddThirdCookerBounds(new Bounds(footprint.center, footprint.size), footprint.transform);
            Debug.LogWarning("[Scene] event=pocha_cooker_facing result=fallback reason=missing_model_mesh slot=3", this);
        }

        private void AddThirdCookerBounds(Bounds bounds, Transform frame)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        corner = _insideRoom.InverseTransformPoint(frame.TransformPoint(corner));
                        if (_hasThirdCookerBounds) _thirdCookerBounds.Encapsulate(corner);
                        else _thirdCookerBounds = new Bounds(corner, Vector3.zero);
                        _hasThirdCookerBounds = true;
                    }
                }
            }
        }

        private bool IsFacingThirdCooker(Vector3 player, Vector3 facing)
        {
            if (!_hasThirdCookerBounds) return false;

            Vector3 origin = _insideRoom.InverseTransformPoint(player);
            Vector3 direction = _insideRoom.InverseTransformDirection(facing);
            origin.y = _thirdCookerBounds.center.y;
            direction.y = 0f;
            if (direction.sqrMagnitude < .0001f) return false;

            return _thirdCookerBounds.IntersectRay(new Ray(origin, direction));
        }

        private void ConsiderCooker(int slot, Vector3 player, Vector3 facingPosition, Vector3 facing)
        {
            Transform cooker = _cook[slot];
            if (cooker == null || !cooker.gameObject.activeInHierarchy) return;
            if (slot == 2 && !IsFacingThirdCooker(facingPosition, facing)) return;

            Vector3 approach = _cookApproaches[slot];
            Vector3 delta = player - approach;
            delta.y = 0f;
            float alongCookers = Vector3.Dot(delta, _insideRoom.forward);
            float towardCooker = Vector3.Dot(delta, _insideRoom.right);
            float halfWidth = CookerHalfWidth;
            if (slot == 2)
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(_insideMinZ - _insideRoom.InverseTransformPoint(cooker.position).z));
            GetCookerSideRange(slot, halfWidth, out float minAlong, out float maxAlong);
            bool atFront = alongCookers >= minAlong && alongCookers <= maxAlong &&
                towardCooker >= -CookerAisleReach && towardCooker <= CookerFrontReach;
            bool extended = Mathf.Abs(alongCookers) > halfWidth;
            if (extended && slot < 2 &&
                (GetTargetDistanceSquared(_food, player) <= StorageRange * StorageRange ||
                 GetTargetDistanceSquared(_fridge, player) <= StorageRange * StorageRange))
                atFront = false;

            if (atFront)
            {
                AddTarget(cooker, Target.Cook, slot, delta.magnitude, cooker.position);
                return;
            }
            if (slot != 2) return;

            // 3번 아래 통로에서는 앞쪽 접근점을 지나 실제 조리대 남쪽 면에도 다가갈 수 있다.
            BoxCollider footprint = cooker.GetComponent<BoxCollider>();
            if (footprint == null || !footprint.enabled) return;

            Vector3 floor = _insideRoom.InverseTransformPoint(player);
            float southEdge = GetInteriorFace(footprint, Vector3.back).z;
            if (floor.z >= _counterContactMinZ && floor.z <= southEdge)
                Consider(cooker, player, Target.Cook, slot, StorageRange);
        }

        private void Consider(Transform target, Vector3 player, Target type, int slot, float range = Range)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;

            float distanceSquared = GetTargetDistanceSquared(target, player);
            if (distanceSquared <= range * range) AddTarget(target, type, slot, Mathf.Sqrt(distanceSquared));
        }

        private float GetTargetDistanceSquared(Transform target, Vector3 player)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return float.PositiveInfinity;

            Collider collider = target.GetComponent<Collider>();
            Vector3 reach = target.position;
            if (collider != null && collider.enabled)
                reach = collider.ClosestPoint(new Vector3(player.x, collider.bounds.center.y, player.z));
            Vector3 delta = player - reach;
            delta.y = 0;
            return delta.sqrMagnitude;
        }

        private void AddTarget(Transform target, Target type, int slot, float distance, Vector3? position = null)
        {
            _targets.Add(new PlayerWorldInteraction.InteractionCandidate
                { Target = target, Position = position ?? target.position, Distance = distance });
            _targetUses.Add((type, slot));
        }

        private void OpenWheel(Wheel wheel, int slot)
        {
            ClearTarget();
            _wheel = wheel;
            _wheelCook = slot;
            _selected = -1;
            _wheelSize = 7;
            if (wheel == Wheel.Fridge) _wheelSize = HamPocha.Gameplay.Inventory.IngredientCount + 1;
            else if (wheel == Wheel.Food) _wheelSize = Stock.State.Food.Length;
            else if (wheel == Wheel.Outdoor) _wheelSize = OutdoorMenu.RecipeCount;
            else if (wheel == Wheel.Display) _wheelSize = OutdoorMenu.RecipeCount + 1;
            for (int i = 0; i < _wheelSize; i++)
            {
                if (wheel == Wheel.Fridge) _wheelItems[i] = i == HamPocha.Gameplay.Inventory.IngredientCount ? DepositSlot :
                    Inventory.GetStorageCount((IngredientType)i) > 0 ? HamPocha.Gameplay.Inventory.IngredientKey((IngredientType)i) : -1;
                else if (wheel == Wheel.Food) _wheelItems[i] = Stock.StoredItem(i);
                else if (wheel == Wheel.Display) _wheelItems[i] = DisplayItem(i);
                else if (wheel == Wheel.Outdoor) _wheelItems[i] = SkewerKeyStart + OutdoorMenu.DisplayRecipeIndex(i);
                else _wheelItems[i] = Stock.IsUnlocked(i) ? i + 5 : -1;
            }
            _hud.OpenWheel(_wheelItems, _wheelSize);
            _loader.RefreshKitchenControl();
            PlayInteractionMotion();
            Debug.Log($"[Input] event=kitchen_wheel result=opened type={wheel}", this);
        }

        private void ReadWheel()
        {
            if (!CanUseWheelInput) return;

            Vector2 direction = _input.ReadKitchenDirection(out bool held);
            if (held && direction.sqrMagnitude >= .16f)
            {
                float angle = Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 360f);
                int index = WheelSlot(angle, _wheelSize, _selected);
                if (_selected != index)
                {
                    _selected = index;
                    _hud.Highlight(index);
                }
            }
        }

        private static int WheelSlot(float angle, int slotCount, int selected)
        {
            float slotDegrees = 360f / slotCount;
            if (selected >= 0 && selected < slotCount)
            {
                // 경계 부근에서 손이 조금 흔들려도 선택한 칸을 유지한다.
                float padding = Mathf.Min(WheelSwitchMaxDegrees, slotDegrees * WheelSwitchPadding);
                if (Mathf.Abs(Mathf.DeltaAngle(selected * slotDegrees, angle)) <= slotDegrees * .5f + padding)
                    return selected;
            }
            return Mathf.FloorToInt(angle / slotDegrees + .5f) % slotCount;
        }

        private void ConfirmWheel()
        {
            if (!CanUseWheelInput) return;

            ReadWheel();
            ConfirmSlot(_selected);
        }

        private bool CanUseWheelInput => HasWheel && CanShow && !Sales.SummaryPending &&
            _loader.CanUseKitchenWheelInput && TutorialUi.Current?.AllowsKitchenWheelInput != false;

        internal void TapWheelSlot(int index)
        {
            if (!CanUseWheelInput || index < 0 || index >= _wheelSize) return;
            bool confirm = _wheel != Wheel.Outdoor || _selected == index;
            _selected = index;
            _hud.Highlight(index);
            Debug.Log($"[Input] event=kitchen_wheel result=tapped slot={index} type={_wheel} confirm={confirm}", this);
            if (confirm) ConfirmSlot(index);
        }

        private void ConfirmSlot(int index)
        {
            Wheel wheel = _wheel;
            if (wheel == Wheel.Display) return;
            int item = index < 0 ? -1 : _wheelItems[index];
            if (TutorialUi.Current?.AllowsFirstDayWheelItem(item) == false)
            {
                Debug.Log($"[Input] event=kitchen_wheel result=rejected reason=tutorial_item slot={index} type={wheel}", this);
                return;
            }
            if (index >= 0 && WheelItemLocked(item))
            {
                LogAction(wheel == Wheel.Outdoor ? "skewer_locked" : "recipe_locked");
                return;
            }

            int cook = _wheelCook;
            if (wheel == Wheel.Outdoor && item >= SkewerKeyStart)
            {
                CloseWheel();
                QueueOutdoorRecipe(cook, item - SkewerKeyStart);
                return;
            }
            if (item < 0)
            {
                LogAction("empty_slot");
                return;
            }
            CloseWheel();
            if (wheel == Wheel.Fridge && item == DepositSlot) DepositBag();
            else if (wheel == Wheel.Fridge) PlayInteractionAndLog(Stock.TakeIngredient(item), "fridge_pickup");
            else if (wheel == Wheel.Food) PlayInteractionAndLog(Stock.TakeStoredItem(index), "shelf_pickup");
            else PlayInteractionAndLog(Stock.SelectMenu(cook, item - 5), "select_recipe");
        }

        public void CancelWheel() { CloseWheel(); }

        private void DepositBag()
        {
            int moved = Stock.DepositBag();
            if (moved > 0)
            {
                PlayInteractionMotion();
                TutorialUi.Current?.OnFirstDayDeposit();
            }
            bool remaining = HasUnstoredIngredients;
            LogAction(remaining ? "deposit_partial" : "deposit", moved > 0);
        }

        private void RefreshHandVisual()
        {
            if (_insideHand == null || Hand == _shownHand) return;
            _shownHand = Hand;
            InsideMotor.SetCarryPose(Hand >= 0);
            _loader.PlayerBody.GetComponent<PlayerMotor>().SetCarryPose(Hand >= 0);
            if (_insideHeldVisual != null)
            {
                _insideHeldVisual.SetActive(false);
                Destroy(_insideHeldVisual);
            }
            if (_outsideHeldVisual != null)
            {
                _outsideHeldVisual.SetActive(false);
                Destroy(_outsideHeldVisual);
            }
            if (Hand < 0) return;

            GameObject prefab;
            if (HamPocha.Gameplay.Inventory.IsFoodKey(Hand)) prefab = Stock.Recipe(Hand - 5).Visual;
            else
            {
                int ingredient = (int)HamPocha.Gameplay.Inventory.KeyIngredient(Hand);
                GameObject[] visuals = Sales.Catalog.IngredientVisuals;
                prefab = visuals != null && ingredient < visuals.Length ? visuals[ingredient] : null;
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[Scene] event=kitchen_hand_visual result=missing item={Hand}", this);
                return;
            }
            _insideHeldVisual = CreateHeldVisual(prefab, _insideHand);
            _outsideHeldVisual = CreateHeldVisual(prefab, _outsideHand);
        }

        internal static GameObject CreateHeldVisual(GameObject prefab, Transform anchor)
        {
            GameObject visual = Instantiate(prefab, anchor, false);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return visual;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float width = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (width > 0f) visual.transform.localScale *= HeldVisualSize / width;
            return visual;
        }

        private void CloseWheel()
        {
            if (!HasWheel) return;
            _wheel = Wheel.None;
            _hud.CloseWheel();
            _loader.RefreshKitchenControl();
        }

        private void UpdateOrders(float delta)
        {
            bool canTakeNewOrders = _session.CanTakeNewOrders;
            for (int slot = 0; slot < Stock.State.Orders.Length; slot++)
            {
                KitchenOrder order = Stock.State.Orders[slot];
                if (order.Phase == 0)
                {
                    if (order.ReplacementPending && canTakeNewOrders)
                    {
                        order.Remaining = Mathf.Max(0f, order.Remaining - delta);
                        if (order.Remaining <= VisitTimerTolerance && !TryStartVisit(slot))
                            order.Remaining = KitchenSaveData.VisitIntervalSeconds;
                    }
                    continue;
                }
                order.Remaining = Mathf.Max(0, order.Remaining - delta);
                if (order.Remaining > VisitTimerTolerance) continue;
                order.Remaining = 0f;
                if (order.Phase == 1)
                {
                    if (canTakeNewOrders)
                    {
                        order.Phase = 2;
                        order.Remaining = _timing.AcceptWaitSeconds;
                        Sfx.Play(SfxCue.MenuOpen);
                    }
                    else
                    {
                        order.Phase = 4;
                        order.Remaining = WalkSeconds;
                    }
                }
                else if (order.Phase == 4)
                {
                    order.Phase = 0;
                    order.ReplacementPending = canTakeNewOrders;
                    if (order.ReplacementPending)
                    {
                        // 손님이 완전히 나간 뒤 대기와 입장 이동을 합쳐 5초다.
                        order.Remaining = ReplacementArrivalSeconds - WalkSeconds;
                        Debug.Log($"[Gameplay] event=pocha_visit result=replacement_wait point={slot} arrival_seconds={ReplacementArrivalSeconds}", this);
                    }
                }
                else
                {
                    Leave(slot, order, _session.IsClosing ? OrderOutcome.BusinessClosed : OrderOutcome.Missed);
                    if (order.Menu == (int)RecipeId.CarrotSoup && TutorialUi.Current?.RequiresFirstDaySoup == true)
                    {
                        _hud?.ShowFirstOrderTimeout(Stock.HasReadyFood(order.Menu), _session.CanTakeNewOrders);
                    }
                }
            }
            if (!canTakeNewOrders) return;
            Stock.State.NextVisit = Mathf.Max(0, Stock.State.NextVisit - delta);
            if (Stock.State.NextVisit > 0) return;
            Stock.State.NextVisit = KitchenSaveData.VisitIntervalSeconds;
            int freeSlot = -1;
            int available = 0;
            for (int i = 0; i < 3; i++)
                if (Stock.State.Orders[i].Phase == 0 && !Stock.State.Orders[i].ReplacementPending &&
                    Random.Range(0, ++available) == 0) freeSlot = i;
            if (freeSlot >= 0) TryStartVisit(freeSlot);
        }

        private bool TryStartVisit(int slot)
        {
            int menu = -1;
            if (TutorialUi.Current?.RequiresFirstDaySoup == true)
            {
                foreach (KitchenOrder order in Stock.State.Orders)
                    if (order.Phase >= 1 && order.Phase <= 3) return false;
                if (Stock.CanOffer((int)RecipeId.CarrotSoup)) menu = (int)RecipeId.CarrotSoup;
            }
            else
            {
                int available = 0;
                for (int i = 0; i < 7; i++)
                    if (Stock.CanOffer(i) && Random.Range(0, ++available) == 0) menu = i;
            }
            if (menu < 0) return false;
            KitchenOrder next = Stock.State.Orders[slot];
            next.ResidentTicket = NextResidentTicket();
            next.Menu = menu;
            next.Gold = Sales.GetSaleGold((RecipeId)menu);
            next.Xp = Sales.GetSaleXp((RecipeId)menu);
            next.ExitOutcome = OrderOutcome.None;
            next.WarnedPhase = 0;
            next.Phase = 1;
            next.Remaining = WalkSeconds;
            next.ReplacementPending = false;
            Debug.Log($"[Gameplay] event=pocha_visit result=arriving point={slot} menu={menu} resident={(CustomerKind)(next.ResidentTicket - 1)}", this);
            return true;
        }

        private int NextResidentTicket()
        {
            for (int i = 0; i < _indoorResidents.Length; i++)
            {
                int ticket = Stock.State.NextIndoorResident + 1;
                Stock.State.NextIndoorResident = ticket % _indoorResidents.Length;
                bool isPresent = false;
                for (int slot = 0; slot < Stock.State.Orders.Length; slot++)
                {
                    KitchenOrder order = Stock.State.Orders[slot];
                    if (order.Phase != 0 && GuestTicket(order, slot) == ticket) isPresent = true;
                }
                if (!isPresent) return ticket;
            }
            return 1;
        }

        private static int GuestTicket(KitchenOrder order, int slot)
        {
            return order.ResidentTicket > 0 ? order.ResidentTicket : slot + 1;
        }

        private void EnsureGuestVisual(int slot, int ticket)
        {
            if (_visitors[slot] != null && _visitorTickets[slot] == ticket) return;
            if (_visitors[slot] != null)
            {
                _visitors[slot].gameObject.SetActive(false);
                Destroy(_visitors[slot].gameObject);
            }
            VillageNpc resident = _indoorResidents[ticket - 1];
            _visitors[slot] = resident != null
                ? resident.CreatePochaVisual(_guestPoints[slot])
                : _loader.PlayerBody.GetComponent<PlayerMotor>().CopyVisual(_guestPoints[slot], "CustomerVisual");
            _visitorBaseScales[slot] = resident != null ? resident.transform.lossyScale : _visitors[slot].localScale;
            _visitorMotions[slot] = PoseMotion.Attach(_visitors[slot], _clock);
            _visitors[slot].gameObject.SetActive(false);
            _visitorTickets[slot] = ticket;
            if (resident == null)
                Debug.LogWarning($"[Scene] event=pocha_guest_visual result=fallback reason=missing_resident point={slot} ticket={ticket}", this);
        }

        private void BeginGuestRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!IsInside || camera != _insideCamera || !camera.orthographic || _guestsMovedForRender) return;

            // 직교 카메라의 시선 방향으로만 옮기면 화면상의 발 위치는 그대로이고 차체에 가려지는 정도만 줄어든다.
            Vector3 offset = -camera.transform.forward * GuestCameraDepth;
            for (int i = 0; i < _visitors.Length; i++)
            {
                Transform guest = _visitors[i];
                if (guest == null) continue;
                _guestRenderPositions[i] = guest.position;
                if (!guest.gameObject.activeInHierarchy) continue;
                guest.position += offset;
                _guestsMovedForRender = true;
            }
        }

        private void EndGuestRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _insideCamera) RestoreGuestRenderPositions();
        }

        private void RestoreGuestRenderPositions()
        {
            if (!_guestsMovedForRender) return;

            for (int i = 0; i < _visitors.Length; i++)
            {
                if (_visitors[i] != null) _visitors[i].position = _guestRenderPositions[i];
            }
            _guestsMovedForRender = false;
        }

        private void UpdateVisitors()
        {
            for (int i = 0; i < 3; i++)
            {
                KitchenOrder order = Stock.State.Orders[i];
                Transform point = IsInside ? _guestPoints[i] : MarketPoint;
                bool present = order.Phase != 0 && point != null;
                if (present) EnsureGuestVisual(i, GuestTicket(order, i));
                Transform guest = _visitors[i];
                if (guest == null) continue;
                if (guest.gameObject.activeSelf != present) guest.gameObject.SetActive(present);
                if (!present) continue;
                bool leaving = order.Phase == 4;
                bool reacting = leaving && (order.ExitOutcome == OrderOutcome.Served ||
                    order.ExitOutcome == OrderOutcome.Missed || order.ExitOutcome == OrderOutcome.WrongFood);
                float distance = order.Phase == 1 ? order.Remaining / WalkSeconds : order.Phase == 4 ?
                    Mathf.Clamp01(1 - order.Remaining / WalkSeconds) : 0;
                distance = Mathf.Clamp01(distance);
                // 같은 모델과 자세를 카메라에 해당하는 가판대 앞 위치에 표시한다.
                Vector3 offset = IsInside
                    ? new Vector3(distance * .76f, 0f, -distance * .17f)
                    : new Vector3((i - 1) * VillageGuestSpacing + distance * .48f, 0f, -distance * .45f);
                float facing = leaving && distance > 0 ? 138 : order.Phase == 1 ? -42 : 0;
                guest.SetPositionAndRotation(point.position + point.rotation * offset,
                    point.rotation * Quaternion.Euler(0, facing, 0));
                guest.localScale = _visitorBaseScales[i] * (IsInside ? InteriorGuestScale : 1f);
                PoseMotion motion = _visitorMotions[i];
                if (motion != null)
                {
                    motion.SetLocomotion(order.Phase == 1 || (leaving && distance > 0f));
                    if (reacting)
                    {
                        float elapsed = WalkSeconds + PoseMotion.ReactionHoldSeconds - order.Remaining;
                        motion.ShowTimedReaction(order.ExitOutcome == OrderOutcome.Served ? PoseMotion.Happy : PoseMotion.Angry, elapsed);
                    }
                }
            }
        }

        private void InteractWithOrder(int slot)
        {
            KitchenOrder order = Stock.State.Orders[slot];
            if (!IsOpen || order.Remaining <= 0) return;
            if (order.Phase == 2)
            {
                order.Phase = 3;
                order.Remaining = _timing.FoodWaitSeconds;
                order.Sequence = ++Stock.State.Sequence;
                Sfx.Play(SfxCue.Confirm);
                PlayInteractionMotion();
                LogAction("order_accept", true);
            }
            else if (order.Phase == 3 && HamPocha.Gameplay.Inventory.IsFoodKey(Hand))
            {
                bool correct = Hand == order.Menu + 5;
                Stock.State.Hand = -1;
                PlayInteractionMotion();
                Leave(slot, order, correct ? OrderOutcome.Served : OrderOutcome.WrongFood);
                if (correct && NeedsFirstServe)
                {
                    Sales.Progress.Tutorial.TakeawayServed = true;
                    Debug.Log($"[Gameplay] frame={Time.frameCount} event=tutorial_first_serve result=completed menu={order.Menu}", this);
                }
                LogAction(correct ? "served" : "wrong_food", correct);
                if (!correct)
                {
                    TutorialUi.Current?.OnWrongDelivery();
                }
            }
        }

        private void CloseOrders()
        {
            for (int slot = 0; slot < Stock.State.Orders.Length; slot++)
            {
                KitchenOrder order = Stock.State.Orders[slot];
                if (order.ReplacementPending)
                {
                    order.ReplacementPending = false;
                    order.Remaining = 0f;
                }
                if (order.Phase == 1) { order.Phase = 4; order.Remaining = WalkSeconds; }
                else if (order.Phase == 2 || order.Phase == 3)
                {
                    Leave(slot, order, OrderOutcome.BusinessClosed);
                }
                else if (order.Phase == 4 && (order.ExitOutcome == OrderOutcome.Missed || order.ExitOutcome == OrderOutcome.WrongFood))
                {
                    order.ExitOutcome = OrderOutcome.BusinessClosed;
                    order.Remaining = Mathf.Min(order.Remaining, WalkSeconds);
                    _visitorMotions[slot]?.ShowTimedReaction(PoseMotion.Angry, float.PositiveInfinity);
                }
            }
            Stock.State.NextVisit = KitchenSaveData.VisitIntervalSeconds;
        }

        public void SettleBusinessClose()
        {
            // 자정에 선택창이 열려 있어도 결산을 가리지 않도록 먼저 닫는다.
            if (HasWheel)
            {
                CloseWheel();
                Debug.Log("[UI] event=kitchen_wheel result=closed reason=business_summary", this);
            }

            CloseOrders();
            Stock.CancelSkewerCooks();
        }

        internal bool HasGuestsAtClose
        {
            get
            {
                if (Stock == null) return false;
                foreach (KitchenOrder order in Stock.State.Orders)
                    if (order.Phase != 0) return true;
                foreach (TableOrder table in _outdoorTables)
                    if (table != null && table.Customer != null) return true;
                return false;
            }
        }

        internal void ReleaseArrivingOrders()
        {
            if (Stock == null) return;
            for (int slot = 0; slot < Stock.State.Orders.Length; slot++)
            {
                KitchenOrder order = Stock.State.Orders[slot];
                if (order.Phase == 1)
                {
                    order.Phase = 4;
                    order.Remaining = WalkSeconds;
                }
            }
        }

        private void Leave(int slot, KitchenOrder order, OrderOutcome outcome)
        {
            if (outcome != OrderOutcome.BusinessClosed && TryGetGuestHead(slot, out Vector3 head))
            {
                if (outcome == OrderOutcome.Served) _hud.ShowSale(head, order.Gold);
                else _hud.ShowUpset(head);
            }
            order.ExitOutcome = outcome;
            order.Phase = 4;
            order.Remaining = outcome == OrderOutcome.BusinessClosed ? WalkSeconds : WalkSeconds + PoseMotion.ReactionHoldSeconds;
            if (outcome == OrderOutcome.Served) Sfx.Play(SfxCue.Happy);
            Sales.RecordOrderOutcome(outcome, order.Gold, order.Xp, order.Menu);
            Debug.Log($"[Gameplay] event=pocha_order result={outcome} menu={order.Menu}", this);
        }

        private void BindDisplayModels()
        {
            for (int i = 0; i < _displayModels.Length; i++)
            {
                if (_displayModels[i] != null) Destroy(_displayModels[i]);
                _displayModels[i] = null;
                _displayModelKeys[i] = -1;
            }
            _shownDisplayVersion = -1;
            if (_counter == null) return;

            Transform visual = _counter.Find("DisplayVisual");
            Renderer[] renderers = visual == null ? System.Array.Empty<Renderer>() : visual.GetComponentsInChildren<Renderer>();
            _displaySurface = renderers.Length == 0 ? _counter.position.y + .5f : renderers[0].bounds.max.y;
            foreach (Renderer renderer in renderers)
                _displaySurface = Mathf.Max(_displaySurface, renderer.bounds.max.y);
        }

        private void RefreshDisplayModels()
        {
            if (_counter == null) return;
            bool unlocked = OutdoorUnlocked;
            if (_shownDisplayVersion == Stock.DisplayVersion && _shownDisplayUnlocked == unlocked) return;
            _shownDisplayVersion = Stock.DisplayVersion;
            _shownDisplayUnlocked = unlocked;
            for (int i = 0; i < _displayModels.Length; i++)
            {
                int key = unlocked ? DisplayItem(i) : -1;
                if (key < 0)
                {
                    if (_displayModels[i] != null) _displayModels[i].SetActive(false);
                    continue;
                }
                if (_displayModels[i] != null && _displayModelKeys[i] == key)
                {
                    _displayModels[i].SetActive(true);
                    continue;
                }
                if (_displayModels[i] != null)
                {
                    _displayModels[i].SetActive(false);
                    Destroy(_displayModels[i]);
                }

                GameObject prefab = IsSkewerItem(key) ? Stock.Skewer(key - SkewerKeyStart)?.Visual : Stock.Recipe(key - 5).Visual;
                _displayModelKeys[i] = key;
                _displayModels[i] = prefab == null ? null : AddDisplayModel(prefab, i);
            }
        }

        private GameObject AddDisplayModel(GameObject prefab, int slot)
        {
            GameObject model = Instantiate(prefab, _counter, false);
            model.name = prefab.name;
            model.transform.rotation = _counter.rotation;
            Vector3 parentScale = _counter.lossyScale;
            model.transform.localScale = Vector3.Scale(model.transform.localScale,
                new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z));
            Vector3 target = _counter.position + _counter.forward * ((slot - 2.5f) * .25f);
            target.y = _displaySurface;
            model.transform.position = target;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return model;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float width = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (width <= 0f) return model;
            float scale = .23f / width;
            model.transform.localScale *= scale;
            Vector3 baseCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            model.transform.position += (target - baseCenter) * scale;
            return model;
        }

        public Vector3 DisplayApproach
        {
            get { return _displayApproach == null ? _counter.position + new Vector3(-.85f, -.45f, 0) : _displayApproach.position; }
        }
        public bool HasDisplayFood
        {
            get
            {
                return _session.CanTakeNewOrders && OutdoorUnlocked && _counter != null && Stock.HasDisplayStock();
            }
        }

        internal bool TryGetOutdoorMeal(int slot, out Vector3 viewport, out float remaining)
        {
            viewport = default;
            remaining = 0;
            if (IsInside || _villageCamera == null) return false;
            VillageNpc customer = _outdoorTables[slot]?.Customer;
            if (customer == null || !customer.IsEatingOutdoor) return false;
            viewport = _villageCamera.WorldToViewportPoint(customer.transform.position + Vector3.up * 1.65f);
            remaining = customer.EatingRemaining / Mathf.Max(.01f, customer.EatingDuration);
            return viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
        }

        public bool TryBuyDisplay(VillageNpc buyer)
        {
            if (buyer == null || !buyer.CanBuyOutdoorFood || !HasDisplayFood ||
                !Stock.TryRemoveDisplayFood(out int skewer, out RecipeId legacyDish)) return false;

            buyer.MarkOutdoorPurchase(legacyDish, skewer);
            OutdoorRecipe recipe = Stock.Skewer(skewer);
            int gold = recipe == null ? Sales.GetSaleGold(legacyDish) :
                Growth.SkewerPrice(Sales.Catalog, recipe, Growth.SkewerLevel(Sales.Progress, skewer));
            int xp = recipe == null ? Sales.GetSaleXp(legacyDish) : recipe.SalesXp;
            int menu = recipe == null ? (int)legacyDish : BusinessSaveData.IndoorMenuCount + skewer;
            Sales.RecordOrderOutcome(OrderOutcome.Served, gold, xp, menu);
            Sfx.Play(SfxCue.Gold);
            Debug.Log($"[Gameplay] event=outdoor_sale result=paid skewer={skewer} legacy={legacyDish} gold={gold} xp={xp}", this);
            return true;
        }

        internal void ShowOutdoorMealHeart(VillageNpc customer)
        {
            if (IsInside || !CanShow || _villageCamera == null || _hud == null) return;
            Vector3 head = customer.transform.position + Vector3.up * 1.65f;
            _hud.ShowSale(_villageCamera.WorldToViewportPoint(head), 0);
        }

        private void PlayInteractionAndLog(bool success, string reason)
        {
            if (success) PlayInteractionMotion();
            LogAction(reason, success);
        }

        private void PlayInteractionMotion()
        {
            if (IsInside) InsideMotor.PlayInteractionMotion();
            else _interaction.PlayInteractionMotion();
        }

        private void NotifyLocked(string text, string reason, bool facility = false, bool missingIngredients = false,
            string title = null)
        {
            _hud.ShowLockedNotice(text, facility, missingIngredients, title);
            LogAction(reason);
        }

        private void LogAction(string reason, bool success = false)
        {
            Debug.Log($"[Gameplay] event=pocha_action result={(success ? "success" : "rejected")} reason={reason}", this);
        }
    }
}
