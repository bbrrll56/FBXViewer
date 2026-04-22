#if UNITY_EDITOR
using System;
using System.IO;
using FBXViewer.Data;
using UnityEditor;
using UnityEngine;

namespace FBXViewer.Converter
{
    [InitializeOnLoad]
    public static class QuestPackageBuildScheduler
    {
        private const string PendingRequestSessionKey = "FBXViewer.QuestPackageBuildScheduler.HasPendingRequest";
        private static readonly string PendingRequestPath = Path.Combine("Library", "FBXViewerPendingQuestPackage.json");

        [Serializable]
        private class PendingPackageRequest
        {
            public ProjectData projectData;
            public string packageOutputPath;
        }

        static QuestPackageBuildScheduler()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += TryRunPendingRequest;
        }

        public static void Schedule(ProjectData projectData, string packageOutputPath)
        {
            if (projectData == null)
            {
                Debug.LogError("[QuestPackageBuildScheduler] Cannot schedule package build because project data is missing.");
                return;
            }

            var request = new PendingPackageRequest
            {
                projectData = projectData,
                packageOutputPath = packageOutputPath
            };

            File.WriteAllText(PendingRequestPath, JsonUtility.ToJson(request, true));
            SessionState.SetBool(PendingRequestSessionKey, true);

            Debug.Log("[QuestPackageBuildScheduler] AssetBundle package build was queued. Exiting play mode to complete packaging.");
            EditorApplication.ExitPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += TryRunPendingRequest;
            }
        }

        private static void TryRunPendingRequest()
        {
            if (!SessionState.GetBool(PendingRequestSessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(PendingRequestPath))
            {
                SessionState.EraseBool(PendingRequestSessionKey);
                return;
            }

            bool clearPendingRequest = true;

            try
            {
                string json = File.ReadAllText(PendingRequestPath);
                PendingPackageRequest request = JsonUtility.FromJson<PendingPackageRequest>(json);

                if (request == null || request.projectData == null || string.IsNullOrEmpty(request.packageOutputPath))
                {
                    Debug.LogError("[QuestPackageBuildScheduler] Pending package request was invalid.");
                    return;
                }

                if (!EnsureAndroidBuildTarget(out bool waitingForBuildTargetSwitch))
                {
                    clearPendingRequest = !waitingForBuildTargetSwitch;
                    return;
                }

                bool success = QuestPackager.GeneratePackage(request.projectData, request.packageOutputPath);
                if (success)
                {
                    Debug.Log($"[QuestPackageBuildScheduler] Package generation completed: {request.packageOutputPath}");
                    EditorUtility.DisplayDialog("Package Generated", $"Package folder:\n{request.packageOutputPath}", "OK");
                }
                else
                {
                    Debug.LogError("[QuestPackageBuildScheduler] Package generation failed.");
                    EditorUtility.DisplayDialog("Package Generation Failed", "See the Console for details.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestPackageBuildScheduler] Package generation error: {ex.Message}");
                EditorUtility.DisplayDialog("Package Generation Error", ex.Message, "OK");
            }
            finally
            {
                if (clearPendingRequest)
                {
                    SessionState.EraseBool(PendingRequestSessionKey);

                    if (File.Exists(PendingRequestPath))
                    {
                        File.Delete(PendingRequestPath);
                    }
                }
            }
        }

        private static bool EnsureAndroidBuildTarget(out bool waitingForBuildTargetSwitch)
        {
            waitingForBuildTargetSwitch = false;

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[QuestPackageBuildScheduler] Android Build Support is not available to this Unity Editor installation.");
                EditorUtility.DisplayDialog(
                    "Android Build Support Required",
                    "Unity cannot build the Quest AssetBundle because Android Build Support is not available to this Editor installation.",
                    "OK");
                return false;
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return true;
            }

            Debug.Log("[QuestPackageBuildScheduler] Switching active build target to Android before building Quest AssetBundle.");
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            if (!switched)
            {
                Debug.LogError("[QuestPackageBuildScheduler] Failed to switch active build target to Android.");
                EditorUtility.DisplayDialog("Build Target Switch Failed", "Switch the project to Android in Build Settings, then run conversion again.", "OK");
                return false;
            }

            waitingForBuildTargetSwitch = true;
            EditorApplication.delayCall += TryRunPendingRequest;
            return false;
        }
    }
}
#endif
