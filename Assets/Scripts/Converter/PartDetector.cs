using System.Collections.Generic;
using UnityEngine;

namespace FBXViewer.Converter
{
    /// <summary>
    /// FBXオブジェクトの階層構造からパーツを自動検出するクラス
    /// </summary>
    public class PartDetector
    {
        /// <summary>
        /// オブジェクト階層を解析してパーツリストを取得
        /// </summary>
        public static List<DetectedPart> DetectParts(GameObject rootObject)
        {
            var detectedParts = new List<DetectedPart>();

            if (rootObject == null)
            {
                Debug.LogError("ルートオブジェクトがnullです");
                return detectedParts;
            }

            // ルート含めて全オブジェクトを走査
            ScanHierarchy(rootObject.transform, detectedParts, 0);

            Debug.Log($"[PartDetector] {detectedParts.Count}個のパーツを検出しました");
            return detectedParts;
        }

        /// <summary>
        /// 再帰的にオブジェクト階層を走査
        /// </summary>
        private static void ScanHierarchy(Transform parent, List<DetectedPart> parts, int depth)
        {
            // 現在のオブジェクトを追加（ルートは除外）
            if (parent != parent.root)
            {
                var part = new DetectedPart
                {
                    objectName = parent.gameObject.name,
                    transform = parent,
                    depth = depth,
                    childCount = parent.childCount,
                    hasCollider = parent.GetComponent<Collider>() != null,
                    hasMeshFilter = parent.GetComponent<MeshFilter>() != null
                };
                parts.Add(part);
            }

            // 子オブジェクトを再帰的に処理
            foreach (Transform child in parent)
            {
                ScanHierarchy(child, parts, depth + 1);
            }
        }

        /// <summary>
        /// パーツ候補を自動判定（MeshがあるGameObjectなど）
        /// </summary>
        public static List<DetectedPart> FilterMeshParts(List<DetectedPart> allParts)
        {
            var meshParts = new List<DetectedPart>();

            foreach (var part in allParts)
            {
                // Meshを持つオブジェクトのみ抽出
                if (part.hasMeshFilter && part.childCount == 0)
                {
                    meshParts.Add(part);
                }
            }

            Debug.Log($"[PartDetector] Mesh を持つパーツ: {meshParts.Count}個");
            return meshParts;
        }

        /// <summary>
        /// 親オブジェクトのみを抽出（グループ化されたパーツ用）
        /// </summary>
        public static List<DetectedPart> FilterParentParts(List<DetectedPart> allParts)
        {
            var parentParts = new List<DetectedPart>();

            foreach (var part in allParts)
            {
                // 子オブジェクトを持つもののみ抽出
                if (part.childCount > 0)
                {
                    parentParts.Add(part);
                }
            }

            Debug.Log($"[PartDetector] 親オブジェクト（グループ）: {parentParts.Count}個");
            return parentParts;
        }
    }

    /// <summary>
    /// 検出されたパーツの情報
    /// </summary>
    public class DetectedPart
    {
        public string objectName;
        public Transform transform;
        public int depth;
        public int childCount;
        public bool hasCollider;
        public bool hasMeshFilter;

        public override string ToString()
        {
            string indent = new string('-', depth * 2);
            string info = hasCollider ? "C" : "_";
            info += hasMeshFilter ? "M" : "_";
            return $"{indent}{objectName} [{info}] (子: {childCount})";
        }
    }
}
