using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HamPocha.Audio;
using HamPocha.Core;
using HamPocha.Data;
using HamPocha.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace HamPocha.UI
{
    // 저장된 단계와 실제 게임 상태를 함께 확인한다. UI 클릭만으로 성공을 가정하지 않는다.
    [DefaultExecutionOrder(200)]
    public partial class TutorialUi : MonoBehaviour
    {
        private const int CheckSummaryTip = 20;
        private const float GuideBottom = 18f;
        private const float GuideMinHeight = 196f;
        private const float GuideTextPadding = 32f;
        private const int GuidePageLimit = 2;
        private const float GuideLeft = .215f;
        private const float GuideRight = .75f;
        private const float CursorSize = 91.2f;
        private const float WaypointCursorSize = CursorSize * 1.3f;
        private const float CursorNearHeight = 28.8f;
        private const float CursorFarHeight = 50.4f;
        private const float EdgeTravel = 18f;
        private const float EdgeHudBlend = 84f;
        private const float EdgeSideInset = .07f;
        private const float EdgeVerticalInset = .13f;
        private const float TouchShadeAlpha = .62f;
        private const float NoticeShadeAlpha = .08f;
        private const float OverviewSpeed = .85f;
        private const float OverviewReturnSeconds = .6f;
        private const float OverviewEdgeInset = .06f;
        private const float VillageOverviewScale = 2.7f;
        private const float VillagePanBow = 3.2f;
        private const int ForestEntryNotice = 1;
        private enum InterruptNotice { None, ForestExit, MarketTravel, WrongDelivery, SleepTooEarly, FirstDayClosing }
        private static readonly string FirstForestName = ForestEntryUi.ForestEntryTitle(SceneType.Forest01);
        private static readonly string ForestEntryCopy = $"{FirstForestName}에서 당근과 빵을 구할 수 있어요. 확인을 누른 뒤 입장할 수 있어요.";
        [SerializeField, Min(0.5f)] private float _panSeconds = 3f;
        [SerializeField, Min(1f)] private float _forestOverviewScale = 1.5f;
        [SerializeField, Min(1f)] private float _marketOverviewScale = 1.6f;
        private SceneLoadManager _loader;
        private WorldClock _clock;
        private GameSessionState _session;
        private CookingStation _station;
        private Inventory _inventory;
        private GameSave _save;
        private CookingUi _cookingUi;
        private ContentUi _contentUi;
        private HamPocha.Input.PlayerInputReader _input;
        private Camera _camera;
        private Canvas _canvas;
        private GameObject _root;
        private PanelMotion _guide;
        private CanvasGroup _guideGroup;
        private RectTransform _guideMascot;
        private string _guideCopy = "";
        private float _guideWidth;
        private readonly List<string> _guidePages = new List<string>();

        private int _guidePageIndex;
        internal static TutorialUi Current { get; private set; }
        private int _businessTip = -1;
        private InterruptNotice _interruptNotice;
        private bool _portalNotice;
        private TutorialInputMask _mask;
        private bool _restrictInput;
        private float _guideInputResumeAt;
        private Image _shade;
        private TutorialButtonFocus _buttonFocus;
        private TutorialButtonFocus _confirmFocus;
        private TutorialTouch _joystickTouch;
        private ForestEntryUi _entryUi;
        private ForestRegrowth _gatherRegion;
        private ForestPortal _exit;
        private Text _message;
        private TutorialGatherPanel _quest;
        private Button _next;
        private RectTransform _cursor;
        private RectTransform _hud;
        private Selectable[] _hudControls;
        private RectTransform _sprintControl;
        private readonly List<Rect> _markerObstacles = new List<Rect>();
        private readonly Vector3[] _uiCorners = new Vector3[4];
        private RectTransform _uiTarget;
        private Transform _worldTarget;
        private Transform _waypointVisual;
        private Vector3 _waypointLocalCenter;
        private Camera _waypointCamera;
        private MarketInteractable _market;
        private ForestPortal _forest;
        private ForestPortal _homeEntrance;
        private PlayerWorldInteraction _interaction;
        private int _shownStep = -1;
        private int _questVersion = -1;
        private SceneType _region;
        private bool _hasRegion;
        private bool _panning;
        private bool _forestPreviewShown;
        private bool _outdoorPreviewShown;
        private bool _openingReminder;
        private bool _lessonNotice;
        private Transform _exhibit;
        private Camera _exhibitCamera;
        private HarvestTarget _forestFocusTarget;
        private HomePlacementHud _homePlacement;
        private VillageSceneController _villageHud;
        private MinimapHud _minimap;
        private Coroutine _panRoutine;
        private Vector3 _cameraLocal;
        private Vector3 _panFrom;
        private Vector3 _panTo;
        private Vector3 _panBow;
        private float _cameraSize;
        private float _panSize;
        private float _panDuration;
        private float _panStarted;
        private BoxCollider _panGround;
        private TutorialSaveData State { get { return _station.Progress.Tutorial != null && _station.Progress.Tutorial.Enabled ? _station.Progress.Tutorial : null; } }
        internal bool RequiresForestEntryNotice
        {
            get
            {
                return isActiveAndEnabled && _root != null && State != null &&
                    (FirstDayRound > 0 ? FirstDayRound >= 16 && FirstDayRound < 20 :
                        State.Step == 7 && (State.ConfirmedPortalNotices & ForestEntryNotice) == 0);
            }
        }

        internal bool RequiresFirstForestTravel => _root != null && State != null &&
            (FirstDayRound > 0 ? FirstDayRound >= 16 && FirstDayRound <= 20 :
                State.Step == 7 && !State.VisitedFirstForest && (State.ConfirmedGuideSteps & (1 << 8)) == 0);
        internal bool IsIntroducingPocha => _root != null && State != null && (State.Step == 4 || State.Step == 5);
        internal bool IsPracticingHomePlacement => _root != null && State != null && State.Step == 0 &&
            (FirstDayRound > 0 ? FirstDayRound >= 2 && FirstDayRound <= 9 : State.HomeLesson == 2);
        internal TutorialSaveData HomePlacementPractice => IsPracticingHomePlacement ? State : null;
        internal bool IsHomePlacementNoticeOpen => _root != null && _root.activeInHierarchy && State != null &&
            State.Step == 0 && (FirstDayRound > 0 ? FirstDayRound <= 11 : State.HomeLesson >= 2 && State.HomeLesson <= 4) && _guide.IsOpen;
        internal bool HoldsHomePlacementInput => IsHomePlacementNoticeOpen ||
            (_root != null && _root.activeInHierarchy && FirstDayRound is >= 2 and <= 11);
        private bool GuideInputHandoff => Time.unscaledTime < _guideInputResumeAt;
        internal bool BlocksPanelDismissal => _root != null && _root.activeInHierarchy &&
            (GuideInputHandoff || (_guide.IsOpen && _next.gameObject.activeSelf) ||
                (FirstDayRound > 0 && _mask.isActiveAndEnabled && _mask.ExclusiveUi));
        internal bool BlocksFloatingJoystick => _mask != null && _mask.isActiveAndEnabled &&
            (_mask.BlockAll || (_mask.ExclusiveUi && _mask.AllowedUi != _input.TutorialJoystick && _mask.AllowedJoystick == null));
        internal Transform NavigationTarget => _root != null && _root.activeInHierarchy && (!_panning || GuidesFirstDayForestDuringPan) &&
            (!_guide.IsOpen || GuidesFirstDayWorldDuringNotice) && _uiTarget == null ? _worldTarget : null;
        internal string NavigationMarker
        {
            get
            {
                Transform target = NavigationTarget;
                if (target == null) return null;
                if (target == _market?.InteractionPoint) return "MainMarket";
                if (_forest != null && target == _forest.transform) return _forest.name;
                if (_homeEntrance != null && target == _homeEntrance.transform) return "PlayerHouse";
                if (_exit != null && target == _exit.transform) return _exit.name;
                return null;
            }
        }

        internal void OnIntroBusinessPanelUsed()
        {
            if (!IsIntroducingPocha || State.PochaLesson != 5) return;
            State.PochaLesson = 6;
            SaveProgress();
            Debug.Log("[Gameplay] event=tutorial_business_panel result=demonstrated", this);
        }

        internal void OnMinimapToggled(bool collapsed)
        {
            if (_root == null || State == null || State.Step != 9) return;
            if (State.TutorialMapPractice == 0 && collapsed) State.TutorialMapPractice = 1;
            else if (State.TutorialMapPractice == 1 && !collapsed) State.TutorialMapPractice = 2;
            else return;
            SaveProgress();
            Debug.Log($"[UI] event=tutorial_map_practice result=changed stage={State.TutorialMapPractice}", this);
        }

        private IEnumerator Start()
        {
            Current = this;
            _loader = FindObjectOfType<SceneLoadManager>();
            _clock = FindObjectOfType<WorldClock>();
            _session = FindObjectOfType<GameSessionState>();
            _station = FindObjectOfType<CookingStation>();
            _inventory = FindObjectOfType<Inventory>();
            _cookingUi = FindObjectOfType<CookingUi>();
            _contentUi = FindObjectOfType<ContentUi>();
            _input = FindObjectOfType<HamPocha.Input.PlayerInputReader>();
            _entryUi = FindObjectOfType<ForestEntryUi>();
            _villageHud = FindObjectOfType<VillageSceneController>();
            _camera = Camera.main;
            _interaction = FindObjectOfType<PlayerWorldInteraction>();
            if (_loader == null || _clock == null || _session == null || _station == null || _inventory == null ||
                _cookingUi == null || _contentUi == null || _camera == null || _input == null || _entryUi == null || _interaction == null)
            {
                Debug.LogError("[UI] event=tutorial_initialize result=failed reason=missing_reference", this);
                enabled = false;
                yield break;
            }

            // Main의 UI 생성과 계정 복원이 끝난 뒤 저장된 단계에 연결한다.
            yield return null;
            _save = FindObjectOfType<GameSave>();
            while (_save != null && !_save.IsReady) yield return null;
            if (State == null || State.Step == 21) yield break;

            _canvas = _cookingUi.GetComponentInParent<Canvas>();
            if (_canvas == null || _cookingUi.TutorialFont == null)
            {
                Debug.LogError("[UI] event=tutorial_initialize result=failed reason=missing_canvas_or_font", this);
                enabled = false;
                yield break;
            }
            BuildUi();
            _hud = HudLayout.On(_canvas);
            _hudControls = _hud.GetComponentsInChildren<Selectable>(true);
            _sprintControl = _input.TutorialInteract.parent.Find("SprintButton") as RectTransform;
            _root.SetActive(false);
            WarmText();
            // 계정 복원 뒤 로딩 화면이 남아 있을 때 글꼴을 준비하고 실제 안내는 준비 완료 후 시작한다.
            yield return null;
            while (_loader.IsStartupLoading || !_loader.HasActiveScene || _loader.IsTransitioning) yield return null;
            _joystickTouch = _input.TutorialJoystick.GetComponent<TutorialTouch>();
            if (_joystickTouch == null) _joystickTouch = _input.TutorialJoystick.gameObject.AddComponent<TutorialTouch>();
            _joystickTouch.Pressed += OnJoystickPressed;
            Debug.Log($"[Gameplay] event=tutorial_initialize result=ready step={State.Step}", this);
        }

        private void WarmText()
        {
            UiTextCache.Warm(_cookingUi.TutorialFont, _root.transform,
                HomeTip + VillageTip + MarketRoute + ForestRoute + ForestCollect + CollectAll +
                ForestReturn + FirstSale + GrowthMenu + UpgradeTip + OutdoorTip + UnlockTip + FinishTip + HelpTip + FirstDaySprintTip +
                string.Join(" ", PochaKitchen.BusinessTips) +
                ForestEntryCopy + "확인 모든 재료 모으기 나가기 버튼을 눌러 마을로 돌아가세요. 표시된 출구로 이동해 마을로 돌아가세요. " +
                Inventory.GetIngredientName(IngredientType.Carrot) + Inventory.GetIngredientName(IngredientType.Bread));
        }

        private const string HomeTip = "이곳은 햄스터의 집이에요. 가구와 소품을 원하는 위치에 배치해 꾸밀 수 있어요.";
        private const string VillageTip = "여기가 마을이에요. 포차 위치를 둘러볼게요.";
        private const string MarketRoute = "표시된 포차로 이동하세요. 포차 가까이에서 상호작용하면 안으로 들어갈 수 있어요.";
        private const string ForestRoute = "확인 후 표시된 숲 입구로 이동하세요.";
        private const string ForestCollect = "미니맵에는 출입구와 재료, 몬스터 위치가 보여요. 지도를 터치하면 접히고 버튼으로 다시 펼 수 있어요. 밤에 숲에 들어오면 몬스터가 난폭해져요.";
        private const string CollectAll = "당근에 다가가 상호작용으로 채집하세요. 크루상을 공격하면 빵을 얻을 수 있어요. 미니맵 아래의 목표 수량을 모두 채워 보세요.";
        private const string ForestReturn = "재료를 모두 모았어요! 표시된 출구로 이동한 뒤 포차로 돌아가세요.";
        private const string FirstSale = "첫 실내 서빙을 마쳤어요! 주문한 음식을 건네면 골드와 판매 경험치를 받아요.";
        private const string GrowthMenu = "성장·해금 메뉴를 열어 당근 수프를 강화해보세요.";
        private const string UpgradeTip = "당근 수프의 첫 강화(레벨 1 → 2)는 무료예요.";
        private const string OutdoorTip = "야시장은 햄스터 레벨 5와 5,000골드로 해금할 수 있어요. 야외 시점으로 전환해 둘러볼게요.";
        private const string UnlockTip = "세 번째 음식은 당근 수프와 바삭 토스트가 각각 레벨 5여야 해요. 그다음부터는 직전 음식 레벨 10이 필요해요. 해금 비용은 성장 메뉴에서 확인하세요. 재료 숲도 함께 열려요.";
        private const string GrowthScrollTip = "성장 목록을 아래로 내려 야시장 항목을 찾아보세요.";
        private const string HomeAtMidnightTip = "자정이 됐어요. 집으로 돌아가 잠을 자 보세요.";
        private const string BedRecoveryTip = "이제 잠을 자서 피로를 회복할 수 있어요.";
        private const string FinishTip = "튜토리얼을 완료했어요! 보상으로 100골드를 받았어요.";
        private const string HelpTip = "지금까지 튜토리얼로 알아본 게임 조작법은 / 메뉴 버튼을 눌러서 도움말에서 확인할 수 있어요";
        private static readonly string[] FacilityTips =
        {
            "오른쪽 영업 버튼으로 오늘의 판매량을 확인할 수 있어요.",
            "이곳은 냉장고예요. 채집한 재료를 일괄 보관하고 요리에 쓸 재료를 한 개씩 꺼낼 수 있어요.",
            "이곳은 임시 보관대예요. 음식이나 들고 있는 재료를 한 칸에 하나씩, 총 5칸까지 보관할 수 있어요.",
            "이곳은 조리대예요. 레시피에 필요한 재료를 모두 넣으면 자동으로 조리가 시작돼요.",
            "오른쪽 영업 버튼을 눌러 햄포차 패널을 열어 보세요."
        };

        private RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Text text = CreateRect(name, parent, anchor, pos, size).gameObject.AddComponent<Text>();
            text.font = UiSkin.FontFor(_cookingUi.TutorialFont);
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private void BuildUi()
        {
            RectTransform root = CreateRect("TutorialHud", _canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            _root = root.gameObject;
            Canvas overlay = root.gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 3100;
            root.gameObject.AddComponent<GraphicRaycaster>();
            RectTransform shield = CreateRect("InputMask", root, Vector2.zero, Vector2.zero, Vector2.zero);
            shield.anchorMax = Vector2.one;
            shield.offsetMin = shield.offsetMax = Vector2.zero;
            Image inputSurface = shield.gameObject.AddComponent<Image>();
            inputSurface.color = Color.clear;
            inputSurface.canvasRenderer.cullTransparentMesh = false;
            _mask = shield.gameObject.AddComponent<TutorialInputMask>();
            if (Application.isEditor || Debug.isDebugBuild)
                _mask.ContentTestButton = _canvas.transform.Find("HudSafeArea/ContentTestButton") as RectTransform;
            _mask.WorldCamera = _camera;
            RectTransform shade = CreateRect("FocusShade", root, Vector2.zero, Vector2.zero, Vector2.zero);
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            _shade = shade.gameObject.AddComponent<Image>();
            _shade.raycastTarget = false;
            _shade.gameObject.SetActive(false);
            RectTransform buttonFocus = CreateRect("ButtonFocus", root, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            _buttonFocus = new TutorialButtonFocus(buttonFocus);
            _confirmFocus = new TutorialButtonFocus(buttonFocus);
            RectTransform box = CreateRect("Guide", root, new Vector2(GuideLeft, 0), Vector2.up * GuideBottom, new Vector2(0, GuideMinHeight));
            box.anchorMax = new Vector2(GuideRight, 0);
            box.pivot = new Vector2(.5f, 0f);
            _guide = PanelMotion.Attach(box.gameObject, box);
            _guideGroup = box.GetComponent<CanvasGroup>();
            _guide.SetImmediate(false);
            Image background = box.gameObject.AddComponent<Image>();
            UiSkin skin = UiSkin.Load();
            UiSkin.SetNoticeArtwork(background, skin);
            _message = Label("Message", box, Vector2.zero, Vector2.zero, Vector2.zero);
            _message.rectTransform.anchorMax = Vector2.one;
            _message.rectTransform.offsetMin = new Vector2(56, GuideTextPadding);
            _message.rectTransform.offsetMax = new Vector2(-216, -GuideTextPadding);
            _message.alignment = TextAnchor.MiddleLeft;
            _message.color = UiSkin.Ink;
            _message.fontSize = 32;
            _message.resizeTextForBestFit = false;
            _message.horizontalOverflow = HorizontalWrapMode.Overflow;
            _message.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform next = CreateRect("Continue", box, Vector2.right, new Vector2(-111, 45), new Vector2(154, 59) * 1.1f);
            Image image = next.gameObject.AddComponent<Image>();
            image.color = new Color(.28f, .43f, .23f);
            _next = next.gameObject.AddComponent<Button>();
            _next.targetGraphic = image;
            Text caption = Label("Caption", next, new Vector2(.5f, .5f), Vector2.zero, new Vector2(128, 42));
            caption.text = "확인";
            if (skin != null)
            {
                UiSkin.SetDialogButton(_next, skin.GreenButton);
            }
            _next.onClick.AddListener(Continue);
            _guideMascot = UiSkin.AddGuideMascot(box, skin);
            _quest = new TutorialGatherPanel(root, skin, UiSkin.FontFor(_cookingUi.TutorialFont), FindObjectOfType<InventoryHud>());
            _quest.Card.SetSiblingIndex(shade.GetSiblingIndex());
            box.SetAsLastSibling();
            Sprite cursor = Resources.Load<Sprite>("TutorialCursor");
            if (cursor != null)
            {
                _cursor = HudLayout.Icon("GuideCursor", root, Vector2.zero, Vector2.one * CursorSize, cursor).rectTransform;
                _cursor.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[UI] event=tutorial_marker result=unavailable reason=missing_cursor_sprite", this);
        }

        private bool NoticeClosed
        {
            get { return State.NoticeClosed && State.NoticeStep == State.Step || (State.ConfirmedGuideSteps & (1 << State.Step)) != 0; }
        }

        private bool IsTouchStep
        {
            get { return State.Step == 18 || State.Step == 19; }
        }

        private void Update()
        {
            if (_root == null) return;
            _clock?.SetTutorialOpeningHold(State != null && State.Step < 21 &&
                _session != null && _session.BusinessSequence == 0);
            _clock?.SetTutorialSleepHold(NeedsTutorialSleepHold());
            if (State == null || State.Step == 21)
            {
                _root.SetActive(false);
                _quest.SetVisible(false);
                _loader.SetTutorialMovementBlocked(false);
                if (_clock != null) _clock.SetTutorialPaused(false);
                return;
            }
            bool available = !_loader.IsStartupLoading && !_loader.IsTransitioning && !_loader.IsSessionBlocked &&
                !_loader.IsMenuPaused;
            if (!available)
            {
                if (_root.activeSelf)
                {
                    _guide.SetImmediate(false);
                    _shownStep = -1;
                }
                _root.SetActive(false);
                _loader.SetTutorialMovementBlocked(false);
                if (_clock != null) _clock.SetTutorialPaused(false);
                return;
            }

            if (_loader.NeedsHomeNickname)
            {
                _lessonNotice = false;
                PresentAction(_villageHud?.TutorialNicknameTarget);
                _clock?.SetTutorialPaused(true);
                return;
            }

            if (!_hasRegion || _region != _loader.CurrentScene) RefreshRegion();
            if (_interruptNotice != InterruptNotice.None)
            {
                PresentInterruptNotice();
                return;
            }
            if (_openingReminder || (_clock != null && _clock.AtTutorialOpeningDeadline && !State.OpeningReminderSeen))
            {
                _openingReminder = true;
                SetExhibit(null);
                PresentLesson("포차로 돌아가 영업을 시작해 주세요. 영업을 열면 시간이 다시 흘러요.");
                return;
            }
            if (FirstDayRound > 0)
            {
                UpdateFirstDay();
                return;
            }
            _lessonNotice = false;
            if (!IsIntroducingPocha) SetExhibit(null);
            if (State.Step == 0 && _region == SceneType.Village) MoveTo(1);
            if ((State.Step == 8 || State.Step == 9) && _region != SceneType.Forest01) MoveTo(7);
            if (State.Step == 0 && _region == SceneType.Home)
            {
                UpdateHomeLesson();
                return;
            }
            if (State.Step == 4 || State.Step == 5)
            {
                UpdatePochaLesson();
                return;
            }
            if (State.Step == 20)
            {
                UpdateClosingLesson();
                return;
            }
            if (State.Step >= 11 && State.Step <= 16)
            {
                UpdateFirstBusiness();
                return;
            }

            if (State.Step >= 17 && State.Step < 20 && _station.SummaryPending)
            {
                if (_root.activeSelf)
                {
                    _guide.SetImmediate(false);
                    _shownStep = -1;
                }
                _root.SetActive(false);
                _loader.SetTutorialMovementBlocked(false);
                if (_clock != null) _clock.SetTutorialPaused(false);
                return;
            }

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            if (_shownStep != State.Step) ShowStep();

            RefreshFocus();
            UpdateQuest();
            if (_guide.IsOpen && _next.gameObject.activeSelf) return;
            switch (State.Step)
            {
                case 0: if (_region == SceneType.Village) MoveTo(1); break;
                case 2:
                case 3:
                    if (PochaKitchen.Current != null && PochaKitchen.Current.IsInside) MoveTo(4);
                    else _uiTarget = AtMarket() ? _input.TutorialInteract : null;
                    break;
                case 7:
                    if (_region == SceneType.Forest01)
                    {
                        State.VisitedFirstForest = true;
                        MoveTo(8);
                    }
                    break;
                case 9:
                    if (HarvestComplete()) MoveTo(10);
                    else UpdateForestGuide();
                    break;
                case 10: if (_region == SceneType.Village) MoveTo(11); break;
                case 18:
                    if (_station.Progress.FoodLevels[0] >= 2) MoveTo(20);
                    else if (_contentUi.IsOpen) MoveTo(19);
                    break;
                case 19:
                    if (_station.Progress.FoodLevels[0] >= 2) MoveTo(20);
                    else if (!_contentUi.IsOpen) MoveTo(18);
                    break;
            }
        }

        private bool NeedsTutorialSleepHold()
        {
            TutorialSaveData state = State;
            if (state == null || state.Step >= 21 || state.CompletedTutorialSleep ||
                _session == null || _session.BusinessSequence == 0) return false;

            return state.FirstDayRound is >= 62 and <= 100 ||
                state.FirstDayRound == 0 && state.Step == 20 && state.ClosingLesson is 5 or 6;
        }

        private void UpdateForestGuide()
        {
            HarvestTarget selected = _interaction.TutorialHarvestTarget;
            if (_forestFocusTarget != selected) _forestFocusTarget = null;
            RectTransform button = _input.TutorialInteract;
            if (selected != null && _forestFocusTarget == null && button.gameObject.activeInHierarchy &&
                !_loader.IsUiOpen && !_panning && !(selected.IsMonster ? State.ForestMonsterFocusSeen : State.ForestGatherFocusSeen))
            {
                _forestFocusTarget = selected;
                if (selected.IsMonster) State.ForestMonsterFocusSeen = true;
                else State.ForestGatherFocusSeen = true;
                SaveProgress();
                Debug.Log($"[UI] event=tutorial_forest_focus result=shown kind={(selected.IsMonster ? "monster" : "gather")}", this);
            }

            _uiTarget = _forestFocusTarget != null ? button : null;
            if (selected == null && State.TutorialMapPractice < 2)
            {
                if (_minimap == null) _minimap = FindObjectOfType<MinimapHud>();
                _uiTarget = _minimap?.TutorialControl;
            }
        }

        internal void OnForestGuideUsed(HarvestTarget target)
        {
            if (FirstDayRound == 30)
            {
                if (target.IsMonster) State.ForestMonsterFocusSeen = true;
                else State.ForestGatherFocusSeen = true;
                SaveProgress();
            }
            if (_forestFocusTarget != target) return;
            _forestFocusTarget = null;
            _uiTarget = null;
        }

        private void RefreshRegion()
        {
            _region = _loader.CurrentScene;
            _hasRegion = true;
            _market = null;
            foreach (MarketInteractable candidate in FindObjectsOfType<MarketInteractable>())
                if (candidate.MarketIndex == 0) { _market = candidate; break; }
            _forest = null;
            _exit = null;
            _homeEntrance = null;
            foreach (ForestPortal portal in FindObjectsOfType<ForestPortal>())
            {
                if (portal.Destination == SceneType.Forest01) _forest = portal;
                if (portal.Destination == SceneType.Village) _exit = portal;
                if (portal.Destination == SceneType.Home) _homeEntrance = portal;
            }
            _portalNotice = false;
            _gatherRegion = _region == SceneType.Forest01 ? _loader.CurrentRegion.GetComponent<ForestRegrowth>() : null;
            _forestPreviewShown = false;
            if (_gatherRegion != null && State.Step <= 9)
            {
                State.HarvestGoals = new int[Inventory.IngredientCount];
                for (int i = 0; i < Inventory.IngredientCount; i++) State.HarvestGoals[i] = _gatherRegion.GetHarvestYield((IngredientType)i, false);
                int total = 0;
                foreach (int count in State.HarvestGoals) total += count;
                if (total == 0) Debug.LogWarning("[Gameplay] event=tutorial_goal result=blocked reason=no_spawned_yield", this);
                else Debug.Log($"[Gameplay] event=tutorial_goal result=spawn_totals_captured total={total}", this);
                SaveProgress();
            }
            _questVersion = -1;
            _shownStep = -1;
        }

        private void PresentLesson(string copy, Transform exhibit = null, Camera camera = null)
        {
            SetExhibit(exhibit, camera);
            _root.SetActive(true);
            _guide.SetVisible(true);
            _next.gameObject.SetActive(true);
            _lessonNotice = true;
            SetGuideText(copy);
            ApplyFocus(true, true);
        }

        private void PresentAction(RectTransform button, Transform world = null, Camera camera = null)
        {
            SetExhibit(null);
            _root.SetActive(true);
            _guide.SetVisible(false);
            _next.gameObject.SetActive(false);
            _uiTarget = button;
            _worldTarget = world;
            _waypointCamera = camera;
            ApplyFocus(false, _panning);
        }

        private void UpdateHomeLesson()
        {
            if (_homePlacement == null) _homePlacement = FindObjectOfType<HomePlacementHud>();
            HomePlacement room = _loader.CurrentRegion.GetComponent<HomePlacement>();
            switch (State.HomeLesson)
            {
                case 0: PresentLesson(HomeTip); break;
                case 1: PresentLesson("시점 버튼으로 배치 모드를 켜세요. 가구 창고 1번 슬롯의 거울을 골라 배치를 누르세요. 빈 칸으로 옮기고 회전한 뒤 배치를 확정해 보세요."); break;
                case 2:
                    if (MirrorPlaced(room))
                    {
                        State.HomePlacementStage = 0;
                        State.HomeLesson = 3;
                        SaveProgress();
                    }
                    else
                    {
                        _homePlacement?.RestoreTutorialPractice(State);
                        PresentAction(_homePlacement?.TutorialMirrorTarget());
                    }
                    break;
                case 3: PresentLesson("배치한 물건은 터치한 뒤 보관할 수 있어요. 시점 버튼을 다시 눌러 배치 모드를 나가세요."); break;
                case 4:
                    if (_homePlacement == null || !_homePlacement.TutorialEditing)
                    {
                        State.HomeLesson = 5;
                        SaveProgress();
                    }
                    else PresentAction(PochaKitchen.Current?.TutorialViewTarget);
                    break;
                case 5: PresentLesson("집은 언제든 자유롭게 꾸밀 수 있어요. 밖으로 나갈 때는 현관 카페트에 다가가 상호작용하세요."); break;
                default:
                    Transform exit = _exit != null ? _exit.transform : null;
                    PresentWorldAction(exit);
                    break;
            }
        }

        private static bool MirrorPlaced(HomePlacement room)
        {
            if (room == null || !room.IsConfigured) return false;
            for (int i = 0; i < room.ItemCount; i++)
            {
                HomePlacement.Item item = room.GetItem(i);
                if (item.Id == "Mirror") return item.Root != null && item.Root.gameObject.activeSelf && !room.IsStored(i);
            }
            return false;
        }

        private void PresentWorldAction(Transform world, Camera camera = null)
        {
            RectTransform button = null;
            PochaKitchen kitchen = PochaKitchen.Current;
            if (world != null)
            {
                if (kitchen != null && kitchen.GetOutlineTarget() == world) button = _input.TutorialInteract;
                else if (camera == null && world == _market?.InteractionPoint && AtMarket()) button = _input.TutorialInteract;
                else if (_exit != null && world == _exit.transform && _interaction.IsNearPortal(_exit))
                    button = _input.TutorialInteract;
            }
            PresentAction(button, world, camera);
        }

        private Transform BuildingWaypoint(bool home)
        {
            if (_region == SceneType.Village)
                return home ? _homeEntrance?.transform : _market?.InteractionPoint;
            return _exit?.transform;
        }

        private void UpdatePochaLesson()
        {
            PochaKitchen kitchen = PochaKitchen.Current;
            if (kitchen == null || !kitchen.IsInside)
            {
                SetExhibit(null);
                if (State.PochaLesson == 7) MoveTo(7);
                else PresentAction(null, BuildingWaypoint(home: false));
                return;
            }
            if (kitchen.IsLockNoticeOpen)
            {
                PresentAction(null);
                return;
            }
            int page = State.PochaLesson;
            if (page < FacilityTips.Length)
            {
                PresentLesson(FacilityTips[page], kitchen.TutorialFacility(page), kitchen.InteriorCamera);
                return;
            }
            if (page == 5) PresentAction(kitchen.IsBusinessPanelOpen ? null : kitchen.TutorialBusinessButton);
            else if (page == 6)
                PresentLesson($"영업 패널에서 영업을 시작하거나 마감할 수 있어요. 정오부터 개점할 수 있고, 자정부터는 새 주문을 받지 않아요. 받은 주문을 처리하면 자동으로 마감해요. 직접 마감하면 당일 재개점은 불가능해요. 이제 {FirstForestName}으로 가서 영업 준비에 필요한 재료를 모아 보세요!");
            else PresentWorldAction(kitchen.TutorialExit, kitchen.InteriorCamera);
        }

        private void UpdateClosingLesson()
        {
            PochaKitchen kitchen = PochaKitchen.Current;
            switch (State.ClosingLesson)
            {
                case 0: UpdateMarketIntro(); break;
                case 1:
                    if (kitchen != null && !kitchen.IsInside && _region == SceneType.Village)
                    {
                        State.ClosingLesson = 2;
                        SaveProgress();
                    }
                    else PresentAction(kitchen?.TutorialViewTarget);
                    break;
                case 2:
                    if (!_outdoorPreviewShown && !_panning && kitchen?.TutorialOutdoorCooker != null)
                        Pan(kitchen.TutorialOutdoorCooker.position, 20, _marketOverviewScale);
                    if (_panning) PresentAction(null);
                    else PresentLesson("야시장 운영 중에는 메뉴를 고르면 화로가 자동으로 조리해요. 조리 중 1개와 예약 4개까지 넣을 수 있어요. 완성품은 자동 진열되고 손님들이 구매한 뒤 테이블에서 먹어요.",
                        kitchen?.TutorialOutdoorCooker);
                    break;
                case 3: PresentLesson("포차 내부로 돌아가 영업을 이어가세요.", _market != null ? _market.transform : null); break;
                case 4:
                    if (kitchen != null && kitchen.IsInside)
                    {
                        State.ClosingLesson = 5;
                        State.NoticeClosed = false;
                        SaveProgress();
                    }
                    else PresentAction(kitchen?.TutorialViewTarget, BuildingWaypoint(home: false));
                    break;
                case 5:
                    if (!State.NoticeClosed) PresentLesson("이제 영업 시간 동안 자유롭게 영업해 보세요. 영업 중에는 포차 영업권 안에서 이동할 수 있어요. 영업 패널에서 직접 마감할 수도 있어요.");
                    else if (_session.CurrentActivity == PlayerActivity.Business || _station.SummaryPending)
                        PresentAction(kitchen?.TutorialBusinessAction);
                    else
                    {
                        State.ClosingLesson = 6;
                        State.NoticeClosed = false;
                        SaveProgress();
                    }
                    break;
                case 6:
                    if (State.CompletedTutorialSleep)
                    {
                        State.ClosingLesson = 7;
                        SaveProgress();
                    }
                    else UpdateSleepLesson(kitchen);
                    break;
                case 8:
                    PresentLesson(HelpTip);
                    break;
                default:
                    PresentLesson(FinishTip + (State.TutorialFurnitureGranted
                        ? " 가구 창고에서 책상·의자·소파를 한 개씩 확인할 수 있어요."
                        : " 보상 가구는 창고에 빈 슬롯이 3개 생기면 받을 수 있어요."));
                    break;
            }
        }

        private void UpdateMarketIntro()
        {
            if (_station.SummaryPending)
            {
                PresentAction(null);
                return;
            }
            if (!State.GrowthMarketScrollInstructed)
            {
                PresentLesson(UnlockTip + "\n" + GrowthScrollTip);
                return;
            }
            if (State.GrowthMarketRowSeen)
            {
                PresentLesson(OutdoorTip);
                return;
            }
            if (!_contentUi.IsOpen)
            {
                PresentAction(_contentUi.TutorialTarget(false));
                return;
            }
            if (!_contentUi.IsMarketRowVisible)
            {
                PresentAction(null);
                return;
            }
            State.GrowthMarketRowSeen = true;
            SaveProgress();
            PresentLesson(OutdoorTip);
        }

        private void UpdateSleepLesson(PochaKitchen kitchen)
        {
            if (_clock.IsDaytime || _session.CurrentActivity == PlayerActivity.Sleeping)
            {
                PresentAction(null);
                return;
            }
            if (_region == SceneType.Home)
            {
                SleepInteractable bed = _loader.CurrentRegion.GetComponentInChildren<SleepInteractable>();
                if (!State.BedRecoveryGuideSeen)
                {
                    PresentLesson(BedRecoveryTip, bed != null ? bed.transform : null);
                    return;
                }
                RectTransform button = bed != null && _interaction.CanInteractWithBed(bed) ? _input.TutorialInteract : null;
                PresentAction(button, bed != null ? bed.transform : null);
            }
            else if (!State.BedRecoveryGuideSeen && !State.SleepReturnGuideSeen) PresentLesson(HomeAtMidnightTip);
            else if (kitchen != null && kitchen.IsInside) PresentWorldAction(kitchen.TutorialExit, kitchen.InteriorCamera);
            else PresentAction(null, BuildingWaypoint(home: true));
        }

        private bool AtMarket()
        {
            if (_region != SceneType.Village || _market == null) return false;
            PochaKitchen kitchen = PochaKitchen.Current;
            return kitchen != null ? kitchen.GetOutlineTarget() == _market.InteractionPoint :
                _interaction.CanInteractWith(_market, true);
        }

        private void UpdateFirstBusiness()
        {
            PochaKitchen kitchen = PochaKitchen.Current;
            _loader.SetTutorialMovementBlocked(false);
            bool started = State.TakeawayServed || _session != null && _session.BusinessSequence > 0;
            bool open = _session != null && _session.CurrentActivity == PlayerActivity.Business;
            bool summary = started && _station.SummaryPending;
            if (State.TakeawayServed)
            {
                if (summary) PresentAction(null);
                else FinishFirstBusinessGuide(17, "first_serve");
                return;
            }

            int closingTip = summary ? CheckSummaryTip : -1;
            bool outside = kitchen == null || !kitchen.IsInside;
            if (!summary && !outside)
            {
                int step = kitchen.GetFirstBusinessStep();
                if (step != State.Step) MoveTo(step);
            }

            if (!summary && outside && (_region != SceneType.Village || State.ConfirmedBusinessTips != 0 || _businessTip >= 2))
            {
                // 실내 설명 확인은 재진입 후 이어가고, 외부에서는 목적지까지 이동할 수 있게 한다.
                _businessTip = -1;
                PresentAction(null, BuildingWaypoint(home: false));
                return;
            }

            int tip = closingTip;
            if (!outside && open && (State.ConfirmedBusinessTips & (1 << 21)) == 0) tip = 21;
            if (tip < 0)
                tip = _region == SceneType.Village && kitchen != null
                    ? kitchen.FirstBusinessTipId(kitchen.HasWheel ? kitchen.FirstBusinessWheelItem() : -1) : 0;
            if (closingTip >= 0 && _businessTip >= 0 && _businessTip != closingTip) _businessTip = -1;
            if (_businessTip < 0 && tip >= 0 && (State.ConfirmedBusinessTips & (1 << tip)) == 0) _businessTip = tip;

            _root.SetActive(true);
            if (summary) _root.transform.SetAsLastSibling();
            _quest.SetVisible(false);
            _uiTarget = kitchen?.TutorialBusinessAction ?? kitchen?.TutorialWheelTarget;
            if (!summary && !outside && tip == 6 && !kitchen.IsBusinessPanelOpen && _uiTarget == null)
                _uiTarget = kitchen.TutorialBusinessButton;
            _worldTarget = !summary && outside ? BuildingWaypoint(home: false) : kitchen?.BusinessTipTarget(tip);
            _waypointCamera = kitchen != null && kitchen.IsInside ? kitchen.InteriorCamera : null;
            bool show = _businessTip >= 0;
            if (show)
                SetExhibit(WorldVisual(outside ? _worldTarget : kitchen.BusinessTipTarget(_businessTip)), _waypointCamera);
            if (!show && _uiTarget == null && _worldTarget != null && kitchen != null &&
                kitchen.GetOutlineTarget() == _worldTarget) _uiTarget = _input.TutorialInteract;
            if (show)
            {
                _next.gameObject.SetActive(true);
                if (_businessTip == CheckSummaryTip)
                    SetGuideText("결산에서 오늘 서빙 결과와 수입을 확인하세요. 결산을 닫으면 다음 안내로 이어져요.");
                else if (_businessTip == 21)
                    SetGuideText("손님의 주문은 25초 안에 받고, 수락 후 50초 안에 서빙하세요. 조리대에서 레시피를 고른 뒤 냉장고의 재료를 한 개씩 넣으면 자동으로 조리돼요. 시간이 지나면 주문은 끝나요. 다른 음식을 주면 음식이 소모돼요. 두 경우 모두 보상은 없어요.");
                else
                    SetGuideText(PochaKitchen.BusinessTips[_businessTip]);
            }
            _guide.SetVisible(show);
            ApplyFocus(show, show && (kitchen == null || !kitchen.HasWheel));
        }

        private void FinishFirstBusinessGuide(int next, string result)
        {
            _businessTip = -1;
            _guide.SetImmediate(false);
            ApplyFocus(false, false);
            MoveTo(next);
            Debug.Log($"[UI] event=first_business_guide result={result} next={next}", this);
        }

        internal bool BlockTravel(SceneType destination)
        {
            if (_root == null || State == null || _loader.CurrentScene == destination) return false;
            if (FirstDayRound > 0) return BlockFirstDayTravel(destination);
            bool gathering = _loader.CurrentScene == SceneType.Forest01 && State.Step >= 7 && State.Step <= 9 && !HarvestComplete();
            bool goingToMarket = _loader.CurrentScene == SceneType.Village && SceneLoadManager.IsForest(destination) &&
                State.Step >= 1 && State.Step <= 5;
            bool homeLesson = _loader.CurrentScene == SceneType.Home && State.Step == 0 && State.HomeLesson < 6;
            if (homeLesson) return true;
            if (!gathering && !goingToMarket) return false;

            OpenInterruptNotice(gathering ? InterruptNotice.ForestExit : InterruptNotice.MarketTravel);
            return true;
        }

        internal void OnWrongDelivery()
        {
            if (_root != null && State != null && State.Step >= 11 && State.Step <= 16)
                OpenInterruptNotice(InterruptNotice.WrongDelivery);
        }

        internal bool ShowEarlySleepNotice()
        {
            if (_root == null || State == null || State.Step >= 21) return false;
            OpenInterruptNotice(InterruptNotice.SleepTooEarly);
            return true;
        }

        private void OpenInterruptNotice(InterruptNotice notice)
        {
            if (_root == null || _interruptNotice != InterruptNotice.None) return;
            _interruptNotice = notice;
            PresentInterruptNotice();
            _guidePageIndex = 0;
            ShowGuidePage();
            Debug.Log($"[UI] event=tutorial_interrupt result=shown notice={notice}", this);
        }

        private void PresentInterruptNotice()
        {
            string copy;
            switch (_interruptNotice)
            {
                case InterruptNotice.ForestExit: copy = "재료를 모두 모은 뒤에 숲에서 나갈 수 있어요. 남은 재료를 모아 보세요."; break;
                case InterruptNotice.MarketTravel: copy = "표시된 포차로 먼저 이동해 주세요."; break;
                case InterruptNotice.WrongDelivery: copy = "오배송을 했어요. 투입한 재료를 잃고 정산을 받지 못해요."; break;
                case InterruptNotice.FirstDayClosing: copy = "첫 영업은 자정까지 이어가 보세요!"; break;
                default: copy = "자정부터 수면할 수 있어요."; break;
            }
            PresentLesson(copy);
        }

        private void RefreshFocus()
        {
            if (State.Step == 0 && NoticeClosed)
                _uiTarget = State.JoystickTouched ? null : _input.TutorialJoystick;
            if ((State.Step == 7 || State.Step == 10) && NoticeClosed)
            {
                bool entering = State.Step == 7;
                bool atPortal = _entryUi.IsTravelCardOpen && (entering ? _entryUi.IsForestOne : _entryUi.IsVillageExit);
                ForestPortal portal = entering ? _forest : _exit;
                int flag = entering ? ForestEntryNotice : 2;
                _uiTarget = atPortal && _entryUi.CanConfirmTravel ? _entryUi.TutorialConfirmTarget : null;
                _worldTarget = atPortal || portal == null ? null : portal.transform;
                _portalNotice = atPortal && (State.ConfirmedPortalNotices & flag) == 0;
                _guide.SetVisible(_portalNotice);
                _next.gameObject.SetActive(_portalNotice);
                if (_portalNotice)
                    SetGuideText(entering ? ForestEntryCopy : "나가기 버튼을 눌러 마을로 돌아가세요.");
                ApplyFocus(_portalNotice || _uiTarget != null, atPortal);
                return;
            }
            bool explanation = _guide.IsOpen && _next.gameObject.activeSelf;
            bool touch = IsTouchStep || (State.Step == 0 && NoticeClosed && !State.JoystickTouched);
            if (!explanation && !_panning && _loader.IsUiOpen && (_uiTarget == null || State.Step == 0))
            {
                _guide.SetVisible(false);
                ApplyFocus(false, false);
                return;
            }
            ApplyFocus(explanation || touch || _panning, explanation || (touch && State.Step != 0) || _panning);
        }

        private void SetGuideText(string text)
        {
            float width = _message.rectTransform.rect.width;
            if (_guidePages.Count > 0 && _guideCopy == text && Mathf.Approximately(_guideWidth, width)) return;
            _guideCopy = text;
            _guideWidth = width;
            _guidePageIndex = 0;
            BuildGuidePages(text, width);
            ShowGuidePage();
        }

        private void BuildGuidePages(string text, float width)
        {
            _guidePages.Clear();
            var page = new List<string>(GuidePageLimit);
            foreach (string paragraph in text.Split('/', '\n'))
            {
                string[] parts = Regex.Split(paragraph, @"(?<=[.!?])(?:[ \t]+(?=\S)|(?=[가-힣]))");
                foreach (string part in parts)
                {
                    string sentence = UiSkin.TrimNoticeLine(part.Trim());
                    if (sentence.Length == 0) continue;
                    string[] lines = UiSkin.WrapNoticeText(_message, sentence, width).Split('\n');
                    if (page.Count > 0 && page.Count + lines.Length > GuidePageLimit)
                        FlushGuidePage(page);
                    foreach (string line in lines)
                    {
                        if (page.Count == GuidePageLimit)
                            FlushGuidePage(page);
                        page.Add(line);
                    }
                }
            }
            FlushGuidePage(page);
            if (_guidePages.Count == 0) _guidePages.Add("");
        }

        private void FlushGuidePage(List<string> lines)
        {
            if (lines.Count == 0) return;
            _guidePages.Add(string.Join("\n", lines));
            lines.Clear();
        }

        private void ShowGuidePage()
        {
            _message.text = _guidePages[_guidePageIndex];
            if (_guideWidth <= 0f) return;
            RectTransform guide = (RectTransform)_guide.transform;
            float height = Mathf.Max(GuideMinHeight, Mathf.Ceil(_message.preferredHeight) + GuideTextPadding * 2f);
            guide.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void ApplyFocus(bool restrict, bool blockMovement)
        {
            Rect bounds = HudLayout.ScreenBounds(Screen.safeArea, Screen.width, Screen.height);
            if (_guide.gameObject.activeSelf) LayoutGuide(bounds);
            _quest.Align(new Vector2(bounds.xMin, bounds.yMax));
            bool explanation = _guide.IsOpen && _next.gameObject.activeSelf;
            bool outdoorTour = FirstDayRound > 0 ? FirstDayRound >= 90 && FirstDayRound <= 96 :
                State != null && State.Step == 20 && State.ClosingLesson >= 2 && State.ClosingLesson <= 4;
            if (_clock != null) _clock.SetTutorialPaused(explanation || GuideInputHandoff || outdoorTour || FirstDayRound > 0 && _panning);
            _restrictInput = restrict;
            UpdateInputMask(FocusedControl(explanation), explanation);
            if (FirstDayRound > 0 && !_loader.NeedsHomeNickname)
                ApplyFirstDayMovement(explanation);
            else _loader.SetTutorialMovementBlocked(blockMovement);
        }

        private RectTransform FocusedControl(bool explanation)
        {
            return !explanation && !_panning && _uiTarget != null && _uiTarget.gameObject.activeInHierarchy ? _uiTarget : null;
        }

        private void UpdateInputMask(RectTransform control, bool explanation)
        {
            _mask.BlockAll = GuideInputHandoff;
            bool firstDay = FirstDayRound > 0;
            bool movementGuide = firstDay ? _firstDayMoves && !_firstDayFree : control == _input.TutorialJoystick;
            bool allowMovementControls = movementGuide && !explanation && !_panning &&
                !_mask.BlockAll && !_loader.NeedsHomeNickname;
            RectTransform sprint = allowMovementControls ? _sprintControl : null;
            RectTransform menu = allowMovementControls ? _villageHud?.TutorialMenuControl : null;
            if (Debug.isDebugBuild && (_mask.AllowedSprint != sprint || _mask.AllowedMenu != menu))
                Debug.Log($"[UI] event=tutorial_movement_controls result={(allowMovementControls ? "allowed" : "released")} sprint={sprint != null} menu={menu != null}", this);
            _mask.AllowedSprint = sprint;
            _mask.AllowedMenu = menu;
            if (_mask.BlockAll)
            {
                _mask.gameObject.SetActive(true);
                return;
            }
            if (firstDay && !_loader.NeedsHomeNickname)
            {
                UpdateFirstDayInput(control, explanation);
                return;
            }
            if (Debug.isDebugBuild && (_mask.ExclusiveUi != (control != null) || control != null && _mask.AllowedUi != control))
                Debug.Log($"[UI] event=tutorial_input_focus result={(control != null ? "restricted" : "released")} target={control?.name}", this);
            _mask.ExclusiveUi = control != null;
            _mask.AllowedUi = _panning ? null : explanation ? (RectTransform)_next.transform : control;
            _mask.EditableField = control != null && _loader.NeedsHomeNickname ? _villageHud?.TutorialNicknameInput : null;
            bool wheel = control != null && IsGuidedKitchenWheel;
            _mask.AllowedJoystick = wheel ? _input.TutorialJoystick : null;
            _mask.AllowedConfirm = wheel ? KitchenConfirmFocus : null;
            PochaKitchen kitchen = PochaKitchen.Current;
            _mask.DismissPanel = control != null && control == kitchen?.TutorialBusinessAction
                ? kitchen.BusinessPanelCard : null;
            _mask.AllowedWorld = null;
            _mask.gameObject.SetActive(_restrictInput || control != null);
        }

        private bool IsGuidedKitchenWheel => State != null && State.Step >= 11 && State.Step <= 16 &&
            PochaKitchen.Current != null && PochaKitchen.Current.HasWheel;

        private RectTransform KitchenConfirmFocus => IsGuidedKitchenWheel && PochaKitchen.Current.HasSelectedTutorialWheelSlot
            ? _input.TutorialInteract : null;

        private void LayoutGuide(Rect bounds)
        {
            RectTransform guide = (RectTransform)_guide.transform;
            Vector2 guideMin = new Vector2(bounds.xMin + GuideLeft * bounds.width, bounds.yMin);
            Vector2 guideMax = new Vector2(bounds.xMin + GuideRight * bounds.width, bounds.yMin);
            Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            float gap = 12f * _canvas.scaleFactor;
            Rect joystick = ScreenRect(_input.TutorialJoystick, eventCamera);
            guideMin.x = Mathf.Max(guideMin.x, (joystick.xMax + gap) / Screen.width);
            if (_sprintControl != null && _sprintControl.gameObject.activeInHierarchy)
            {
                Rect sprint = ScreenRect(_sprintControl, eventCamera);
                guideMax.x = Mathf.Min(guideMax.x, (sprint.xMin - gap) / Screen.width);
            }
            if (guideMax.x <= guideMin.x)
            {
                guideMin.x = bounds.xMin + GuideLeft * bounds.width;
                guideMax.x = bounds.xMin + GuideRight * bounds.width;
            }
            if (guide.anchorMin != guideMin || guide.anchorMax != guideMax)
            {
                guide.anchorMin = guideMin;
                guide.anchorMax = guideMax;
            }
            SetGuideText(_guideCopy);
            float mascotOverhang = _guideMascot != null ? _guideMascot.anchoredPosition.y + _guideMascot.rect.height * .5f : 0f;
            float safeHeight = ((RectTransform)guide.parent).rect.height * bounds.height;
            if (safeHeight > 0f)
            {
                float scale = Mathf.Clamp01((safeHeight - GuideBottom - 16f) / (guide.rect.height + mascotOverhang));
                _guide.SetLayoutScale(Vector3.one * scale);
            }
        }

        private void OnJoystickPressed()
        {
            if (State == null || State.Step != 0 || !NoticeClosed || State.JoystickTouched) return;
            State.JoystickTouched = true;
            _uiTarget = null;
            ApplyFocus(false, false);
            SaveProgress();
            Debug.Log("[Input] event=tutorial_joystick result=first_touch", this);
        }

        private void UpdateQuest()
        {
            bool visible = State.Step == 9 && _gatherRegion != null && State.HarvestGoals != null && State.HarvestGoals.Length == Inventory.IngredientCount;
            _quest.SetVisible(visible);
            if (!visible || _questVersion == _inventory.Version) return;

            _questVersion = _inventory.Version;
            _quest.SetProgress(_gatherRegion, State.HarvestGoals);
        }

        private bool HarvestComplete()
        {
            if (_gatherRegion == null || State.HarvestGoals == null || State.HarvestGoals.Length != Inventory.IngredientCount) return false;
            int total = 0;
            for (int i = 0; i < Inventory.IngredientCount; i++)
            {
                total += State.HarvestGoals[i];
                if (_gatherRegion.GetHarvestYield((IngredientType)i, true) < State.HarvestGoals[i]) return false;
            }
            return total > 0;
        }

        private void SaveProgress()
        {
            if (_save != null && _save.IsReady) _save.RequestSave();
        }

        private void ShowStep()
        {
            _shownStep = State.Step;
            _forestFocusTarget = null;
            _uiTarget = null;
            _worldTarget = null;
            _waypointCamera = null;
            bool explain = !NoticeClosed;
            _guide.SetVisible(explain);
            _next.gameObject.SetActive(explain);
            string text = "";
            switch (State.Step)
            {
                case 0: text = HomeTip; _uiTarget = NoticeClosed && !State.JoystickTouched ? _input.TutorialJoystick : null; break;
                case 1:
                    text = VillageTip;
                    _worldTarget = _market == null ? null : _market.InteractionPoint;
                    if (NoticeClosed) PanToWaypoint(2);
                    break;
                case 2:
                case 3: text = MarketRoute; _worldTarget = _market == null ? null : _market.InteractionPoint; break;
                case 6:
                    text = $"{FirstForestName} 입구를 확인하세요.";
                    _worldTarget = _forest == null ? null : _forest.transform;
                    if (NoticeClosed) PanToWaypoint(7);
                    break;
                case 7: text = ForestRoute; _worldTarget = _forest == null ? null : _forest.transform; break;
                case 8:
                    if (!_forestPreviewShown)
                    {
                        _guide.SetVisible(false);
                        // 현재 숲의 지역 루트/지면 중심을 보여주며 시점의 높이는 유지한다.
                        PanOverview(8, village: false);
                        return;
                    }
                    text = ForestCollect;
                    break;
                case 9: text = CollectAll; _questVersion = -1; break;
                case 10: text = ForestReturn; _worldTarget = _exit == null ? null : _exit.transform; break;
                case 17: text = FirstSale; break;
                case 18: text = GrowthMenu; _uiTarget = _contentUi.TutorialTarget(false); break;
                case 19: text = UpgradeTip; _uiTarget = _contentUi.TutorialTarget(true); break;
            }
            SetGuideText(text);
        }

        private void Continue()
        {
            if (!_guide.IsOpen || !_next.gameObject.activeSelf || GuideInputHandoff) return;
            _guideInputResumeAt = Time.unscaledTime + PanelMotion.Duration;
            UpdateInputMask(null, true);
            if (_guidePageIndex + 1 < _guidePages.Count)
            {
                _guidePageIndex++;
                ShowGuidePage();
                Debug.Log($"[UI] event=tutorial_guide_page result=advanced page={_guidePageIndex + 1} total={_guidePages.Count}", this);
                return;
            }
            Sfx.Play(SfxCue.MenuClose);
            if (_interruptNotice != InterruptNotice.None)
            {
                Debug.Log($"[UI] event=tutorial_interrupt result=confirmed notice={_interruptNotice}", this);
                _interruptNotice = InterruptNotice.None;
                _lessonNotice = false;
                // 접히는 동안 기본 단계 문구가 같은 패널에서 한 번 더 보이지 않게 한다.
                _guide.SetImmediate(false);
                _next.gameObject.SetActive(false);
                _shownStep = -1;
                ApplyFocus(false, false);
                return;
            }
            if (_openingReminder)
            {
                _openingReminder = false;
                _lessonNotice = false;
                State.OpeningReminderSeen = true;
                _shownStep = -1;
                _guide.SetVisible(false);
                ApplyFocus(false, false);
                SaveProgress();
                return;
            }
            if (FirstDayRound > 0)
            {
                ConfirmFirstDayNotice();
                return;
            }
            if (_lessonNotice)
            {
                ContinueLesson();
                return;
            }
            if (_portalNotice)
            {
                State.ConfirmedPortalNotices |= State.Step == 7 ? ForestEntryNotice : 2;
                _entryUi.RefreshInfo();
                Debug.Log($"[UI] event=tutorial_portal_notice result=confirmed step={State.Step}", this);
                SaveProgress();
                _portalNotice = false;
                RefreshFocus();
                return;
            }
            if (_businessTip >= 0)
            {
                _guide.SetVisible(false);
                State.ConfirmedBusinessTips |= 1 << _businessTip;
                Debug.Log($"[UI] event=tutorial_notice result=confirmed tip={_businessTip}", this);
                _businessTip = -1;
                SaveProgress();
                ApplyFocus(false, false);
                return;
            }
            State.NoticeClosed = true;
            State.ConfirmedGuideSteps |= 1 << State.Step;
            State.NoticeStep = State.Step;
            _guide.SetVisible(false);
            SaveProgress();
            switch (State.Step)
            {
                case 1: PanToWaypoint(2); break;
                case 6: PanToWaypoint(7); break;
                case 8: MoveTo(9); break;
                case 17: MoveTo(18); break;
                default: RefreshFocus(); break;
            }
        }

        private void ContinueLesson()
        {
            _lessonNotice = false;
            SetExhibit(null);
            _guide.SetVisible(false);
            if (State.Step == 0) State.HomeLesson++;
            else if (State.Step == 4 || State.Step == 5)
            {
                State.PochaLesson++;
            }
            else if (State.Step == 20)
            {
                if (State.ClosingLesson == 8)
                {
                    MoveTo(21);
                    _root.SetActive(false);
                }
                else if (State.ClosingLesson == 7)
                {
                    State.ClosingLesson = 8;
                    Debug.Log("[UI] event=tutorial_help result=reward_confirmed", this);
                }
                else if (State.ClosingLesson == 6)
                {
                    if (_region == SceneType.Home) State.BedRecoveryGuideSeen = true;
                    else State.SleepReturnGuideSeen = true;
                    Debug.Log($"[UI] event=tutorial_sleep_guide result=confirmed region={_region}", this);
                }
                else if (State.ClosingLesson == 5) State.NoticeClosed = true;
                else if (State.ClosingLesson == 0 && !State.GrowthMarketScrollInstructed)
                {
                    State.GrowthMarketScrollInstructed = true;
                    Debug.Log("[UI] event=tutorial_growth_scroll result=instructed", this);
                }
                else
                {
                    State.ClosingLesson++;
                    _contentUi.CloseTutorialPanel();
                }
            }
            ApplyFocus(_guide.IsOpen, _guide.IsOpen);
            SaveProgress();
        }

        internal void FinishForContentTest()
        {
            if (State == null || State.Step == 21) return;
            if (_panRoutine != null) StopCoroutine(_panRoutine);
            RestoreCamera();
            MoveTo(21);
            if (_root != null)
            {
                ApplyFocus(false, false);
                _root.SetActive(false);
            }
        }

        private void MoveTo(int step)
        {
            int previous = State.Step;
            State.Step = step;
            State.NoticeClosed = false;
            State.NoticeStep = step;
            _loader.SetTutorialMovementBlocked(step < 11 || (step >= 17 && step < 21));
            _shownStep = -1;
            if (_save != null && _save.IsReady) _save.RequestSave();
            Debug.Log($"[Gameplay] event=tutorial_step result=changed previous={previous} step={step}", this);
        }

        private void PanToWaypoint(int next)
        {
            if (State.Step == 1) PanOverview(next, village: true);
            else Pan(_worldTarget == null ? _loader.PlayerBody.position : _worldTarget.position, next);
        }

        private void PanOverview(int next, bool village)
        {
            Transform region = _loader.CurrentRegion.transform;
            BoxCollider ground = region.Find("Ground")?.GetComponent<BoxCollider>();
            Vector3 center = ground != null ? ground.transform.TransformPoint(ground.center) : region.position;
            if (village)
            {
                Transform pond = region.Find("VillagePark/PondGarden/PondFootprint");
                if (pond != null) center = pond.position;
                if (_market != null) center = Vector3.Lerp(_market.transform.position, center, .95f);
                center += Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized * 4.5f;
            }
            center.y = _loader.PlayerBody.position.y;
            Pan(center, next, village ? VillageOverviewScale : Mathf.Max(2.25f, _forestOverviewScale), ground, village ? VillagePanBow : 0f);
        }

        private void Pan(Vector3 target, int next, float overviewScale = 1f, BoxCollider ground = null, float bow = 0f)
        {
            if (_panRoutine != null)
            {
                StopCoroutine(_panRoutine);
                RestoreCamera();
            }

            _panRoutine = StartCoroutine(PanRoutine(target, next, overviewScale, ground, bow));
        }

        private IEnumerator PanRoutine(Vector3 target, int next, float overviewScale, BoxCollider ground, float bow)
        {
            _cameraLocal = _camera.transform.localPosition;
            _cameraSize = _camera.orthographicSize;
            _panSize = _cameraSize * Mathf.Max(1f, overviewScale);
            _panGround = ground;
            _panDuration = ground != null ? _panSeconds / OverviewSpeed : _panSeconds;
            _panFrom = _camera.transform.position;
            _panTo = _panFrom + target - (ground != null ? GroundViewCenter(_camera, target.y) : _loader.PlayerBody.position);
            _panBow = _camera.transform.right * bow;
            _panStarted = Time.unscaledTime;
            _panning = true;
            Debug.Log($"[UI] frame={Time.frameCount} event=tutorial_camera result=started step={State.Step} overview_scale={overviewScale} duration={_panDuration:F2}", this);
            _loader.SetTutorialMovementBlocked(true);
            float duration = _panDuration + 1f + (ground != null ? OverviewReturnSeconds : 0f);
            while (Time.unscaledTime - _panStarted < duration && !_loader.IsTransitioning) yield return null;
            if (FirstDayRound == 92 && next == 93 && !_loader.IsTransitioning)
            {
                _panning = false;
                _panRoutine = null;
                _firstDayOutdoorFrame = true;
                MoveFirstDay(next);
                yield break;
            }
            RestoreCamera();
            if (_loader.IsTransitioning)
            {
                _shownStep = -1;
                yield break;
            }

            if (FirstDayRound > 0)
            {
                MoveFirstDay(next);
                yield break;
            }
            if (next == 8) _forestPreviewShown = true;
            if (next == 20 && State.ClosingLesson == 2) _outdoorPreviewShown = true;
            MoveTo(next);
        }

        private static Vector3 GroundViewCenter(Camera camera, float height)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
            return ray.GetPoint((height - ray.origin.y) / ray.direction.y);
        }

        private static float MaximumOverviewSize(Camera camera, BoxCollider ground)
        {
            Transform surface = ground.transform;
            Vector3 top = ground.center + Vector3.up * (ground.size.y * .5f);
            Vector3 center = surface.InverseTransformPoint(GroundViewCenter(camera, surface.TransformPoint(top).y)) - ground.center;
            Vector3 forward = camera.transform.forward;
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            // 화면의 가로·세로 단위 벡터를 바닥에 투영한 뒤 회전된 지면 좌표로 비교한다.
            right = surface.InverseTransformVector((right - forward * (right.y / forward.y)) * camera.aspect);
            up = surface.InverseTransformVector(up - forward * (up.y / forward.y));
            Vector3 inner = ground.size * (.5f - OverviewEdgeInset);
            float x = (inner.x - Mathf.Abs(center.x)) / Mathf.Max(.0001f, Mathf.Abs(right.x) + Mathf.Abs(up.x));
            float z = (inner.z - Mathf.Abs(center.z)) / Mathf.Max(.0001f, Mathf.Abs(right.z) + Mathf.Abs(up.z));
            return Mathf.Min(x, z);
        }

        private void LateUpdate()
        {
            if (UpdateForestDiscoveryCamera()) return;
            if ((_panning || _firstDayOutdoorFrame) && (_loader.IsMenuPaused || _loader.IsSessionBlocked || _loader.IsTransitioning))
            {
                if (_panRoutine != null) StopCoroutine(_panRoutine);
                RestoreCamera();
                _shownStep = -1;
            }
            if (_root == null || !_root.activeSelf)
            {
                InteractionOutline.ShowTutorial(null);
                _buttonFocus?.Show(null, default);
                _confirmFocus?.Show(null, default);
                return;
            }
            float noticeAlpha = NoticeShadeAlpha;
            if (_firstDayOutdoorFrame)
            {
                _camera.transform.position = _panTo;
                _camera.orthographicSize = _panSize;
            }
            if (_panning)
            {
                float elapsed = Time.unscaledTime - _panStarted;
                float t = Mathf.SmoothStep(0, 1, elapsed / _panDuration);
                if (_panGround != null && elapsed > _panDuration + 1f)
                    t = Mathf.SmoothStep(1, 0, (elapsed - _panDuration - 1f) / OverviewReturnSeconds);
                float travel = _panGround != null && _panBow == Vector3.zero ? 1f - (1f - t) * (1f - t) : t;
                _camera.transform.position = Vector3.Lerp(_panFrom, _panTo, travel) + _panBow * (4f * t * (1f - t));
                if (_panBow != Vector3.zero)
                {
                    float alpha = Mathf.SmoothStep(1f, 0f, elapsed / PanelMotion.Duration);
                    _guideGroup.alpha = alpha;
                    noticeAlpha *= alpha;
                }
                float size = Mathf.Lerp(_cameraSize, _panSize, t);
                if (_panGround != null)
                    size = Mathf.Min(size, Mathf.Max(_cameraSize, MaximumOverviewSize(_camera, _panGround)));
                _camera.orthographicSize = size;
            }
            bool notice = _guide.IsOpen && _next.gameObject.activeSelf;
            Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            RectTransform touchTarget = FocusedControl(notice);
            RectTransform visualControl = notice ? _firstDayNoticeFocus : touchTarget;
            if (visualControl != null && !visualControl.gameObject.activeInHierarchy) visualControl = null;
            bool touch = touchTarget != null;
            UpdateInputMask(touchTarget, notice);
            bool exhibit = notice && !_panning && _exhibit != null;
            bool forestFocus = touch && _forestFocusTarget != null;
            Camera waypointCamera = _waypointCamera != null ? _waypointCamera : _camera;
            bool moving = _worldTarget != null && (!_panning || GuidesFirstDayForestDuringPan) &&
                (!notice || GuidesFirstDayWorldDuringNotice) && !_loader.IsUiOpen;
            Transform visual = moving ? WorldVisual(_worldTarget) : null;
            if (forestFocus) visual = _forestFocusTarget.OutlineTarget;
            bool freshGuideGroup = FirstDayRound > 0 && _firstDayWorldGroup != null;
            bool dimWorld = exhibit || touch && visual != null || freshGuideGroup;
            Transform focusTarget = exhibit ? _exhibit : visual;
            Transform[] focusGroup = freshGuideGroup ? _firstDayWorldGroup : null;
            if (focusGroup == null && _forest != null && focusTarget == _forest.TutorialVisual)
                focusGroup = _forest.TutorialFocusGroup;
            bool worldFocus = InteractionOutline.ShowTutorial(focusTarget,
                exhibit ? _exhibitCamera ?? _camera : waypointCamera, dimWorld ? TouchShadeAlpha : 0f,
                focusGroup);
            _shade.gameObject.SetActive((touch || _mask.gameObject.activeSelf) && (!dimWorld || !worldFocus));
            _shade.color = new Color(0f, 0f, 0f,
                touch || notice && _firstDayNoticeFocus != null ? TouchShadeAlpha : noticeAlpha);
            _buttonFocus.Show(visualControl, visualControl != null ? ScreenRect(visualControl, eventCamera) : default);
            RectTransform confirm = FirstDayRound > 0 ? _firstDaySecond :
                touch ? KitchenConfirmFocus : null;
            _confirmFocus.Show(confirm, confirm != null ? ScreenRect(confirm, eventCamera) : default);
            if (touch || visualControl != null && GuidesFirstDayButtonDuringNotice)
            {
                ShowTouchFocus(visualControl, eventCamera);
                return;
            }
            if (!moving)
            {
                HideCursor();
                return;
            }

            if (_cursor == null) return;
            Vector3 target = GetWaypointCenter(visual);
            Vector3 projected = waypointCamera.WorldToScreenPoint(target);
            Vector2 screen = projected;
            Rect visible = Screen.safeArea;
            float scale = _canvas.scaleFactor;
            CollectMarkerObstacles(eventCamera);

            float radiusOffset = (WaypointCursorSize - CursorSize) * .5f;
            Vector2 near = screen + Vector2.down * ((CursorNearHeight + radiusOffset) * scale);
            Vector2 far = screen + Vector2.down * ((CursorFarHeight + radiusOffset) * scale);
            float half = WaypointCursorSize * .5f * scale;
            Rect marker = Rect.MinMaxRect(near.x - half, far.y - half, near.x + half, near.y + half);
            if (projected.z <= 0 || !visible.Contains(screen))
            {
                PochaKitchen kitchen = PochaKitchen.Current;
                Rigidbody player = kitchen != null && kitchen.IsInside ? kitchen.InsideBody : _loader.PlayerBody;
                Vector2 origin = visible.center;
                if (player != null)
                {
                    Vector3 playerPosition = player.position;
                    playerPosition.y = target.y;
                    origin = waypointCamera.WorldToScreenPoint(playerPosition);
                }
                ShowDirectionCursor(screen, screen - origin, visible, eventCamera);
                return;
            }

            Vector2 aim = Vector2.up;
            if ((!visible.Contains(marker.min) || !visible.Contains(marker.max) || OverlapsHud(marker)) &&
                screen.y + half + (CursorFarHeight + radiusOffset) * scale < visible.yMax)
            {
                near = 2f * screen - near;
                far = 2f * screen - far;
                aim = Vector2.down;
            }
            Vector2 position = Vector2.Lerp(near, far, CursorPulse(Time.unscaledTime));
            position.x = Mathf.Clamp(position.x, visible.xMin + half, visible.xMax - half);
            position.y = Mathf.Clamp(position.y, visible.yMin + half, visible.yMax - half);
            Vector2 towardTarget = screen - position;
            if (towardTarget.sqrMagnitude > 1f) aim = towardTarget.normalized;
            ShowCursor(position, aim, WaypointCursorSize, eventCamera);
        }

        private Vector3 GetWaypointCenter(Transform visual)
        {
            if (_waypointVisual != visual)
            {
                _waypointVisual = visual;
                _waypointLocalCenter = visual.InverseTransformPoint(FindVisualCenter(visual));
            }
            return visual.TransformPoint(_waypointLocalCenter);
        }

        private static Vector3 FindVisualCenter(Transform visual)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled ||
                    renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly ||
                    renderer.GetComponent<TextMesh>() != null) continue;

                Bounds part = renderer.bounds;
                if (part.size.sqrMagnitude < .0001f) continue;
                if (hasBounds) bounds.Encapsulate(part);
                else bounds = part;
                hasBounds = true;
            }
            if (hasBounds) return bounds.center;
            if (visual.TryGetComponent(out Collider collider) && collider.bounds.size.sqrMagnitude >= .0001f)
                return collider.bounds.center;
            return visual.position + Vector3.up;
        }

        private Transform WorldVisual(Transform target)
        {
            if (target == null) return null;
            if (_market != null && target == _market.InteractionPoint) return _market.transform;
            return target.TryGetComponent(out ForestPortal portal) ? portal.TutorialVisual : target;
        }

        private void SetExhibit(Transform exhibit, Camera camera = null)
        {
            _exhibit = exhibit;
            _exhibitCamera = camera;
        }

        private void ShowTouchFocus(RectTransform touchTarget, Camera eventCamera)
        {
            Rect screen = ScreenRect(touchTarget, eventCamera);
            float pulse = CursorPulse(Time.unscaledTime);
            if (_cursor == null) return;

            // 기본은 오른쪽 아래에서 누른다. 가장자리 버튼은 화면 안쪽에서 가리킨다.
            Rect safe = Screen.safeArea;
            float reach = CursorSize * _canvas.scaleFactor;
            float x = screen.center.x + reach * 1.4f < safe.xMax ? 1f : -1f;
            float y = screen.center.y - reach * 1.23f > safe.yMin ? -1f : 1f;
            Vector2 approach = new Vector2(x, y).normalized;
            // 글자 중앙을 피하면서 손끝은 버튼 안을 향하게 둔다.
            Vector2 aim = screen.center + new Vector2(x * Mathf.Min(screen.width * .22f, reach * .33f),
                y * Mathf.Min(screen.height * .18f, reach * .14f));
            Vector2 position = aim + approach * (reach * .33f + Mathf.Lerp(3f, 18f, pulse) * _canvas.scaleFactor);
            ShowCursor(position, -approach, CursorSize, eventCamera);
        }

        internal static float CursorPulse(float time)
        {
            return (1f - Mathf.Cos(time * Mathf.PI * 2f / .9f)) * .5f;
        }

        private static Vector2 ScreenToLocal(RectTransform parent, Vector2 screen, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, eventCamera, out Vector2 local);
            return local;
        }

        private void ShowCursor(Vector2 screen, Vector2 direction, float size, Camera eventCamera)
        {
            Vector2 dimensions = Vector2.one * size;
            if (_cursor.sizeDelta != dimensions) _cursor.sizeDelta = dimensions;
            _cursor.gameObject.SetActive(true);
            _cursor.localPosition = ScreenToLocal((RectTransform)_cursor.parent, screen, eventCamera);
            _cursor.localRotation = Quaternion.Euler(0, 0, GetCursorAngle(direction, out float thumbScale));
            _cursor.localScale = new Vector3(thumbScale, 1f, 1f);
        }

        internal static float GetCursorAngle(Vector2 direction, out float thumbScale)
        {
            // 원본 손의 엄지는 검지 왼쪽에 있다. 검지가 왼쪽을 가리킬 때 좌우를 뒤집어 엄지를 위로 올린다.
            thumbScale = direction.x < 0f ? -1f : 1f;
            return Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
        }

        private void HideCursor()
        {
            if (_cursor != null) _cursor.gameObject.SetActive(false);
        }

        private Rect ScreenRect(RectTransform target, Camera eventCamera)
        {
            target.GetWorldCorners(_uiCorners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, _uiCorners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, _uiCorners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void CollectMarkerObstacles(Camera eventCamera)
        {
            _markerObstacles.Clear();
            foreach (Selectable control in _hudControls)
            {
                if (control != null) AddMarkerObstacle(control.transform as RectTransform, eventCamera);
            }
            RectTransform map = _hud.Find("MinimapHud/Minimap") as RectTransform;
            if (map == null) map = _hud.Find("Minimap") as RectTransform;
            AddMarkerObstacle(map, eventCamera);
            AddMarkerObstacle(_hud.Find("MinimapHud/MinimapToggle") as RectTransform, eventCamera);
            AddMarkerObstacle(_hud.Find("ForestHealth") as RectTransform, eventCamera);
            AddMarkerObstacle(_hud.Find("DayPhase") as RectTransform, eventCamera);
            AddMarkerObstacle(_hud.Find("ClockRow") as RectTransform, eventCamera);
            AddMarkerObstacle(_hud.Find("TimeCountdown") as RectTransform, eventCamera);
            AddMarkerObstacle(_hud.Find("PlayerResources") as RectTransform, eventCamera);
            AddMarkerObstacle(_quest.Card, eventCamera);
        }

        private void AddMarkerObstacle(RectTransform target, Camera eventCamera)
        {
            if (target != null && target.gameObject.activeInHierarchy)
                _markerObstacles.Add(ScreenRect(target, eventCamera));
        }

        private bool OverlapsHud(Rect marker)
        {
            foreach (Rect obstacle in _markerObstacles)
                if (marker.Overlaps(obstacle)) return true;
            return false;
        }

        private void ShowDirectionCursor(Vector2 targetScreen, Vector2 direction, Rect visible, Camera eventCamera)
        {
            // 회전한 손과 왕복 거리 전체가 안전영역 안에 들어오도록 여백을 잡는다.
            float scale = _canvas.scaleFactor;
            float half = (WaypointCursorSize * .71f + EdgeTravel + 4f) * scale;
            if (visible.width <= half * 2 || visible.height <= half * 2)
            {
                HideCursor();
                return;
            }

            // 직교 카메라의 화면 좌표는 깊이가 음수여도 반전되지 않는다.
            if (direction.sqrMagnitude < 1f) direction = Vector2.down;
            direction.Normalize();
            Vector2 center = visible.center;
            Vector2 space = visible.size * .5f - Vector2.one * half;
            Vector2 innerSpace = InnerEdgeSpace(visible.size, space);
            float edgeDistance = EdgeDistance(direction, space);
            float distance = edgeDistance;
            float blend = EdgeHudBlend * scale;
            foreach (Rect obstacle in _markerObstacles)
            {
                float clearDistance = ClearEdgeDistance(center, direction, edgeDistance, obstacle, half, blend);
                distance = Mathf.Min(distance, clearDistance);
            }

            // HUD를 피할 자리가 부족해도 손은 안쪽 경계 바깥에서 계속 이동한다.
            distance = Mathf.Max(distance, EdgeDistance(direction, innerSpace));
            Vector2 offset = direction * distance;
            Vector2 towardTarget = targetScreen - (center + offset);
            Vector2 aim = towardTarget.sqrMagnitude > 1f ? towardTarget.normalized : direction;
            float travel = EdgeTravel * CursorPulse(Time.unscaledTime) * scale;
            Vector2 position = center + KeepInEdgeBand(offset + aim * travel, innerSpace, direction);
            ShowCursor(position, aim, WaypointCursorSize, eventCamera);
        }

        private static Vector2 InnerEdgeSpace(Vector2 visibleSize, Vector2 edgeSpace)
        {
            Vector2 inset = Vector2.Scale(visibleSize, new Vector2(EdgeSideInset, EdgeVerticalInset));
            return Vector2.Min(visibleSize * .5f - inset, edgeSpace);
        }

        private static Vector2 KeepInEdgeBand(Vector2 offset, Vector2 innerSpace, Vector2 direction)
        {
            if (Mathf.Abs(offset.x) >= innerSpace.x || Mathf.Abs(offset.y) >= innerSpace.y) return offset;

            Vector2 outward = offset.sqrMagnitude > .0001f ? offset.normalized : direction;
            return outward * EdgeDistance(outward, innerSpace);
        }

        private static float EdgeDistance(Vector2 direction, Vector2 space)
        {
            float toX = direction.x != 0f ? space.x / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float toY = direction.y != 0f ? space.y / Mathf.Abs(direction.y) : float.PositiveInfinity;
            return Mathf.Min(toX, toY);
        }

        private static float ClearEdgeDistance(Vector2 center, Vector2 direction, float edgeDistance,
            Rect obstacle, float half, float blend)
        {
            Vector2 offset = obstacle.center - center;
            float forward = Vector2.Dot(offset, direction);
            if (forward <= 0f) return edgeDistance;

            // 방향선은 따라가고 HUD에 다가갈 때만 안쪽으로 들어갔다 나와 빈 자리에 고정되지 않게 한다.
            Vector2 extent = obstacle.size * .5f + Vector2.one * half;
            Vector2 axis = new Vector2(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            Vector2 sideways = new Vector2(-direction.y, direction.x);
            float gap = Mathf.Abs(Vector2.Dot(offset, sideways)) - (extent.x * axis.y + extent.y * axis.x);
            if (gap >= blend) return edgeDistance;

            float near = Mathf.Clamp(forward - Vector2.Dot(extent, axis) - 1f, 0f, edgeDistance);
            return Mathf.SmoothStep(near, edgeDistance, gap / blend);
        }

        private void RestoreCamera()
        {
            if (!_panning && !_firstDayOutdoorFrame) return;
            _panning = false;
            _firstDayOutdoorFrame = false;
            if (_camera != null)
            {
                _camera.transform.localPosition = _cameraLocal;
                _camera.orthographicSize = _cameraSize;
            }

            _panRoutine = null;
            _panGround = null;
            if (_guideGroup != null) _guideGroup.alpha = 1f;
            Debug.Log($"[UI] frame={Time.frameCount} event=tutorial_camera result=restored", this);
        }

        private void OnDestroy()
        {
            _buttonFocus?.Dispose();
            _confirmFocus?.Dispose();
            if (Current == this) Current = null;
        }

        private void OnDisable()
        {
            StopForestDiscoveryCamera(completed: false);
            InteractionOutline.ShowTutorial(null);
            _buttonFocus?.Show(null, default);
            _confirmFocus?.Show(null, default);
            if (_panRoutine != null) StopCoroutine(_panRoutine);
            RestoreCamera();
            if (_loader != null) _loader.SetTutorialMovementBlocked(false);
            if (_clock != null) _clock.SetTutorialPaused(false);
            _clock?.SetTutorialOpeningHold(false);
            _clock?.SetTutorialSleepHold(false);
            if (_joystickTouch != null) _joystickTouch.Pressed -= OnJoystickPressed;
            if (_root != null) _root.SetActive(false);
        }
    }
}
