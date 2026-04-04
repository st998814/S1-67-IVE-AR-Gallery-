using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace FrostweepGames.Plugins
{
    [InitializeOnLoad]
    public class DefineProcessing : Editor
    {
        public static void AddOrRemoveDefines(bool add, bool allTargets, params string[] definesToChange)
        {
            BuildTargetGroup[] buildTargets;

            if (allTargets)
            {
                System.Collections.Generic.List<BuildTargetGroup> targets = new System.Collections.Generic.List<BuildTargetGroup>();
                foreach (BuildTarget target in System.Enum.GetValues(typeof(BuildTarget)))
                {
                    BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

                    if (group == BuildTargetGroup.Unknown)
                        continue;
                    targets.Add(group);
                }
                buildTargets = targets.ToArray();
            }
            else
            {
                buildTargets = new BuildTargetGroup[] { EditorUserBuildSettings.selectedBuildTargetGroup };
            }

            for (int i = 0; i < buildTargets.Length; i++)
            {
                NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargets[i]);
                string definesString = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                var defines = definesString.Split(';').ToList();

                if (add)
                    defines.AddRange(definesToChange.Except(defines));
                else
                    defines.RemoveAll(item => definesToChange.Contains(item));
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", defines));
            }
        }
    }
}