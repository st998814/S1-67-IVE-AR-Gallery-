using System;
using System.Threading;
using System.Threading.Tasks;
using MobileViewer.Content;
using MobileViewer.UI;
using UnityEngine;

namespace MobileViewer.AR
{
    public class TargetContentCoordinator : MonoBehaviour
    {
        [SerializeField] private VuforiaCloudTargetController vuforiaController;
        [SerializeField] private MonoBehaviour contentServiceBehaviour;
        [SerializeField] private ContentRenderer contentRenderer;
        [SerializeField] private MobileViewerStatusUI statusUI;
        [SerializeField] private bool ignoreConsecutiveDuplicateTarget = true;

        private IContentService contentService;
        private CancellationTokenSource loadingCts;
        private string lastLoadedTarget;

        private void Awake()
        {
            contentService = contentServiceBehaviour as IContentService;
            if (contentService == null && contentServiceBehaviour != null)
            {
                Debug.LogError("TargetContentCoordinator: Assigned contentServiceBehaviour does not implement IContentService.");
            }
        }

        private void OnEnable()
        {
            if (vuforiaController != null)
            {
                vuforiaController.TargetDetected += HandleTargetDetected;
                vuforiaController.TargetTrackingFound += HandleTrackingFound;
                vuforiaController.TargetTrackingLost += HandleTrackingLost;
                vuforiaController.StatusMessage += HandleControllerStatus;
            }

            statusUI?.SetScanning();
            contentRenderer?.Hide();
        }

        private void OnDisable()
        {
            if (vuforiaController != null)
            {
                vuforiaController.TargetDetected -= HandleTargetDetected;
                vuforiaController.TargetTrackingFound -= HandleTrackingFound;
                vuforiaController.TargetTrackingLost -= HandleTrackingLost;
                vuforiaController.StatusMessage -= HandleControllerStatus;
            }

            loadingCts?.Cancel();
            loadingCts?.Dispose();
            loadingCts = null;
        }

        private void HandleControllerStatus(string message)
        {
            statusUI?.SetStatus(message);
        }

        private void HandleTargetDetected(string targetName)
        {
            // Detection callback can arrive before the observer reports tracked state.
        }

        private void HandleTrackingFound(string targetName, Transform targetTransform)
        {
            _ = ResolveAndRenderAsync(targetName, targetTransform);
        }

        private void HandleTrackingLost(string targetName)
        {
            loadingCts?.Cancel();
            loadingCts?.Dispose();
            loadingCts = null;
            lastLoadedTarget = null;
            contentRenderer?.Hide();
            statusUI?.SetScanning();
        }

        private async Task ResolveAndRenderAsync(string targetName, Transform targetTransform)
        {
            if (contentService == null)
            {
                Debug.LogError("TargetContentCoordinator: IContentService is not configured.");
                return;
            }

            if (ignoreConsecutiveDuplicateTarget &&
                string.Equals(lastLoadedTarget, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            statusUI?.SetTargetDetected(targetName);
            statusUI?.SetLoadingContent();

            loadingCts?.Cancel();
            loadingCts?.Dispose();
            loadingCts = new CancellationTokenSource();

            try
            {
                var contentData = await contentService.GetContentForTargetAsync(targetName, loadingCts.Token);
                contentRenderer?.Render(contentData, targetTransform);
                lastLoadedTarget = targetName;
                statusUI?.SetContentLoaded();
            }
            catch (OperationCanceledException)
            {
                // Expected when a newer target interrupts the previous request.
            }
            catch (Exception ex)
            {
                Debug.LogError($"TargetContentCoordinator: Failed to load content for target '{targetName}'. {ex.Message}");
                statusUI?.SetStatus("Content load failed");
            }
        }
    }
}
