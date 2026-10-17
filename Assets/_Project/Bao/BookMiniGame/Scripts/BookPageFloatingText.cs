using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using echo17.EndlessBook;

[System.Serializable]
public class BookPageFloatingTextEntry
{
    [Tooltip("So trang cua EndlessBook se kich hoat chu noi nay.")]
    public int pageNumber = 2;

    [TextArea(2, 4)]
    [Tooltip("Noi dung chu hien len tren trang.")]
    public string text = "Day la manh moi an trong trang sach...";

    [Tooltip("Vi tri tam khung chu theo local space cua quyen sach.")]
    public Vector3 localStartPosition = new Vector3(0.075f, 0.08f, 0f);

    [Tooltip("Do cao chu noi len theo truc up cua quyen sach.")]
    public float riseDistance = 0.08f;

    [Tooltip("Thoi gian chu noi tu mat trang len.")]
    public float riseDuration = 0.6f;

    [Tooltip("Thoi gian giu chu sau khi noi len.")]
    public float displayDuration = 2f;

    [Tooltip("Thoi gian chu tan bien.")]
    public float fadeDuration = 0.6f;

    [Tooltip("Thoi gian tan nhanh khi nguoi choi chuyen sang trang khac.")]
    public float pageChangeFadeDuration = 0.2f;

    [Tooltip("Bat hieu ung chay tung chu khi text bien mat.")]
    public bool useBurnOut = true;

    [Tooltip("Mau chu khi dang bi chay.")]
    public Color burnColor = new Color(1f, 0.28f, 0.02f, 1f);

    [Tooltip("So ky tu bat dau chay moi giay. Tang len de chay nhanh hon.")]
    public float burnCharactersPerSecond = 28f;

    [Tooltip("Thoi gian moi ky tu sang mau chay truoc khi bien mat.")]
    public float burnCharacterLifetime = 0.12f;

    [Tooltip("Co chu lon nhat cua TextMeshPro 3D.")]
    public float fontSize = 0.035f;

    [Tooltip("Co chu nho nhat khi noi dung dai va can tu co lai.")]
    public float minFontSize = 0.018f;

    [Tooltip("Kich thuoc khung chu TextMeshPro 3D. Giam X/Y neu chu tran ra ngoai trang.")]
    public Vector2 textBoxSize = new Vector2(0.12f, 0.1f);

    [Tooltip("Khoang dem ben trong khung chu de chu khong cham sat vien trang.")]
    public Vector4 textPadding = new Vector4(0.005f, 0.005f, 0.005f, 0.005f);

    [Tooltip("Neu bat, chu se luon quay mat ve camera.")]
    public bool faceCamera = false;

    [Tooltip("Goc xoay them theo local Euler de can chinh huong chu tren trang.")]
    public Vector3 localRotationOffset = new Vector3(90f, 0f, 90f);

    [Tooltip("Bat/tat panel mo nam sau chu.")]
    public bool showBackgroundPanel = true;

    [Tooltip("Mau panel mo sau chu.")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.35f);

    [Tooltip("Do rong them cua panel so voi khung chu.")]
    public Vector2 backgroundPadding = new Vector2(0.02f, 0.015f);

    [Tooltip("Do lui panel ve sau chu theo local Z cua text.")]
    public float backgroundBackOffset = 0.002f;

    [Tooltip("Mau chu khi hien ro.")]
    public Color color = new Color(1f, 0.92f, 0.55f, 1f);

    [Tooltip("Neu bat, entry nay chi hien mot lan moi lan prefab duoc enable.")]
    public bool playOnlyOnce = true;
}

public class BookPageFloatingText : MonoBehaviour
{
    [Header("References")]
    [Tooltip("EndlessBook tren cung prefab. Neu trong se tu tim tren GameObject nay.")]
    public EndlessBook book;

    [Tooltip("Camera de chu quay mat ve nguoi choi. Neu trong se dung Camera.main.")]
    public Camera targetCamera;

    [Header("Floating Text")]
    public List<BookPageFloatingTextEntry> pageTexts = new List<BookPageFloatingTextEntry>();

    private readonly HashSet<int> playedEntryIndexes = new HashSet<int>();
    private readonly List<FloatingTextInstance> activeTexts = new List<FloatingTextInstance>();
    private int lastLeftPageNumber = int.MinValue;
    private int lastRightPageNumber = int.MinValue;
    private EndlessBook.StateEnum lastState;

