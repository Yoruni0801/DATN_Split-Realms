using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace CustomUITutorial
{
    [Serializable]
    public class TutorialStep
    {
        [Tooltip("UI Element")]
        public RectTransform targetElement;
        // public string title;
        [TextArea(3, 5)]
        public string content;
        [Tooltip("Phím bấm để chuyển sang step tiếp theo")]
        public KeyCode nextButtonKey = KeyCode.E;
    }

    public class TutorialSystem : MonoBehaviour
    {
        [Header("Cài đặt các bước")]
        public TutorialStep[] steps;
        public float padding = 20f;
        public float transitionDuration = 0.5f;

        [Header("Thành phần UI")]
        public TutorialSpotlight spotlightOverlay;
        public RectTransform tooltipPanel;
        // public TextMeshProUGUI titleText;
        public TextMeshProUGUI contentText;
        // public TextMeshProUGUI stepCountText;
        public Button skipButton;

        private int currentStepIndex = 0;
        private Coroutine moveCoroutine;

        private void Start()
        {
            if (skipButton != null) skipButton.onClick.AddListener(EndTutorial);

            StartTutorial();
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy && currentStepIndex < steps.Length)
            {
                if (Input.GetKeyDown(steps[currentStepIndex].nextButtonKey))
                {
                    NextStep();
                }
            }
        }

        public void StartTutorial()
        {
            if (steps == null || steps.Length == 0) return;

            currentStepIndex = 0;
            gameObject.SetActive(true);
            tooltipPanel.gameObject.SetActive(false);
            _ = spotlightOverlay.rectTransform;
            spotlightOverlay.centerRect = new Rect(
                -2000, -2000, 4000, 4000 
            );

            ShowCurrentStep();
        }

        public void NextStep()
        {
            if (currentStepIndex < steps.Length - 1)
            {
                currentStepIndex++;
                ShowCurrentStep();
            }
            else
            {
                EndTutorial();
            }
        }

        public void EndTutorial()
        {
            gameObject.SetActive(false);
            Debug.Log("Tutorial Finished!");
        }

        private void ShowCurrentStep()
        {
            TutorialStep step = steps[currentStepIndex];

            // if (titleText) titleText.text = step.title;
            if (contentText) contentText.text = step.content;
            // if (stepCountText) stepCountText.text = $"{currentStepIndex + 1} / {steps.Length}";

            Rect targetLocalRect = GetLocalRectInSpotlight(step.targetElement);

            targetLocalRect.x -= padding;
            targetLocalRect.y -= padding;
            targetLocalRect.width += padding * 2;
            targetLocalRect.height += padding * 2;

            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(MoveSpotlightRoutine(targetLocalRect, step.targetElement));
        }

        private IEnumerator MoveSpotlightRoutine(Rect targetRect, RectTransform targetUI)
        {
            tooltipPanel.gameObject.SetActive(false); 
            Vector2 targetCenter = targetRect.center;
            Rect startRect = new Rect(targetCenter.x - 3000f, targetCenter.y - 3000f, 6000f, 6000f);
            
            float time = 0;
            while (time < transitionDuration)
            {
                time += Time.deltaTime;
                float t = time / transitionDuration;
                t = 1f - Mathf.Pow(1f - t, 3f);

                float x = Mathf.Lerp(startRect.x, targetRect.x, t);
                float y = Mathf.Lerp(startRect.y, targetRect.y, t);
                float w = Mathf.Lerp(startRect.width, targetRect.width, t);
                float h = Mathf.Lerp(startRect.height, targetRect.height, t);

                spotlightOverlay.centerRect = new Rect(x, y, w, h);
                yield return null;
            }

            spotlightOverlay.centerRect = targetRect;

            // PositionTooltip(targetUI);
            tooltipPanel.gameObject.SetActive(true);
        }

        // private void PositionTooltip(RectTransform targetUI)
        // {
        //     Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, targetUI.position);
            
        //     bool isBottomHalf = screenPos.y < Screen.height / 2f;

        //     tooltipPanel.position = targetUI.position;
            
        //     float verticalOffset = targetUI.rect.height / 2f * tooltipPanel.lossyScale.y + 20f;
        //     if (isBottomHalf)
        //     {
        //         tooltipPanel.position += new Vector3(0, verticalOffset + (tooltipPanel.rect.height * tooltipPanel.lossyScale.y / 2f), 0);
        //     }
        //     else
        //     {
        //         tooltipPanel.position -= new Vector3(0, verticalOffset + (tooltipPanel.rect.height * tooltipPanel.lossyScale.y / 2f), 0);
        //     }
        // }

        private Rect GetLocalRectInSpotlight(RectTransform target)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector3 min = spotlightOverlay.rectTransform.InverseTransformPoint(corners[0]);
            Vector3 max = spotlightOverlay.rectTransform.InverseTransformPoint(corners[2]);

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }
    }
}