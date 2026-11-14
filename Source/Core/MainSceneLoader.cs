using UnityEngine;
using System.Collections;
using HamPocha.UI;

namespace HamPocha.Core
{
    // Main의 공통 시스템 초기화가 끝난 뒤 최초 지역 로드를 요청한다.
    public class MainSceneLoader : MonoBehaviour
    {
        [SerializeField] private SceneLoadManager _sceneLoader;
        [SerializeField] private SceneType _startScene = SceneType.Home;

        private IEnumerator Start()
        {
            if (_sceneLoader == null)
            {
                Debug.LogError($"[Scene] [frame={Time.frameCount}] 시작 지역 불러오기: 실패. 씬 로더가 연결되지 않았습니다. event=boot result=failed reason=missing_scene_loader", this);
                enabled = false;

                yield break;
            }

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
