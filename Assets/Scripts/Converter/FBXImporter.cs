using UnityEngine;
using UnityEditor;

namespace FBXViewer.Converter
{
    /// <summary>
    /// FBXファイルをUnityにインポートして、オブジェクト構造を解析するクラス
    /// </summary>
    public class FBXImporter
    {
        /// <summary>
        /// FBXファイルを一時的にシーンに読み込む
        /// </summary>
        public static GameObject ImportFBX(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath))
            {
                Debug.LogError("FBXファイルパスが空です");
                return null;
            }

            // パスをノーマライズ（バックスラッシュをスラッシュに統一）
            string normalizedPath = fbxPath.Replace("\\", "/");

            // Assets/ 以降のパスを抽出
            string assetPath = normalizedPath;
            int assetsIndex = normalizedPath.IndexOf("Assets/");
            if (assetsIndex >= 0)
            {
                assetPath = normalizedPath.Substring(assetsIndex);
            }

            Debug.Log($"[ImportFBX] 元のパス: {fbxPath}");
            Debug.Log($"[ImportFBX] アセットパス: {assetPath}");

            // AssetDatabase から直接読み込む
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab == null)
            {
                Debug.LogError($"FBXの読み込みに失敗しました: {assetPath}");

                // デバッグ：マッチするアセットを検索
                string searchName = System.IO.Path.GetFileNameWithoutExtension(fbxPath);
                string[] results = AssetDatabase.FindAssets(searchName + " t:GameObject");
                if (results.Length > 0)
                {
                    Debug.Log($"[デバッグ] 見つかった類似ファイル:");
                    foreach (var guid in results)
                    {
                        Debug.Log($"  - {AssetDatabase.GUIDToAssetPath(guid)}");
                    }
                }

                return null;
            }

            // シーンに一時インスタンス化
            GameObject instance = Object.Instantiate(prefab);
            instance.name = prefab.name + "_Temp";

            Debug.Log($"✓ FBXを読み込みました: {assetPath}");
            return instance;
        }

        /// <summary>
        /// 一時的に読み込んだFBXを削除
        /// </summary>
        public static void UnloadFBX(GameObject instance)
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
                Debug.Log("FBXインスタンスを削除しました");
            }
        }

        /// <summary>
        /// FBXのモデルインポート設定を自動で最適化
        /// </summary>
        public static void OptimizeFBXImport(string fbxPath)
        {
            string assetPath = FileUtil.GetProjectRelativePath(fbxPath);
            if (!assetPath.StartsWith("Assets/"))
            {
                assetPath = "Assets/" + System.IO.Path.GetFileName(fbxPath);
            }

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null)
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
                Debug.Log("FBXインポート設定を最適化しました");
            }
        }
    }
}
