using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FBXViewer.Converter
{
    public class RuntimeConversionFbxSelectionView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button refreshFbxListButton;
        [SerializeField] private Button openSelectedFbxButton;
        [SerializeField] private Button backToStartButton;
        [SerializeField] private RectTransform fbxListContent;
        [SerializeField] private Button fbxListItemTemplate;
        [SerializeField] private Text fbxListStatusText;

        private readonly List<Button> spawnedItems = new();

        public GameObject Root => root != null ? root : gameObject;

        public bool HasRequiredReferences()
        {
            return Root != null &&
                   refreshFbxListButton != null &&
                   openSelectedFbxButton != null &&
                   backToStartButton != null &&
                   fbxListContent != null &&
                   fbxListItemTemplate != null &&
                   fbxListStatusText != null;
        }

        public void Initialize(Action onRefresh, Action onOpenSelected, Action onBack)
        {
            BindButton(refreshFbxListButton, onRefresh, "Refresh");
            BindButton(openSelectedFbxButton, onOpenSelected, "Select");
            BindButton(backToStartButton, onBack, "Back");
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
        }

        public void RenderFbxList(IReadOnlyList<string> labels, int selectedIndex, Action<int> onSelect)
        {
            ClearFbxList();
            for (int i = 0; i < labels.Count; i++)
            {
                int capturedIndex = i;
                Button item = Instantiate(fbxListItemTemplate, fbxListContent);
                item.gameObject.name = $"FbxItem_{i}";
                item.gameObject.SetActive(true);
                SetButtonLabel(item, labels[i]);
                SetItemSelected(item, i == selectedIndex);
                item.onClick.RemoveAllListeners();
                item.onClick.AddListener(() => onSelect?.Invoke(capturedIndex));
                spawnedItems.Add(item);
            }
        }

        public void SetSelectedIndex(int selectedIndex)
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                SetItemSelected(spawnedItems[i], i == selectedIndex);
            }
        }

        public void SetStatus(string message)
        {
            fbxListStatusText.text = message;
        }

        public void SetInteractable(bool isConverting, bool hasSelection)
        {
            refreshFbxListButton.interactable = !isConverting;
            openSelectedFbxButton.interactable = !isConverting && hasSelection;
            backToStartButton.interactable = !isConverting;
        }

        private void ClearFbxList()
        {
            foreach (Button item in spawnedItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            spawnedItems.Clear();
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

        private static void SetItemSelected(Button button, bool selected)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected ? new Color(0.85f, 0.93f, 1f, 1f) : Color.white;
            }
        }
    }
}
