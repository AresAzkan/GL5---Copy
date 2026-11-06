using System.Collections;
using UnityEngine;

public class StudentGroupConnectEncounter : IInteractable
{
    [Header("Encounter")]
    [SerializeField] private BoxCollider _overhearTrigger;
    [SerializeField] private string _contactName = "Student Group";
    [SerializeField] private int _overheardNodeId = 2800;
    [SerializeField] private int _followUpNodeId = 2801;

    [Header("Automatic Camera Focus")]
    [Tooltip("Optional explicit focus point. When empty, the midpoint of the student children is used.")]
    [SerializeField] private Transform _focusTarget;
    [SerializeField, Min(0f)] private float _focusTurnDuration = 0.65f;
    [SerializeField] private float _focusHeightOffset = 0.5f;

    [Header("Events")]
    [SerializeField] private string _classFinishedEvent = "SchoolClassFinished";
    [SerializeField] private string _overheardEvent = "D1_StudentConnectOverheard";
    [SerializeField] private string _conversationFinishedEvent = "D1_StudentConnectConversationComplete";
    [SerializeField] private string _connectProfileCompletedEvent = "D1_Profile_End";
    [SerializeField] private string _exitAttemptEvent = "D1_StudentConnectExitAttempt";

    private bool _encounterArmed;
    private bool _overheard;
    private bool _encounterComplete;
    private bool _startingOverheardEncounter;
    private MapObject _exitDoor;
    private Collider _playerCollider;
    private float _nextOverlapCheckTime;

