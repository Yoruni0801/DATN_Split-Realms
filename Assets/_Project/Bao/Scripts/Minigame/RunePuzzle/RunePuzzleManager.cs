using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;

namespace Minigame.RunePuzzle
{
    [System.Serializable]
    public class RuneData
    {
        public string runeId;           // VD: "Fire", "Water", "Light", "Earth", "Desert"
        public string riddleText;       // Câu đố ẩn dụ tiếng Việt
        public Color elementColor;      // Màu glow khi đúng
        public Sprite slotSprite;       // HÌNH DÁNG CỦA CÁI LÕM (Hình tam giác, tròn, vuông...)
    }

    public class RunePuzzleManager : MonoBehaviour
    {
        public static RunePuzzleManager Instance { get; private set; }

        [Header("=== PUZZLE DATA ===")]
        public List<RuneData> allRuneData = new List<RuneData>();


        [Tooltip("Khoảng cách tối đa (pixel) để viên đá tự động hít vào rãnh khi thả chuột")]
        public float snapRadius = 100f;

        [Header("=== UI REFERENCES ===")]
        public Sprite defaultSlotSprite;       // Ảnh Lõm mặc định (tròn, nứt nẻ...)
        public List<RuneSlot> runeSlots;

        public List<RuneStone> runeStones;

        [SerializeField] private TextMeshProUGUI riddleText;

        [SerializeField] private Image mindLampButton;


        [Header("=== FOG SETTINGS ===")]
        [SerializeField] private CanvasGroup fogCanvasGroup;
        [SerializeField] private CanvasGroup extraFogCanvasGroup;

        [SerializeField] private float maxFogAlpha = 1f; // Chỉnh lên 1 để che khuất hoàn toàn (100% đục)
        #pragma warning disable CS0414
        [SerializeField] private float lightTransitionSpeed = 4f;
        #pragma warning restore CS0414
        [SerializeField] private float fogFadeDuration = 2.0f; // Thời gian sương mù mờ dần khi giải xong
        [SerializeField] private float videoDelayAfterFogOff = 0.5f;

        [Header("=== VFX & EFFECTS ===")]
        [SerializeField] private Image glowVignette;

        [SerializeField] private float shakeIntensity = 5f;

        [SerializeField] private float shakeDuration = 0.3f;

        [Header("=== SOLVED DISMISS EFFECT ===")]
        [Tooltip("Các GameObject sẽ fade/scale rồi tắt sau khi giải xong minigame. Để trống thì script tự tìm MainPanel, GlowVignette, FogOverlay, KinhLup, PanelTutorial theo tên.")]
        [SerializeField] private List<GameObject> solvedHideTargets = new List<GameObject>();
        [Tooltip("Thời gian chờ sau khi rune lóe sáng, trước khi đổi video và tắt UI.")]
        [SerializeField] private float solvedFlashHoldDuration = 0.75f;
        [Tooltip("Thời gian fade/scale các object trong Solved Hide Targets.")]
        [SerializeField] private float solvedHideDuration = 0.45f;
        [Tooltip("Scale cuối cùng của các object khi fade out. 0.86 nghĩa là nhỏ lại còn 86%.")]
        [SerializeField] private float solvedHideScale = 0.86f;

        [Header("=== SCENE RETURN ===")]
        [SerializeField] private bool returnToPreviousTimelineSceneOnSolved = true;
        [SerializeField] private ChangeVideoBackground videoBackgroundChanger;

        [Header("=== TUTORIAL TIMELINE ===")]
        [SerializeField] private PlayableDirector tutorialTimeline;

        #pragma warning disable CS0414
        private bool isLightActive = false;
        #pragma warning restore CS0414
        private bool isPuzzleSolved = false;
        private List<int> shuffledSlotOrder = new List<int>();
        private readonly Dictionary<RectTransform, Vector3> solvedTargetOriginalScales = new Dictionary<RectTransform, Vector3>();


        private Dictionary<RuneStone, Vector2> originalStonePositions = new Dictionary<RuneStone, Vector2>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            if (tutorialTimeline != null)
            {
                tutorialTimeline.stopped += OnTimelineStopped;
            }
        }

        private void OnDisable()
        {
            if (tutorialTimeline != null)
            {
                tutorialTimeline.stopped -= OnTimelineStopped;
            }
        }

        private void OnTimelineStopped(PlayableDirector director)
        {
            // Khi timeline kết thúc, nó có thể tự tắt các rãnh.
            // Ta cần gọi lại UpdateSlotsVisibility để rãnh đầu tiên hiện ra lại.
            UpdateSlotsVisibility();
        }

