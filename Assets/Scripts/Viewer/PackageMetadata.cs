using System;

namespace FBXViewer.Viewer
{
    /// <summary>
    /// パッケージのメタデータ構造定義
    /// </summary>
    [Serializable]
    public class PartMetadataData
    {
        public string partName;
        public int descriptionCount;
        public string[] descriptions;
        public string[] audioFiles;
        public string[] selectedObjectNames;  // FBX内のオブジェクト名リスト
    }

    [Serializable]
    public class ProjectMetadataData
    {
        public string projectName;
        public string fbxPath;
        public int partCount;
        public PartMetadataData[] parts;
    }

    [Serializable]
    public class PackageManifest
    {
        public string version;
        public string projectName;
        public string createdAt;
        public int partCount;
        public int audioCount;
        public PackagePart[] parts;
    }

    [Serializable]
    public class PackagePart
    {
        public string name;
        public int descriptionCount;
        public string[] audioFiles;
    }
}
