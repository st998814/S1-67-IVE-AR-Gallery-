using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MobileViewer.Content
{
    public class MockContentService : MonoBehaviour, IContentService
    {
        [SerializeField] private float simulatedDelaySeconds = 0.05f;

        // CloudReco may report a UUID instead of the human-readable target name.
        private static readonly Dictionary<string, string> targetAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "fe110d05a9094aa3bfe693ea21a502a3", "ttest" }
        };

        private readonly Dictionary<string, ContentData> mockContentByTarget = new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "ttest",
                new ContentData
                {
                    targetName = "ttest",
                    title = "Test Exhibit",
                    description = "Mock content loaded for target: ttest",
                    contentType = "cube",
                    mockColor = new Color(0.32f, 0.63f, 0.96f),
                    displayLabel = "TT"
                }
            },
            {
                "demo-vuforia-target-2",
                new ContentData
                {
                    targetName = "demo-vuforia-target-2",
                    title = "Demo Vuforia Target 2",
                    description = "This content was resolved from a real Vuforia cloud target.",
                    contentType = "capsule",
                    mockColor = new Color(0.45f, 0.85f, 0.55f),
                    displayLabel = "DV2"
                }
            },
            {
                "desktop-mountain-target",
                new ContentData
                {
                    targetName = "desktop-mountain-target",
                    title = "Mountain Research Display",
                    description = "Prototype content for desktop-mountain-target.",
                    contentType = "sphere",
                    mockColor = new Color(0.75f, 0.67f, 0.52f),
                    displayLabel = "MTN"
                }
            }
        };

        public async Task<ContentData> GetContentForTargetAsync(string targetName, CancellationToken cancellationToken = default)
        {
            if (simulatedDelaySeconds > 0f)
            {
                var delayMs = Mathf.RoundToInt(simulatedDelaySeconds * 1000f);
                await Task.Delay(delayMs, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(targetName) &&
                targetAliases.TryGetValue(targetName, out var canonicalTargetName))
            {
                targetName = canonicalTargetName;
            }

            if (!string.IsNullOrWhiteSpace(targetName) && mockContentByTarget.TryGetValue(targetName, out var knownContent))
            {
                return knownContent;
            }

            return new ContentData
            {
                targetName = string.IsNullOrWhiteSpace(targetName) ? "unknown-target" : targetName,
                title = "Unknown Target",
                description = "No mock content assigned for this target.",
                contentType = "cube",
                mockColor = new Color(0.9f, 0.4f, 0.4f),
                displayLabel = "?"
            };
        }
    }
}
