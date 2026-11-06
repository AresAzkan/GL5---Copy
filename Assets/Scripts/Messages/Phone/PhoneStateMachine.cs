using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[Serializable]
public enum PhoneState
{
    IDLE,       //0
    MESSAGING,  //1
    CONTACTS,   //2
    GALLERY,    //3
    EMAIL,      //4
}

/// <summary>
/// State machine for the phone interface, managing different states and transitions.
/// </summary>
public class PhoneStateMachine : MonoBehaviour
{
    private PhoneState _currentState = PhoneState.IDLE;

    [Header("Buttons")]
    [SerializeField]
    private Button _backButton;

    [Header("Tabs")]
    [SerializeField]
    private GameObject _startTab;
    [SerializeField]
    private GameObject _contactsTab;

    [SerializeField]
    private GameObject _messagingTab;

    [SerializeField]
    private GameObject _galleryTab;

    [SerializeField]
    private GameObject _emailTab;

    [SerializeField]
    private GameObject _dialogueTab;

    [Header("Gallery")]
    [SerializeField]
    private Sprite[] _galleryPictures;


    private int _backButtonState = 0; // State to change to when the back button is pressed
    private RectTransform _galleryContentArea;
    private GameObject _galleryGrid;
    private GameObject _galleryViewer;
    private Image _galleryViewerImage;
    private bool _galleryBuilt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _backButton.onClick.AddListener(() => ChangeState(_backButtonState));
        EventBus.Instance.Subscribe<int>("Phone_Closed", ChangeState);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Changes the current state to the specified new state and updates the button callbacks accordingly.
    /// </summary>
    /// <param name="newState"></param>
    public void ChangeState(int newState)
    {
        if (_currentState == (PhoneState)newState) return;

        _backButtonState = (int)_currentState;
        ExitCurrentState();
        _currentState = (PhoneState)newState;
        EnterNewState();
    }

    /// <summary>
    /// Disable the tab associated to the current state
    /// </summary>
    private void ExitCurrentState()
    {
        switch (_currentState)
        {
            case PhoneState.IDLE:
                _startTab?.SetActive(false);
                break;
            case PhoneState.CONTACTS:
                _contactsTab?.SetActive(false);
                break;
            case PhoneState.MESSAGING:
                _messagingTab?.SetActive(false);
                _dialogueTab?.SetActive(false);
                _backButtonState = (int)PhoneState.IDLE;
                break;
            case PhoneState.GALLERY:
                _galleryTab?.SetActive(false);
                break;
            case PhoneState.EMAIL:
                _emailTab?.SetActive(false);
                break;
        }

    }

    /// <summary>
    /// Enable the tab associated with the new state and calls the required methods if needed
    /// </summary>
    private void EnterNewState()
    {
        switch (_currentState)
        {
            case PhoneState.IDLE:
                _startTab?.SetActive(true);
                break;
            case PhoneState.CONTACTS:
                _contactsTab?.SetActive(true);
                MessagesManager.Instance?.GetMessagesUI()?.RefreshContacts();
                break;
            case PhoneState.MESSAGING:
                _messagingTab?.SetActive(true);
                break;
            case PhoneState.GALLERY:
                _galleryTab?.SetActive(true);
                EnsureGalleryBuilt();
                ShowGalleryGrid();
                break;
            case PhoneState.EMAIL:
                _emailTab?.SetActive(true);
                break;
        }

    }

    private void EnsureGalleryBuilt()
    {
        if (_galleryBuilt || _galleryTab == null) return;

        Transform contentTransform = _galleryTab.transform.Find("GalleryContent");
        _galleryContentArea = contentTransform as RectTransform;
        if (_galleryContentArea == null)
        {
            Debug.LogWarning("[PhoneGallery] GalleryContent was not found.");
            return;
        }

        BuildGalleryGrid();
        BuildGalleryViewer();
        _galleryBuilt = true;
    }

