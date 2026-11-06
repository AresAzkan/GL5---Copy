using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DayManager : MonoBehaviour
{
    public enum TestRoute
    {
        Safe,
        Neutral,
        Risky
    }

    public static DayManager Instance { get; private set; }

    private static bool _hasQueuedTestStart;
    private static int _queuedTestDay = 1;
    private static TestRoute _queuedTestRoute = TestRoute.Neutral;

    public static bool ShouldSkipSceneOneOpening => _hasQueuedTestStart && _queuedTestDay > 1;

    [SerializeField] private int _currentDay = 1;

    private int _riskScore;
    private readonly Dictionary<string, System.Action> _choiceSubscriptions = new();
    private readonly HashSet<int> _skippedDays = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (_hasQueuedTestStart)
            {
                _currentDay = Mathf.Clamp(_queuedTestDay, 1, 5);
            }
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        EventBus.Instance.Subscribe("NextDay", OnNextDay);
        RegisterNarrativeChoices();
        EventBus.Instance.Subscribe("ResolveEnding_Safe", ResolveSafeEnding);
        EventBus.Instance.Subscribe("ResolveEnding_Neutral", ResolveNeutralEnding);
        EventBus.Instance.Subscribe("ResolveEnding_Risky", ResolveRiskyEnding);
        StartCoroutine(InitializeCampaignDelayed());
    }

    private IEnumerator InitializeCampaignDelayed()
    {
        yield return new WaitForSeconds(1f);
        if (EventBus.Instance != null)
        {
            if (_hasQueuedTestStart)
            {
                ApplyQueuedTestStart();
            }

            EventBus.Instance.Publish($"Start_Day{_currentDay}");
        }
    }

    public static void QueueTestStart(int day, TestRoute route)
    {
        _queuedTestDay = Mathf.Clamp(day, 1, 5);
        _queuedTestRoute = route;
        _hasQueuedTestStart = _queuedTestDay > 1;
    }

    public static void ClearQueuedTestStart()
    {
        _hasQueuedTestStart = false;
        _queuedTestDay = 1;
        _queuedTestRoute = TestRoute.Neutral;
    }

    private void ApplyQueuedTestStart()
    {
        int targetDay = Mathf.Clamp(_queuedTestDay, 2, 5);
        TestRoute route = _queuedTestRoute;

        UIManager.Instance?.ApplyCompletedConnectSetupForTestSkip();

        RebuildTestSkipHistories(targetDay, route);

        for (int completedDay = 1; completedDay < targetDay; completedDay++)
        {
            ApplyCompletedDayContactState(completedDay, route);
        }

        int objectiveId = GetOpeningObjectiveId(targetDay);
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.ForceSetObjective(objectiveId);
        }
        else
        {
            EventBus.Instance.Publish("ForceSetObjective", objectiveId);
        }

        int lastPreviousChoice = GetLastChoiceBeforeDay(targetDay);
        string routeName = route.ToString();
        for (int choice = 1; choice <= lastPreviousChoice; choice++)
        {
            EventBus.Instance.Publish($"Choice{choice}_{routeName}");
        }

        Debug.Log(
            $"[DayManager] Test start prepared for Day {targetDay}: " +
            $"Choices 1-{lastPreviousChoice} set to {routeName}.");

        ClearQueuedTestStart();
    }

    private static int GetOpeningObjectiveId(int day)
    {
        return day switch
        {
            2 => 8,
            3 => 16,
            4 => 23,
            5 => 31,
            _ => 0
        };
    }

    private static int GetLastChoiceBeforeDay(int day)
    {
        return day switch
        {
            2 => 2,
            3 => 5,
            4 => 8,
            5 => 10,
            _ => 0
        };
    }

    private static void RebuildTestSkipHistories(int targetDay, TestRoute route)
    {
        if (MessagesManager.Instance == null) return;

        MessagesManager.Instance.BeginTestSkipHistoryRebuild();

        if (targetDay > 1)
        {
            MessagesManager.Instance.AppendTestSkipHistory(
                "Connect", 1, 1000, RouteNode(route, 1001, 1002, 1003));
            MessagesManager.Instance.AppendTestSkipHistory(
                "Alex", 1, 1200, RouteNode(route, 1201, 1202, 1203), 1210);
            MessagesManager.Instance.AppendTestSkipHistory("Jamie", 1, 1220);
        }

        if (targetDay > 2)
        {
            MessagesManager.Instance.AppendTestSkipHistory("Alex", 2, 1300);
            MessagesManager.Instance.AppendTestSkipHistory(
                "Jamie", 2,
                1500,
                RouteNode(route, 1501, 1502, 1503),
                1510,
                RouteNode(route, 1511, 1512, 1513));
            MessagesManager.Instance.AppendTestSkipHistory("Noah", 2, 1600);
        }

        if (targetDay > 3)
        {
            if (route == TestRoute.Safe)
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 1700, 1850, 1900, 1901, 1904, 1905);
            }
            else if (route == TestRoute.Neutral)
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 1700, 1850, 1900, 1902, 1904, 1906, 1910, 1912);
            }
            else
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 1700, 1850, 1900, 1903, 1904, 1907, 1910, 1913);
            }

            MessagesManager.Instance.AppendTestSkipHistory("Unknown", 3, 1950);
        }

        if (targetDay > 4)
        {
            MessagesManager.Instance.AppendTestSkipHistory(
                "Unknown", 4,
                2000,
                2150,
                2200,
                RouteNode(route, 2201, 2202, 2203),
                2210,
                RouteNode(route, 2211, 2212, 2213));
            MessagesManager.Instance.AppendTestSkipHistory("Riley", 4, 2300);
        }

        MessagesManager.Instance.CompleteTestSkipHistoryRebuild();
    }

    private static int RouteNode(TestRoute route, int safeNode, int neutralNode, int riskyNode)
    {
        return route switch
        {
            TestRoute.Safe => safeNode,
            TestRoute.Risky => riskyNode,
            _ => neutralNode
        };
    }

    private static void ApplyCompletedDayContactState(int day, TestRoute route)
    {
        if (MessagesManager.Instance == null) return;

        switch (day)
        {
            case 1:
                SetContactState("Student Group", 2801);
                SetContactState("Connect", RouteNode(route, 1001, 1002, 1003));
                SetContactState("Emma", 1100);
                SetContactState("Alex", 1210);
                SetContactState("Jamie", 1220);
                break;

            case 2:
                SetContactState("Alex", 1300);
                SetContactState("Emma", RouteNode(route, 1401, 1402, 1403));
                SetContactState("Jamie", RouteNode(route, 1511, 1512, 1513));
                SetContactState("Noah", 1600);
                break;

            case 3:
                SetContactState("Emma", 1800);
                SetContactState("Noah", RouteNode(route, 1905, 1912, 1913));
                SetContactState("Unknown", 1950);
                break;

            case 4:
                SetContactState("Emma", 2100);
                SetContactState("Unknown", RouteNode(route, 2211, 2212, 2213));
                SetContactState("Riley", 2300);
                break;
        }
    }

    private static void SetContactState(string contactName, int narrativeNodeId)
    {
        MessagesManager.Instance.SetContactNarrativeStateSilently(contactName, narrativeNodeId);
    }


    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("NextDay", OnNextDay);
            EventBus.Instance.Unsubscribe("ResolveEnding_Safe", ResolveSafeEnding);
            EventBus.Instance.Unsubscribe("ResolveEnding_Neutral", ResolveNeutralEnding);
            EventBus.Instance.Unsubscribe("ResolveEnding_Risky", ResolveRiskyEnding);

            foreach (KeyValuePair<string, System.Action> subscription in _choiceSubscriptions)
            {
                EventBus.Instance.Unsubscribe(subscription.Key, subscription.Value);
            }
        }

        _choiceSubscriptions.Clear();
    }

    private void OnNextDay()
    {
        _currentDay++;
        Debug.Log($"[DayManager] Advanced to Day {_currentDay}");
        
        // Broadcast the specific day start event so EventNodeUpdaters can catch it
        // e.g., "Start_Day2", "Start_Day3"
        EventBus.Instance.Publish($"Start_Day{_currentDay}");
    }

    public int GetCurrentDay()
    {
        return _currentDay;
    }

    /// <summary>
    /// Records a laptop test-skip once per day. Keeping this on the persistent
    /// day manager prevents the same day's consequences from being applied
    /// repeatedly if the player interacts with the laptop again before bed.
    /// </summary>
    public bool TryMarkDaySkipped(int day)
    {
        return _skippedDays.Add(day);
    }

    public bool WasDaySkipped(int day)
    {
        return _skippedDays.Contains(day);
    }

    private void RegisterNarrativeChoices()
    {
        for (int choice = 1; choice <= 13; choice++)
        {
            SubscribeChoice($"Choice{choice}_Safe", -1);
            SubscribeChoice($"Choice{choice}_Neutral", 0);
            SubscribeChoice($"Choice{choice}_Risky", 1);
        }
    }

    private void SubscribeChoice(string eventName, int riskDelta)
    {
        System.Action action = () =>
        {
            _riskScore += riskDelta;
            Debug.Log($"[DayManager] Recorded {eventName}. Current narrative risk score: {_riskScore}.");
        };

        _choiceSubscriptions[eventName] = action;
        EventBus.Instance.Subscribe(eventName, action);
    }

    private void ResolveSafeEnding() => StartCoroutine(ResolveEndingNextFrame(-3));
    private void ResolveNeutralEnding() => StartCoroutine(ResolveEndingNextFrame(0));
    private void ResolveRiskyEnding() => StartCoroutine(ResolveEndingNextFrame(3));

    private IEnumerator ResolveEndingNextFrame(int finalDecisionModifier)
    {
        // DialogueButton finishes the selected branch immediately after publishing
        // its end event. Waiting one frame prevents that cleanup from overwriting
        // the ending node selected here.
        yield return null;

        int finalScore = _riskScore + finalDecisionModifier;
        int endingNode = finalScore <= -4 ? 2700 : finalScore >= 5 ? 2702 : 2701;

        Debug.Log($"[DayManager] Resolving campaign ending with score {finalScore}; node {endingNode}.");
        MessagesManager.Instance?.AdvanceContactNarrativeNodeByEvent("Riley", endingNode);
    }
}
