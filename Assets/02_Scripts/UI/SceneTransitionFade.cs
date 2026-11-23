using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HamPocha.UI
{
    // 지역 이동 중에는 로딩 화면을 표시하고, 수면 중에는 검은 화면을 유지한다.
    public class SceneTransitionFade : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Slider _loadingBar;
        [SerializeField] private Text _statusText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Min(0.1f)] private float _halfFadeSeconds = 0.2f;
        [SerializeField, Min(0f)] private float _blackHoldSeconds = 0.5f;

        private const float LoadingDotSize = 34f;
        private const float LoadingDotSpacing = 50f;

        private RectTransform _travelArtwork;
        private readonly Image[] _dots = new Image[3];
        private float _travelStart;
        private int _activeDot;

        public float Alpha { get { return _canvasGroup.alpha; } }

        public void Show(bool startBlack, bool showTravel = false)
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
            if (showTravel && _travelArtwork == null) BuildTravelArtwork();
            if (_travelArtwork != null) _travelArtwork.gameObject.SetActive(showTravel);
            _travelStart = Time.unscaledTime;
            _activeDot = -1;
            if (showTravel) Update();
            _canvasGroup.alpha = startBlack ? 1f : 0f;
            _canvasGroup.blocksRaycasts = true;
        }

        private void BuildTravelArtwork()
        {
            UiSkin skin = UiSkin.Load();
            _travelArtwork = HudLayout.Create("TravelLoading", _panel.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            _travelArtwork.anchorMax = Vector2.one;
            Image background = _travelArtwork.gameObject.AddComponent<Image>();
            background.color = new Color32(167, 198, 135, 255);
            HudLayout.Icon("Hamster", _travelArtwork, Vector2.zero, new Vector2(164f, 164f), skin?.HappyHamster);
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = HudLayout.Icon("LoadingDot" + (i + 1), _travelArtwork,
                    new Vector2((i - 1) * LoadingDotSpacing, -125f), Vector2.one * LoadingDotSize, skin?.BadgeCircle);
                _dots[i].preserveAspect = false;
            }
        }

        private void Update()
        {
            if (_travelArtwork == null || !_travelArtwork.gameObject.activeInHierarchy) return;
            int active = Mathf.FloorToInt((Time.unscaledTime - _travelStart) / .22f) % _dots.Length;
            if (_activeDot == active) return;
            _activeDot = active;
            for (int i = 0; i < _dots.Length; i++)
                _dots[i].color = i == active ? new Color32(95, 118, 79, 255) : new Color32(95, 118, 79, 75);
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
