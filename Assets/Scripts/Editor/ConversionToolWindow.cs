using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FBXViewer.Audio;
using FBXViewer.Converter;
using FBXViewer.Data;
using UnityEditor;
using UnityEngine;

namespace FBXViewer.Editor
{
    public class ConversionToolWindow : EditorWindow
    {
        private FBXConverter converter;
        private ProjectData projectData;

        private Vector2 scrollPosition;
        private bool showPartsList = true;
        private bool showDetectionResult = true;
        private int selectedPartIndex = -1;

        private GameObject currentFBXInstance;
        private List<DetectedPart> detectedParts = new List<DetectedPart>();
        private List<bool> partSelection = new List<bool>();
        private int detectionFilterMode;

        private string projectName = "NewProject";
        private string fbxFilePath = string.Empty;
        private string exportPath = string.Empty;
        private string newPartName = string.Empty;
        private List<string> newDescriptions = new List<string> { string.Empty };

        private bool isConverting;
        private float conversionProgress;
        private string conversionMessage = string.Empty;
        private string lastPackagePath = string.Empty;
        private bool showPackageInfo;

        [MenuItem("Window/FBXViewer/Conversion Tool")]
        public static void ShowWindow()
        {
            GetWindow<ConversionToolWindow>("FBX Conversion Tool");
        }

        private void OnEnable()
        {
            converter = FindObjectOfType<FBXConverter>();
            if (converter == null)
            {
                var go = new GameObject("FBXConverter");
                converter = go.AddComponent<FBXConverter>();
            }
        }

