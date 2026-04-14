using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FBXViewer.Converter
{
    public class RuntimeConversionPartEditorView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button backToFbxListButton;
        [SerializeField] private Button refreshDetectedMeshesButton;
        [SerializeField] private Button savePartButton;
        [SerializeField] private Button clearDraftButton;
        [SerializeField] private Button exportButton;
        [SerializeField] private RectTransform detectedMeshListContent;
        [SerializeField] private Toggle detectedMeshToggleTemplate;
        [SerializeField] private RectTransform savedPartsContent;
        [SerializeField] private Text savedPartSummaryTemplate;
        [SerializeField] private Text currentFbxLabel;
        [SerializeField] private Text selectedMeshSummaryText;
        [SerializeField] private Text editorStatusText;
        [SerializeField] private InputField partTitleInput;
        [SerializeField] private InputField descriptionInput;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Text progressLabel;

        private readonly List<Toggle> spawnedMeshToggles = new();
        private readonly List<Text> spawnedPartSummaries = new();

        public GameObject Root => root != null ? root : gameObject;

        public bool HasRequiredReferences()
        {
            return Root != null &&
                   backToFbxListButton != null &&
                   refreshDetectedMeshesButton != null &&
                   savePartButton != null &&
                   clearDraftButton != null &&
                   exportButton != null &&
                   detectedMeshListContent != null &&
                   detectedMeshToggleTemplate != null &&
                   savedPartsContent != null &&
                   savedPartSummaryTemplate != null &&
                   currentFbxLabel != null &&
                   selectedMeshSummaryText != null &&
                   editorStatusText != null &&
                   partTitleInput != null &&
                   descriptionInput != null &&
                   progressSlider != null &&
                   progressLabel != null;
        }

        public void Initialize(Action onBack, Action onRefreshMeshes, Action onSavePart, Action onClearDraft, Action onExport, Action onInputChanged)
        {
            BindButton(backToFbxListButton, onBack, "Back to List");
            BindButton(refreshDetectedMeshesButton, onRefreshMeshes, "Refresh Meshes");
            BindButton(savePartButton, onSavePart, "Save Part");
            BindButton(clearDraftButton, onClearDraft, "Clear");
            BindButton(exportButton, onExport, "Export");

            partTitleInput.onValueChanged.RemoveAllListeners();
            partTitleInput.onValueChanged.AddListener(_ => onInputChanged?.Invoke());
            descriptionInput.onValueChanged.RemoveAllListeners();
            descriptionInput.onValueChanged.AddListener(_ => onInputChanged?.Invoke());
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
        }

        public void RenderDetectedMeshes(IReadOnlyList<string> labels, IReadOnlyList<bool> selectedStates, Action<int, bool> onToggle)
        {
            ClearDetectedMeshItems();
            for (int i = 0; i < labels.Count; i++)
            {
                int capturedIndex = i;
                Toggle toggle = Instantiate(detectedMeshToggleTemplate, detectedMeshListContent);
                toggle.gameObject.name = $"MeshToggle_{i}";
                toggle.gameObject.SetActive(true);
                SetToggleLabel(toggle, labels[i]);
                toggle.isOn = i < selectedStates.Count && selectedStates[i];
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(value => onToggle?.Invoke(capturedIndex, value));
                spawnedMeshToggles.Add(toggle);
            }
        }

        public void RenderSavedParts(IReadOnlyList<string> summaries)
        {
            ClearSavedPartItems();
            for (int i = 0; i < summaries.Count; i++)
            {
                Text item = Instantiate(savedPartSummaryTemplate, savedPartsContent);
                item.gameObject.name = $"SavedPart_{i}";
                item.gameObject.SetActive(true);
                item.text = summaries[i];
                spawnedPartSummaries.Add(item);
            }
        }

        public void ClearDetectedMeshes()
        {
            ClearDetectedMeshItems();
        }

        public void ClearSavedParts()
        {
            ClearSavedPartItems();
        }

        public void ClearDraft()
        {
            partTitleInput.text = string.Empty;
            descriptionInput.text = string.Empty;
        }

        public string GetPartTitle()
        {
            return partTitleInput.text.Trim();
        }

        public string GetDescription()
        {
            return descriptionInput.text.Trim();
        }

        public void SetPartTitle(string value)
        {
            partTitleInput.text = value;
        }

        public void SetCurrentFbxLabel(string value)
        {
            currentFbxLabel.text = value;
        }

        public void SetSelectedMeshSummary(string value)
        {
            selectedMeshSummaryText.text = value;
        }

        public void SetStatus(string value)
        {
            editorStatusText.text = value;
        }

        public void SetProgress(bool visible, float progress, string message)
        {
            progressSlider.gameObject.SetActive(visible);
            progressLabel.gameObject.SetActive(visible);
            progressSlider.value = progress;
            progressLabel.text = visible ? message : string.Empty;
        }

        public void SetInteractable(bool isConverting, bool hasCurrentFbx, bool hasDetectedMeshes, bool hasDraftSelection, bool hasDraftText, bool hasSavedParts, bool projectIsValid)
        {
            backToFbxListButton.interactable = !isConverting;
            refreshDetectedMeshesButton.interactable = !isConverting && hasCurrentFbx;
            savePartButton.interactable = !isConverting && hasDetectedMeshes && hasDraftSelection;
            clearDraftButton.interactable = !isConverting && (hasDraftSelection || hasDraftText);
            exportButton.interactable = !isConverting && hasSavedParts && projectIsValid;
        }

        private void ClearDetectedMeshItems()
        {
            foreach (Toggle toggle in spawnedMeshToggles)
            {
                if (toggle != null)
                {
                    Destroy(toggle.gameObject);
                }
            }

            spawnedMeshToggles.Clear();
        }

        private void ClearSavedPartItems()
        {
            foreach (Text item in spawnedPartSummaries)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            spawnedPartSummaries.Clear();
        }

        private static void BindButton(Button button, Action action, string label)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
            SetButtonLabel(button, label);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private static void SetToggleLabel(Toggle toggle, string label)
        {
            Text text = toggle.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