        private void Start()
        {
            if (allRuneData.Count == 0)
            {
                Debug.LogError("Chưa có Rune Data! Hãy set up trong Inspector.");
            }

            for (int i = 0; i < runeStones.Count && i < allRuneData.Count; i++)
            {
                runeStones[i].runeId = allRuneData[i].runeId;
            }

            // Tắt LayoutGroup đi để nó không giành quyền điều khiển vị trí của các viên đá lúc trượt về
            if (runeStones.Count > 0 && runeStones[0] != null && runeStones[0].transform.parent != null)
            {
                RectTransform parentRect = runeStones[0].transform.parent.GetComponent<RectTransform>();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

                LayoutGroup layout = parentRect.GetComponent<LayoutGroup>();
                if (layout != null) layout.enabled = false;
            }

            foreach (var stone in runeStones)
            {
                if (stone != null)
                {
                    RectTransform stoneRect = stone.GetComponent<RectTransform>();
                    originalStonePositions[stone] = stoneRect.anchoredPosition;

                    // Cập nhật lại originalPosition, Anchor, Pivot vì LayoutGroup đã thay đổi chúng

                    stone.UpdateOriginalPosition(originalStonePositions[stone]);
                    stone.UpdateOriginalAnchors(stoneRect.anchorMin, stoneRect.anchorMax, stoneRect.pivot);
                }
            }

            // Ép đục 100% để sương mù che khuất hoàn toàn (override số lưu trong Inspector)
            maxFogAlpha = 1f;
            UpdateFogImmediate(maxFogAlpha);
            ResetAndShuffle();
        }

        private void Update()
        {
            if (isPuzzleSolved) return;

            // Fog và RiddleText luôn hiển thị, sự che khuất giờ được điều khiển bởi Shader của Kính Lúp
            if (riddleText != null && riddleText.color.a < 0.99f)
            {
                Color c = riddleText.color;
                c.a = 1f;
                riddleText.color = c;
            }
        }

        public void SetLightActive(bool active)
        {
            // Legacy, không dùng nữa vì Kính lúp dùng Shader
        }

        public bool CanDragRunes()
        {
            return !isPuzzleSolved;
        }

        public void OnStonePlacedInSlot(RuneSlot slot, RuneStone stone)
        {
            if (slot.IsCorrectRunePlaced())
            {
                Debug.Log($"<color=green>✅ ĐÚNG RỒI! {stone.name} khớp với lõm {slot.name}</color>");
                RuneData data = GetRuneDataById(stone.runeId);
                if (data != null)

                {
                    stone.PlayCorrectEffect(data.elementColor);
                    slot.TransitionToSprite(data.slotSprite); // Khảm đá vào và hiện hình thật
                }


                UpdateRiddleDisplay();
                UpdateSlotsVisibility(); // Hiện Rãnh tiếp theo


                if (riddleText != null)
                {
                    Color c = riddleText.color;
                    c.a = 1f;
                    riddleText.color = c;
                }

                CheckWinCondition();
            }
            else
            {
                Debug.Log($"<color=red>❌ SAI RỒI! {stone.name} (ID: {stone.runeId}) KHÔNG khớp với lõm {slot.name} (Chờ ID: {slot.expectedRuneId})</color>");
                StartCoroutine(OnWrongAnswer(stone));
            }
        }

        public void UpdateSlotsVisibility()
        {
            // Hiển thị tất cả các rãnh khảm
            for (int i = 0; i < runeSlots.Count; i++)
            {
                if (runeSlots[i] != null)
                {
                    runeSlots[i].gameObject.SetActive(true);
                }
            }
        }

        public void OnStoneRemovedFromSlot()
        {
        }

        private void CheckWinCondition()
        {
            bool allFilled = true;
            foreach (var slot in runeSlots)
            {
                if (!slot.IsCorrectRunePlaced())
                {
                    allFilled = false;
                    break;
                }
            }

            if (allFilled)
            {
                isPuzzleSolved = true;
                StartCoroutine(PuzzleSolvedSequence());
            }
        }

        private IEnumerator OnWrongAnswer(RuneStone wrongStone)
        {
            wrongStone.PlayWrongEffect();
            yield return StartCoroutine(ShakeEffect());
            yield return new WaitForSeconds(0.5f);
            ResetAndShuffle();
        }

