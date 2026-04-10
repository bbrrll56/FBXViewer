using System.Collections;
using System.Collections.Generic;
using System.IO;
using FBXViewer.Audio;
using FBXViewer.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FBXViewer.Converter
{
    /// <summary>
    /// Runtime IMGUI overlay for configuring and running the conversion flow in Play Mode.
    /// This is the first step toward replacing the editor window workflow with an in-app GUI.
    /// </summary>
    public class RuntimeConversionOverlay : MonoBehaviour
    {
        private class PreviewMaterialState
        {
            public bool HasBaseColor;
            public Color BaseColor;
            public bool HasColor;
            public Color Color;
            public bool HasEmissionColor;
            public Color EmissionColor;
            public bool EmissionKeywordEnabled;
            public bool HasSurface;
            public float Surface;
            public bool HasBlend;
            public float Blend;
            public bool HasSrcBlend;
            public int SrcBlend;
            public bool HasDstBlend;
            public int DstBlend;
            public bool HasZWrite;
            public int ZWrite;
            public int RenderQueue;
            public bool SurfaceTransparentKeywordEnabled;
            public bool AlphaPremultiplyKeywordEnabled;
        }

        private class FbxOption
        {
            public string AssetPath;
            public string DisplayName;
        }

        private const string DefaultInputFolder = "Assets/Converer/Input";
        private const string DefaultExportFolderName = "Converer/Output";
        private static readonly Color SelectionHighlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color SelectionEmissionColor = new Color(1f, 0.8f, 0.2f, 1f);
        private const float PreviewDimAlpha = 0.2f;

        private readonly List<FbxOption> availableFbxOptions = new List<FbxOption>();
        private readonly List<DetectedPart> detectedParts = new List<DetectedPart>();
        private readonly List<bool> partSelection = new List<bool>();
        private readonly List<string> newDescriptions = new List<string> { string.Empty };
        private readonly List<Material> previewMaterials = new List<Material>();
        private readonly Dictionary<Material, PreviewMaterialState> previewStates = new Dictionary<Material, PreviewMaterialState>();

        private FBXConverter converter;
        private AudioGenerationPipeline audioPipeline;
        private ProjectData currentProject;
        private GameObject currentFbxInstance;

        private Rect windowRect = new Rect(16f, 16f, 540f, 760f);
        private Vector2 scrollPosition;

        private string inputFolder = DefaultInputFolder;
        private string exportPath = string.Empty;
        private string projectName = "NewProject";
        private string newPartName = string.Empty;

        private int selectedFbxIndex = -1;
        private int detectionFilterMode;

        private bool isConverting;
        private float conversionProgress;
        private string conversionMessage = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureOverlayExists()
        {
            if (FindObjectOfType<RuntimeConversionOverlay>() != null)
            {
                return;
            }

            var overlay = new GameObject(nameof(RuntimeConversionOverlay));
            DontDestroyOnLoad(overlay);
            overlay.AddComponent<RuntimeConversionOverlay>();
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(exportPath))
            {
                exportPath = Path.Combine(Application.dataPath, DefaultExportFolderName);
            }

            converter = GetComponent<FBXConverter>();
            if (converter == null)
            {
                converter = gameObject.AddComponent<FBXConverter>();
            }

            audioPipeline = GetComponent<AudioGenerationPipeline>();
            if (audioPipeline == null)
            {
                audioPipeline = gameObject.AddComponent<AudioGenerationPipeline>();
            }

            converter.Initialize();
            audioPipeline.Initialize();
            RefreshAvailableFbxOptions();
        }

        private void OnDestroy()
        {
            ResetSelectionPreview();

            if (currentFbxInstance != null)
            {
                FBXImporter.UnloadFBX(currentFbxInstance);
                currentFbxInstance = null;
            }
        }

        private void OnGUI()
        {
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Runtime Conversion");
        }

        private void DrawWindow(int windowId)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            DrawStatusHelp();
            DrawProjectSection();
            DrawFbxSection();
            DrawDetectionSection();
            DrawPartCreationSection();
            DrawCurrentPartsSection();
            DrawConversionSection();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawStatusHelp()
        {
            GUILayout.Label("Goal", GUI.skin.box);
            GUILayout.Label("This overlay lets you configure conversion in Play Mode instead of using the editor window.");

            #if !UNITY_EDITOR
            GUILayout.Label("Current limitation: automatic FBX folder scanning is only implemented in the editor right now.");
            #endif
        }

        private void DrawProjectSection()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Project", GUI.skin.box);

            GUILayout.Label("Project Name");
            projectName = GUILayout.TextField(projectName);

            GUILayout.Label("Input Folder");
            inputFolder = GUILayout.TextField(inputFolder);

            GUILayout.Label("Export Folder");
            exportPath = GUILayout.TextField(exportPath);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh FBX List"))
            {
                RefreshAvailableFbxOptions();
            }

            if (GUILayout.Button("Create Project"))
            {
                CreateProjectFromSelection();
            }

            GUILayout.EndHorizontal();

            if (currentProject != null)
            {
                GUILayout.Label($"Current project: {currentProject.projectName}");
                GUILayout.Label($"Parts: {currentProject.GetPartCount()} / Descriptions: {currentProject.GetTotalDescriptionCount()}");
            }
        }

        private void DrawFbxSection()
        {
            GUILayout.Space(8f);
            GUILayout.Label("FBX Selection", GUI.skin.box);

            if (availableFbxOptions.Count == 0)
            {
                GUILayout.Label("No FBX assets were found.");
                return;
            }

            string[] optionLabels = new string[availableFbxOptions.Count];
            for (int i = 0; i < availableFbxOptions.Count; i++)
            {
                optionLabels[i] = availableFbxOptions[i].DisplayName;
            }

            selectedFbxIndex = Mathf.Clamp(selectedFbxIndex, -1, availableFbxOptions.Count - 1);
            selectedFbxIndex = GUILayout.SelectionGrid(selectedFbxIndex, optionLabels, 1);

            if (selectedFbxIndex >= 0)
            {
                GUILayout.Label($"Selected: {availableFbxOptions[selectedFbxIndex].AssetPath}");
            }

            GUI.enabled = currentProject != null && selectedFbxIndex >= 0;
            if (GUILayout.Button("Load Selected FBX And Detect Parts"))
            {
                DetectSelectedFbxParts();
            }
            GUI.enabled = true;
        }

        private void DrawDetectionSection()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Detected FBX Objects", GUI.skin.box);

            string[] filterLabels = { "All", "Meshes Only", "Groups Only" };
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.Width(60f));
            int newMode = GUILayout.SelectionGrid(detectionFilterMode, filterLabels, filterLabels.Length);
            GUILayout.EndHorizontal();

            if (newMode != detectionFilterMode)
            {
                detectionFilterMode = newMode;
                RefreshDetectionResult();
            }

            if (detectedParts.Count == 0)
            {
                GUILayout.Label("No objects detected yet.");
                return;
            }

            for (int i = 0; i < detectedParts.Count; i++)
            {
                bool newValue = GUILayout.Toggle(partSelection[i], detectedParts[i].ToString());
                if (newValue != partSelection[i])
                {
                    partSelection[i] = newValue;
                    ApplySelectionPreview();
                }
            }

            if (GUILayout.Button("Add Selected As Individual Parts"))
            {
                AddSelectedAsIndividualParts();
            }
        }

        private void DrawPartCreationSection()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Create Bound Part", GUI.skin.box);

            string[] selectedObjectNames = GetSelectedDetectedObjectNames();
            GUILayout.Label($"Bound Objects: {selectedObjectNames.Length}");
            if (selectedObjectNames.Length > 0)
            {
                GUILayout.Label(string.Join(", ", selectedObjectNames));
            }

            GUILayout.Label("Part Name");
            newPartName = GUILayout.TextField(newPartName);

            GUILayout.Label("Descriptions");
            for (int i = 0; i < newDescriptions.Count; i++)
            {
                newDescriptions[i] = GUILayout.TextArea(newDescriptions[i], GUILayout.MinHeight(40f));
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Description"))
            {
                newDescriptions.Add(string.Empty);
            }

            if (newDescriptions.Count > 1 && GUILayout.Button("Remove Last"))
            {
                newDescriptions.RemoveAt(newDescriptions.Count - 1);
            }
            GUILayout.EndHorizontal();

            GUI.enabled = currentProject != null && !string.IsNullOrWhiteSpace(newPartName);
            if (GUILayout.Button("Add Part With Current Selection"))
            {
                AddBoundPart();
            }
            GUI.enabled = true;
        }

        private void DrawCurrentPartsSection()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Current Parts", GUI.skin.box);

            if (currentProject == null || currentProject.parts.Count == 0)
            {
                GUILayout.Label("No parts added.");
                return;
            }

            for (int i = 0; i < currentProject.parts.Count; i++)
            {
                PartInfo part = currentProject.parts[i];
                GUILayout.BeginVertical("box");
                GUILayout.Label(part.partName);
                GUILayout.Label($"Descriptions: {part.descriptions.Length}");
                GUILayout.Label($"Bound Objects: {FormatBoundObjects(part.selectedObjectNames)}");

                if (GUILayout.Button($"Remove {part.partName}"))
                {
                    currentProject.RemovePart(i);
                    break;
                }

                GUILayout.EndVertical();
            }
        }

        private void DrawConversionSection()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Conversion", GUI.skin.box);

            if (isConverting)
            {
                GUILayout.Label($"Progress: {(conversionProgress * 100f):F0}%");
                GUILayout.HorizontalSlider(conversionProgress, 0f, 1f);
                GUILayout.Label(conversionMessage);
            }

            GUI.enabled = currentProject != null && currentProject.IsValid() && !isConverting;
            if (GUILayout.Button("Start Conversion"))
            {
                StartConversion();
            }
            GUI.enabled = true;
        }

        private void RefreshAvailableFbxOptions()
        {
            availableFbxOptions.Clear();
            selectedFbxIndex = -1;

            #if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(inputFolder) || !AssetDatabase.IsValidFolder(inputFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { inputFolder });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                availableFbxOptions.Add(new FbxOption
                {
                    AssetPath = assetPath,
                    DisplayName = Path.GetFileNameWithoutExtension(assetPath)
                });
            }
            #endif
        }

        private void CreateProjectFromSelection()
        {
            if (selectedFbxIndex < 0 || selectedFbxIndex >= availableFbxOptions.Count)
            {
                return;
            }

            string selectedAssetPath = availableFbxOptions[selectedFbxIndex].AssetPath;
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), selectedAssetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            converter.CreateNewProject(fullPath, projectName);
            currentProject = converter.GetCurrentProject();
            currentProject.exportPath = exportPath;
        }

        private void DetectSelectedFbxParts()
        {
            if (selectedFbxIndex < 0 || selectedFbxIndex >= availableFbxOptions.Count)
            {
                return;
            }

            if (currentFbxInstance != null)
            {
                ResetSelectionPreview();
                FBXImporter.UnloadFBX(currentFbxInstance);
                currentFbxInstance = null;
            }

            string assetPath = availableFbxOptions[selectedFbxIndex].AssetPath;
            currentFbxInstance = FBXImporter.ImportFBX(assetPath);
            RefreshDetectionResult();
        }

        private void RefreshDetectionResult()
        {
            detectedParts.Clear();
            partSelection.Clear();
            ResetSelectionPreview();

            if (currentFbxInstance == null)
            {
                return;
            }

            List<DetectedPart> allParts = PartDetector.DetectParts(currentFbxInstance);
            List<DetectedPart> filteredParts = detectionFilterMode switch
            {
                1 => PartDetector.FilterMeshParts(allParts),
                2 => PartDetector.FilterParentParts(allParts),
                _ => allParts
            };

            detectedParts.AddRange(filteredParts);
            for (int i = 0; i < detectedParts.Count; i++)
            {
                partSelection.Add(false);
            }
        }

        private void AddSelectedAsIndividualParts()
        {
            if (currentProject == null)
            {
                return;
            }

            for (int i = 0; i < detectedParts.Count; i++)
            {
                if (!partSelection[i])
                {
                    continue;
                }

                string objectName = detectedParts[i].objectName;
                converter.AddPartInfo(objectName, new[] { $"{objectName} description" }, new[] { objectName });
            }

            currentProject = converter.GetCurrentProject();
            ClearSelectionState();
        }

        private void AddBoundPart()
        {
            if (currentProject == null)
            {
                return;
            }

            var descriptions = new List<string>();
            foreach (string description in newDescriptions)
            {
                if (!string.IsNullOrWhiteSpace(description))
                {
                    descriptions.Add(description.Trim());
                }
            }

            if (descriptions.Count == 0)
            {
                return;
            }

            converter.AddPartInfo(newPartName.Trim(), descriptions.ToArray(), GetSelectedDetectedObjectNames());
            currentProject = converter.GetCurrentProject();

            newPartName = string.Empty;
            newDescriptions.Clear();
            newDescriptions.Add(string.Empty);
            ClearSelectionState();
        }

        private void ClearSelectionState()
        {
            for (int i = 0; i < partSelection.Count; i++)
            {
                partSelection[i] = false;
            }

            ApplySelectionPreview();
        }

        private string[] GetSelectedDetectedObjectNames()
        {
            var selectedObjectNames = new List<string>();

            for (int i = 0; i < detectedParts.Count && i < partSelection.Count; i++)
            {
                if (!partSelection[i])
                {
                    continue;
                }

                string objectName = detectedParts[i].objectName;
                if (!string.IsNullOrEmpty(objectName) && !selectedObjectNames.Contains(objectName))
                {
                    selectedObjectNames.Add(objectName);
                }
            }

            return selectedObjectNames.ToArray();
        }

        private string FormatBoundObjects(string[] objectNames)
        {
            if (objectNames == null || objectNames.Length == 0)
            {
                return "(none)";
            }

            return string.Join(", ", objectNames);
        }

        private void StartConversion()
        {
            if (currentProject == null || !currentProject.IsValid() || isConverting)
            {
                return;
            }

            StartCoroutine(RunConversion());
        }

        private IEnumerator RunConversion()
        {
            isConverting = true;
            conversionProgress = 0f;
            conversionMessage = "Initializing...";

            var tempGo = new GameObject("_RuntimeTempConverter_");
            var tempConverter = tempGo.AddComponent<FBXConverter>();
            var tempAudioPipeline = tempGo.AddComponent<AudioGenerationPipeline>();

            tempConverter.Initialize();
            tempAudioPipeline.Initialize();
            tempConverter.CreateNewProject(currentProject.fbxPath, currentProject.projectName);

            ProjectData tempProject = tempConverter.GetCurrentProject();
            tempProject.exportPath = currentProject.exportPath;

            foreach (PartInfo part in currentProject.parts)
            {
                tempConverter.AddPartInfo(part.partName, part.descriptions, part.selectedObjectNames);
            }

            tempAudioPipeline.ProgressUpdated += OnProgressUpdated;

            bool conversionFinished = false;
            tempConverter.ConversionComplete += (_, message) =>
            {
                conversionMessage = message;
                conversionProgress = 1f;
                conversionFinished = true;
            };

            tempConverter.StartCoroutine(tempConverter.Convert());

            while (!conversionFinished)
            {
                yield return null;
            }

            tempAudioPipeline.ProgressUpdated -= OnProgressUpdated;
            Destroy(tempGo);

            isConverting = false;
        }

        private void OnProgressUpdated(int current, int total, string message)
        {
            conversionProgress = total > 0 ? (float)current / total : 0f;
            conversionMessage = message;
        }

        private void ApplySelectionPreview()
        {
            if (currentFbxInstance == null)
            {
                return;
            }

            bool hasAnySelection = false;
            for (int i = 0; i < partSelection.Count; i++)
            {
                if (partSelection[i])
                {
                    hasAnySelection = true;
                    break;
                }
            }

            if (!hasAnySelection)
            {
                ResetSelectionPreview();
                return;
            }

            var selectedTransforms = new HashSet<Transform>();
            for (int i = 0; i < detectedParts.Count && i < partSelection.Count; i++)
            {
                if (!partSelection[i])
                {
                    continue;
                }

                if (detectedParts[i].transform != null)
                {
                    selectedTransforms.Add(detectedParts[i].transform);
                }
            }

            var selectedRenderers = new HashSet<Renderer>();
            foreach (Transform selectedTransform in selectedTransforms)
            {
                foreach (Renderer renderer in selectedTransform.GetComponentsInChildren<Renderer>(true))
                {
                    selectedRenderers.Add(renderer);
                }
            }

            foreach (Renderer renderer in currentFbxInstance.GetComponentsInChildren<Renderer>(true))
            {
                bool isSelected = selectedRenderers.Contains(renderer);
                foreach (Material material in renderer.materials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    CachePreviewMaterialState(material);
                    RegisterPreviewMaterial(material);

                    if (isSelected)
                    {
                        ApplySelectedPreview(material);
                    }
                    else
                    {
                        ApplyDimPreview(material);
                    }
                }
            }

            Debug.Log($"[RuntimeConversionOverlay] Selection preview updated. Selected transforms: {selectedTransforms.Count}, selected renderers: {selectedRenderers.Count}");
        }

        private void RegisterPreviewMaterial(Material material)
        {
            if (!previewMaterials.Contains(material))
            {
                previewMaterials.Add(material);
            }
        }

        private void CachePreviewMaterialState(Material material)
        {
            if (previewStates.ContainsKey(material))
            {
                return;
            }

            var state = new PreviewMaterialState
            {
                HasBaseColor = material.HasProperty("_BaseColor"),
                HasColor = material.HasProperty("_Color"),
                HasEmissionColor = material.HasProperty("_EmissionColor"),
                EmissionKeywordEnabled = material.IsKeywordEnabled("_EMISSION"),
                HasSurface = material.HasProperty("_Surface"),
                HasBlend = material.HasProperty("_Blend"),
                HasSrcBlend = material.HasProperty("_SrcBlend"),
                HasDstBlend = material.HasProperty("_DstBlend"),
                HasZWrite = material.HasProperty("_ZWrite"),
                SurfaceTransparentKeywordEnabled = material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),
                AlphaPremultiplyKeywordEnabled = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"),
                RenderQueue = material.renderQueue
            };

            if (state.HasBaseColor)
            {
                state.BaseColor = material.GetColor("_BaseColor");
            }

            if (state.HasColor)
            {
                state.Color = material.GetColor("_Color");
            }

            if (state.HasEmissionColor)
            {
                state.EmissionColor = material.GetColor("_EmissionColor");
            }

            if (state.HasSurface)
            {
                state.Surface = material.GetFloat("_Surface");
            }

            if (state.HasBlend)
            {
                state.Blend = material.GetFloat("_Blend");
            }

            if (state.HasSrcBlend)
            {
                state.SrcBlend = material.GetInt("_SrcBlend");
            }

            if (state.HasDstBlend)
            {
                state.DstBlend = material.GetInt("_DstBlend");
            }

            if (state.HasZWrite)
            {
                state.ZWrite = material.GetInt("_ZWrite");
            }

            previewStates[material] = state;
        }

        private void ApplySelectedPreview(Material material)
        {
            RestorePreviewMaterial(material);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", SelectionHighlightColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", SelectionHighlightColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", SelectionEmissionColor * 1.5f);
            }
        }

        private void ApplyDimPreview(Material material)
        {
            RestorePreviewMaterial(material);
            SetPreviewTransparent(material);

            if (material.HasProperty("_BaseColor"))
            {
                Color baseColor = previewStates[material].BaseColor;
                baseColor.a = PreviewDimAlpha;
                material.SetColor("_BaseColor", baseColor);
            }
            else if (material.HasProperty("_Color"))
            {
                Color color = previewStates[material].Color;
                color.a = PreviewDimAlpha;
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }
        }

        private void SetPreviewTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void RestorePreviewMaterial(Material material)
        {
            if (material == null || !previewStates.TryGetValue(material, out PreviewMaterialState state))
            {
                return;
            }

            if (state.HasBaseColor)
            {
                material.SetColor("_BaseColor", state.BaseColor);
            }

            if (state.HasColor)
            {
                material.SetColor("_Color", state.Color);
            }

            if (state.HasEmissionColor)
            {
                material.SetColor("_EmissionColor", state.EmissionColor);
            }

            if (state.HasSurface)
            {
                material.SetFloat("_Surface", state.Surface);
            }

            if (state.HasBlend)
            {
                material.SetFloat("_Blend", state.Blend);
            }

            if (state.HasSrcBlend)
            {
                material.SetInt("_SrcBlend", state.SrcBlend);
            }

            if (state.HasDstBlend)
            {
                material.SetInt("_DstBlend", state.DstBlend);
            }

            if (state.HasZWrite)
            {
                material.SetInt("_ZWrite", state.ZWrite);
            }

            material.renderQueue = state.RenderQueue;

            if (state.EmissionKeywordEnabled)
            {
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            if (state.SurfaceTransparentKeywordEnabled)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (state.AlphaPremultiplyKeywordEnabled)
            {
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
        }

        private void ResetSelectionPreview()
        {
            foreach (Material material in previewMaterials)
            {
                RestorePreviewMaterial(material);
            }

            previewMaterials.Clear();
            previewStates.Clear();
        }
    }
}
