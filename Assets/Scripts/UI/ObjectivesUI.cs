using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ObjectivesUI : MonoBehaviour
{
    private const float ObjectiveTopOffset = 82f;
    private const float BottomPadding = 22f;
    private const float MinimumPanelHeight = 150f;

    [Header("UI Components")]
    [Tooltip("The TextMeshPro text element that will show the objective description.")]
    [SerializeField] private TextMeshProUGUI _objectiveText;
    
    [Tooltip("The GameObject representing the whole objectives panel (so it can be hidden if there are no objectives).")]
    [SerializeField] private GameObject _panel;

    private RectTransform _backgroundRect;

    private void Start()
    {
        // Subscribe to the objectives manager event
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated += UpdateUI;
            // Manually update UI once to ensure it doesn't say "New Text"
            UpdateUI(ObjectivesManager.Instance.GetCurrentObjective(), ObjectivesManager.Instance.GetCurrentProgress());
            StartCoroutine(ResizeAfterLayout());
        }
        else
        {
            Debug.LogWarning("[ObjectivesUI] ObjectivesManager instance not found!");
        }
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent memory leaks
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated -= UpdateUI;
        }
    }

    private void UpdateUI(Objective currentObj, int progress)
    {
        if (currentObj == null)
        {
            if (_panel != null) _panel.SetActive(false);
            if (_objectiveText != null) _objectiveText.text = "";
            return;
        }

        if (!currentObj.IsActive())
        {
            if (_panel != null) _panel.SetActive(false);
            if (_objectiveText != null) _objectiveText.text = "";
            return;
        }

        // Ensure the panel is visible if we have an active objective
        if (_panel != null && !_panel.activeSelf)
        {
            _panel.SetActive(true);
        }

        // If the target count is greater than 1, we show a progress counter like (1/3)
        if (currentObj.TargetCount() > 1)
        {
            _objectiveText.text = $"{currentObj.Description()} ({progress}/{currentObj.TargetCount()})";
        }
        else
        {
            // If the target is just 1 (e.g. "Go to class"), we just show the description
            _objectiveText.text = currentObj.Description();
        }

        ResizePanelToText();
    }

    private IEnumerator ResizeAfterLayout()
    {
        yield return null;
        ResizePanelToText();
    }

    private void ResizePanelToText()
    {
        if (_panel == null || _objectiveText == null || string.IsNullOrEmpty(_objectiveText.text)) return;

        if (_backgroundRect == null)
        {
            _backgroundRect = _panel.transform.Find("BackGround") as RectTransform;
        }

        if (_backgroundRect == null) return;

        RectTransform objectiveRect = _objectiveText.rectTransform;
        Canvas.ForceUpdateCanvases();
        _objectiveText.ForceMeshUpdate();

        float availableWidth = Mathf.Max(1f, objectiveRect.rect.width);
        float textHeight = Mathf.Ceil(
            _objectiveText.GetPreferredValues(_objectiveText.text, availableWidth, 0f).y);

        objectiveRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(_objectiveText.fontSize, textHeight));

        float panelHeight = Mathf.Max(
            MinimumPanelHeight,
            ObjectiveTopOffset + textHeight + BottomPadding);

        _backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_backgroundRect);
    }
}
