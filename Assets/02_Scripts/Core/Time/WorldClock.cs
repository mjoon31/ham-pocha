using UnityEngine;
using HamPocha.Gameplay;

namespace HamPocha.Core
{
    internal enum LightingArea { Outdoors, Home, PochaInterior }

    public class WorldClock : MonoBehaviour
    {
        public const int MorningHour = 6;
        public const int OpeningHour = 12;
        public const int SunsetHour = 17;
        public const int EveningHour = 21;
        private const int FullSunsetHour = 19;
        private const float TutorialOpeningDeadline = 18f * 3600f;
        private const float TutorialSleepDeadline = 5f * 3600f;
        private const float SecondsPerDay = 86400f;
        private const float SecondsPerHour = 3600f;
        private const float LightingTransitionSeconds = .85f;

        private struct LightingTransition
        {
            public bool Initialized;
            public int Phase;
            public double StartedAt;
            public Vector2 From;
            public Vector2 To;
        }

        [Header("World Time Settings")]
        [Tooltip("게임 하루에 해당하는 실제 시간입니다. 타이밍 SO를 우선 적용합니다.")]
        [SerializeField, Min(1f)] private float _realSecondsPerDay = 600f;
        [SerializeField] private BusinessPacing _pacing;
        [SerializeField, Min(1)] private int _startDay = 1;

        private float _secondsIntoDay;
        private GameSessionState _session;
        private bool _hasFocus = true;
        private bool _isApplicationPaused;
        private bool _tutorialPaused;
        private bool _tutorialOpeningHold;
        private bool _tutorialSleepHold;
        private bool _skipNextTick = true;
        // 임시 확인 모드는 조명과 그림자만 바꾸고 실제 시간과 저장에는 쓰지 않는다.
        private float? _lightingPreviewProgress;
        private LightingTransition _worldLighting;
        private LightingTransition _pochaLighting;

        private int _resumeAfterFrame;
        public bool CanAdvanceTimers { get { return CanAdvance && Time.frameCount > _resumeAfterFrame; } }

        public int DayNumber { get; private set; }
        public int BusinessDayNumber => Hour < MorningHour ? Mathf.Max(1, DayNumber - 1) : DayNumber;

        public bool IsPaused { get; private set; }

        public bool CanAdvance
        {
            get { return CanAdvanceOutsideTutorial && !_tutorialPaused &&
                (IsDaytime || _session == null || !_session.HasPendingBusinessSummary); }
        }

        internal bool IsApplicationActive => _hasFocus && !_isApplicationPaused;
        internal bool CanAdvanceOutsideTutorial => !IsPaused && IsApplicationActive;
        internal bool AtTutorialOpeningDeadline => _tutorialOpeningHold && DayNumber == 1 &&
            _secondsIntoDay >= TutorialOpeningDeadline;

        public int Hour
        {
            get { return Mathf.FloorToInt(_secondsIntoDay / SecondsPerHour); }
        }

        public int Minute
        {
            get { return Mathf.FloorToInt(_secondsIntoDay / 60f) % 60; }
        }

        public float DayProgress
        {
            get { return _secondsIntoDay / SecondsPerDay; }
        }

        internal bool HoldsBusinessSunset => !IsDaytime && _session != null && !_session.ClosedBusinessManually &&
            (_session.CurrentActivity == PlayerActivity.Business || _session.HasPendingBusinessSummary);
        internal bool ClosedBusinessManually => _session != null && _session.ClosedBusinessManually;

        internal static bool IsForestAttackHour(int hour) =>
            (hour >= EveningHour && hour < 24) || (hour >= 0 && hour < MorningHour);

        internal bool IsForestCombatTime => IsForestAttackHour(Hour);
        internal bool IsOutdoorEvening => !_lightingPreviewProgress.HasValue && Hour >= EveningHour;

        internal bool IsSunset(LightingArea area) => Hour >= SunsetHour || KeepsBusinessSunset(area);

        private bool KeepsBusinessSunset(LightingArea area) =>
            area == LightingArea.PochaInterior && HoldsBusinessSunset;

        internal float GetSunsetLightingWeight(LightingArea area, float dayWeight)
        {
            if (KeepsBusinessSunset(area)) return 1f;
            if (!IsDaytime) return dayWeight;
            float rise = Mathf.InverseLerp(SunsetHour * SecondsPerHour, FullSunsetHour * SecondsPerHour, _secondsIntoDay);
            return Mathf.SmoothStep(0f, 1f, rise);
        }

        internal int GetLightingPhase(LightingArea area)
        {
            if (_lightingPreviewProgress.HasValue) return DayShadows.PhaseAt(_lightingPreviewProgress.Value);
            if (KeepsBusinessSunset(area)) return 2;
            if (!IsDaytime) return -1;
            float morning = MorningHour * SecondsPerHour;
            float daylight = (_secondsIntoDay - morning) / (SecondsPerDay - morning);
            return DayShadows.PhaseAt(daylight * .5f);
        }