    protected override void Awake()
    {
        _canInteract = false;
        _interactMessage = "Talk to students";
        _notAvailableMessage = "The students are busy talking.";

        if (_overhearTrigger == null)
        {
            foreach (BoxCollider candidate in GetComponents<BoxCollider>())
            {
                if (Mathf.Approximately(candidate.size.x, 20f) &&
                    Mathf.Approximately(candidate.size.y, 1f) &&
                    Mathf.Approximately(candidate.size.z, 1f))
                {
                    _overhearTrigger = candidate;
                    break;
                }
            }
        }

        if (_overhearTrigger != null)
        {
            _overhearTrigger.isTrigger = true;
        }

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        ResolveExitDoor();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe(_classFinishedEvent, ArmEncounter);
            EventBus.Instance.Subscribe(_overheardEvent, EnableFollowUp);
            EventBus.Instance.Subscribe(_conversationFinishedEvent, CompleteEncounter);
            EventBus.Instance.Subscribe(_connectProfileCompletedEvent, EnableExitAfterConnectSetup);
            EventBus.Instance.Subscribe(_exitAttemptEvent, BeginOverheardEncounter);
        }
    }

    private void Update()
    {
        if (!_encounterArmed || _overheard || _encounterComplete || !IsDayOne()) return;
        if (_overhearTrigger == null || !_overhearTrigger.enabled) return;
        if (Time.unscaledTime < _nextOverlapCheckTime) return;

        _nextOverlapCheckTime = Time.unscaledTime + 0.1f;

        if (_playerCollider == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                _playerCollider = player.GetComponent<Collider>();
            }
        }

        // Some versions of Unity can miss OnTriggerEnter when the player is moved
        // by a state transition. Checking the actual collider bounds makes this
        // encounter deterministic while keeping the placed trigger as the source.
        if (_playerCollider != null &&
            _overhearTrigger.bounds.Intersects(_playerCollider.bounds))
        {
            BeginOverheardEncounter();
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe(_classFinishedEvent, ArmEncounter);
            EventBus.Instance.Unsubscribe(_overheardEvent, EnableFollowUp);
            EventBus.Instance.Unsubscribe(_conversationFinishedEvent, CompleteEncounter);
            EventBus.Instance.Unsubscribe(_connectProfileCompletedEvent, EnableExitAfterConnectSetup);
            EventBus.Instance.Unsubscribe(_exitAttemptEvent, BeginOverheardEncounter);
        }
    }

    private void ArmEncounter()
    {
        if (!IsDayOne() || _encounterComplete) return;

        _encounterArmed = true;
        StartCoroutine(LockExitAfterClass());
    }

    private IEnumerator LockExitAfterClass()
    {
        // The exit also listens for SchoolClassFinished. Waiting one frame makes
        // this Day 1 lock deterministic regardless of event subscription order.
        yield return null;
        ResolveExitDoor();
        _exitDoor?.DisableInteraction();
    }

    private void EnableFollowUp()
    {
        if (!IsDayOne() || _encounterComplete) return;

        _overheard = true;
        _canInteract = true;
    }

    private void CompleteEncounter()
    {
        if (!IsDayOne()) return;

        _encounterComplete = true;
        _canInteract = false;
        ResolveExitDoor();
        _exitDoor?.DisableInteraction();
    }

    private void EnableExitAfterConnectSetup()
    {
        if (!IsDayOne() || !_encounterComplete) return;

        ResolveExitDoor();
        _exitDoor?.EnableInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_encounterArmed || _overheard || _encounterComplete || !IsDayOne()) return;
        if (other.GetComponentInParent<PlayerController>() == null &&
            other.GetComponentInParent<PlayerStateMachine>() == null) return;

        BeginOverheardEncounter();
    }

    private void BeginOverheardEncounter()
    {
        if (!_encounterArmed || _overheard || _encounterComplete ||
            _startingOverheardEncounter || !IsDayOne()) return;

        StartCoroutine(BeginOverheardEncounterRoutine());
    }

    private IEnumerator BeginOverheardEncounterRoutine()
    {
        _startingOverheardEncounter = true;
        _encounterArmed = false;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            PlayerStateMachine playerState = player.GetComponent<PlayerStateMachine>();
            playerState?.ChangeState(PlayerState.Interacting);

            if (InputManager.Instance != null)
            {
                InputManager.Instance.DisableMovementMap();
            }

            yield return player.FocusViewOn(GetFocusPosition(), _focusTurnDuration);
        }

        if (StartConversation(_overheardNodeId))
        {
            _overheard = true;

            if (_overhearTrigger != null)
            {
                _overhearTrigger.enabled = false;
            }
        }
        else
        {
            _encounterArmed = true;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnableMovementMap();
            }

            ReturnPlayerToIdle();
        }

        _startingOverheardEncounter = false;
    }

    private Vector3 GetFocusPosition()
    {
        if (_focusTarget != null)
        {
            return _focusTarget.position;
        }

        if (transform.childCount == 0)
        {
            return transform.position + Vector3.up * _focusHeightOffset;
        }

        Vector3 midpoint = Vector3.zero;
        for (int i = 0; i < transform.childCount; i++)
        {
            midpoint += transform.GetChild(i).position;
        }

        midpoint /= transform.childCount;
        return midpoint + Vector3.up * _focusHeightOffset;
    }

    public override void Interact()
    {
        if (!_canInteract || !_overheard || _encounterComplete)
        {
            ReturnPlayerToIdle();
            return;
        }

        if (StartConversation(_followUpNodeId))
        {
            _canInteract = false;
        }
        else
        {
            ReturnPlayerToIdle();
        }
    }

    private bool StartConversation(int narrativeNodeId)
    {
        if (MessagesManager.Instance == null || UIManager.Instance == null)
        {
            Debug.LogWarning("[StudentGroupConnectEncounter] Dialogue systems are unavailable.");
            return false;
        }

        MessagesManager.Instance.AdvanceContactNarrativeNodeByEvent(_contactName, narrativeNodeId);
        MessagesManager.Instance.SetCurrentContact(_contactName);
        UIManager.Instance.ChangeState((int)UIState.CONVERSATION);
        return true;
    }

    private void ResolveExitDoor()
    {
        if (_exitDoor != null) return;

        GameObject mainDoor = GameObject.Find("MainDoor");
        if (mainDoor != null)
        {
            _exitDoor = mainDoor.GetComponent<MapObject>();
        }
    }

    private static bool IsDayOne()
    {
        return DayManager.Instance == null || DayManager.Instance.GetCurrentDay() == 1;
    }

    private static void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }
}