    private void Awake()
    {
        if (book == null) book = GetComponent<EndlessBook>();
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        playedEntryIndexes.Clear();
        lastLeftPageNumber = int.MinValue;
        lastRightPageNumber = int.MinValue;
        lastState = book != null ? book.CurrentState : EndlessBook.StateEnum.ClosedFront;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearActiveTexts();
    }

    private void Update()
    {
        if (book == null) return;

        if (book.CurrentState != EndlessBook.StateEnum.OpenMiddle)
        {
            if (lastState == EndlessBook.StateEnum.OpenMiddle)
            {
                FadeOutAllActiveTexts();
            }

            lastState = book.CurrentState;
            return;
        }

        if (book.IsTurningPages)
        {
            FadeOutAllActiveTexts();
        }

        bool openedMiddleThisFrame = lastState != EndlessBook.StateEnum.OpenMiddle;
        bool pageChanged = book.CurrentLeftPageNumber != lastLeftPageNumber || book.CurrentRightPageNumber != lastRightPageNumber;

        if (openedMiddleThisFrame || pageChanged)
        {
            FadeOutTextsOutsideVisiblePages(book.CurrentLeftPageNumber, book.CurrentRightPageNumber);
            TryPlayForVisiblePages(book.CurrentLeftPageNumber, book.CurrentRightPageNumber);
            lastLeftPageNumber = book.CurrentLeftPageNumber;
            lastRightPageNumber = book.CurrentRightPageNumber;
        }

        lastState = book.CurrentState;
    }

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        for (int i = activeTexts.Count - 1; i >= 0; i--)
        {
            FloatingTextInstance activeText = activeTexts[i];
            if (activeText.TextObject == null)
            {
                activeTexts.RemoveAt(i);
                continue;
            }

            if (activeText.Entry == null || !activeText.Entry.faceCamera) continue;

            activeText.TextObject.transform.rotation = Quaternion.LookRotation(activeText.TextObject.transform.position - targetCamera.transform.position, transform.up)
                * Quaternion.Euler(activeText.Entry.localRotationOffset);
        }
    }

    private void TryPlayForVisiblePages(int leftPageNumber, int rightPageNumber)
    {
        for (int i = 0; i < pageTexts.Count; i++)
        {
            BookPageFloatingTextEntry entry = pageTexts[i];
            if (entry == null) continue;
            if (entry.pageNumber != leftPageNumber && entry.pageNumber != rightPageNumber) continue;
            if (entry.playOnlyOnce && playedEntryIndexes.Contains(i)) continue;
            if (HasActiveTextForPage(entry.pageNumber)) continue;

            playedEntryIndexes.Add(i);
            StartFloatingText(entry);
        }
    }

    private bool HasActiveTextForPage(int pageNumber)
    {
        for (int i = 0; i < activeTexts.Count; i++)
        {
            FloatingTextInstance activeText = activeTexts[i];
            if (activeText == null || activeText.Entry == null || activeText.IsFadingOut) continue;
            if (activeText.Entry.pageNumber == pageNumber) return true;
        }

        return false;
    }

    private void StartFloatingText(BookPageFloatingTextEntry entry)
    {
        GameObject textObject = new GameObject($"FloatingText_Page_{entry.pageNumber}");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = entry.localStartPosition;
        textObject.transform.localRotation = Quaternion.Euler(entry.localRotationOffset);
        textObject.transform.localScale = Vector3.one;

        TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
        textMesh.text = entry.text;
        textMesh.fontSize = entry.fontSize;
        textMesh.fontSizeMax = entry.fontSize;
        textMesh.fontSizeMin = entry.minFontSize;
        textMesh.enableAutoSizing = true;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.textWrappingMode = TextWrappingModes.Normal;
        textMesh.overflowMode = TextOverflowModes.Truncate;
        textMesh.rectTransform.sizeDelta = entry.textBoxSize;
        textMesh.margin = entry.textPadding;
        textMesh.color = WithAlpha(entry.color, 0f);
        textMesh.renderer.sortingOrder = 1;

        MeshRenderer backgroundRenderer = null;
        if (entry.showBackgroundPanel)
        {
            backgroundRenderer = CreateBackgroundPanel(textObject.transform, entry);
            backgroundRenderer.material.color = WithAlpha(entry.backgroundColor, 0f);
        }

        FloatingTextInstance activeText = new FloatingTextInstance(textObject, entry, textMesh, backgroundRenderer);
        activeTexts.Add(activeText);
        activeText.Routine = StartCoroutine(PlayFloatingText(activeText));
    }

    private IEnumerator PlayFloatingText(FloatingTextInstance activeText)
    {
        BookPageFloatingTextEntry entry = activeText.Entry;
        Vector3 startPosition = entry.localStartPosition;
        Vector3 endPosition = entry.localStartPosition + transform.InverseTransformDirection(transform.up * entry.riseDistance);

        yield return AnimateText(activeText, startPosition, endPosition, Mathf.Max(0.01f, entry.riseDuration), 0f, 1f);

        float holdTime = Mathf.Max(0f, entry.displayDuration);
        float elapsed = 0f;
        while (elapsed < holdTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return FadeOut(activeText, Mathf.Max(0.01f, entry.fadeDuration));

        activeTexts.Remove(activeText);
        Destroy(activeText.TextObject);
    }

    private MeshRenderer CreateBackgroundPanel(Transform parent, BookPageFloatingTextEntry entry)
    {
        GameObject panelObject = new GameObject("FloatingText_Background");
        panelObject.transform.SetParent(parent, false);
        panelObject.transform.localPosition = new Vector3(0f, 0f, entry.backgroundBackOffset);
        panelObject.transform.localRotation = Quaternion.identity;
        panelObject.transform.localScale = Vector3.one;

        Vector2 panelSize = entry.textBoxSize + entry.backgroundPadding;
        Mesh mesh = new Mesh();
        mesh.name = "FloatingText_BackgroundMesh";
        mesh.vertices = new[]
        {
            new Vector3(-panelSize.x * 0.5f, -panelSize.y * 0.5f, 0f),
            new Vector3(-panelSize.x * 0.5f, panelSize.y * 0.5f, 0f),
            new Vector3(panelSize.x * 0.5f, panelSize.y * 0.5f, 0f),
            new Vector3(panelSize.x * 0.5f, -panelSize.y * 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();

        MeshFilter meshFilter = panelObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = panelObject.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        meshRenderer.material = new Material(shader);
        meshRenderer.sortingOrder = 0;

        return meshRenderer;
    }

    private void FadeOutTextsOutsideVisiblePages(int leftPageNumber, int rightPageNumber)
    {
        for (int i = activeTexts.Count - 1; i >= 0; i--)
        {
            FloatingTextInstance activeText = activeTexts[i];
            if (activeText == null || activeText.TextObject == null)
            {
                activeTexts.RemoveAt(i);
                continue;
            }

            int pageNumber = activeText.Entry.pageNumber;
            if (pageNumber == leftPageNumber || pageNumber == rightPageNumber) continue;

            FadeOutActiveText(activeText);
        }
    }

    private void FadeOutAllActiveTexts()
    {
        for (int i = activeTexts.Count - 1; i >= 0; i--)
        {
            FloatingTextInstance activeText = activeTexts[i];
            if (activeText == null || activeText.TextObject == null)
            {
                activeTexts.RemoveAt(i);
                continue;
            }

            FadeOutActiveText(activeText);
        }
    }

    private void FadeOutActiveText(FloatingTextInstance activeText)
    {
        if (activeText.IsFadingOut) return;

        activeText.IsFadingOut = true;
        if (activeText.Routine != null) StopCoroutine(activeText.Routine);
        activeText.Routine = StartCoroutine(FadeOutAndDestroy(activeText));
    }

    private IEnumerator FadeOutAndDestroy(FloatingTextInstance activeText)
    {
        float fadeDuration = Mathf.Max(0.01f, activeText.Entry.pageChangeFadeDuration);
        yield return FadeOut(activeText, fadeDuration);

        activeTexts.Remove(activeText);
        Destroy(activeText.TextObject);
    }

    private IEnumerator FadeOut(FloatingTextInstance activeText, float duration)
    {
        if (activeText.Entry.useBurnOut)
        {
            yield return BurnOutText(activeText, duration);
        }
        else
        {
            Vector3 currentPosition = activeText.TextObject.transform.localPosition;
            float currentAlpha = activeText.TextMesh != null && activeText.Entry.color.a > 0f
                ? activeText.TextMesh.color.a / activeText.Entry.color.a
                : 1f;
            yield return AnimateText(activeText, currentPosition, currentPosition, duration, currentAlpha, 0f);
        }
    }

    private IEnumerator AnimateText(FloatingTextInstance activeText, Vector3 from, Vector3 to, float duration, float fromAlpha, float toAlpha)
    {
        Transform textTransform = activeText.TextObject.transform;
        BookPageFloatingTextEntry entry = activeText.Entry;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            textTransform.localPosition = Vector3.Lerp(from, to, eased);
            activeText.TextMesh.color = WithAlpha(entry.color, alpha);
            if (activeText.BackgroundRenderer != null) activeText.BackgroundRenderer.material.color = WithAlpha(entry.backgroundColor, alpha * entry.backgroundColor.a);
            elapsed += Time.deltaTime;
            yield return null;
        }

        textTransform.localPosition = to;
        activeText.TextMesh.color = WithAlpha(entry.color, toAlpha);
        if (activeText.BackgroundRenderer != null) activeText.BackgroundRenderer.material.color = WithAlpha(entry.backgroundColor, toAlpha * entry.backgroundColor.a);
    }

    private IEnumerator BurnOutText(FloatingTextInstance activeText, float duration)
    {
        TextMeshPro textMesh = activeText.TextMesh;
        BookPageFloatingTextEntry entry = activeText.Entry;
        textMesh.ForceMeshUpdate();

        TMP_TextInfo textInfo = textMesh.textInfo;
        int visibleCharacters = textInfo.characterCount;
        if (visibleCharacters == 0)
        {
            yield return AnimateText(activeText, activeText.TextObject.transform.localPosition, activeText.TextObject.transform.localPosition, duration, 1f, 0f);
            yield break;
        }

        float burnLifetime = Mathf.Max(0.01f, entry.burnCharacterLifetime);
        float totalDuration = Mathf.Max(0.01f, duration);
        float burnWindow = Mathf.Max(0.01f, totalDuration - burnLifetime);
        float burnStep = Mathf.Min(1f / Mathf.Max(1f, entry.burnCharactersPerSecond), burnWindow / Mathf.Max(1, visibleCharacters));
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            float panelAlpha = Mathf.Clamp01(1f - (elapsed / totalDuration));
            if (activeText.BackgroundRenderer != null)
            {
                activeText.BackgroundRenderer.material.color = WithAlpha(entry.backgroundColor, panelAlpha * entry.backgroundColor.a);
            }

            for (int i = 0; i < visibleCharacters; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                float charBurnStart = i * burnStep;
                float charAge = elapsed - charBurnStart;
                Color32 color = entry.color;

                if (charAge >= burnLifetime)
                {
                    color.a = 0;
                }
                else if (charAge >= 0f)
                {
                    float burnT = Mathf.Clamp01(charAge / burnLifetime);
                    color = Color.Lerp(entry.burnColor, new Color(0.08f, 0.02f, 0f, 1f), burnT);
                    color.a = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 0f, burnT));
                }

                SetCharacterColor(textInfo, charInfo, color);
            }

            textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < visibleCharacters; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;
            SetCharacterColor(textInfo, charInfo, new Color32(0, 0, 0, 0));
        }

        textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        if (activeText.BackgroundRenderer != null) activeText.BackgroundRenderer.material.color = WithAlpha(entry.backgroundColor, 0f);
    }

    private void SetCharacterColor(TMP_TextInfo textInfo, TMP_CharacterInfo charInfo, Color32 color)
    {
        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;
        Color32[] vertexColors = textInfo.meshInfo[materialIndex].colors32;

        vertexColors[vertexIndex + 0] = color;
        vertexColors[vertexIndex + 1] = color;
        vertexColors[vertexIndex + 2] = color;
        vertexColors[vertexIndex + 3] = color;
    }

    private void ClearActiveTexts()
    {
        for (int i = activeTexts.Count - 1; i >= 0; i--)
        {
            if (activeTexts[i].TextObject != null) Destroy(activeTexts[i].TextObject);
        }

        activeTexts.Clear();
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private class FloatingTextInstance
    {
        public readonly GameObject TextObject;
        public readonly BookPageFloatingTextEntry Entry;
        public readonly TextMeshPro TextMesh;
        public readonly MeshRenderer BackgroundRenderer;
        public Coroutine Routine;
        public bool IsFadingOut;

        public FloatingTextInstance(GameObject textObject, BookPageFloatingTextEntry entry, TextMeshPro textMesh, MeshRenderer backgroundRenderer)
        {
            TextObject = textObject;
            Entry = entry;
            TextMesh = textMesh;
            BackgroundRenderer = backgroundRenderer;
        }
    }
}
