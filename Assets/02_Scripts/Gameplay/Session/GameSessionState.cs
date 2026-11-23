using HamPocha.Audio;
using HamPocha.Core;
using HamPocha.Data;
using UnityEngine;

namespace HamPocha.Gameplay
{
    public enum PlayerActivity
    {
        FreeRoam,
        Exploration,
        Business,
        Sleeping
    }

    public enum DailyBusinessState
    {
        NotOpened,
        Open,
        LastOrder,
        Closed
    }

    public class GameSessionState : MonoBehaviour
    {
        [SerializeField] private WorldClock _worldClock;
        [SerializeField] private Fatigue _fatigue;
        [SerializeField] private BusinessPacing _pacing;

        public const int OpeningHour = WorldClock.OpeningHour;
        public const int ClosingHour = 24;
        public const int ExplorationEntryCost = 10;
        public const int BusinessStartCost = 20;

        public int LastOrderMinute { get { return ClosingHour * 60; } }
        public string BusinessHoursText => "12:00~24:00 · 하루 한 번 개점";
        public int CompletedExplorations { get; private set; }
        public int CompletedBusinesses { get; private set; }
        public int BusinessSequence { get; private set; }
        public string Nickname { get; private set; } = "";
        private CookingStation _station;
        private int _openedBusinessDay;

        internal bool CanTakeNewOrders => CurrentActivity == PlayerActivity.Business &&
            BusinessState == DailyBusinessState.Open && _worldClock.IsDaytime;
        internal bool IsClosing => CurrentActivity == PlayerActivity.Business && BusinessState == DailyBusinessState.LastOrder;
        internal bool CanOpenAtCurrentTime => _worldClock.Hour >= OpeningHour &&
            _openedBusinessDay != _worldClock.BusinessDayNumber;
        internal bool HasOpenedBusinessToday => _openedBusinessDay == _worldClock.BusinessDayNumber;
        internal string OpeningStatus
        {
            get
            {
                if (_openedBusinessDay == _worldClock.BusinessDayNumber || !_worldClock.IsDaytime) return "오늘 영업 종료";
                if (_worldClock.Hour < OpeningHour) return "아직 개점 전";
                return "개점 가능";
            }
        }

        // 현재 지역은 SceneLoadManager가 관리한다. 지역 변경이 하루 영업 기록을 초기화하지 않는다.
        public PlayerActivity CurrentActivity { get; private set; }

        public DailyBusinessState BusinessState { get; private set; }

        public int StateVersion { get; private set; }

        public string ActivityDisplayName
        {
            get
            {
                switch (CurrentActivity)
                {
                    case PlayerActivity.FreeRoam: return "자유 활동";
                    case PlayerActivity.Exploration: return "탐험";
                    case PlayerActivity.Business: return "영업";
                    case PlayerActivity.Sleeping: return "취침";
                    default: return "활동";
                }
            }
        }

        public string BusinessDisplayName
        {
            get
            {
                switch (BusinessState)
                {
                    case DailyBusinessState.NotOpened: return OpeningStatus;
                    case DailyBusinessState.Open: return "영업 중";
                    case DailyBusinessState.LastOrder: return "주문 접수 마감";
                    case DailyBusinessState.Closed: return "영업 종료";
                    default: return "영업";
                }
            }
        }

