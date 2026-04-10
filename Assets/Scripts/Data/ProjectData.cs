using System.Collections.Generic;
using UnityEngine;

namespace FBXViewer.Data
{
    /// <summary>
    /// PC変換ツール全体のプロジェクトデータを管理
    /// </summary>
    [System.Serializable]
    public class ProjectData
    {
        [SerializeField]
        public string fbxPath;

        [SerializeField]
        public string projectName;

        [SerializeField]
        public List<PartInfo> parts;

        [SerializeField]
        public string exportPath;

        public ProjectData()
        {
            fbxPath = "";
            projectName = "NewProject";
            parts = new List<PartInfo>();
            exportPath = "";
        }

        /// <summary>
        /// パーツを追加
        /// </summary>
        public void AddPart(PartInfo part)
        {
            if (part != null && part.IsValid())
            {
                parts.Add(part);
            }
        }

        /// <summary>
        /// パーツを削除
        /// </summary>
        public void RemovePart(int index)
        {
            if (index >= 0 && index < parts.Count)
            {
                parts.RemoveAt(index);
            }
        }

        /// <summary>
        /// 検証: プロジェクトが有効な状態か
        /// </summary>
        public bool IsValid()
        {
            bool hasValidParts = parts.Count > 0;
            bool allPartsValid = parts.TrueForAll(p => p.IsValid());
            bool hasExportPath = !string.IsNullOrEmpty(exportPath);

            return hasValidParts && allPartsValid && hasExportPath;
        }

        /// <summary>
        /// パーツ数を取得
        /// </summary>
        public int GetPartCount()
        {
            return parts.Count;
        }

        /// <summary>
        /// 説明文の総数を取得
        /// </summary>
        public int GetTotalDescriptionCount()
        {
            int total = 0;
            foreach (var part in parts)
            {
                total += part.descriptions.Length;
            }
            return total;
        }
    }
}
