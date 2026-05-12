using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Vuforia;

namespace MobileViewer.AR
{
    public class VuforiaCloudTargetController : MonoBehaviour
    {
        [SerializeField] private CloudRecoBehaviour cloudRecoBehaviour;
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private bool requireDirectTracking = true;

        public event Action<string> TargetDetected;
        public event Action<string, Transform> TargetTrackingFound;
        public event Action<string> TargetTrackingLost;
        public event Action<string> StatusMessage;
        private bool handlersRegistered;
        private ObserverBehaviour currentObserver;
        private string currentTargetName;
        private bool targetCurrentlyTracked;

        private void Awake()
        {
            if (cloudRecoBehaviour == null)
            {
                cloudRecoBehaviour = FindAnyObjectByType<CloudRecoBehaviour>();
            }
        }

        private void OnEnable()
        {
            if (cloudRecoBehaviour == null)
            {
                if (debugLogs)
                {
                    Debug.LogWarning("VuforiaCloudTargetController: CloudRecoBehaviour not assigned yet.");
                }
                return;
            }

            RegisterHandlers();
        }

        private void OnDisable()
        {
            UnregisterHandlers();
            DetachCurrentObserver();
        }

        public void SetCloudRecoBehaviour(CloudRecoBehaviour behaviour)
        {
            if (ReferenceEquals(cloudRecoBehaviour, behaviour))
            {
                return;
            }

            UnregisterHandlers();
            cloudRecoBehaviour = behaviour;

            if (isActiveAndEnabled)
            {
                RegisterHandlers();
            }

            ReportStatus(behaviour == null
                ? "Cloud reco disconnected"
                : "Cloud reco connected");
        }

        private void OnNewSearchResult(CloudRecoBehaviour.CloudRecoSearchResult searchResult)
        {
            var targetName = ExtractTargetName(searchResult);

            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = "unknown-target";
            }

            if (debugLogs)
            {
                Debug.Log($"[VuforiaCloudTargetController] Cloud target detected: {targetName}");
            }

            ReportStatus($"Target detected: {targetName}");
            TargetDetected?.Invoke(targetName);

            var observer = EnableObserverForResult(searchResult, targetName);
            if (observer == null)
            {
                return;
            }

            AttachObserver(observer, targetName);
        }

        private void OnQueryError(CloudRecoBehaviour.QueryError error)
        {
            if (debugLogs && error != CloudRecoBehaviour.QueryError.NONE)
            {
                Debug.LogWarning($"[VuforiaCloudTargetController] Cloud query error: {error}");
            }

            if (error != CloudRecoBehaviour.QueryError.NONE)
            {
                ReportStatus($"Cloud query error: {error}");
            }
        }

        private void OnInitError(CloudRecoBehaviour.InitError error)
        {
            if (debugLogs && error != CloudRecoBehaviour.InitError.NONE)
            {
                Debug.LogError($"[VuforiaCloudTargetController] Cloud reco init error: {error}");
            }

            if (error != CloudRecoBehaviour.InitError.NONE)
            {
                ReportStatus($"Cloud init error: {error}");
            }
        }

        private void OnRecoStateChanged(bool scanning)
        {
            if (debugLogs)
            {
                Debug.Log($"[VuforiaCloudTargetController] Cloud scanning: {scanning}");
            }

            ReportStatus(scanning ? "Scanning..." : "Cloud scanning paused");
        }

        private static string ExtractTargetName(CloudRecoBehaviour.CloudRecoSearchResult searchResult)
        {
            if (searchResult == null)
            {
                return null;
            }

            var namedMembers = new[]
            {
                "TargetName",
                "TrackableName",
                "Name",
                "UniqueTargetId",
                "TargetId",
                "Id"
            };

            foreach (var memberName in namedMembers)
            {
                var value = GetStringProperty(searchResult, memberName) ?? GetStringField(searchResult, memberName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            // Last-resort fallback for SDK variants where member names differ.
            var fallback = searchResult.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.GetValue(searchResult) as string)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            return fallback;
        }

        private static string GetStringProperty(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || property.PropertyType != typeof(string))
            {
                return null;
            }

            return property.GetValue(source) as string;
        }

        private static string GetStringField(object source, string fieldName)
        {
            var field = source.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(string))
            {
                return null;
            }