        public int AdvanceLightingPreview()
        {
            int phase = GetLightingPhase(LightingArea.Outdoors);
            int next = phase < 0 ? 0 : phase + 1;
            _lightingPreviewProgress = (next + .5f) / 6f;
            return next;
        }

        public void ClearLightingPreview()
        {
            _lightingPreviewProgress = null;
        }

        internal void GetLightingBlend(LightingArea area, out float phasePosition, out float dayWeight)
        {
            int phase = GetLightingPhase(area);
            double now = Time.unscaledTimeAsDouble;
            Vector2 blend = area == LightingArea.PochaInterior
                ? EvaluateLightingBlend(ref _pochaLighting, phase, now)
                : EvaluateLightingBlend(ref _worldLighting, phase, now);
            phasePosition = blend.x;
            dayWeight = blend.y;
            if (_lightingPreviewProgress.HasValue) return;
            if (area == LightingArea.Home && !IsDaytime) dayWeight = 0f;
            if (area != LightingArea.Outdoors) return;

            // 야외는 자정까지 낮 조명 비중을 줄여 00:00에 밝기가 되돌아오지 않게 한다.
            if (!IsDaytime) dayWeight = 0f;
            else if (IsOutdoorEvening)
            {
                float fade = Mathf.InverseLerp(EveningHour * SecondsPerHour, SecondsPerDay, _secondsIntoDay);
                dayWeight *= 1f - Mathf.SmoothStep(0f, 1f, fade);
            }
        }

        private static Vector2 EvaluateLightingBlend(ref LightingTransition transition, int targetPhase, double now)
        {
            if (!transition.Initialized)
            {
                transition.Initialized = true;
                transition.Phase = targetPhase;
                transition.From = transition.To = new Vector2(Mathf.Max(0, targetPhase), targetPhase < 0 ? 0f : 1f);
                transition.StartedAt = now;
                return transition.To;
            }

            float elapsed = (float)(now - transition.StartedAt);
            float t = Mathf.SmoothStep(0f, 1f, elapsed / LightingTransitionSeconds);
            Vector2 current = Vector2.Lerp(transition.From, transition.To, t);
            if (targetPhase == transition.Phase) return current;

            if (targetPhase >= 0 && current.y == 0f) current.x = targetPhase;
            transition.From = current;
            transition.To = targetPhase < 0 ? new Vector2(current.x, 0f) : new Vector2(targetPhase, 1f);
            transition.StartedAt = now;
            transition.Phase = targetPhase;
            return current;
        }

        public bool IsDaytime
        {
            get { return Hour >= MorningHour; }
        }

