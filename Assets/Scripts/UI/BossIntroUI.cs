using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Category5.Audio;
using Category5.Boss;
using Category5.Core;

namespace Category5.UI
{
    // drives the boss intro title card
    // all visibility is controlled via canvas groups
    public class BossIntroUI : MonoBehaviour
    {
        // checked by PlayerController and ThirdPersonCamera to block all player input during the intro
        public static bool IntroIsPlaying { get; private set; }

        // =====================================
        // panel references
        // =====================================

        [Header("panel references")]
        [Tooltip("canvas group on the root of this panel — controls overall visibility and blocks raycasts")]
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Tooltip("full-panel image used as a semi-transparent background portrait (boss model will still render in front of image)")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("canvas group on the portrait image — used only for its fade-in alpha")]
        [SerializeField] private CanvasGroup portraitCanvasGroup;

        [Tooltip("recttransform of the container holding the boss name text — slides in from the left")]
        [SerializeField] private RectTransform nameGroup;

        [Tooltip("recttransform of the container holding the subtitle text — slides in from the right")]
        [SerializeField] private RectTransform subtitleGroup;

        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text subtitleText;

        [Tooltip("canvas group on a full-panel white image — flashes on impact when the name lands")]
        [SerializeField] private CanvasGroup flashOverlayGroup;

        // =====================================
        // slide offsets
        // =====================================

        [Header("slide offsets")]
        [Tooltip("x distance in canvas pixels the name slides in from — negative = from the left")]
        [SerializeField] private float nameSlideOffsetX = -700f;

        [Tooltip("optional vertical offset — nonzero gives a diagonal entrance")]
        [SerializeField] private float nameSlideOffsetY = 0f;

        [Tooltip("x distance in canvas pixels the subtitle slides in from — positive = from the right")]
        [SerializeField] private float subtitleSlideOffsetX = 700f;

        // timing

        [Header("timing")]
        [SerializeField] private float slideInDuration = 0.35f;
        [SerializeField] private float subtitleDelay = 0.25f;
        [SerializeField] private float holdDuration = 2.5f;
        [SerializeField] private float fadeOutDuration = 0.6f;
        [SerializeField] private float portraitFadeInDuration = 0.5f;

        [Tooltip("how opaque the background portrait gets at max")]
        [SerializeField] [Range(0f, 1f)] private float portraitMaxAlpha = 0.45f;

        [Tooltip("total duration for the full white flash — half flash-in, half flash-out")]
        [SerializeField] private float flashDuration = 0.15f;

        // impact shake

        [Header("impact shake")]
        [SerializeField] private float introShakeIntensity = 0.35f;
        [SerializeField] private float introShakeDuration = 0.4f;
        [SerializeField] private float introShakeFrequency = 20f;

        // active coroutine ref so we can cancel and restart if needed
        private Coroutine _activeCoroutine;

        // rest positions read from the editor
        private Vector2 _nameGroupRestPosition;
        private Vector2 _subtitleGroupRestPosition;

        // lifecycle

        private void Awake()
        {
            // cache the positions set in the editor -> these are the targets the elements slide TO
            if (nameGroup != null) _nameGroupRestPosition = nameGroup.anchoredPosition;
            if (subtitleGroup != null) _subtitleGroupRestPosition = subtitleGroup.anchoredPosition;

            // hide panel at startup — alpha 0 + block raycasts off
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            BossEvents.OnBossIntro += OnBossIntroReceived;
        }

        private void OnDisable()
        {
            BossEvents.OnBossIntro -= OnBossIntroReceived;
        }

        private void OnBossIntroReceived(BossData data, Vector3 bossPosition)
        {
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(ShowIntro(data));
        }

        // =====================================
        // animation coroutine (SUMMER THIS IS FOR YOU)
        // =====================================

        private IEnumerator ShowIntro(BossData data)
        {
            // populate content
            if (bossNameText != null) bossNameText.text = data.bossName.ToUpper();
            if (subtitleText != null) subtitleText.text = data.introSubtitle;

            bool hasPortrait = data.introPortrait != null;
            if (backgroundImage != null)
                backgroundImage.sprite = data.introPortrait;

            // reset all positions and alphas before showing anything
            // start positions add the slide offset on top of the editor rest position
            if (nameGroup != null)
                nameGroup.anchoredPosition = _nameGroupRestPosition + new Vector2(nameSlideOffsetX, nameSlideOffsetY);
            if (subtitleGroup != null)
                subtitleGroup.anchoredPosition = _subtitleGroupRestPosition + new Vector2(subtitleSlideOffsetX, 0f);
            if (portraitCanvasGroup != null)
                portraitCanvasGroup.alpha = 0f;
            if (flashOverlayGroup != null)
                flashOverlayGroup.alpha = 0f;

            // show panel and start blocking input
            IntroIsPlaying = true;
            SetPanelVisible(true);

            // slide name in from left + fade portrait in simultaneously
            float maxDuration = Mathf.Max(slideInDuration, portraitFadeInDuration);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / maxDuration;

                if (nameGroup != null)
                {
                    float nameProg = Mathf.Clamp01(t * maxDuration / slideInDuration);
                    nameGroup.anchoredPosition = Vector2.Lerp(
                        _nameGroupRestPosition + new Vector2(nameSlideOffsetX, nameSlideOffsetY),
                        _nameGroupRestPosition,
                        nameProg
                    );
                }

                if (hasPortrait && portraitCanvasGroup != null)
                    portraitCanvasGroup.alpha = Mathf.Lerp(0f, portraitMaxAlpha, Mathf.Clamp01(t));

                yield return null;
            }

            // name has landed > fire impact shake and flash
            HitFeedbackManager.Instance?.TriggerScreenShake(introShakeIntensity, introShakeDuration, introShakeFrequency);
            if (flashOverlayGroup != null)
                StartCoroutine(FlashCoroutine());

            // short pause then slide subtitle in from the right
            yield return new WaitForSeconds(subtitleDelay);

            if (subtitleGroup != null)
            {
                t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / slideInDuration;
                    subtitleGroup.anchoredPosition = Vector2.Lerp(
                        _subtitleGroupRestPosition + new Vector2(subtitleSlideOffsetX, 0f),
                        _subtitleGroupRestPosition,
                        Mathf.Clamp01(t)
                    );
                    yield return null;
                }
            }

            // hold on screen
            yield return new WaitForSeconds(holdDuration);

            // fade the whole panel out
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / fadeOutDuration;
                if (rootCanvasGroup != null)
                    rootCanvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t));
                yield return null;
            }

            // hide and release input
            SetPanelVisible(false);
            IntroIsPlaying = false;
            _activeCoroutine = null;
        }

        // quick white flash on impact
        private IEnumerator FlashCoroutine()
        {
            if (flashOverlayGroup == null) yield break;

            float half = flashDuration * 0.5f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / half;
                flashOverlayGroup.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t));
                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / half;
                flashOverlayGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t));
                yield return null;
            }

            flashOverlayGroup.alpha = 0f;
        }


        // visibility helper
        // uses canvas group only so dotween can animate this freely
        private void SetPanelVisible(bool visible)
        {
            if (rootCanvasGroup == null) return;
            rootCanvasGroup.alpha = visible ? 1f : 0f;
            rootCanvasGroup.blocksRaycasts = visible;
            rootCanvasGroup.interactable = false;
        }
    }
}
