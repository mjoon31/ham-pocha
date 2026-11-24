using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using HamPocha.Audio;
using HamPocha.UI;

namespace HamPocha.Core
{
    // Main의 공통 시스템 초기화가 끝난 뒤 최초 지역 로드를 요청한다.
    public class MainSceneLoader : MonoBehaviour
    {
        [SerializeField] private SceneLoadManager _sceneLoader;
        [SerializeField] private SceneType _startScene = SceneType.Home;
        private bool _lostFocus;
        private bool _wasPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStartupFrameRate()
        {
            ApplyFrameRate("startup", null);
        }

        private void Awake()
        {
            // 상태 로그는 유지하되 일반 정보 로그의 호출 스택 생성 비용은 줄인다.
            // Warning/Error/Exception의 스택 설정은 바꾸지 않는다.
            if (!Application.isEditor) Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            ApplyFrameRate("main_awake", this);
        }

        private static void ApplyFrameRate(string reason, Object context)
        {
            int previous = Application.targetFrameRate;
            Application.targetFrameRate = 60;

            // 목표 설정과 기기의 표시 조건을 구분해서 기록한다. 실측 FPS는 아니다.
            Debug.Log($"[Performance] frame={Time.frameCount} event=frame_rate result=configured reason={reason} previous={previous} target={Application.targetFrameRate} refresh_hz={Screen.currentResolution.refreshRateRatio.value:F2} render_interval={OnDemandRendering.renderFrameInterval}", context);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _lostFocus = true;
                return;
            }

            if (!_lostFocus) return;

            _lostFocus = false;
            ApplyFrameRate("focus_returned", this);
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                _wasPaused = true;
                return;
            }

            if (!_wasPaused) return;

            _wasPaused = false;
            ApplyFrameRate("app_resumed", this);
        }

        private IEnumerator Start()
        {
            if (_sceneLoader == null)
            {
                Debug.LogError($"[Scene] [frame={Time.frameCount}] 시작 지역 불러오기: 실패. 씬 로더가 연결되지 않았습니다. event=boot result=failed reason=missing_scene_loader", this);
                enabled = false;

                yield break;
            }

            if (GetComponent<HamPocha.UI.TutorialUi>() == null) gameObject.AddComponent<HamPocha.UI.TutorialUi>();
            if (GetComponent<HamPocha.Gameplay.PochaKitchen>() == null)
                gameObject.AddComponent<HamPocha.Gameplay.PochaKitchen>().Initialize(_sceneLoader);
            yield return null;
            Bgm.Attach(transform, _sceneLoader);

            LoadingSceneController loadingScreen = FindObjectOfType<LoadingSceneController>();
            if (CloudSave.Instance != null && CloudSave.Instance.IsActiveSession)
            {
                GameSave save = _sceneLoader.GetComponent<GameSave>();
                if (save == null) save = _sceneLoader.gameObject.AddComponent<GameSave>();

                yield return save.Boot(_sceneLoader, _startScene);
                if (loadingScreen == null && save.IsReady) _sceneLoader.SetStartupLoading(false);
                yield break;
            }

            _sceneLoader.SetStartupLoading(loadingScreen != null && loadingScreen.IsLoading);
            _sceneLoader.ChangeScene(_startScene);
        }
    }
}
