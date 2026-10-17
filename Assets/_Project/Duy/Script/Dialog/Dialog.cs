using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Dialog : MonoBehaviour
{
    public static Dialog ActiveDialog { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject _dialogRoot;

    [Header("Portraits")]
    [SerializeField] private Image _dialog_left;
    [SerializeField] private Image _dialog_right;

    [Header("Talk Text")]
    [SerializeField] private TextMeshProUGUI _textTalk;

    [Header("Choice Button")]
    [SerializeField] private Button _choiceButton;
    [SerializeField] private TextMeshProUGUI _choiceLabel;

    [Header("Talk UI")]
    [SerializeField] private Color _activeColor = Color.white;
    [SerializeField] private Color _inactiveColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private float _shakeDistance = 6f;
    [SerializeField] private float _shakeDuration = 0.12f;
    [SerializeField] private float _shakeFrequency = 45f;

    [Header("Source")]
    [SerializeField] private DialogConversation _conversation;

    private RectTransform _leftRect;
    private RectTransform _rightRect;
    private Vector2 _leftBasePos;
    private Vector2 _rightBasePos;
    private Coroutine _leftShakeRoutine;
    private Coroutine _rightShakeRoutine;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        ActiveDialog = this;

        AutoWireReferences();

        if (_dialog_left != null)
        {
            _leftRect = _dialog_left.rectTransform;
            _leftBasePos = _leftRect.anchoredPosition;
        }

        if (_dialog_right != null)
        {
            _rightRect = _dialog_right.rectTransform;
            _rightBasePos = _rightRect.anchoredPosition;
        }

        if (_choiceButton != null)
        {
            _choiceButton.onClick.RemoveAllListeners();
            _choiceButton.onClick.AddListener(SelectChoice);
        }

        SetVisible(false);
    }

    private void AutoWireReferences()
    {
        if (_dialogRoot == null)
        {
            _dialogRoot = gameObject;
        }

        if (_dialog_left == null)
        {
            _dialog_left = GetComponentInChildren<Image>(true);
        }

        if (_dialog_right == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i] != _dialog_left)
                {
                    _dialog_right = images[i];
                    break;
                }
            }
        }

        if (_textTalk == null)
        {
            _textTalk = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_choiceButton == null)
        {
            _choiceButton = GetComponentInChildren<Button>(true);
        }

        if (_choiceLabel == null && _choiceButton != null)
        {
            _choiceLabel = _choiceButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_conversation == null)
        {
            _conversation = GetComponent<DialogConversation>();
        }
    }

    private void OnDestroy()
    {
        if (ActiveDialog == this)
        {
            ActiveDialog = null;
        }
    }

    private void Update()
    {
        if (!_isOpen)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Continue();
        }
    }

    public void SetConversation(DialogConversation conversation)
    {
        _conversation = conversation;
    }

    public void Enable()
    {
        if (_dialogRoot != null)
        {
            _dialogRoot.SetActive(true);
        }

        _isOpen = true;
        if (_conversation != null)
        {
            _conversation.StartConversation();
            ShowCurrent();
        }
        else
        {
            ClearView();
        }
    }

    public void Disable()
    {
        _isOpen = false;
        ClearView();

        if (_dialogRoot != null)
        {
            _dialogRoot.SetActive(false);
        }
    }

    public void Continue()
    {
        if (_conversation == null)
        {
            return;
        }

        if (_conversation.HasChoice)
        {
            ShowChoice(_conversation.CurrentChoice);
            return;
        }

        if (!_conversation.Advance())
        {
            Disable();
            return;
        }

        ShowCurrent();
    }

    public void SelectChoice()
    {
        if (_conversation == null || !_conversation.HasChoice)
        {
            return;
        }

        if (!_conversation.SelectChoice())
        {
            return;
        }

        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (_conversation == null || !_conversation.HasCurrentLine)
        {
            Disable();
            return;
        }

        ClearTalkText();
        ApplySpeaker(_conversation.CurrentSpeakerSide);
        SetTalkText(_conversation.CurrentText);
        ShowChoice(_conversation.CurrentChoice);
    }

    private void ApplySpeaker(DialogSpeakerSide speakerSide)
    {
        int speakerIndex = speakerSide == DialogSpeakerSide.Left ? 0 : 1;
        if (_dialog_left != null)
        {
            _dialog_left.color = speakerIndex == 0 ? _activeColor : _inactiveColor;
        }

        if (_dialog_right != null)
        {
            _dialog_right.color = speakerIndex == 1 ? _activeColor : _inactiveColor;
        }

        StopShake(_leftShakeRoutine, _leftRect, _leftBasePos);
        StopShake(_rightShakeRoutine, _rightRect, _rightBasePos);

        if (speakerIndex == 0)
        {
            _leftShakeRoutine = StartCoroutine(ShakePortrait(_leftRect, _leftBasePos));
        }
        else if (speakerIndex == 1)
        {
            _rightShakeRoutine = StartCoroutine(ShakePortrait(_rightRect, _rightBasePos));
        }
    }

    private void SetTalkText(string content)
    {
        if (_textTalk != null)
        {
            _textTalk.text = content;
        }
    }

    private void ClearTalkText()
    {
        if (_textTalk != null)
        {
            _textTalk.text = string.Empty;
        }
    }

    private void ShowChoice(DialogChoice choice)
    {
        if (_choiceButton == null || _choiceLabel == null)
        {
            return;
        }

        bool active = choice.IsValid;
        _choiceButton.gameObject.SetActive(active);
        _choiceButton.interactable = active;
        _choiceLabel.text = active ? choice.label : string.Empty;
    }

    private void ClearView()
    {
        ClearTalkText();
        ShowChoice(DialogChoice.Empty);
    }

    private void SetVisible(bool visible)
    {
        _isOpen = visible;

        if (_dialogRoot != null)
        {
            _dialogRoot.SetActive(visible);
        }
    }

    private void StopShake(Coroutine routine, RectTransform rect, Vector2 basePos)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        if (rect != null)
        {
            rect.anchoredPosition = basePos;
        }
    }

    private IEnumerator ShakePortrait(RectTransform rect, Vector2 basePos)
    {
        if (rect == null)
        {
            yield break;
        }

        float timer = 0f;
        while (timer < _shakeDuration)
        {
            timer += Time.deltaTime;
            float offsetX = Mathf.Sin(timer * _shakeFrequency) * _shakeDistance;
            float offsetY = Mathf.Cos(timer * _shakeFrequency * 0.5f) * (_shakeDistance * 0.35f);
            rect.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
            yield return null;
        }

        rect.anchoredPosition = basePos;
    }
}