        public void ResetAndShuffle()
        {
            // Trả đá về
            foreach (var stone in runeStones)
            {
                if (stone != null && originalStonePositions.ContainsKey(stone))
                {
                    stone.ResetStone(originalStonePositions[stone]);
                }
            }

            foreach (var slot in runeSlots)
            {
                slot.RemoveStone();
            }

            // Shuffle
            shuffledSlotOrder.Clear();
            for (int i = 0; i < allRuneData.Count && i < runeSlots.Count; i++)
            {
                shuffledSlotOrder.Add(i);
            }
            ShuffleList(shuffledSlotOrder);

            // Gán dữ liệu (ID và HÌNH DÁNG) cho các Lõm
            for (int slotIdx = 0; slotIdx < runeSlots.Count && slotIdx < shuffledSlotOrder.Count; slotIdx++)
            {
                int dataIdx = shuffledSlotOrder[slotIdx];
                RuneData data = allRuneData[dataIdx];


                runeSlots[slotIdx].expectedRuneId = data.runeId;

                // Mặc định giấu hình dáng thật đi bằng hình basic

                if (defaultSlotSprite != null)
                {
                    runeSlots[slotIdx].UpdateVisualSprite(defaultSlotSprite);
                }
                else
                {
                    runeSlots[slotIdx].UpdateVisualSprite(data.slotSprite); // Fallback nếu chưa gán default
                }
            }

            UpdateRiddleDisplay();
            UpdateSlotsVisibility(); // Đảm bảo chỉ rãnh đầu tiên hiện ra
        }

        public void UpdateRiddleDisplay()
        {
            if (riddleText == null) return;

            // Tìm slot ĐẦU TIÊN chưa được giải

            string currentRiddle = "";
            for (int i = 0; i < runeSlots.Count; i++)
            {
                if (!runeSlots[i].IsCorrectRunePlaced())
                {
                    RuneData data = GetRuneDataById(runeSlots[i].expectedRuneId);
                    if (data != null)
                    {
                        currentRiddle = data.riddleText;
                    }
                    break; // Chỉ lấy câu đố đầu tiên tìm thấy (theo thứ tự từ trái sang phải)
                }
            }

            riddleText.text = currentRiddle;


            Color c = riddleText.color;
            c.a = 0f;
            riddleText.color = c;
        }

        private IEnumerator PuzzleSolvedSequence()
        {
            // Hiện thông báo chiến thắng
            if (riddleText != null)
            {
                riddleText.text = "Ngươi đã chứng minh được bản thân";
                Color c = riddleText.color;
                c.a = 1f;
                riddleText.color = c;
            }

            if (mindLampButton != null)
            {
                mindLampButton.gameObject.SetActive(false);
            }

            // 1. Toàn bộ rune nháy sáng
            foreach (var stone in runeStones)
            {
                if (stone == null) continue;
                RuneData data = GetRuneDataById(stone.runeId);
                if (data != null) stone.PlayCorrectEffect(data.elementColor);
            }

            foreach (var slot in runeSlots)
            {
                if (slot == null) continue;
                RuneData data = GetRuneDataById(slot.expectedRuneId);
                if (data != null) slot.TransitionToSprite(data.slotSprite);
            }

            yield return new WaitForSeconds(solvedFlashHoldDuration);

            yield return StartCoroutine(FadeFogOut());
            yield return new WaitForSeconds(Mathf.Max(0f, videoDelayAfterFogOff));

            ChangeVideoBackground activeVideoChanger = TriggerVideoClip2();

            // 2. Tắt mượt các cụm UI của minigame sau khi sương mù đã biến mất.
            yield return StartCoroutine(HideSolvedTargetsSmoothly());

            if (activeVideoChanger != null)
            {
                yield return StartCoroutine(activeVideoChanger.WaitForClip2AndDoorEffect());
            }

            // 3. Trở về màn trước đó
            if (returnToPreviousTimelineSceneOnSolved && global::TimelineSceneTravelManager.HasPreviousScene)
            {
                global::TimelineSceneTravelManager.ReturnToPreviousScene();
                yield break;
            }

            gameObject.SetActive(false);
        }

        private ChangeVideoBackground TriggerVideoClip2()
        {
            if (videoBackgroundChanger == null)
            {
                videoBackgroundChanger = Object.FindFirstObjectByType<ChangeVideoBackground>(FindObjectsInactive.Include);
            }

            if (videoBackgroundChanger != null)
            {
                videoBackgroundChanger.ChangeToVideoClip2();
            }

            return videoBackgroundChanger;
        }

