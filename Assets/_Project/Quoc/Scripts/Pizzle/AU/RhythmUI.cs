using System.Collections;
using TMPro;
using UnityEngine;

public class RhythmUI : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private RhythmGameManager gameManager;

    [Header("Texts")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text maxComboText;
    [SerializeField] private TMP_Text judgmentText;

    [Header("Fade")]
    [SerializeField] private float judgmentVisibleTime = 0.6f;
    [SerializeField] private float judgmentFadeTime = 0.25f;

    private Coroutine fadeCoroutine;
    private Color judgmentBaseColor = Color.white;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<RhythmGameManager>();
        }

        if (judgmentText != null)
        {
            judgmentBaseColor = judgmentText.color;
        }
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnScoreChanged += HandleScoreChanged;
            gameManager.OnJudgmentChanged += HandleJudgmentChanged;
            gameManager.OnComboChanged += HandleComboChanged;
            Refresh(gameManager.Score, gameManager.Combo, gameManager.MaxCombo);
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnScoreChanged -= HandleScoreChanged;
            gameManager.OnJudgmentChanged -= HandleJudgmentChanged;
            gameManager.OnComboChanged -= HandleComboChanged;
        }
    }

    private void HandleScoreChanged(int score, int combo, int maxCombo)
    {
        Refresh(score, combo, maxCombo);
    }

    private void HandleComboChanged(int combo)
    {
        if (comboText != null)
        {
            comboText.text = $"Combo: {combo}";
        }
    }

    private void HandleJudgmentChanged(string judgment)
    {
        ShowJudgment(judgment);
    }

    public void Refresh(int score, int combo, int maxCombo)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }

        if (comboText != null)
        {
            comboText.text = $"Combo: {combo}";
        }

        if (maxComboText != null)
        {
            maxComboText.text = $"Max: {maxCombo}";
        }
    }

    public void ShowJudgment(string text)
    {
        if (judgmentText == null)
        {
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (string.IsNullOrEmpty(text))
        {
        judgmentText.text = string.Empty;
        judgmentText.color = judgmentBaseColor;
        return;
    }

        judgmentText.text = text;
        judgmentText.color = new Color(judgmentBaseColor.r, judgmentBaseColor.g, judgmentBaseColor.b, 1f);
        fadeCoroutine = StartCoroutine(FadeJudgment());
    }

    private IEnumerator FadeJudgment()
    {
        yield return new WaitForSeconds(judgmentVisibleTime);

        float elapsed = 0f;
        Color start = judgmentText.color;

        while (elapsed < judgmentFadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / judgmentFadeTime);
            judgmentText.color = Color.Lerp(start, new Color(start.r, start.g, start.b, 0f), t);
            yield return null;
        }

        judgmentText.color = new Color(judgmentBaseColor.r, judgmentBaseColor.g, judgmentBaseColor.b, 0f);
        fadeCoroutine = null;
    }
}