            return field.GetValue(source) as string;
        }

        private void RegisterHandlers()
        {
            if (cloudRecoBehaviour == null || handlersRegistered)
            {
                return;
            }

            cloudRecoBehaviour.RegisterOnNewSearchResultEventHandler(OnNewSearchResult);
            cloudRecoBehaviour.RegisterOnUpdateErrorEventHandler(OnQueryError);
            cloudRecoBehaviour.RegisterOnInitErrorEventHandler(OnInitError);
            cloudRecoBehaviour.RegisterOnStateChangedEventHandler(OnRecoStateChanged);
            handlersRegistered = true;
            ReportStatus("Cloud reco handlers registered");
        }

        private void UnregisterHandlers()
        {
            if (cloudRecoBehaviour == null || !handlersRegistered)
            {
                return;
            }

            cloudRecoBehaviour.UnregisterOnNewSearchResultEventHandler(OnNewSearchResult);
            cloudRecoBehaviour.UnregisterOnUpdateErrorEventHandler(OnQueryError);
            cloudRecoBehaviour.UnregisterOnInitErrorEventHandler(OnInitError);
            cloudRecoBehaviour.UnregisterOnStateChangedEventHandler(OnRecoStateChanged);
            handlersRegistered = false;
        }

        private ObserverBehaviour EnableObserverForResult(CloudRecoBehaviour.CloudRecoSearchResult searchResult, string targetName)
        {
            if (cloudRecoBehaviour == null || searchResult == null)
            {
                return null;
            }

            var method = cloudRecoBehaviour.GetType().GetMethod(
                "EnableObservers",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { searchResult.GetType(), typeof(string) },
                null);

            if (method == null)
            {
                ReportStatus("Cloud observer API unavailable");
                return null;
            }

            var observerObject = method.Invoke(cloudRecoBehaviour, new object[] { searchResult, $"CloudTarget_{targetName}" });
            if (observerObject is ObserverBehaviour directObserver)
            {
                return directObserver;
            }

            if (observerObject is Component component)
            {
                return component.GetComponent<ObserverBehaviour>();
            }

            if (observerObject is GameObject gameObject)
            {
                return gameObject.GetComponent<ObserverBehaviour>();
            }

            return null;
        }

        private void AttachObserver(ObserverBehaviour observer, string targetName)
        {
            if (observer == null)
            {
                return;
            }

            if (ReferenceEquals(currentObserver, observer))
            {
                return;
            }

            DetachCurrentObserver();

            currentObserver = observer;
            currentTargetName = targetName;
            currentObserver.OnTargetStatusChanged += OnObserverStatusChanged;
            currentObserver.OnBehaviourDestroyed += OnObserverDestroyed;
            OnObserverStatusChanged(currentObserver, currentObserver.TargetStatus);
        }

        private void DetachCurrentObserver()
        {
            if (currentObserver == null)
            {
                return;
            }

            currentObserver.OnTargetStatusChanged -= OnObserverStatusChanged;
            currentObserver.OnBehaviourDestroyed -= OnObserverDestroyed;

            if (targetCurrentlyTracked && !string.IsNullOrWhiteSpace(currentTargetName))
            {
                TargetTrackingLost?.Invoke(currentTargetName);
                ReportStatus($"Target lost: {currentTargetName}");
            }

            currentObserver = null;
            currentTargetName = null;
            targetCurrentlyTracked = false;
        }

        private void OnObserverDestroyed(ObserverBehaviour observer)
        {
            if (!ReferenceEquals(observer, currentObserver))
            {
                return;
            }

            DetachCurrentObserver();
        }

        private void OnObserverStatusChanged(ObserverBehaviour observer, TargetStatus targetStatus)
        {
            if (!ReferenceEquals(observer, currentObserver))
            {
                return;
            }

            var trackedNow = IsTracked(targetStatus.Status);
            if (trackedNow == targetCurrentlyTracked)
            {
                return;
            }

            targetCurrentlyTracked = trackedNow;

            if (trackedNow)
            {
                TargetTrackingFound?.Invoke(currentTargetName, observer.transform);
                ReportStatus($"Tracking: {currentTargetName}");
            }
            else
            {
                TargetTrackingLost?.Invoke(currentTargetName);
                ReportStatus($"Target lost: {currentTargetName}");
            }
        }

        private bool IsTracked(Status status)
        {
            if (requireDirectTracking)
            {
                return status == Status.TRACKED;
            }

            return status == Status.TRACKED || status == Status.EXTENDED_TRACKED || status == Status.LIMITED;
        }

        public void ReportStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (debugLogs)
            {
                Debug.Log($"[VuforiaCloudTargetController] {message}");
            }

            StatusMessage?.Invoke(message);
        }
    }
}
