using System.Collections;
using System.Collections.Generic;
using System.IO;
using FBXViewer.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FBXViewer.Converter
{
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

        private enum ScreenState
        {
            Start,
            Browser,
            Editor
        }

        private const string DefaultInputFolder = "Assets/Converer/Input";
        private const string DefaultExportFolderName = "Converer/Output";
        private static readonly Color SelectionHighlightColor = new(1f, 0.85f, 0.2f, 1f);
        private static readonly Color SelectionEmissionColor = new(1f, 0.8f, 0.2f, 1f);
        private const float PreviewDimAlpha = 0.2f;

        [SerializeField] private RuntimeConversionStartView startView;
        [SerializeField] private RuntimeConversionFbxSelectionView fbxSelectionView;
        [SerializeField] private RuntimeConversionPartEditorView partEditorView;
        [SerializeField] private string inputFolder = DefaultInputFolder;
        [SerializeField] private string exportRootPath = string.Empty;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private bool autoFramePreviewCamera = true;
        [SerializeField] private Vector3 previewDirection = new(0.35f, 0.25f, -1f);
        [SerializeField, Min(1f)] private float previewDistancePadding = 1.35f;

        private readonly List<FbxOption> availableFbxOptions = new();
        private readonly List<DetectedPart> detectedParts = new();
        private readonly List<bool> partSelection = new();
        private readonly List<Material> previewMaterials = new();
        private readonly Dictionary<Material, PreviewMaterialState> previewStates = new();
        private FBXConverter converter;
        private AudioGenerationPipeline audioPipeline;
        private ProjectData currentProject;
        private GameObject currentFbxInstance;
        private int selectedFbxIndex = -1;
        private bool isConverting;
        private float conversionProgress;
        private string conversionMessage = string.Empty;

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "EditorScene")
            {
                Destroy(gameObject);
                return;
            }

            if (startView == null)
            {
                startView = GetComponentInChildren<RuntimeConversionStartView>(true);
            }

            if (fbxSelectionView == null)
            {
                fbxSelectionView = GetComponentInChildren<RuntimeConversionFbxSelectionView>(true);
            }

            if (partEditorView == null)
            {
                partEditorView = GetComponentInChildren<RuntimeConversionPartEditorView>(true);
            }

            if (startView == null || !startView.HasRequiredReferences() ||
                fbxSelectionView == null || !fbxSelectionView.HasRequiredReferences() ||
                partEditorView == null || !partEditorView.HasRequiredReferences())
            {
                Debug.LogError("[RuntimeConversionOverlay] One or more view references are missing or incomplete.");
                enabled = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(exportRootPath))
            {
                exportRootPath = Path.Combine(Application.dataPath, DefaultExportFolderName);
            }

            converter = GetComponent<FBXConverter>() ?? gameObject.AddComponent<FBXConverter>();
            audioPipeline = GetComponent<AudioGenerationPipeline>() ?? gameObject.AddComponent<AudioGenerationPipeline>();
            converter.Initialize();
            audioPipeline.Initialize();

            InitializeViews();
            RefreshAvailableFbxOptions();
            ClearDraft();
            RefreshProgressUi();
            ShowScreen(ScreenState.Start);
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

        private void InitializeViews()
        {
            startView.Initialize(OnStartPressed);
            fbxSelectionView.Initialize(RefreshAvailableFbxOptions, OpenSelectedFbx, () => ShowScreen(ScreenState.Start));
            partEditorView.Initialize(ReturnToFbxList, RefreshDetectionResult, SaveCurrentPart, ClearDraft, StartConversion, UpdateUiState);
        }

        private void OnStartPressed()
        {
            RefreshAvailableFbxOptions();
            ShowScreen(ScreenState.Browser);
        }

        private void ShowScreen(ScreenState screenState)
        {
            startView.Show(screenState == ScreenState.Start);
            fbxSelectionView.Show(screenState == ScreenState.Browser);
            partEditorView.Show(screenState == ScreenState.Editor);
            UpdateUiState();
        }

        private void RefreshAvailableFbxOptions()
        {
            availableFbxOptions.Clear();
            selectedFbxIndex = -1;

#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(inputFolder) || !AssetDatabase.IsValidFolder(inputFolder))
            {
                SetBrowserStatus($"FBX input folder not found: {inputFolder}");
                RenderFbxList();
                UpdateUiState();
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

            availableFbxOptions.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase));
            SetBrowserStatus(availableFbxOptions.Count > 0 ? $"{availableFbxOptions.Count} FBX file(s) found." : "No FBX files were found.");