        private void OnDisable()
        {
            if (currentFBXInstance != null)
            {
                FBXImporter.UnloadFBX(currentFBXInstance);
                currentFBXInstance = null;
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Label("FBX to Quest package converter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            DrawProjectSettings();
            DrawSeparator();
            DrawFBXDetection();
            DrawSeparator();
            DrawPartsList();
            DrawSeparator();
            DrawAddPartForm();
            DrawSeparator();
            DrawConversionButton();

            GUILayout.EndScrollView();
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space();
            GUILayout.Box(string.Empty, GUILayout.ExpandWidth(true), GUILayout.Height(1));
            EditorGUILayout.Space();
        }

        private void DrawProjectSettings()
        {
            GUILayout.Label("Project", EditorStyles.boldLabel);

            projectName = EditorGUILayout.TextField("Project Name", projectName);

            EditorGUILayout.BeginHorizontal();
            fbxFilePath = EditorGUILayout.TextField("FBX File", fbxFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                fbxFilePath = EditorUtility.OpenFilePanel("Select FBX File", string.Empty, "fbx");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            exportPath = EditorGUILayout.TextField("Export Folder", exportPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                exportPath = EditorUtility.OpenFolderPanel("Select Export Folder", string.Empty, string.Empty);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Create Project", GUILayout.Height(30)))
            {
                CreateProject();
            }

            if (projectData != null)
            {
                EditorGUILayout.HelpBox(
                    $"Project: {projectData.projectName}\n" +
                    $"Parts: {projectData.GetPartCount()}\n" +
                    $"Descriptions: {projectData.GetTotalDescriptionCount()}",
                    MessageType.Info);
            }
        }

        private void DrawFBXDetection()
        {
            GUILayout.Label("FBX Parts", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(fbxFilePath) || projectData == null);
            if (GUILayout.Button("Load FBX And Detect Parts", GUILayout.Height(30)))
            {
                DetectFBXParts();
            }
            EditorGUI.EndDisabledGroup();

            if (currentFBXInstance == null)
            {
                return;
            }

            EditorGUILayout.HelpBox($"Loaded FBX: {currentFBXInstance.name}", MessageType.Info);

            string[] filterOptions = { "All Objects", "Meshes Only", "Groups Only" };
            int newMode = EditorGUILayout.Popup("Filter", detectionFilterMode, filterOptions);
            if (newMode != detectionFilterMode)
            {
                detectionFilterMode = newMode;
                RefreshDetectionResult();
            }

            DrawDetectionResult();
        }

        private void DrawDetectionResult()
        {
            showDetectionResult = EditorGUILayout.Foldout(showDetectionResult, $"Detected Objects ({detectedParts.Count})", true);
            if (!showDetectionResult || detectedParts.Count == 0)
            {
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < detectedParts.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                partSelection[i] = EditorGUILayout.ToggleLeft(detectedParts[i].ToString(), partSelection[i]);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;

            if (GUILayout.Button("Add Selected As Individual Parts", GUILayout.Height(25)))
            {
                AddSelectedParts();
            }
        }

        private void DrawPartsList()
        {
            int partCount = projectData?.GetPartCount() ?? 0;
            showPartsList = EditorGUILayout.Foldout(showPartsList, $"Added Parts ({partCount})", true);

            if (!showPartsList || projectData == null)
            {
                return;
            }

            if (projectData.parts.Count == 0)
            {
                EditorGUILayout.HelpBox("No parts have been added yet.", MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;

            for (int i = 0; i < projectData.parts.Count; i++)
            {
                PartInfo part = projectData.parts[i];
                bool isSelected = i == selectedPartIndex;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(isSelected ? "-" : "+", GUILayout.Width(24)))
                {
                    selectedPartIndex = isSelected ? -1 : i;
                }

                EditorGUILayout.LabelField($"{part.partName} ({part.descriptions.Length} descriptions)");

                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("Remove Part", $"Remove '{part.partName}'?", "Remove", "Cancel"))
                    {
                        projectData.RemovePart(i);
                        selectedPartIndex = -1;
                        return;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (!isSelected)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Bound Objects", GetBoundObjectSummary(part));

                for (int j = 0; j < part.descriptions.Length; j++)
                {
                    EditorGUILayout.TextArea(part.descriptions[j], GUILayout.Height(40));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawAddPartForm()
        {
            GUILayout.Label("Add Part", EditorStyles.boldLabel);

            string[] selectedObjectNames = GetSelectedDetectedObjectNames();
            EditorGUILayout.HelpBox(
                $"Bound FBX objects: {selectedObjectNames.Length}" +
                (selectedObjectNames.Length > 0
                    ? $"\n{string.Join(", ", selectedObjectNames)}"
                    : "\nSelect one or more FBX objects above to bind them to this part."),
                MessageType.Info);

            newPartName = EditorGUILayout.TextField("Part Name", newPartName);
            EditorGUILayout.LabelField("Descriptions");

            for (int i = 0; i < newDescriptions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                newDescriptions[i] = EditorGUILayout.TextArea(newDescriptions[i], GUILayout.Height(50));

                if (GUILayout.Button("-", GUILayout.Width(30)))
                {
                    newDescriptions.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Description", GUILayout.Height(25)))
            {
                newDescriptions.Add(string.Empty);
            }

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(newPartName) || newDescriptions.Count == 0 || projectData == null);
            if (GUILayout.Button("Add Part", GUILayout.Height(30)))
            {
                AddPart();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawConversionButton()
        {
            GUILayout.Label("Convert", EditorStyles.boldLabel);

            if (isConverting)
            {
                EditorGUILayout.HelpBox($"Converting: {conversionMessage}", MessageType.Info);
                Rect progressRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
                EditorGUI.ProgressBar(progressRect, conversionProgress, $"{conversionProgress * 100f:F0}%");
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "VoiceVox must be running at http://localhost:50021 during audio generation.",
                    MessageType.Info);
            }

            EditorGUI.BeginDisabledGroup(projectData == null || !projectData.IsValid() || isConverting);
            if (GUILayout.Button("Start Conversion", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog(
                    "Start Conversion",
                    "Start conversion now?\nVoiceVox must be running for audio generation.",
                    "Start",
                    "Cancel"))
                {
                    ExecuteConversion();
                }
            }
            EditorGUI.EndDisabledGroup();

            if (projectData != null && !projectData.IsValid())
            {
                EditorGUILayout.HelpBox("At least one valid part and an export folder are required.", MessageType.Warning);
            }

            if (string.IsNullOrEmpty(lastPackagePath))
            {
                return;
            }

            EditorGUILayout.Space();
            showPackageInfo = EditorGUILayout.Foldout(showPackageInfo, "Last Package", true);
            if (!showPackageInfo)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox($"Package generated at:\n{lastPackagePath}", MessageType.Info);
            EditorGUILayout.LabelField("Output");
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Package/");
            EditorGUILayout.LabelField("Audio/");
            EditorGUILayout.LabelField("Model/");
            EditorGUILayout.LabelField("Metadata/");
            EditorGUILayout.LabelField("manifest.json");
            EditorGUI.indentLevel -= 2;

            if (GUILayout.Button("Open Package Folder", GUILayout.Height(25)))
            {
                Process.Start("explorer.exe", lastPackagePath);
            }
        }

        private void CreateProject()
        {
            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(fbxFilePath) || string.IsNullOrEmpty(exportPath))
            {
                EditorUtility.DisplayDialog("Error", "Project name, FBX file, and export folder are required.", "OK");
                return;
            }

            converter.CreateNewProject(fbxFilePath, projectName);
            projectData = converter.GetCurrentProject();
            projectData.exportPath = exportPath;

            EditorUtility.DisplayDialog("Created", $"Project '{projectName}' was created.", "OK");
        }

        private void DetectFBXParts()
        {
            if (currentFBXInstance != null)
            {
                FBXImporter.UnloadFBX(currentFBXInstance);
            }

            currentFBXInstance = FBXImporter.ImportFBX(fbxFilePath);
            if (currentFBXInstance != null)
            {
                RefreshDetectionResult();
            }
        }

        private void RefreshDetectionResult()
        {
            if (currentFBXInstance == null)
            {
                return;
            }

            List<DetectedPart> allParts = PartDetector.DetectParts(currentFBXInstance);
            detectedParts = detectionFilterMode switch
            {
                1 => PartDetector.FilterMeshParts(allParts),
                2 => PartDetector.FilterParentParts(allParts),
                _ => allParts
            };

            partSelection = new List<bool>(new bool[detectedParts.Count]);
        }

        private void AddSelectedParts()
        {
            int added = 0;

            for (int i = 0; i < detectedParts.Count; i++)
            {
                if (!partSelection[i])
                {
                    continue;
                }

                string partName = detectedParts[i].objectName;
                string[] descriptions = { $"{partName} description" };
                string[] selectedObjectNames = { detectedParts[i].objectName };
                converter.AddPartInfo(partName, descriptions, selectedObjectNames);
                added++;
            }

            partSelection = new List<bool>(new bool[detectedParts.Count]);
            projectData = converter.GetCurrentProject();
            EditorUtility.DisplayDialog("Added", $"{added} parts were added.", "OK");
        }

        private void AddPart()
        {
            var validDescriptions = new List<string>();
            foreach (string description in newDescriptions)
            {
                if (!string.IsNullOrWhiteSpace(description))
                {
                    validDescriptions.Add(description.Trim());
                }
            }

            if (validDescriptions.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Add at least one description.", "OK");
                return;
            }

            string[] selectedObjectNames = GetSelectedDetectedObjectNames();
            converter.AddPartInfo(newPartName, validDescriptions.ToArray(), selectedObjectNames);
            projectData = converter.GetCurrentProject();

            newPartName = string.Empty;
            newDescriptions = new List<string> { string.Empty };
            partSelection = new List<bool>(new bool[detectedParts.Count]);

            EditorUtility.DisplayDialog("Added", "The part was added.", "OK");
        }

        private string[] GetSelectedDetectedObjectNames()
        {
            var selectedNames = new List<string>();

            for (int i = 0; i < detectedParts.Count && i < partSelection.Count; i++)
            {
                if (!partSelection[i])
                {
                    continue;
                }

                string objectName = detectedParts[i].objectName;
                if (!string.IsNullOrEmpty(objectName) && !selectedNames.Contains(objectName))
                {
                    selectedNames.Add(objectName);
                }
            }

            return selectedNames.ToArray();
        }

        private string GetBoundObjectSummary(PartInfo part)
        {
            if (part.selectedObjectNames == null || part.selectedObjectNames.Length == 0)
            {
                return "(none)";
            }

            return string.Join(", ", part.selectedObjectNames);
        }

        private void ExecuteConversion()
        {
            isConverting = true;
            conversionProgress = 0f;
            conversionMessage = "Initializing...";

            var tempGO = new GameObject("_TempConverter_");
            var tempConverter = tempGO.AddComponent<FBXConverter>();
            var pipeline = tempGO.AddComponent<AudioGenerationPipeline>();

            tempConverter.Initialize();
            pipeline.Initialize();

            tempConverter.CreateNewProject(projectData.fbxPath, projectData.projectName);
            ProjectData tempProject = tempConverter.GetCurrentProject();
            tempProject.exportPath = projectData.exportPath;

            foreach (PartInfo part in projectData.parts)
            {
                tempConverter.AddPartInfo(part.partName, part.descriptions, part.selectedObjectNames);
            }

            pipeline.ProgressUpdated += (current, total, message) =>
            {
                conversionProgress = total > 0 ? (float)current / total : 0f;
                conversionMessage = message;
            };

            tempConverter.ConversionComplete += (success, message) =>
            {
                UnityEngine.Debug.Log(message);
                isConverting = false;
                lastPackagePath = Path.Combine(projectData.exportPath, "Package");
                showPackageInfo = true;

                EditorUtility.DisplayDialog("Completed", $"{message}\n\nPackage folder:\n{lastPackagePath}", "OK");
                UnityEngine.Object.DestroyImmediate(tempGO);
            };

            tempConverter.StartCoroutine(tempConverter.Convert());
        }
    }
}
