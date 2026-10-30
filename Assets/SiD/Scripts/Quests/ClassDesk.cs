using UnityEngine;

public class ClassDesk : IInteractable
{
    [Header("Legacy Test Scene")]
    [SerializeField] private SchoolQuestManager schoolQuest;

    private const string Day1EncounterFinished = "Day1_LockerEncounterEnd";
    private const string Day2EncounterFinished = "Day2_ClassroomEncounterEnd";
    private const string Day1ClassFinished = "Day1_ClassFinished";
    private const string Day2ClassFinished = "Day2_ClassFinished";

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
            EventBus.Instance.Subscribe(Day1EncounterFinished, EnableDesk);
            EventBus.Instance.Subscribe(Day2EncounterFinished, EnableDesk);
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe(Day1EncounterFinished, EnableDesk);
            EventBus.Instance.Unsubscribe(Day2EncounterFinished, EnableDesk);
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
        string completionEvent = DayManager.Instance != null && DayManager.Instance.GetCurrentDay() >= 2
            ? Day2ClassFinished
            : Day1ClassFinished;

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
