using System;
using System.Collections.Generic;
using ARGallery.Spawning;
using ARGallery.Workspace.Persistence;
using UnityEngine;

namespace ARGallery.Content
{
    public enum ContentReplacementRenderKind
    {
        None,
        Surface,
        Volumetric
    }

    public struct ContentReplacementContext
    {
        public bool hadPreviousContent;
        public Vector3 localPosition;
        public Vector3 localEuler;
        public Vector3 localScale;
        public ContentReplacementRenderKind previousRenderKind;
        public string serverContentId;
        public string localContentId;
        public string targetId;
    }

    /// <summary>
    /// Clears all content under the active target ContentRoot and captures state for replace-in-place spawning.
    /// </summary>
    public static class ContentReplacementService
    {
        public static ContentReplacementContext ClearActiveTargetContent(
            AuthoringTransformCoordinator coordinator,
            ISpawnerManager spawner,
            Action<Transform> removeDraftForTransform,
            Action onActiveDraftCleared)
        {
            var empty = new ContentReplacementContext();
            if (coordinator == null)
                return empty;

            IReadOnlyList<Transform> entries = coordinator.GetActiveContentEntries();
            if (entries == null || entries.Count == 0)
                return empty;

            if (entries.Count > 1)
            {
                Debug.LogWarning(
                    $"ContentReplacementService: found {entries.Count} children under ContentRoot; removing all and capturing transform from the first.");
            }

            Transform first = entries[0];
            ContentReplacementContext ctx = CaptureFromTransform(first);
            ctx.hadPreviousContent = true;

            var copy = new List<Transform>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                    copy.Add(entries[i]);
            }

            for (int i = 0; i < copy.Count; i++)
            {
                Transform tr = copy[i];
                removeDraftForTransform?.Invoke(tr);
                if (tr == null || tr.gameObject == null)
                    continue;

                if (spawner != null)
                    spawner.ReleaseSpawnedContent(tr.gameObject);
                else
                    UnityEngine.Object.Destroy(tr.gameObject);
            }

            onActiveDraftCleared?.Invoke();
            return ctx;
        }

        public static void ApplyContextToSpawnRequest(
            SpawnRequest request,
            ContentReplacementContext ctx,
            SpawnContentType newContentType)
        {
            if (request == null || !ctx.hadPreviousContent)
                return;

            SpawnRenderKind newKind = ToSpawnRenderKind(newContentType);
            SpawnRenderKind previousKind = ToSpawnRenderKind(ctx.previousRenderKind);
            bool sameRenderKind = previousKind == newKind;

            request.hasTransformOverride = true;
            request.transformOverride = new SpawnTransformData
            {
                localPosition = ctx.localPosition,
                localEuler = ctx.localEuler,
                localScale = sameRenderKind ? ctx.localScale : Vector3.one
            };

            if (!string.IsNullOrWhiteSpace(ctx.serverContentId))
                request.contentIdOverride = ctx.serverContentId.Trim();
        }

        public static void ApplyAuthoredIdentityAfterReplace(Transform contentTransform, ContentReplacementContext ctx)
        {
            if (contentTransform == null)
                return;

            AuthoredContentInstance ac = contentTransform.GetComponent<AuthoredContentInstance>()
                ?? contentTransform.GetComponentInParent<AuthoredContentInstance>()
                ?? contentTransform.GetComponentInChildren<AuthoredContentInstance>(true);
            if (ac == null)
                return;

            ac.Title = "";
            ac.Description = "";

            if (!string.IsNullOrWhiteSpace(ctx.localContentId))
                ac.LocalContentId = ctx.localContentId.Trim();
            else if (string.IsNullOrWhiteSpace(ac.LocalContentId))
                ac.LocalContentId = Guid.NewGuid().ToString("N");

            if (!string.IsNullOrWhiteSpace(ctx.serverContentId))
                ac.ServerContentId = ctx.serverContentId.Trim();
            else if (string.IsNullOrWhiteSpace(ac.ServerContentId))
                ac.ServerContentId = ac.LocalContentId;

            if (!string.IsNullOrWhiteSpace(ctx.targetId))
                ac.TargetId = ctx.targetId.Trim();
        }

        private static ContentReplacementContext CaptureFromTransform(Transform tr)
        {
            var ctx = new ContentReplacementContext
            {
                hadPreviousContent = false,
                localPosition = tr.localPosition,
                localEuler = tr.localEulerAngles,
                localScale = tr.localScale,
                previousRenderKind = ResolveRenderKind(tr)
            };

            AuthoredContentInstance ac = tr.GetComponent<AuthoredContentInstance>()
                ?? tr.GetComponentInParent<AuthoredContentInstance>()
                ?? tr.GetComponentInChildren<AuthoredContentInstance>(true);
            if (ac != null)
            {
                ctx.serverContentId = ac.ServerContentId ?? "";
                ctx.localContentId = ac.LocalContentId ?? "";
                ctx.targetId = ac.TargetId ?? "";
                if (!string.IsNullOrWhiteSpace(ac.RenderKind))
                {
                    if (string.Equals(ac.RenderKind, "volumetric", StringComparison.OrdinalIgnoreCase))
                        ctx.previousRenderKind = ContentReplacementRenderKind.Volumetric;
                    else
                        ctx.previousRenderKind = ContentReplacementRenderKind.Surface;
                }
                else if (string.Equals(ac.ContentType, "model", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.previousRenderKind = ContentReplacementRenderKind.Volumetric;
                }
            }

            return ctx;
        }

        private static ContentReplacementRenderKind ResolveRenderKind(Transform tr)
        {
            AuthoredContentInstance ac = tr.GetComponent<AuthoredContentInstance>();
            if (ac == null)
                return ContentReplacementRenderKind.Surface;

            if (string.Equals(ac.RenderKind, "volumetric", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ac.ContentType, "model", StringComparison.OrdinalIgnoreCase))
            {
                return ContentReplacementRenderKind.Volumetric;
            }

            return ContentReplacementRenderKind.Surface;
        }

        private static SpawnRenderKind ToSpawnRenderKind(SpawnContentType type) =>
            type == SpawnContentType.Model ? SpawnRenderKind.Volumetric : SpawnRenderKind.Surface;

        private static SpawnRenderKind ToSpawnRenderKind(ContentReplacementRenderKind kind) =>
            kind == ContentReplacementRenderKind.Volumetric ? SpawnRenderKind.Volumetric : SpawnRenderKind.Surface;
    }
}
