using UnityEngine;
using Vuforia;

namespace MobileViewer.AR
{
    public class VuforiaSceneBootstrap : MonoBehaviour
    {
        [Header("Cloud Recognition Credentials")]
        [SerializeField] private string accessKey;
        [SerializeField] private string secretKey;

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private VuforiaCloudTargetController vuforiaCloudTargetController;
        [SerializeField] private bool debugLogs = true;

        private bool cloudRecoCreated;

        private void OnEnable()
        {
            if (VuforiaApplication.Instance != null)
            {
                VuforiaApplication.Instance.OnVuforiaStarted += HandleVuforiaStarted;
            }
        }

        private void OnDisable()
        {
            if (VuforiaApplication.Instance != null)
            {
                VuforiaApplication.Instance.OnVuforiaStarted -= HandleVuforiaStarted;
            }
        }

        private void Start()
        {
            TryCreateCloudReco();
        }

        private void HandleVuforiaStarted()
        {
            TryCreateCloudReco();
        }

        private void TryCreateCloudReco()
        {
            if (cloudRecoCreated)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            {
                ReportStatus("Cloud keys missing in inspector");
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                ReportStatus("No target camera found");
                return;
            }

            if (targetCamera.GetComponent<VuforiaBehaviour>() == null)
            {
                targetCamera.gameObject.AddComponent<VuforiaBehaviour>();
            }

            var vuforiaBehaviour = VuforiaBehaviour.Instance;
            if (vuforiaBehaviour == null)
            {
                ReportStatus("Waiting for Vuforia initialization...");
                return;
            }

            var cloudReco = vuforiaBehaviour.ObserverFactory.CreateCloudRecoBehaviour(accessKey, secretKey);
            if (cloudReco == null)
            {
                ReportStatus("Failed to create CloudRecoBehaviour");
                return;
            }

            cloudReco.gameObject.name = "CloudRecoBehaviour";

            if (vuforiaCloudTargetController != null)
            {
                vuforiaCloudTargetController.SetCloudRecoBehaviour(cloudReco);
            }

            cloudRecoCreated = true;
            ReportStatus("Scanning...");
        }

        private void ReportStatus(string message)
        {
            if (debugLogs)
            {
                Debug.Log($"[VuforiaSceneBootstrap] {message}");
            }

            if (vuforiaCloudTargetController != null)
            {
                vuforiaCloudTargetController.ReportStatus(message);
            }
        }
    }
}
