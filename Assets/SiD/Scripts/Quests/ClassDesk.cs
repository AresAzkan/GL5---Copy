using UnityEngine;

public class ClassDesk : IInteractable
{
    [Header("Legacy Test Scene")]
    [SerializeField] private SchoolQuestManager schoolQuest;

    private const string EncounterFinished = "SchoolEncounterComplete";
    private const string ClassFinished = "SchoolClassFinished";

    protected override void Start()
    {
        base.Start();

        _canInteract = false;
        if (string.IsNullOrEmpty(_interactMessage))
        {
            _interactMessage = "Sit at your desk and attend class";
        }

        if (string.IsNullOrEmpty(_notAvailableMessage))
        {
            _notAvailableMessage = "Deal with Emma and Sophie first";
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe(EncounterFinished, EnableDesk);
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe(EncounterFinished, EnableDesk);
        }
    }

    private void EnableDesk()
    {
        _canInteract = true;
    }

    public override void Interact()
    {
        if (!_canInteract)
        {
            ReturnPlayerToIdle();
            return;
        }

        _canInteract = false;
        string completionEvent = ClassFinished;

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.PlaySchoolClass(completionEvent);
            return;
        }

        // Keep the old test-scene setup usable if it has no shared transition UI.
        if (schoolQuest != null)
        {
            schoolQuest.StartClass();
        }

        EventBus.Instance?.Publish(completionEvent);
        ReturnPlayerToIdle();
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
