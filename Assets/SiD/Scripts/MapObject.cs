using UnityEngine;
using UnityEngine.SceneManagement;

public class MapObject : IInteractable
{

    [Header("Conditions")]

    [Tooltip("Optional: Events that enable this door (e.g. Day3_ReadyToLeave).")]
    [SerializeField] private System.Collections.Generic.List<string> _enableEvents;

    [Tooltip("Optional: Events that disable this door.")]
    [SerializeField] private System.Collections.Generic.List<string> _disableEvents;
    
    [Tooltip("Optional: Event to publish if interacted when locked (e.g. 'Show_DoorLocked_Text').")]
    [SerializeField] private string _disabledEventToPublish;

    [Header("Direct Travel")]

    [Tooltip("Optional: Load this scene directly instead of opening the travel map.")]
    [SerializeField] private string _directScene;

    [Tooltip("Optional: Event to publish immediately before direct travel.")]
    [SerializeField] private string _eventBeforeSceneChange;

    private void Start()
    {
        if (_enableEvents != null)
        {
            foreach (var ev in _enableEvents)
            {
                if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Subscribe(ev, EnableInteraction);
            }
        }
        
        if (_disableEvents != null)
        {
            foreach (var ev in _disableEvents)
            {
                if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Subscribe(ev, DisableInteraction);
            }
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            if (_enableEvents != null)
            {
                foreach (var ev in _enableEvents)
                {
                    if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Unsubscribe(ev, EnableInteraction);
                }
            }
            
            if (_disableEvents != null)
            {
                foreach (var ev in _disableEvents)
                {
                    if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Unsubscribe(ev, DisableInteraction);
                }
            }
        }
    }

    public void EnableInteraction() => _canInteract = true;
    public void DisableInteraction() => _canInteract = false;

    public override void Interact()
    {
        // Several door models have MapObject components on both the parent door
        // and its visible child panels. The player's raycast hits a panel first,
        // so let it use the configured direct-travel parent instead of opening
        // the generic map from the child component.
        MapObject parentDoor = transform.parent == null
            ? null
            : transform.parent.GetComponentInParent<MapObject>();

        if (parentDoor != null && parentDoor != this &&
            !string.IsNullOrEmpty(parentDoor._directScene))
        {
            parentDoor.Interact();
            return;
        }

        if (!_canInteract)
        {
            Debug.Log($"Scene change disabled for {gameObject.name}.");
            if (!string.IsNullOrEmpty(_disabledEventToPublish))
            {
                EventBus.Instance.Publish(_disabledEventToPublish);
            }
            
            // Revert player state just in case it got stuck
            PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
            if (playerState != null && playerState.GetCurrentState() == PlayerState.Interacting)
            {
                playerState.ChangeState(PlayerState.Idle);
            }
            return;
        }

        if (!string.IsNullOrEmpty(_directScene))
        {
            if (!string.IsNullOrEmpty(_eventBeforeSceneChange))
            {
                EventBus.Instance.Publish(_eventBeforeSceneChange);
            }

            GameManager.Instance.ChangeScene(_directScene);
            return;
        }

        UIManager.Instance.ChangeState(2);
    }
}
