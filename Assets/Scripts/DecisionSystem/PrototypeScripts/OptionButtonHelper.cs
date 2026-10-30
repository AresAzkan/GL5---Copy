using UnityEngine;
using UnityEngine.UI;

public class OptionButtonHelper : MonoBehaviour
{
    [SerializeField]
    private int _optionIndex;
    private Button _button;
    void Start()
    {
        _button = GetComponent<Button>();
        SetButtonCallbacks();
    }

    public void Configure(int optionIndex)
    {
        _optionIndex = optionIndex;
    }

    void SetButtonCallbacks()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(() => MessagesManager.Instance.GetOptionsChanger().ChangeOptions(_optionIndex));
        }
    }
}
