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
        public bool MarkReadyAndContinue(string targetId)
        {
            if (SceneTransitionService.IsTransitioning)
                return false;

            AppFlowController.MarkWorkspaceReady(targetId);
            return SceneTransitionService.TransitionToScene(AppFlowController.AuthoringSceneName);
        }

        public bool CancelToSwitcher()
        {
            if (SceneTransitionService.IsTransitioning)
                return false;

            AppFlowController.ClearWorkspaceSession();
            return SceneTransitionService.TransitionToScene(AppFlowController.WorkspaceSwitcherSceneName);
        }
    }
}