        private void Awake()
        {
            _session = GetComponent<GameSessionState>();
            DayNumber = _startDay;
            _secondsIntoDay = MorningHour * SecondsPerHour;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Gameplay] [frame={Time.frameCount}] 월드 시간 초기화: 준비 완료. event=clock_initialize result=ready day={DayNumber} hour={Hour} real_seconds_per_day={_realSecondsPerDay}", this);
            }
        }

        private void Update()
        {
            if (!CanAdvanceTimers)
            {
                return;
            }

            // 복귀 첫 프레임의 긴 deltaTime으로 백그라운드 시간을 한꺼번에 더하지 않는다.
            if (_skipNextTick)
            {
                _skipNextTick = false;

                return;
            }

            // 전면 플레이 중에는 주문 타이머와 같이 테스트 배속을 적용한다.
            float daySeconds = _pacing != null ? _pacing.Current.WorldDaySeconds : _realSecondsPerDay;
            float worldSecondsPerGameSecond = SecondsPerDay / daySeconds;
            Advance(Time.deltaTime * worldSecondsPerGameSecond);
        }

        private void Advance(float worldSeconds)
        {
            bool wasDaytime = IsDaytime;
            bool wasForestCombatTime = IsForestCombatTime;
            int previousBusinessDay = BusinessDayNumber;
            float previousTime = _secondsIntoDay;
            _secondsIntoDay += worldSeconds;
            if (_tutorialOpeningHold && DayNumber == 1)
                _secondsIntoDay = Mathf.Min(_secondsIntoDay, TutorialOpeningDeadline);

            if (_tutorialSleepHold)
            {
                float deadline = wasDaytime ? SecondsPerDay + TutorialSleepDeadline : TutorialSleepDeadline;
                if (_secondsIntoDay >= deadline)
                {
                    _secondsIntoDay = deadline;
                    if (previousTime != deadline)
                        Debug.Log("[Gameplay] event=tutorial_sleep_clock result=held hour=05:00", this);
                }
            }

            while (_secondsIntoDay >= SecondsPerDay)
            {
                _secondsIntoDay -= SecondsPerDay;
                DayNumber++;

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[Gameplay] [frame={Time.frameCount}] 월드 날짜 진행: 진행 완료. event=world_day result=advanced day={DayNumber}", this);
                }
            }
            if (wasDaytime != IsDaytime && Debug.isDebugBuild)
                Debug.Log($"[Gameplay] event=daylight_period result=changed daytime={IsDaytime}", this);
            if (wasForestCombatTime != IsForestCombatTime && Debug.isDebugBuild)
                Debug.Log($"[Gameplay] event=forest_combat_time result=changed active={IsForestCombatTime} hour={Hour:00}", this);
            if (previousBusinessDay != BusinessDayNumber && Debug.isDebugBuild)
                Debug.Log($"[Gameplay] event=business_day result=advanced day={BusinessDayNumber}", this);
        }

        public bool TryAdvanceToOpeningTime() => TryAdvanceForTest(OpeningHour);

        internal bool TryAdvanceForTest(int hour)
        {
            if ((!Application.isEditor && !Debug.isDebugBuild) || !CanAdvance ||
                (hour != 0 && hour != OpeningHour)) return false;

            ClearLightingPreview();
            float target = hour * SecondsPerHour;
            if (_secondsIntoDay == target) return false;
            if (_secondsIntoDay > target) DayNumber++;
            _secondsIntoDay = target;

            Debug.Log($"[Gameplay] frame={Time.frameCount} event=test_clock_jump result=advanced day={DayNumber} hour={hour:00}", this);
            return true;
        }

        public void AdvanceToMorning()
        {
            if (IsDaytime) return;
            SetTutorialSleepHold(false);
            _secondsIntoDay = MorningHour * SecondsPerHour;
            Debug.Log($"[Gameplay] event=business_day result=advanced reason=sleep day={BusinessDayNumber}", this);
        }

        public void SetPaused(bool isPaused)
        {
            if (IsPaused == isPaused)
            {
                return;
            }

            IsPaused = isPaused;

            if (!isPaused)
            {
                _skipNextTick = true;
                _resumeAfterFrame = Time.frameCount + 1;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Gameplay] [frame={Time.frameCount}] 월드 시간 일시정지 상태: 변경. event=clock_pause result=changed paused={IsPaused}", this);
            }
        }

        internal void SetTutorialPaused(bool paused)
        {
            if (_tutorialPaused == paused) return;

            _tutorialPaused = paused;
            if (!paused)
            {
                _skipNextTick = true;
                _resumeAfterFrame = Time.frameCount + 1;
            }
            if (Debug.isDebugBuild)
                Debug.Log($"[Gameplay] frame={Time.frameCount} event=tutorial_pause result=changed paused={paused}", this);
        }

        internal void SetTutorialOpeningHold(bool hold)
        {
            if (_tutorialOpeningHold == hold) return;
            _tutorialOpeningHold = hold;
            Debug.Log($"[Gameplay] event=tutorial_opening_clock result=changed enabled={hold}", this);
        }

        internal void SetTutorialSleepHold(bool hold)
        {
            if (_tutorialSleepHold == hold) return;
            _tutorialSleepHold = hold;
            Debug.Log($"[Gameplay] event=tutorial_sleep_clock result=changed enabled={hold}", this);
        }

        public float SecondsIntoDay { get { return _secondsIntoDay; } }

        public void Restore(int day, float secondsIntoDay)
        {
            ClearLightingPreview();
            _worldLighting = default;
            _pochaLighting = default;
            DayNumber = day;
            _secondsIntoDay = secondsIntoDay;
            _skipNextTick = true;
            _resumeAfterFrame = Time.frameCount + 1;
            Debug.Log($"[Save] frame={Time.frameCount} event=clock_restore result=success", this);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (_hasFocus == hasFocus)
            {
                return;
            }

            _hasFocus = hasFocus;

            if (hasFocus)
            {
                _skipNextTick = true;
                _resumeAfterFrame = Time.frameCount + 1;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[App] [frame={Time.frameCount}] 앱 포커스 상태: 변경. event=clock_focus result=changed focused={hasFocus}", this);
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (_isApplicationPaused == isPaused)
            {
                return;
            }

            _isApplicationPaused = isPaused;

            if (!isPaused)
            {
                _skipNextTick = true;
                _resumeAfterFrame = Time.frameCount + 1;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[App] [frame={Time.frameCount}] 앱 정지 상태: 변경. event=clock_application_pause result=changed paused={isPaused}", this);
            }
        }

        private void OnValidate()
        {
            _realSecondsPerDay = Mathf.Max(1f, _realSecondsPerDay);
            _startDay = Mathf.Max(1, _startDay);
        }
    }
}
