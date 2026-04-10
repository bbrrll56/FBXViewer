using UnityEngine;

namespace FBXViewer.Data
{
    /// <summary>
    /// パーツの情報と生成された音声を管理するクラス
    /// </summary>
    [System.Serializable]
    public class PartInfo
    {
        [SerializeField]
        public string partName;

        [SerializeField]
        public string[] descriptions;

        [SerializeField]
        public string[] audioFilePaths;

        [SerializeField]
        public string[] selectedObjectNames;

        public PartInfo()
        {
            partName = "";
            descriptions = new string[] { };
            audioFilePaths = new string[] { };
            selectedObjectNames = new string[] { };
        }

        public PartInfo(string name, string[] descs)
        {
            partName = name;
            descriptions = descs ?? new string[] { };
            audioFilePaths = new string[descriptions.Length];
            selectedObjectNames = new string[] { };
        }

        public PartInfo(string name, string[] descs, string[] objectNames)
        {
            partName = name;
            descriptions = descs ?? new string[] { };
            audioFilePaths = new string[descriptions.Length];
            selectedObjectNames = objectNames ?? new string[] { };
        }

        /// <summary>
        /// 検証: 必須項目が揃っているか
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(partName) && descriptions.Length > 0;
        }

        /// <summary>
        /// 音声ファイルパスを設定（index対応）
        /// </summary>
        public void SetAudioPath(int descriptionIndex, string path)
        {
            if (descriptionIndex >= 0 && descriptionIndex < audioFilePaths.Length)
            {
                audioFilePaths[descriptionIndex] = path;
            }
        }
    }
}
