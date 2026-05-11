using UnityEngine;

namespace ARGallery.AppFlow
{
    /// <summary>
    /// Scene-level coordinator for the target-instantiation gate.
    /// UI can call MarkReadyAndContinue when publish succeeds,
    /// or CancelToSwitcher when the user cancels setup.
    /// </summary>
    public class TargetInstantiationSceneController : MonoBehaviour
    {
        public void MarkReadyAndContinue(string targetId)
        {
            AppFlowController.MarkWorkspaceReady(targetId);
            SceneTransitionService.TransitionToScene(AppFlowController.AuthoringSceneName);
        }

        public void CancelToSwitcher()
        {
            // Roll back pending workspace session when user cancels setup.
            AppFlowController.ClearWorkspaceSession();
            SceneTransitionService.TransitionToScene(AppFlowController.WorkspaceSwitcherSceneName);
        }
    }
}
