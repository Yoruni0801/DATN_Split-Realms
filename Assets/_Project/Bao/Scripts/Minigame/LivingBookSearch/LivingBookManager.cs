using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Minigame.LivingBookSearch
{
    public enum BookBehaviorType
    {
        FlashColor, // Trục Color: Chớp màu (Đúng/Sai)
        FloatUp,    // Trục Position Y: Bay bồng bềnh
        Breathe,    // Trục Scale: Phình to xẹp xuống
        Jitter,     // Trục Rotation: Rung lắc/xoay
        Phantom     // Trục Alpha: Mờ tỏ ma mị
    }

    [System.Serializable]
    public class BookHintData
    {
        [TextArea]
        public string hintText;         // Câu gợi ý
        public BookBehaviorType targetBehavior; // Hành vi ĐỘC QUYỀN của sách thật trong màn này
        public Color secretGlowColor = Color.white; // Dùng nếu targetBehavior là FlashColor
        public float timeToReveal = 3f; // Bao lâu thì sách làm hành vi này một lần
    }

    public class LivingBookManager : MonoBehaviour
    {
        public static LivingBookManager Instance { get; private set; }

        [Header("=== DATA ===")]
        public List<BookHintData> hintDatabase = new List<BookHintData>();

        [Header("=== REFERENCES ===")]
        public List<LivingBook> books = new List<LivingBook>();
        public TextMeshProUGUI hintText;
        public CanvasGroup minigameCanvasGroup; // Để fade out khi thắng

        private bool isGameActive = false;
        private LivingBook correctBook;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            StartNewGame();
        }

        public void StartNewGame()
        {
            if (books.Count == 0 || hintDatabase.Count == 0) return;

            isGameActive = true;

            // Dọn dẹp danh sách lỡ người chơi lỡ xóa bớt sách trong Inspector mà quên xóa trong Manager
            books.RemoveAll(b => b == null);
            if (books.Count == 0) return;

            // Đổi chỗ: Thu thập danh sách vị trí
            List<Vector2> allPositions = new List<Vector2>();
            foreach (var book in books)
            {
                var rect = book.GetComponent<RectTransform>();
                if (rect != null)
                {
                    allPositions.Add(rect.anchoredPosition);
                }
                else
                {
                    allPositions.Add(Vector2.zero); // An toàn nếu sách mất RectTransform
                }
            }

            // Fisher-Yates xịn để xáo trộn hoàn toàn mảng vị trí
            for (int i = 0; i < allPositions.Count; i++)
            {
                int randomIndex = Random.Range(i, allPositions.Count);
                Vector2 temp = allPositions[i];
                allPositions[i] = allPositions[randomIndex];
                allPositions[randomIndex] = temp;
            }

            // Gán lại vị trí mới (nếu xếp tay) và xáo trộn luôn SiblingIndex (nếu dùng Grid)
            for (int i = 0; i < books.Count; i++)
            {
                books[i].GetComponent<RectTransform>().anchoredPosition = allPositions[i];
                books[i].transform.SetSiblingIndex(Random.Range(0, books.Count));
                books[i].SaveOriginalPosition();
            }

            // 1. Chọn ngẫu nhiên 1 cuốn sách thật
            int correctIndex = Random.Range(0, books.Count);
            correctBook = books[correctIndex];

            // 2. Chọn ngẫu nhiên 1 câu đố/màu sắc
            int hintIndex = Random.Range(0, hintDatabase.Count);
            BookHintData chosenHint = hintDatabase[hintIndex];

            if (hintText != null)
            {
                hintText.text = chosenHint.hintText;
            }

            // 3. Setup logic cho từng cuốn sách
            for (int i = 0; i < books.Count; i++)
            {
                bool isReal = (books[i] == correctBook);
                books[i].SetupBook(isReal, chosenHint);
            }
            // Khởi động Shuffle vị trí định kỳ trong khi chơi
            StartCoroutine(RandomSwapRoutine());
        }

        private IEnumerator RandomSwapRoutine()
        {
            while (isGameActive)
            {
                // Cứ mỗi 8-15 giây sẽ có một đợt xáo trộn vị trí của 2 quyển sách
                yield return new WaitForSeconds(Random.Range(8f, 15f));

                // Lọc ra tất cả các sách ĐANG NGỦ và KHÔNG ĐANG BAY
                List<LivingBook> idleBooks = books.FindAll(b => b.IsIdle && !b.IsSwapping);
                
                if (idleBooks.Count >= 2)
                {
                    // Chọn ngẫu nhiên 2 quyển sách khác nhau
                    int i1 = Random.Range(0, idleBooks.Count);
                    int i2 = Random.Range(0, idleBooks.Count);
                    while (i1 == i2) i2 = Random.Range(0, idleBooks.Count);

                    LivingBook b1 = idleBooks[i1];
                    LivingBook b2 = idleBooks[i2];

                    Vector2 pos1 = b1.GetOriginalPosition();
                    Vector2 pos2 = b2.GetOriginalPosition();

                    // Ra lệnh hoán đổi vị trí
                    b1.StartSwap(pos2);
                    b2.StartSwap(pos1);

                    // Đổi Sibling Index để đảm bảo UI không bị lỗi hiển thị (đè lên nhau)
                    int sibling1 = b1.transform.GetSiblingIndex();
                    int sibling2 = b2.transform.GetSiblingIndex();
                    b1.transform.SetSiblingIndex(sibling2);
                    b2.transform.SetSiblingIndex(sibling1);
                }
            }
        }

        public void OnBookClicked(LivingBook clickedBook)
        {
            if (!isGameActive) return;

            if (clickedBook == correctBook)
            {
                // Thắng!
                isGameActive = false;
                Debug.Log("<color=green>✅ ĐÚNG RỒI! Bạn đã tìm ra Thạch Hồn Cổ Thư!</color>");
                StartCoroutine(WinSequence(clickedBook));
            }
            else
            {
                // Sai!
                Debug.Log("<color=red>❌ Sai rồi! Sách giả đang lừa bạn!</color>");
                clickedBook.PlayWrongEffect();
                
                // Screen Shake siêu rẻ nhưng cực kỳ "đã mắt"
                StartCoroutine(ScreenShakeRoutine());
            }
        }

        private IEnumerator ScreenShakeRoutine()
        {
            if (Camera.main == null) yield break;
            
            Vector3 origPos = Camera.main.transform.position;
            float elapsed = 0f;
            float duration = 0.2f;
            float magnitude = 0.15f; // Rung nhẹ thôi để không chóng mặt

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;

                Camera.main.transform.position = origPos + new Vector3(x, y, 0);
                yield return null;
            }

            Camera.main.transform.position = origPos;
        }

        private IEnumerator WinSequence(LivingBook correctBook)
        {
            // Các sách giả im lặng/tắt đi
            foreach (var book in books)
            {
                if (book != correctBook)
                {
                    book.StopAllBehaviors();
                    book.FadeOut();
                }
            }

            // Sách thật phát sáng rực rỡ
            correctBook.StopAllBehaviors();
            correctBook.PlayCorrectEffect();

            if (hintText != null)
            {
                hintText.text = "⚡ Đã tìm thấy Thạch Hồn Cổ Thư ⚡";
                hintText.color = new Color(1f, 0.85f, 0.2f, 1f);
            }

            yield return new WaitForSeconds(2.5f);

            // Tắt toàn bộ Minigame (Tắt Canvas cha chứa minigame)
            if (minigameCanvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < 1.5f)
                {
                    elapsed += Time.deltaTime;
                    minigameCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 1.5f);
                    yield return null;
                }
            }
            
            // Tìm Canvas ngoài cùng chứa Minigame này và tắt nó
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvas.gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
