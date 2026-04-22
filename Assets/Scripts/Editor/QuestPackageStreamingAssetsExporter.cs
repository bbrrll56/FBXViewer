#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FBXViewer.Editor
{
    public static class QuestPackageStreamingAssetsExporter
    {
        private const string StreamingAssetsPackagePath = "Assets/StreamingAssets/Package";

        [MenuItem("Tools/FBXViewer/Copy Latest Quest Package To StreamingAssets")]
        public static void CopyLatestPackageToStreamingAssets()
        {
            string sourcePackagePath = FindLatestGeneratedPackage();
            if (string.IsNullOrEmpty(sourcePackagePath))
            {
                EditorUtility.DisplayDialog("Package Not Found", "No generated package was found under Application.persistentDataPath/Packages.", "OK");
                return;
            }

            string destinationPath = Path.Combine(Directory.GetCurrentDirectory(), StreamingAssetsPackagePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }

            CopyDirectory(sourcePackagePath, destinationPath);
            AssetDatabase.Refresh();

            Debug.Log($"[QuestPackageStreamingAssetsExporter] Copied package to {StreamingAssetsPackagePath}");
            EditorUtility.DisplayDialog("Package Copied", $"Copied:\n{sourcePackagePath}\n\nto:\n{StreamingAssetsPackagePath}", "OK");
        }

        private static string FindLatestGeneratedPackage()
        {
            string packagesRoot = Path.Combine(Application.persistentDataPath, "Packages");
            if (!Directory.Exists(packagesRoot))
            {
                return string.Empty;
            }

            string latestPackagePath = string.Empty;
            System.DateTime latestWriteTime = System.DateTime.MinValue;

            foreach (string manifestPath in Directory.GetFiles(packagesRoot, "manifest.json", SearchOption.AllDirectories))
            {
                string packagePath = Path.GetDirectoryName(manifestPath);
                if (string.IsNullOrEmpty(packagePath) ||
                    !File.Exists(Path.Combine(packagePath, "Bundle", "model_android.bundle")) ||
                    !File.Exists(Path.Combine(packagePath, "Metadata", "project_metadata.json")))
                {
                    continue;
                }

                System.DateTime writeTime = File.GetLastWriteTimeUtc(manifestPath);
                if (writeTime > latestWriteTime)
                {
                    latestWriteTime = writeTime;
                    latestPackagePath = packagePath;
                }
            }

            return latestPackagePath;
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string filePath in Directory.GetFiles(sourceDirectory))
            {
                string destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
                File.Copy(filePath, destinationFilePath, true);
            }

            foreach (string directoryPath in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(directoryPath));
                CopyDirectory(directoryPath, destinationSubdirectory);
            }
        }
    }
}
#endif