        private void Awake()
        {
            _station = GetComponent<CookingStation>();

            if (_worldClock == null || _fatigue == null || _pacing == null)
            {
                Debug.LogError($"[Gameplay] [frame={Time.frameCount}] 게임 진행 상태 초기화 실패. 시계·피로도·진행 속도 참조를 확인하세요. event=session_initialize result=failed reason=missing_reference", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!_worldClock.CanAdvanceTimers) return;
            if (BusinessState == DailyBusinessState.Closed && _openedBusinessDay != _worldClock.BusinessDayNumber)
                SetBusinessState(DailyBusinessState.NotOpened);

            if (CurrentActivity != PlayerActivity.Business) return;
            if (BusinessState == DailyBusinessState.Open && !_worldClock.IsDaytime)
            {
                SetBusinessState(DailyBusinessState.LastOrder);
                PochaKitchen.Current?.ReleaseUnacceptedOrders();
            }
            if (IsClosing && (_station == null || !_station.HasAcceptedOrders()))
                FinishBusiness();
        }

        public SessionSaveData Capture()
        {
            return new SessionSaveData { Activity = CurrentActivity, BusinessState = BusinessState,
                CompletedExplorations = CompletedExplorations, CompletedBusinesses = CompletedBusinesses,
                BusinessSequence = BusinessSequence, BusinessDay = _worldClock.BusinessDayNumber,
                OpenedBusinessDay = _openedBusinessDay, HasDailySchedule = true, Nickname = Nickname };
        }

        public void Restore(SessionSaveData data)
        {
            CurrentActivity = data.Activity;
            BusinessState = data.BusinessState;
            CompletedExplorations = data.CompletedExplorations;
            CompletedBusinesses = data.CompletedBusinesses;
            BusinessSequence = data.BusinessSequence;
            _openedBusinessDay = data.OpenedBusinessDay;
            if (!data.HasDailySchedule)
            {
                bool running = CurrentActivity == PlayerActivity.Business;
                RestoreLegacyClock(running);
                BusinessState = running ? DailyBusinessState.Open : DailyBusinessState.NotOpened;
                _openedBusinessDay = running ? _worldClock.BusinessDayNumber : 0;
            }
            else if (CurrentActivity == PlayerActivity.Business && _openedBusinessDay <= 0)
                _openedBusinessDay = data.BusinessDay;
            Nickname = SessionSaveData.TryNormalizeNickname(data.Nickname, out string nickname) ? nickname : "";
            _fatigue.SetActivity(CurrentActivity);
            StateVersion++;
            Debug.Log("[Save] event=session_restore result=success", this);
        }

        private void RestoreLegacyClock(bool running)
        {
            int hour = running ? OpeningHour : WorldClock.MorningHour;
            _worldClock.Restore(_worldClock.DayNumber, hour * 3600f);
            Debug.Log($"[Save] event=daily_schedule_migration result=restored active_business={running}", this);
        }

        public bool TrySetNickname(string value, bool rename = false)
        {
            bool hasNickname = Nickname.Length != 0;
            if (hasNickname != rename)
            {
                Debug.Log($"[Gameplay] event=nickname_set result=rejected reason=unexpected_existing_nickname renaming={rename}", this);
                return false;
            }
            if (!SessionSaveData.TryNormalizeNickname(value, out string nickname))
            {
                Debug.Log($"[Gameplay] event=nickname_set result=rejected reason=invalid_length renaming={rename}", this);
                return false;
            }
            if (Nickname == nickname)
            {
                Debug.Log("[Gameplay] event=nickname_set result=unchanged renaming=True", this);
                return true;
            }

            Nickname = nickname;
            StateVersion++;
            Debug.Log($"[Gameplay] event=nickname_set result=accepted renaming={rename}", this);
            return true;
        }

        public bool CanEnterRegion(SceneType region, out string message)
        {
            if (CurrentActivity == PlayerActivity.Business)
            {
                message = "영업을 종료한 뒤 다른 구역으로 이동하세요";
                return false;
            }

            bool isForest = SceneLoadManager.IsForest(region);

            CookingStation station = GetComponent<CookingStation>();
            int forestIndex = (int)region - (int)SceneType.Forest01;
            if (isForest && station != null && !station.IsForestOwned(forestIndex))
            {
                message = region == SceneType.Forest04 ? Growth.WinterForestRequirement : "해당 숲 재료를 사용하는 음식을 해금하세요";
                return false;
            }

            if (isForest && !_fatigue.CanSpend(ExplorationEntryCost))
            {
                message = "탐험을 시작하려면 피로 10이 필요합니다.";

                return false;
            }

            message = string.Empty;

            return true;
        }

        public bool TryEnterRegion(SceneType region, out string message)
        {
            if (!CanEnterRegion(region, out message))
            {
                return false;
            }

            if (CurrentActivity == PlayerActivity.Sleeping)
            {
                return true;
            }

            switch (region)
            {
                case SceneType.Forest01:
                case SceneType.Forest02:
                case SceneType.Forest03:
                case SceneType.Forest04:
                    // 재입장마다 자원이 복구되므로 숲에 들어갈 때마다 입장 비용을 지불한다.
                    if (!_fatigue.TrySpend(ExplorationEntryCost, "exploration_entry"))
                    {
                        message = "탐험을 시작하려면 피로 10이 필요합니다.";

                        return false;
                    }

                    SetActivity(PlayerActivity.Exploration);

                    break;

                default:
                    if (CurrentActivity == PlayerActivity.Exploration)
                    {
                        CompletedExplorations = 1;
                        Debug.Log("[Gameplay] event=activity_completed type=exploration result=recorded", this);
                    }
                    SetActivity(PlayerActivity.FreeRoam);

                    break;
            }

            return true;
        }

        public bool TryStartBusiness(SceneType region, out string message)
        {
            message = string.Empty;
            if (region != SceneType.Village || CurrentActivity != PlayerActivity.FreeRoam)
            {
                message = "마을에서 현재 활동을 마친 뒤 개점하세요";
                return false;
            }
            if (!CanOpenAtCurrentTime)
            {
                message = OpeningStatus;
                Debug.Log($"[Gameplay] event=business_open result=rejected reason=schedule hour={_worldClock.Hour}", this);
                return false;
            }
            if (_station == null || _station.SummaryPending || !(PochaKitchen.Current != null && !PochaKitchen.Current.UsesLegacyIntro ?
                PochaKitchen.Current.HasOpeningStock() : _station.HasRawRecipeStock()))
            {
                message = "결산을 확인하고, 조리 가능한 원재료를 준비하세요";
                return false;
            }

            if (!_fatigue.TrySpend(BusinessStartCost, "business_start"))
            {
                message = $"영업을 시작하려면 피로 {BusinessStartCost}이 필요합니다.";

                return false;
            }

            BusinessSequence++;
            _worldClock.SetTutorialOpeningHold(false);
            _openedBusinessDay = _worldClock.BusinessDayNumber;
            SetBusinessState(DailyBusinessState.Open);
            SetActivity(PlayerActivity.Business);
            _station.BeginBusiness();
            Sfx.Play(SfxCue.BusinessOpen);
            message = "영업을 시작했습니다.";

            return true;
        }

        public bool TryStopBusiness(out string message)
        {
            if (CurrentActivity != PlayerActivity.Business ||
                (BusinessState != DailyBusinessState.Open && BusinessState != DailyBusinessState.LastOrder))
            {
                message = "영업 중이 아닙니다.";

                return false;
            }

            if (_station != null && _station.HasCarriedFood)
            {
                message = "들고 있는 음식을 서빙하거나 지정 장소에서 폐기하세요";
                return false;
            }
            if (!CanTakeNewOrders)
            {
                message = "새 주문 종료 · 남은 주문 처리 중";
                return false;
            }

            FinishBusiness();
            message = "영업을 종료했습니다. 결산을 확인하세요";
            return true;
        }

        private void FinishBusiness()
        {
            bool midnight = IsClosing;
            SetBusinessState(DailyBusinessState.Closed);
            SetActivity(PlayerActivity.FreeRoam);
            if (_station != null) _station.CompleteBusinessIfNeeded(midnight);
            CompletedBusinesses = 1;
            StateVersion++;
            Sfx.Play(SfxCue.BusinessClose);
        }

        public bool CanSleep(SceneType region, out string message)
        {
            if (region != SceneType.Home)
            {
                message = "집의 침대에서 취침하세요";

                return false;
            }

            if (CurrentActivity != PlayerActivity.FreeRoam)
            {
                message = "현재 활동을 마친 뒤 취침하세요";

                return false;
            }

            if (_worldClock.IsDaytime)
            {
                message = "자정이 지나야 잘 수 있어요";
                return false;
            }

            message = string.Empty;

            return true;
        }

        public bool TryBeginSleep(SceneType region, out string message)
        {
            if (!CanSleep(region, out message))
            {
                return false;
            }

            SetActivity(PlayerActivity.Sleeping);

            return true;
        }

        public void CompleteSleep()
        {
            if (CurrentActivity != PlayerActivity.Sleeping)
            {
                return;
            }

            // 회복과 새 영업일 이동은 같은 호출에서 확정한다.
            CompletedExplorations = 0;
            CompletedBusinesses = 0;
            _fatigue.RestoreFromSleep();
            _worldClock.AdvanceToMorning();
            if (_station != null) _station.GrantTutorialSleepReward();
            SetBusinessState(DailyBusinessState.NotOpened);
            SetActivity(PlayerActivity.FreeRoam);
            Debug.Log("[Gameplay] event=sleep_completed result=recovered activity_counts=reset", this);
        }

        public void CancelSleep()
        {
            if (CurrentActivity == PlayerActivity.Sleeping) SetActivity(PlayerActivity.FreeRoam);
        }

        private void SetActivity(PlayerActivity activity)
        {
            if (CurrentActivity == activity)
            {
                return;
            }

            CurrentActivity = activity;
            _fatigue.SetActivity(activity);
            StateVersion++;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Gameplay] [frame={Time.frameCount}] 플레이어 활동 상태: 변경. event=player_activity result=changed activity={activity}", this);
            }
        }

        private void SetBusinessState(DailyBusinessState state)
        {
            if (BusinessState == state)
            {
                return;
            }

            BusinessState = state;
            StateVersion++;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Gameplay] [frame={Time.frameCount}] 하루 영업 상태: 변경. event=business_state result=changed time={_worldClock.Hour:00}:{_worldClock.Minute:00} state={state}", this);
            }
        }
    }
}