        private IEnumerator HideSolvedTargetsSmoothly()
        {
            EnsureSolvedHideTargets();

            if (solvedHideTargets.Count == 0)
            {
                yield return new WaitForSeconds(fogFadeDuration);
                yield break;
            }

            List<CanvasGroup> groups = new List<CanvasGroup>();
            List<float> startAlphas = new List<float>();
            List<RectTransform> rects = new List<RectTransform>();
            List<Vector3> startScales = new List<Vector3>();
            List<Vector3> targetScales = new List<Vector3>();

            foreach (GameObject target in solvedHideTargets)
            {
                if (target == null || !target.activeInHierarchy) continue;

                CanvasGroup group = target.GetComponent<CanvasGroup>();
                if (group == null) group = target.AddComponent<CanvasGroup>();
                groups.Add(group);
                startAlphas.Add(group.alpha);

                RectTransform rect = target.GetComponent<RectTransform>();
                if (rect != null)
                {
                    if (!solvedTargetOriginalScales.ContainsKey(rect))
                    {
                        solvedTargetOriginalScales[rect] = rect.localScale;
                    }

                    Vector3 startScale = rect.localScale;
                    rects.Add(rect);
                    startScales.Add(startScale);
                    targetScales.Add(solvedTargetOriginalScales[rect] * solvedHideScale);
                }
            }

            float duration = Mathf.Max(0.01f, solvedHideDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                for (int i = 0; i < groups.Count; i++)
                {
                    groups[i].alpha = Mathf.Lerp(startAlphas[i], 0f, ease);
                }

                for (int i = 0; i < rects.Count; i++)
                {
                    rects[i].localScale = Vector3.Lerp(startScales[i], targetScales[i], ease);
                }

                yield return null;
            }

            foreach (CanvasGroup group in groups)
            {
                group.alpha = 0f;
            }

            foreach (GameObject target in solvedHideTargets)
            {
                if (target != null) target.SetActive(false);
            }
        }

        private IEnumerator FadeFogOut()
        {
            if (fogCanvasGroup == null && extraFogCanvasGroup == null)
            {
                yield break;
            }

            float startFogAlpha = fogCanvasGroup != null ? fogCanvasGroup.alpha : 0f;
            float startExtraFogAlpha = extraFogCanvasGroup != null ? extraFogCanvasGroup.alpha : 0f;
            float duration = Mathf.Max(0.01f, fogFadeDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (fogCanvasGroup != null)
                {
                    fogCanvasGroup.alpha = Mathf.Lerp(startFogAlpha, 0f, ease);
                }

                if (extraFogCanvasGroup != null)
                {
                    extraFogCanvasGroup.alpha = Mathf.Lerp(startExtraFogAlpha, 0f, ease);
                }

                yield return null;
            }

            UpdateFogImmediate(0f);
        }

        private void EnsureSolvedHideTargets()
        {
            solvedHideTargets.RemoveAll(target => target == null);
            if (solvedHideTargets.Count > 0) return;

            AddSolvedHideTarget("MainPanel");
            AddSolvedHideTarget("GlowVignette");
            AddSolvedHideTarget("FogOverlay");
            AddSolvedHideTarget("KinhLup");
            AddSolvedHideTarget("PanelTutorial");
        }

        private void AddSolvedHideTarget(string targetName)
        {
            GameObject foundObject = GameObject.Find(targetName);
            if (foundObject != null)
            {
                solvedHideTargets.Add(foundObject);
                return;
            }

            Transform found = FindChildByName(transform.root, targetName);
            if (found != null)
            {
                solvedHideTargets.Add(found.gameObject);
            }
        }

        private Transform FindChildByName(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name == targetName) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildByName(parent.GetChild(i), targetName);
                if (found != null) return found;
            }

            return null;
        }

        private IEnumerator ShakeEffect()
        {
            if (runeSlots.Count == 0) yield break;
            Transform parent = runeSlots[0].transform.parent;
            if (parent == null) yield break;

            Vector3 originalPos = parent.localPosition;
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                float x = Random.Range(-1f, 1f) * shakeIntensity;
                float y = Random.Range(-1f, 1f) * shakeIntensity;
                parent.localPosition = originalPos + new Vector3(x, y, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            parent.localPosition = originalPos;
        }

        private RuneData GetRuneDataById(string id)
        {
            foreach (var data in allRuneData)
            {
                if (data.runeId == id) return data;
            }
            return null;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private void UpdateFogImmediate(float alpha)
        {
            if (fogCanvasGroup != null) fogCanvasGroup.alpha = alpha;
            if (extraFogCanvasGroup != null) extraFogCanvasGroup.alpha = alpha;
        }
    }
}
