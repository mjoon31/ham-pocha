using UnityEngine;
using HamPocha.Gameplay;

namespace HamPocha.Core
{
    public class WorldClock : MonoBehaviour
    {
        public const int MorningHour = 6;
        public const int OpeningHour = 12;
        private const float TutorialOpeningDeadline = 18f * 3600f;
        private const float SecondsPerDay = 86400f;
        private const float SecondsPerHour = 3600f;
        private const float LightingTransitionSeconds = .85f;
        private const int MissingLightingPhase = -2;

        [Header("World Time Settings")]
        [Tooltip("게임 하루에 해당하는 실제 시간입니다. 타이밍 SO를 우선 적용합니다.")]
        [SerializeField, Min(1f)] private float _realSecondsPerDay = 600f;
        [SerializeField] private BusinessPacing _pacing;
        [SerializeField, Min(1)] private int _startDay = 1;

        private float _secondsIntoDay;
        private bool _hasFocus = true;
        private bool _isApplicationPaused;
        private bool _tutorialPaused;
        private bool _tutorialOpeningHold;
        private bool _skipNextTick = true;
        // 임시 확인 모드는 조명과 그림자만 바꾸고 실제 시간과 저장에는 쓰지 않는다.
        private float? _lightingPreviewProgress;
        private int _lightingTargetPhase = MissingLightingPhase;
        private double _lightingTransitionStart;
        // x는 그림자 방향 위치, y는 낮 조명과 그림자의 비중이다.
        private Vector2 _lightingFrom;
        private Vector2 _lightingTo;

        private int _resumeAfterFrame;
        public bool CanAdvanceTimers { get { return CanAdvance && Time.frameCount > _resumeAfterFrame; } }

        public int DayNumber { get; private set; }
        public int BusinessDayNumber => Hour < MorningHour ? Mathf.Max(1, DayNumber - 1) : DayNumber;

        public bool IsPaused { get; private set; }

        public bool CanAdvance
        {
            get { return CanAdvanceOutsideTutorial && !_tutorialPaused; }
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

        public float LightingProgress
        {
            get
            {
                float morning = MorningHour * SecondsPerHour;
                float daylight = (_secondsIntoDay - morning) / (SecondsPerDay - morning);
                return _lightingPreviewProgress ?? (IsDaytime ? daylight * .5f : .5f);
            }
        }

        public int AdvanceLightingPreview()
        {
            int phase = DayShadows.PhaseAt(LightingProgress);
            int next = phase < 0 ? 0 : phase + 1;
            _lightingPreviewProgress = (next + .5f) / 6f;
            return next;
        }

        public void ClearLightingPreview()
        {
            _lightingPreviewProgress = null;
        }

        public void GetLightingBlend(out float phasePosition, out float dayWeight)
        {
            Vector2 blend = EvaluateLightingBlend(DayShadows.PhaseAt(LightingProgress), Time.unscaledTimeAsDouble);
            phasePosition = blend.x;
            dayWeight = blend.y;
        }

        private Vector2 EvaluateLightingBlend(int targetPhase, double now)
        {
            if (_lightingTargetPhase == MissingLightingPhase)
            {
                _lightingTargetPhase = targetPhase;
                _lightingFrom = _lightingTo = new Vector2(Mathf.Max(0, targetPhase), targetPhase < 0 ? 0f : 1f);
                _lightingTransitionStart = now;
                return _lightingTo;
            }

            float elapsed = (float)(now - _lightingTransitionStart);
            float t = Mathf.SmoothStep(0f, 1f, elapsed / LightingTransitionSeconds);
            Vector2 current = Vector2.Lerp(_lightingFrom, _lightingTo, t);
            if (targetPhase == _lightingTargetPhase) return current;

            if (targetPhase >= 0 && current.y == 0f) current.x = targetPhase;
            _lightingFrom = current;
            _lightingTo = targetPhase < 0 ? new Vector2(current.x, 0f) : new Vector2(targetPhase, 1f);
            _lightingTransitionStart = now;
            _lightingTargetPhase = targetPhase;
            return current;
        }

        public bool IsDaytime
        {
            get { return Hour >= MorningHour; }
        }

        private void Awake()
        {
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
            int previousBusinessDay = BusinessDayNumber;
            _secondsIntoDay += worldSeconds;
            if (_tutorialOpeningHold && DayNumber == 1)
                _secondsIntoDay = Mathf.Min(_secondsIntoDay, TutorialOpeningDeadline);

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

        public float SecondsIntoDay { get { return _secondsIntoDay; } }

        public void Restore(int day, float secondsIntoDay)
        {
            ClearLightingPreview();
            _lightingTargetPhase = MissingLightingPhase;
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