    private void BuildGalleryGrid()
    {
        GameObject scrollObject = new GameObject(
            "GalleryGrid",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect));
        scrollObject.layer = _galleryTab.layer;
        scrollObject.transform.SetParent(_galleryContentArea, false);
        _galleryGrid = scrollObject;

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        StretchToParent(scrollRectTransform);

        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(0f, 0f, 0f, 0.01f);
        scrollBackground.raycastTarget = true;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D));
        viewportObject.layer = _galleryTab.layer;
        viewportObject.transform.SetParent(scrollRectTransform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        StretchToParent(viewportRect);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("Pictures", typeof(RectTransform), typeof(GridLayoutGroup));
        contentObject.layer = _galleryTab.layer;
        contentObject.transform.SetParent(viewportRect, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        float availableWidth = Mathf.Max(_galleryContentArea.rect.width, 100f);
        Vector3 inheritedScale = _galleryContentArea.lossyScale;
        float inheritedScaleX = Mathf.Max(Mathf.Abs(inheritedScale.x), 0.0001f);
        float inheritedScaleY = Mathf.Max(Mathf.Abs(inheritedScale.y), 0.0001f);
        const int columns = 2;
        const float padding = 4f;
        const float spacing = 4f;
        float cellWidth = (availableWidth - (padding * 2f) - spacing) / columns;
        float cellHeight = cellWidth * (inheritedScaleX / inheritedScaleY);
        int pictureCount = _galleryPictures != null ? _galleryPictures.Length : 0;
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(pictureCount / (float)columns));
        float contentHeight = (padding * 2f) + (rowCount * cellHeight) + ((rowCount - 1) * spacing);
        contentRect.sizeDelta = new Vector2(0f, contentHeight);

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        grid.spacing = new Vector2(spacing, spacing);
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        for (int i = 0; i < pictureCount; i++)
        {
            Sprite picture = _galleryPictures[i];
            if (picture == null) continue;
            CreateGalleryThumbnail(contentRect, picture, cellWidth, cellHeight, inheritedScaleX, inheritedScaleY, i + 1);
        }

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 18f;
    }

    private void CreateGalleryThumbnail(
        RectTransform parent,
        Sprite picture,
        float cellWidth,
        float cellHeight,
        float inheritedScaleX,
        float inheritedScaleY,
        int index)
    {
        GameObject buttonObject = new GameObject(
            $"Picture_{index}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = _galleryTab.layer;
        buttonObject.transform.SetParent(parent, false);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => OpenGalleryPicture(picture));

        GameObject imageObject = new GameObject(
            "Thumbnail",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = _galleryTab.layer;
        imageObject.transform.SetParent(buttonObject.transform, false);

        Image thumbnail = imageObject.GetComponent<Image>();
        thumbnail.sprite = picture;
        thumbnail.type = Image.Type.Simple;
        thumbnail.preserveAspect = false;
        thumbnail.raycastTarget = false;

        RectTransform thumbnailRect = thumbnail.rectTransform;
        thumbnailRect.anchorMin = new Vector2(0.5f, 0.5f);
        thumbnailRect.anchorMax = new Vector2(0.5f, 0.5f);
        thumbnailRect.pivot = new Vector2(0.5f, 0.5f);
        thumbnailRect.anchoredPosition = Vector2.zero;
        thumbnailRect.sizeDelta = CalculateFittedLocalSize(
            picture,
            cellWidth * 0.9f,
            cellHeight * 0.9f,
            inheritedScaleX,
            inheritedScaleY);
    }

    private void BuildGalleryViewer()
    {
        GameObject viewerObject = new GameObject(
            "GalleryViewer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        viewerObject.layer = _galleryTab.layer;
        viewerObject.transform.SetParent(_galleryContentArea, false);
        _galleryViewer = viewerObject;

        RectTransform viewerRect = viewerObject.GetComponent<RectTransform>();
        StretchToParent(viewerRect);

        Image viewerBackground = viewerObject.GetComponent<Image>();
        viewerBackground.color = new Color(0f, 0f, 0f, 0.96f);

        Button closeButton = viewerObject.GetComponent<Button>();
        closeButton.targetGraphic = viewerBackground;
        closeButton.onClick.AddListener(ShowGalleryGrid);

        GameObject imageObject = new GameObject(
            "FullPicture",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = _galleryTab.layer;
        imageObject.transform.SetParent(viewerRect, false);

        _galleryViewerImage = imageObject.GetComponent<Image>();
        _galleryViewerImage.type = Image.Type.Simple;
        _galleryViewerImage.preserveAspect = false;
        _galleryViewerImage.raycastTarget = false;

        RectTransform imageRect = _galleryViewerImage.rectTransform;
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;

        _galleryViewer.SetActive(false);
    }

    private void OpenGalleryPicture(Sprite picture)
    {
        if (_galleryViewer == null || _galleryViewerImage == null || picture == null) return;

        _galleryViewerImage.sprite = picture;
        Vector3 inheritedScale = _galleryContentArea.lossyScale;
        float inheritedScaleX = Mathf.Max(Mathf.Abs(inheritedScale.x), 0.0001f);
        float inheritedScaleY = Mathf.Max(Mathf.Abs(inheritedScale.y), 0.0001f);
        _galleryViewerImage.rectTransform.sizeDelta = CalculateFittedLocalSize(
            picture,
            _galleryContentArea.rect.width * 0.92f,
            _galleryContentArea.rect.height * 0.92f,
            inheritedScaleX,
            inheritedScaleY);

        _galleryGrid?.SetActive(false);
        _galleryViewer.SetActive(true);
        _galleryViewer.transform.SetAsLastSibling();
    }

    private void ShowGalleryGrid()
    {
        if (!_galleryBuilt) return;
        _galleryViewer?.SetActive(false);
        _galleryGrid?.SetActive(true);
    }

    private static Vector2 CalculateFittedLocalSize(
        Sprite picture,
        float availableLocalWidth,
        float availableLocalHeight,
        float inheritedScaleX,
        float inheritedScaleY)
    {
        float pictureAspect = picture.rect.width / picture.rect.height;
        float availableScreenWidth = availableLocalWidth * inheritedScaleX;
        float availableScreenHeight = availableLocalHeight * inheritedScaleY;
        float availableScreenAspect = availableScreenWidth / Mathf.Max(availableScreenHeight, 0.0001f);

        float screenWidth;
        float screenHeight;
        if (availableScreenAspect > pictureAspect)
        {
            screenHeight = availableScreenHeight;
            screenWidth = screenHeight * pictureAspect;
        }
        else
        {
            screenWidth = availableScreenWidth;
            screenHeight = screenWidth / Mathf.Max(pictureAspect, 0.0001f);
        }

        return new Vector2(screenWidth / inheritedScaleX, screenHeight / inheritedScaleY);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    #region Getters
    public GameObject GetStartTab()
    {
        return _startTab;
    }

    public GameObject GetContactsTab()
    {
        return _contactsTab;
    }

    public GameObject GetMessagingTab()
    {
        return _messagingTab;
    }

    public GameObject GetGalleryTab()
    {
        return _galleryTab;
    }

    public GameObject GetEmailTab()
    {
        return _emailTab;
    }
    #endregion

    /// <summary>
    /// Show the decision tab and include it in the list of tabs to close when coming back to other tabs
    /// </summary>
    public void ShowDecisionsTab()
    {
        //_homeButton.GetTabsToClose().Add(_decisionsTab);
        //_backButton.GetTabsToClose().Add(_decisionsTab);
        //_decisionsTab.SetActive(true);
    }

    public void ShowDialogueTab()
    {
        _dialogueTab.SetActive(true);
    }

}
