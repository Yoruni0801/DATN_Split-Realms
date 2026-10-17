using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bao.IceSigil
{
    public sealed class IceCarvingUIController
    {
        private readonly RectTransform crackFill;
        private readonly RectTransform forceFill;
        private readonly RectTransform progressFill;
        private readonly RectTransform forceSafeBand;
        private readonly Image crackFillImage;
        private readonly Image forceFillImage;
        private readonly TextMeshProUGUI phaseText;
        private readonly TextMeshProUGUI instructionText;
        private readonly TextMeshProUGUI statusText;
        private readonly TextMeshProUGUI crackText;
        private readonly TextMeshProUGUI forceText;
        private readonly TextMeshProUGUI progressText;
        private readonly GameObject resultPanel;
        private readonly TextMeshProUGUI resultTitle;
        private readonly TextMeshProUGUI resultSubtitle;

        private static readonly Color Cyan = new(0.38f, 0.95f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.28f, 0.30f, 1f);
        private static readonly Color Success = new(0.46f, 1f, 0.72f, 1f);

        public IceCarvingUIController(Transform root)
        {
            crackFill = FindRect(root, "Meters/CrackMeter/Fill");
            forceFill = FindRect(root, "Meters/ForceMeter/Fill");
            progressFill = FindRect(root, "Progress/Fill");
            forceSafeBand = FindRect(root, "Meters/ForceMeter/SafeBand");
            crackFillImage = crackFill != null ? crackFill.GetComponent<Image>() : null;
            forceFillImage = forceFill != null ? forceFill.GetComponent<Image>() : null;
            phaseText = Find<TextMeshProUGUI>(root, "Texts/PhaseText");
            instructionText = Find<TextMeshProUGUI>(root, "Texts/InstructionText");
            statusText = Find<TextMeshProUGUI>(root, "Texts/StatusText");
            crackText = Find<TextMeshProUGUI>(root, "Meters/CrackMeter/Value");
            forceText = Find<TextMeshProUGUI>(root, "Meters/ForceMeter/Value");
            progressText = Find<TextMeshProUGUI>(root, "Progress/Value");
            resultPanel = root.Find("ResultPanel") != null ? root.Find("ResultPanel").gameObject : null;
            resultTitle = Find<TextMeshProUGUI>(root, "ResultPanel/ResultTitle");
            resultSubtitle = Find<TextMeshProUGUI>(root, "ResultPanel/ResultSubtitle");
        }

        public void SetPhase(string phase, string instruction, string status)
        {
            SetText(phaseText, phase);
            SetText(instructionText, instruction);
            SetText(statusText, status);
        }

        public void SetStatus(string status)
        {
            SetText(statusText, status);
        }

        public void HideResult()
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        public void ShowResult(bool success, string title, string subtitle)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            SetText(resultTitle, title);
            SetText(resultSubtitle, subtitle);
            if (resultTitle != null)
            {
                resultTitle.color = success ? Success : Danger;
            }
        }

        public void UpdateCarvingMeters(float crackAmount, ShapeEvaluationResult result)
        {
            SetFill(crackFill, crackAmount / 100f, false);
            SetFill(forceFill, 0f, false);
            SetFill(progressFill, result.coverage, true);

            if (crackFillImage != null)
            {
                crackFillImage.color = Color.Lerp(Cyan, Danger, crackAmount / 100f);
            }

            if (forceFillImage != null)
            {
                forceFillImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
            }

            SetText(crackText, $"{GetCrackState(crackAmount)}\n{Mathf.RoundToInt(crackAmount)}%");
            SetText(forceText, "---");
            SetText(progressText,
                $"HT {Mathf.RoundToInt(result.coverage * 100f)}% | CX {Mathf.RoundToInt(result.precision * 100f)}% | SAU {Mathf.RoundToInt(result.depthQuality * 100f)}% | LIEN {Mathf.RoundToInt(result.continuity * 100f)}%");
        }

        public void UpdatePryMeters(float crackAmount, float force, float safeMin, float safeMax, int successCount, int requiredSuccesses)
        {
            SetFill(crackFill, crackAmount / 100f, false);
            SetFill(forceFill, force, false);
            SetFill(progressFill, requiredSuccesses > 0 ? successCount / (float)requiredSuccesses : 1f, true);
            SetSafeBand(safeMin, safeMax);

            bool safe = force >= safeMin && force <= safeMax;
            if (forceFillImage != null)
            {
                forceFillImage.color = safe ? Success : Cyan;
            }

            if (crackFillImage != null)
            {
                crackFillImage.color = Color.Lerp(Cyan, Danger, crackAmount / 100f);
            }

            SetText(crackText, $"{GetCrackState(crackAmount)}\n{Mathf.RoundToInt(crackAmount)}%");
            SetText(forceText, Mathf.RoundToInt(force * 100f) + "%");
            SetText(progressText, $"CAY {successCount}/{requiredSuccesses}");
        }

        private void SetSafeBand(float min, float max)
        {
            if (forceSafeBand == null)
            {
                return;
            }

            forceSafeBand.anchorMin = new Vector2(0f, Mathf.Clamp01(min));
            forceSafeBand.anchorMax = new Vector2(1f, Mathf.Clamp01(max));
            forceSafeBand.offsetMin = Vector2.zero;
            forceSafeBand.offsetMax = Vector2.zero;
        }

        private static string GetCrackState(float crack)
        {
            if (crack >= 86f)
            {
                return "SAP VO";
            }

            if (crack >= 64f)
            {
                return "NGUY HIEM";
            }

            if (crack >= 32f)
            {
                return "RAN NHE";
            }

            return "ON DINH";
        }

        private static void SetFill(RectTransform rect, float value, bool horizontal)
        {
            if (rect == null)
            {
                return;
            }

            value = Mathf.Clamp01(value);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = horizontal ? new Vector2(value, 1f) : new Vector2(1f, value);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform FindRect(Transform root, string path)
        {
            Transform child = root != null ? root.Find(path) : null;
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private static T Find<T>(Transform root, string path) where T : Component
        {
            Transform child = root != null ? root.Find(path) : null;
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
