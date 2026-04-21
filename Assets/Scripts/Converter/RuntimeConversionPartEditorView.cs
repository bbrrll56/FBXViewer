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
        [SerializeField] private RectTransform descriptionInputContent;
        [SerializeField] private RuntimeConversionDescriptionInputView descriptionInputTemplate;
        [SerializeField] private Button addDescriptionButton;
        [SerializeField] private Button removeDescriptionButton;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Text progressLabel;

        private readonly List<Toggle> spawnedMeshToggles = new();
        private readonly List<Text> spawnedPartSummaries = new();
        private readonly List<RuntimeConversionDescriptionInputView> spawnedDescriptionInputs = new();
        private Action onInputChanged;

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
                   descriptionInputContent != null &&
                   descriptionInputTemplate != null &&
                   descriptionInputTemplate.HasRequiredReferences() &&
                   addDescriptionButton != null &&
                   removeDescriptionButton != null &&
                   progressSlider != null &&
                   progressLabel != null;
        }

        public void Initialize(Action onBack, Action onRefreshMeshes, Action onSavePart, Action onClearDraft, Action onExport, Action onInputChanged)
        {
            this.onInputChanged = onInputChanged;

            BindButton(backToFbxListButton, onBack, "Back to List");
            BindButton(refreshDetectedMeshesButton, onRefreshMeshes, "Refresh Meshes");
            BindButton(savePartButton, onSavePart, "Save Part");
            BindButton(clearDraftButton, onClearDraft, "Clear");
            BindButton(exportButton, onExport, "Export");
            BindButton(addDescriptionButton, AddDescriptionInput, "+");
            BindButton(removeDescriptionButton, RemoveLastDescriptionInput, "-");

            partTitleInput.onValueChanged.RemoveAllListeners();
            partTitleInput.onValueChanged.AddListener(_ => onInputChanged?.Invoke());

            descriptionInputTemplate.gameObject.SetActive(false);
            EnsureDescriptionInputCount(1);
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

            RefreshDetectedMeshListLayout(true);
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
            ClearDescriptionInputs();
            AddDescriptionInput();
        }

        public string GetPartTitle()
        {
            return partTitleInput.text.Trim();
        }

        public string[] GetDescriptions()
        {
            var descriptions = new List<string>();
            foreach (RuntimeConversionDescriptionInputView input in spawnedDescriptionInputs)
            {
                if (input == null)
                {
                    continue;
                }

                string description = input.GetText();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    descriptions.Add(description);
                }
            }

            return descriptions.ToArray();
        }

        public bool HasAnyDescriptionText()
        {
            foreach (RuntimeConversionDescriptionInputView input in spawnedDescriptionInputs)
            {
                if (input != null && !string.IsNullOrWhiteSpace(input.GetText()))
                {
                    return true;
                }
            }

            return false;
        }

        public bool AreDescriptionInputsValid()
        {
            bool allValid = true;
            foreach (RuntimeConversionDescriptionInputView input in spawnedDescriptionInputs)
            {
                if (input == null)
                {
                    continue;
                }

                input.RefreshValidation();
                allValid &= input.IsValid();
            }

            return allValid;
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
            addDescriptionButton.interactable = !isConverting;
            removeDescriptionButton.interactable = !isConverting && spawnedDescriptionInputs.Count > 1;
        }

        private void AddDescriptionInput()
        {
            RuntimeConversionDescriptionInputView input = Instantiate(descriptionInputTemplate, descriptionInputContent);
            input.gameObject.name = $"DescriptionInput_{spawnedDescriptionInputs.Count}";
            input.gameObject.SetActive(true);
            input.Initialize(onInputChanged);
            spawnedDescriptionInputs.Add(input);
            RefreshDescriptionInputLayout();
            onInputChanged?.Invoke();
        }

        private void RemoveLastDescriptionInput()
        {
            if (spawnedDescriptionInputs.Count <= 1)
            {
                return;
            }

            int lastIndex = spawnedDescriptionInputs.Count - 1;
            RuntimeConversionDescriptionInputView input = spawnedDescriptionInputs[lastIndex];
            spawnedDescriptionInputs.RemoveAt(lastIndex);
            if (input != null)
            {
                Destroy(input.gameObject);
            }

            RefreshDescriptionInputLayout();
            onInputChanged?.Invoke();
        }

        private void EnsureDescriptionInputCount(int count)
        {
            while (spawnedDescriptionInputs.Count < count)
            {
                AddDescriptionInput();
            }

            while (spawnedDescriptionInputs.Count > count)
            {
                RemoveLastDescriptionInput();
            }
        }

        private void ClearDescriptionInputs()
        {
            foreach (RuntimeConversionDescriptionInputView input in spawnedDescriptionInputs)
            {
                if (input != null)
                {
                    Destroy(input.gameObject);
                }
            }

            spawnedDescriptionInputs.Clear();
            RefreshDescriptionInputLayout();
        }

        private void RefreshDescriptionInputLayout()
        {
            if (descriptionInputContent == null ||
                descriptionInputContent.GetComponent<LayoutGroup>() != null ||
                descriptionInputTemplate == null)
            {
                return;
            }

            RectTransform templateTransform = descriptionInputTemplate.transform as RectTransform;
            if (templateTransform == null)
            {
                return;
            }

            Vector2 basePosition = templateTransform.anchoredPosition;
            float spacing = Mathf.Max(70f, templateTransform.sizeDelta.y + 10f);
            float contentHeight = spacing * Mathf.Max(1, spawnedDescriptionInputs.Count);
            RectTransform viewportTransform = descriptionInputContent.parent as RectTransform;
            if (viewportTransform != null)
            {
                contentHeight = Mathf.Max(contentHeight, viewportTransform.rect.height);
            }

            descriptionInputContent.sizeDelta = new Vector2(descriptionInputContent.sizeDelta.x, contentHeight);

            for (int i = 0; i < spawnedDescriptionInputs.Count; i++)
            {
                if (spawnedDescriptionInputs[i] == null)
                {
                    continue;
                }

                RectTransform inputTransform = spawnedDescriptionInputs[i].transform as RectTransform;
                if (inputTransform != null)
                {
                    inputTransform.anchoredPosition = basePosition + (Vector2.down * spacing * i);
                }
            }
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
            RefreshDetectedMeshListLayout(false);
        }

        private void RefreshDetectedMeshListLayout(bool resetScrollToTop)
        {
            if (detectedMeshListContent == null || detectedMeshToggleTemplate == null)
            {
                return;
            }

            RectTransform templateTransform = detectedMeshToggleTemplate.transform as RectTransform;
            float itemHeight = 60f;
            if (templateTransform != null)
            {
                itemHeight = Mathf.Max(itemHeight, templateTransform.rect.height, templateTransform.sizeDelta.y);
            }

            float spacing = 0f;
            int paddingTop = 0;
            int paddingBottom = 0;
            LayoutGroup layoutGroup = detectedMeshListContent.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                paddingTop = layoutGroup.padding.top;
                paddingBottom = layoutGroup.padding.bottom;
                if (layoutGroup is VerticalLayoutGroup verticalLayoutGroup)
                {
                    spacing = verticalLayoutGroup.spacing;
                }
            }

            int itemCount = Mathf.Max(1, spawnedMeshToggles.Count);
            float contentHeight = paddingTop + paddingBottom + (itemHeight * itemCount) + (spacing * Mathf.Max(0, itemCount - 1));

            RectTransform viewportTransform = detectedMeshListContent.parent as RectTransform;
            if (viewportTransform != null)
            {
                contentHeight = Mathf.Max(contentHeight, viewportTransform.rect.height);
            }

            detectedMeshListContent.sizeDelta = new Vector2(detectedMeshListContent.sizeDelta.x, contentHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(detectedMeshListContent);

            ScrollRect scrollRect = detectedMeshListContent.GetComponentInParent<ScrollRect>();
            if (resetScrollToTop && scrollRect != null && scrollRect.content == detectedMeshListContent)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
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