#else
            SetBrowserStatus("FBX scanning is only available in the Unity Editor.");
#endif

            RenderFbxList();
            UpdateUiState();
        }

        private void RenderFbxList()
        {
            var labels = new List<string>(availableFbxOptions.Count);
            for (int i = 0; i < availableFbxOptions.Count; i++)
            {
                labels.Add(availableFbxOptions[i].DisplayName);
            }

            fbxSelectionView.RenderFbxList(labels, selectedFbxIndex, SelectFbx);
        }

        private void SelectFbx(int index)
        {
            if (index < 0 || index >= availableFbxOptions.Count)
            {
                return;
            }

            selectedFbxIndex = index;
            SetBrowserStatus($"Selected: {availableFbxOptions[index].AssetPath}");
            fbxSelectionView.SetSelectedIndex(selectedFbxIndex);

            UpdateUiState();
        }

        private void OpenSelectedFbx()
        {
            if (selectedFbxIndex < 0 || selectedFbxIndex >= availableFbxOptions.Count)
            {
                SetBrowserStatus("Select an FBX first.");
                return;
            }

            ResetSelectionPreview();
            ClearDraft();
            RenderSavedPartSummaries();

            if (currentFbxInstance != null)
            {
                FBXImporter.UnloadFBX(currentFbxInstance);
                currentFbxInstance = null;
            }

            FbxOption option = availableFbxOptions[selectedFbxIndex];
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), option.AssetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            converter.CreateNewProject(fullPath, option.DisplayName);
            currentProject = converter.GetCurrentProject();
            currentProject.exportPath = ResolveProjectExportPath(option.DisplayName);

            currentFbxInstance = FBXImporter.ImportFBX(option.AssetPath);
            if (currentFbxInstance == null)
            {
                SetBrowserStatus($"Failed to load FBX: {option.AssetPath}");
                UpdateUiState();
                return;
            }

            if (autoFramePreviewCamera)
            {
                FramePreviewCamera();
            }

            RefreshDetectionResult();
            UpdateCurrentFbxLabel();
            SetEditorStatus($"Loaded {option.DisplayName}. Select meshes and save parts.");
            ShowScreen(ScreenState.Editor);
        }

        private void ReturnToFbxList()
        {
            if (isConverting)
            {
                return;
            }

            ResetSelectionPreview();
            ClearDetectionState();
            ClearDraft();
            partEditorView.ClearSavedParts();

            if (currentFbxInstance != null)
            {
                FBXImporter.UnloadFBX(currentFbxInstance);
                currentFbxInstance = null;
            }

            currentProject = null;
            UpdateCurrentFbxLabel();
            SetEditorStatus(string.Empty);
            ShowScreen(ScreenState.Browser);
            RefreshAvailableFbxOptions();
        }

        private void RefreshDetectionResult()
        {
            ClearDetectionState();
            if (currentFbxInstance == null)
            {
                UpdateUiState();
                return;
            }

            List<DetectedPart> allParts = PartDetector.DetectParts(currentFbxInstance);
            List<DetectedPart> meshParts = PartDetector.FilterMeshParts(allParts);
            detectedParts.AddRange(meshParts.Count > 0 ? meshParts : allParts);

            for (int i = 0; i < detectedParts.Count; i++)
            {
                partSelection.Add(false);
            }

            RenderDetectedMeshList();
            SetEditorStatus(detectedParts.Count == 0 ? "No selectable meshes were detected." : $"{detectedParts.Count} selectable object(s) detected.");
            UpdateUiState();
        }

        private void ClearDetectionState()
        {
            ResetSelectionPreview();
            detectedParts.Clear();
            partSelection.Clear();
            partEditorView.ClearDetectedMeshes();
            UpdateSelectedMeshSummary();
        }

        private void RenderDetectedMeshList()
        {
            var labels = new List<string>(detectedParts.Count);
            for (int i = 0; i < detectedParts.Count; i++)
            {
                labels.Add(detectedParts[i].objectName);
            }

            partEditorView.RenderDetectedMeshes(labels, partSelection, OnDetectedMeshToggled);
            UpdateSelectedMeshSummary();
        }

        private void OnDetectedMeshToggled(int index, bool selected)
        {
            if (index < 0 || index >= partSelection.Count)
            {
                return;
            }

            partSelection[index] = selected;
            ApplySelectionPreview();
            UpdateSelectedMeshSummary();

            if (selected && string.IsNullOrWhiteSpace(GetPartTitle()))
            {
                SetPartTitle(detectedParts[index].objectName);
            }

            UpdateUiState();
        }

        private void UpdateSelectedMeshSummary()
        {
            string[] selectedMeshes = GetSelectedDetectedObjectNames();
            partEditorView.SetSelectedMeshSummary(selectedMeshes.Length == 0
                ? "Selected Meshes: 0"
                : $"Selected Meshes: {selectedMeshes.Length}\n{string.Join(", ", selectedMeshes)}");
        }

        private void SaveCurrentPart()
        {
            if (currentProject == null)
            {
                SetEditorStatus("Load an FBX first.");
                return;
            }

            string title = GetPartTitle();
            string description = GetDescription();
            string[] selectedObjectNames = GetSelectedDetectedObjectNames();

            if (string.IsNullOrWhiteSpace(title))
            {
                SetEditorStatus("Enter a part title.");
                return;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                SetEditorStatus("Enter a description.");
                return;
            }

            if (selectedObjectNames.Length == 0)
            {
                SetEditorStatus("Select at least one mesh.");
                return;
            }

            converter.AddPartInfo(title, new[] { description }, selectedObjectNames);
            currentProject = converter.GetCurrentProject();
            RenderSavedPartSummaries();
            SetEditorStatus($"Saved part: {title}");
            ClearDraft();
            UpdateUiState();
        }

        private void ClearDraft()
        {
            partEditorView.ClearDraft();

            for (int i = 0; i < partSelection.Count; i++)
            {
                partSelection[i] = false;
            }

            ApplySelectionPreview();
            RenderDetectedMeshList();
            UpdateUiState();
        }

        private void RenderSavedPartSummaries()
        {
            var summaries = new List<string>();
            if (currentProject == null)
            {
                partEditorView.RenderSavedParts(summaries);
                return;
            }

            for (int i = 0; i < currentProject.parts.Count; i++)
            {
                PartInfo part = currentProject.parts[i];
                summaries.Add($"{i + 1}. {part.partName}\n{FirstOrEmpty(part.descriptions)}\nMeshes: {FormatBoundObjects(part.selectedObjectNames)}");
            }

            partEditorView.RenderSavedParts(summaries);
        }

        private void UpdateCurrentFbxLabel()
        {
            partEditorView.SetCurrentFbxLabel(currentProject == null ? "Model: none" : $"Model: {currentProject.projectName}");
        }

        private void UpdateUiState()
        {
            bool hasSelection = selectedFbxIndex >= 0 && selectedFbxIndex < availableFbxOptions.Count;
            bool hasDetectedMeshes = detectedParts.Count > 0;
            bool hasDraftSelection = GetSelectedDetectedObjectNames().Length > 0;
            bool hasSavedParts = currentProject != null && currentProject.parts.Count > 0;
            bool hasDraftText = !string.IsNullOrWhiteSpace(GetPartTitle()) || !string.IsNullOrWhiteSpace(GetDescription());

            startView.SetInteractable(!isConverting);
            fbxSelectionView.SetInteractable(isConverting, hasSelection);
            partEditorView.SetInteractable(isConverting, currentFbxInstance != null, hasDetectedMeshes, hasDraftSelection, hasDraftText, hasSavedParts, currentProject != null && currentProject.IsValid());
        }

        private string ResolveProjectExportPath(string projectName)
        {
            return Path.Combine(exportRootPath, SanitizePathSegment(projectName));
        }

        private string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "NewProject";
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value.Trim();
        }

        private string GetPartTitle()
        {
            return partEditorView.GetPartTitle();
        }

        private void SetPartTitle(string value)
        {
            partEditorView.SetPartTitle(value);
        }

        private string GetDescription()
        {
            return partEditorView.GetDescription();
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

        private static string FormatBoundObjects(string[] objectNames)
        {
            return objectNames == null || objectNames.Length == 0 ? "(none)" : string.Join(", ", objectNames);
        }

        private static string FirstOrEmpty(string[] values)
        {
            return values == null || values.Length == 0 ? string.Empty : values[0];
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
            RefreshProgressUi();
            UpdateUiState();

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
                RefreshProgressUi();
                yield return null;
            }

            tempAudioPipeline.ProgressUpdated -= OnProgressUpdated;
            Destroy(tempGo);

            isConverting = false;
            RefreshProgressUi();
            SetEditorStatus($"Export completed: {Path.Combine(currentProject.exportPath, "Package")}");
            UpdateUiState();
        }

        private void OnProgressUpdated(int current, int total, string message)
        {
            conversionProgress = total > 0 ? (float)current / total : 0f;
            conversionMessage = message;
            RefreshProgressUi();
        }

        private void RefreshProgressUi()
        {
            partEditorView.SetProgress(
                isConverting,
                conversionProgress,
                isConverting ? $"Progress {(conversionProgress * 100f):F0}%  {conversionMessage}" : string.Empty);
        }

        private void SetBrowserStatus(string message)
        {
            fbxSelectionView.SetStatus(message);
        }

        private void SetEditorStatus(string message)
        {
            partEditorView.SetStatus(message);
        }

        private void FramePreviewCamera()
        {
            Camera targetCamera = previewCamera != null ? previewCamera : Camera.main;
            if (targetCamera == null || currentFbxInstance == null)
            {
                return;
            }

            Renderer[] renderers = currentFbxInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 direction = previewDirection.sqrMagnitude < 0.001f ? new Vector3(0.35f, 0.25f, -1f).normalized : previewDirection.normalized;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            float distance = targetCamera.orthographic
                ? radius * 2f
                : radius / Mathf.Sin(Mathf.Max(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad, 0.1f));

            distance *= previewDistancePadding;
            targetCamera.transform.position = bounds.center + direction * distance;
            targetCamera.transform.LookAt(bounds.center);

            if (targetCamera.orthographic)
            {
                targetCamera.orthographicSize = radius * previewDistancePadding;
            }
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
                if (partSelection[i] && detectedParts[i].transform != null)
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

            if (state.HasBaseColor) state.BaseColor = material.GetColor("_BaseColor");
            if (state.HasColor) state.Color = material.GetColor("_Color");
            if (state.HasEmissionColor) state.EmissionColor = material.GetColor("_EmissionColor");
            if (state.HasSurface) state.Surface = material.GetFloat("_Surface");
            if (state.HasBlend) state.Blend = material.GetFloat("_Blend");
            if (state.HasSrcBlend) state.SrcBlend = material.GetInt("_SrcBlend");
            if (state.HasDstBlend) state.DstBlend = material.GetInt("_DstBlend");
            if (state.HasZWrite) state.ZWrite = material.GetInt("_ZWrite");

            previewStates[material] = state;
        }

        private static void ApplySelectedPreview(Material material)
        {
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
                material.SetColor("_EmissionColor", SelectionEmissionColor);
            }
        }

        private static void ApplyDimPreview(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor");
                color.a = PreviewDimAlpha;
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                Color color = material.GetColor("_Color");
                color.a = PreviewDimAlpha;
                material.SetColor("_Color", color);
            }

            SetPreviewTransparent(material);
        }

        private static void SetPreviewTransparent(Material material)
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

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private void RestorePreviewMaterial(Material material)
        {
            if (!previewStates.TryGetValue(material, out PreviewMaterialState state))
            {
                return;
            }

            if (state.HasBaseColor) material.SetColor("_BaseColor", state.BaseColor);
            if (state.HasColor) material.SetColor("_Color", state.Color);
            if (state.HasEmissionColor) material.SetColor("_EmissionColor", state.EmissionColor);
            if (state.HasSurface) material.SetFloat("_Surface", state.Surface);
            if (state.HasBlend) material.SetFloat("_Blend", state.Blend);
            if (state.HasSrcBlend) material.SetInt("_SrcBlend", state.SrcBlend);
            if (state.HasDstBlend) material.SetInt("_DstBlend", state.DstBlend);
            if (state.HasZWrite) material.SetInt("_ZWrite", state.ZWrite);
            material.renderQueue = state.RenderQueue;

            if (state.EmissionKeywordEnabled) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
            if (state.SurfaceTransparentKeywordEnabled) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (state.AlphaPremultiplyKeywordEnabled) material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); else material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
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
