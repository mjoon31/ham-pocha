using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HamPocha.UI
{
    // 지역 이동·수면 전환에서 검은 화면 페이드와 입력 차단을 표시한다.
    public class SceneTransitionFade : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Slider _loadingBar;
        [SerializeField] private Text _statusText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Min(0.1f)] private float _halfFadeSeconds = 0.2f;
        [SerializeField, Min(0f)] private float _blackHoldSeconds = 0.5f;

        public float Alpha { get { return _canvasGroup.alpha; } }

        public void Show(bool startBlack)
        {
            if (_panel == null || _canvasGroup == null)
            {
                Debug.LogError($"[UI] [frame={Time.frameCount}] 화면 전환 효과 초기화: 실패. 필수 참조가 연결되지 않았습니다. event=scene_fade result=failed reason=missing_reference", this);

                return;
            }

            if (_loadingBar != null)
            {
                _loadingBar.gameObject.SetActive(false);
            }

            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(false);
            }

            _panel.SetActive(true);
            _canvasGroup.alpha = startBlack ? 1f : 0f;
            _canvasGroup.blocksRaycasts = true;
        }

        public IEnumerator FadeOut()
        {
            yield return FadeTo(1f);
        }

        public IEnumerator FadeIn()
        {
            yield return FadeTo(0f);
        }

        public IEnumerator HoldBlack()
        {
            _canvasGroup.alpha = 1f;

            yield return new WaitForSecondsRealtime(_blackHoldSeconds);
        }

        private IEnumerator FadeTo(float target)
        {
            float start = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < _halfFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / _halfFadeSeconds));

                yield return null;
            }

            _canvasGroup.alpha = target;
        }

        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }
    }
}
