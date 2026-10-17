// MINIgame/PuzzleManager.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

namespace Tien.Minigames
{
    public class PuzzleManager : MonoBehaviour
    {
        [Header("UI Cài đặt")]
        [Tooltip("Panel UI chính của mini game (chứa nút X tắt và Board)")]
        public GameObject puzzleUI; 
        [Tooltip("Panel con chứa lưới 9 mảnh (Nên gắn component GridLayoutGroup 3x3)")]
        public Transform boardTransform; 
        [Tooltip("Prefab của 1 mảnh ghép (UI có RawImage và script PuzzlePiece)")]
        public GameObject piecePrefab; 
        [Tooltip("Ảnh dùng để chơi ghép hình")]
        public Texture2D puzzleImage; 

        [Header("Phần thưởng Cầu")]
        [Tooltip("Object Cầu sẽ hiện ra sau khi hoàn thành")]
        public GameObject bridgeObject; 

        private List<PuzzlePiece> pieces = new List<PuzzlePiece>();
        private PuzzlePiece firstSelectedPiece = null;
        private bool isCompleted = false;
        private Player currentPlayer;

        void Awake()
        {
            if (puzzleUI != null) puzzleUI.SetActive(false);
        }

        // Hỗ trợ cả 2D và 3D Collider
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                currentPlayer = other.GetComponent<Player>();
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                currentPlayer = null;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                currentPlayer = other.GetComponent<Player>();
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                currentPlayer = null;
            }
        }

        // Hàm Update đã bị xóa vì giờ NPC sẽ gọi OpenPuzzle()

        public void OpenPuzzle()
        {
            if (isCompleted || currentPlayer == null) return;

            puzzleUI.SetActive(true);
            // Khóa điều khiển của người chơi
            currentPlayer.SetInputActive(false);

            if (pieces.Count == 0)
            {
                AdjustBoardSize();
                GeneratePieces();
            }
            else
            {
                ShufflePieces();
            }
        }

        private void AdjustBoardSize()
        {
            if (puzzleImage == null || boardTransform == null) return;

            RectTransform boardRect = boardTransform.GetComponent<RectTransform>();
            GridLayoutGroup grid = boardTransform.GetComponent<GridLayoutGroup>();

            if (boardRect == null || grid == null) return;

            // Giới hạn khung hình tối đa là 600px
            float maxBoardSize = 600f;
            float imgWidth = puzzleImage.width;
            float imgHeight = puzzleImage.height;
            float aspect = imgWidth / imgHeight;

            float boardWidth = maxBoardSize;
            float boardHeight = maxBoardSize;

            if (imgWidth > imgHeight) // Ảnh ngang
            {
                boardHeight = maxBoardSize / aspect;
            }
            else // Ảnh dọc hoặc vuông
            {
                boardWidth = maxBoardSize * aspect;
            }

            boardRect.sizeDelta = new Vector2(boardWidth, boardHeight);

            // Tính kích thước từng mảnh ghép (3x3 có 2 khoảng cách ở giữa)
            float cellW = (boardWidth - (grid.spacing.x * 2)) / 3f;
            float cellH = (boardHeight - (grid.spacing.y * 2)) / 3f;
            grid.cellSize = new Vector2(cellW, cellH);
        }

        // Gắn hàm này vào sự kiện OnClick của nút "Thoát" (Dấu X)
        public void ClosePuzzle()
        {
            if (isCompleted) return; // Nếu đã xong thì không cho thoát ngang
            
            puzzleUI.SetActive(false);
            if (firstSelectedPiece != null)
            {
                firstSelectedPiece.Highlight(false);
                firstSelectedPiece = null;
            }
            
            // Mở lại điều khiển
            if (currentPlayer != null)
                currentPlayer.SetInputActive(true);
        }

        private void GeneratePieces()
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    
                    GameObject obj = Instantiate(piecePrefab, boardTransform);
                    PuzzlePiece piece = obj.GetComponent<PuzzlePiece>();
                    
                    // Tính toán UV để lấy 1 phần của ảnh
                    float w = 1f / 3f;
                    float h = 1f / 3f;
                    // Unity UV: Y hướng từ dưới lên
                    float x = col * w;
                    float y = (2 - row) * h; 
                    Rect uvRect = new Rect(x, y, w, h);

                    piece.Setup(index, puzzleImage, uvRect, this);
                    piece.currentIndex = index;
                    pieces.Add(piece);
                }
            }
            ShufflePieces();
        }

        private void ShufflePieces()
        {
            // Hoán đổi ngẫu nhiên vị trí các mảnh ghép
            for (int i = 0; i < pieces.Count; i++)
            {
                int rand = Random.Range(0, pieces.Count);
                SwapPieces(pieces[i], pieces[rand]);
            }
            
            if (firstSelectedPiece != null)
            {
                firstSelectedPiece.Highlight(false);
                firstSelectedPiece = null;
            }
        }

        public void OnPieceClicked(PuzzlePiece clickedPiece)
        {
            if (isCompleted) return;

            if (firstSelectedPiece == null)
            {
                // Chọn mảnh đầu tiên
                firstSelectedPiece = clickedPiece;
                firstSelectedPiece.Highlight(true);
            }
            else
            {
                // Nếu click lại mảnh đang chọn -> Hủy chọn
                if (firstSelectedPiece == clickedPiece)
                {
                    firstSelectedPiece.Highlight(false);
                    firstSelectedPiece = null;
                    return;
                }

                // Hoán đổi 2 mảnh
                SwapPieces(firstSelectedPiece, clickedPiece);
                firstSelectedPiece.Highlight(false);
                firstSelectedPiece = null;

                CheckCompletion();
            }
        }

        private void SwapPieces(PuzzlePiece a, PuzzlePiece b)
        {
            // Đổi chỗ trong UI bằng cách thay đổi SiblingIndex
            int indexA = a.transform.GetSiblingIndex();
            int indexB = b.transform.GetSiblingIndex();

            a.transform.SetSiblingIndex(indexB);
            b.transform.SetSiblingIndex(indexA);

            // Đổi currentIndex
            int tempIdx = a.currentIndex;
            a.currentIndex = b.currentIndex;
            b.currentIndex = tempIdx;
        }

        private void CheckCompletion()
        {
            foreach (var piece in pieces)
            {
                if (piece.currentIndex != piece.correctIndex)
                {
                    return; // Chưa hoàn thành
                }
            }

            // Hoàn thành
            isCompleted = true;
            Debug.Log("Ghép hình thành công!");
            StartCoroutine(CompleteMinigameRoutine());
        }

        private IEnumerator CompleteMinigameRoutine()
        {
            // Đợi 0.5s cho người chơi nhìn thấy hình hoàn chỉnh
            yield return new WaitForSeconds(0.5f);
            
            // Cập nhật trạng thái Cầu (đổi từ gãy sang hoàn thành)
            if (bridgeObject != null)
            {
                Bridge bridgeScript = bridgeObject.GetComponent<Bridge>();
                if (bridgeScript != null)
                {
                    bridgeScript.SetVisuals(true);
                }
            }

            // Gọi Cutscene chia tay của ông lão NPC
            if (NPCQuestGiver.Instance != null)
            {
                NPCQuestGiver.Instance.TriggerPanCameraToBridge(2f);
            }
            else
            {
                // Mở lại điều khiển nếu không có NPC
                if (currentPlayer != null)
                    currentPlayer.SetInputActive(true);
            }

            // Tắt UI sau cùng để tránh lỗi Coroutine bị ngắt giữa chừng
            if (puzzleUI != null) puzzleUI.SetActive(false);
        }
    }
}
