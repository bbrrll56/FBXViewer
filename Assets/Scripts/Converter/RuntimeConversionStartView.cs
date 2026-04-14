using System;
using UnityEngine;
using UnityEngine.UI;

namespace FBXViewer.Converter
{
    public class RuntimeConversionStartView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button startButton;

        public GameObject Root => root != null ? root : gameObject;

        public bool HasRequiredReferences()
        {
            return Root != null && startButton != null;
        }

        public void Initialize(Action onStart)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() => onStart?.Invoke());
            SetButtonLabel(startButton, "Start");
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
        }

        public void SetInteractable(bool interactable)
        {
            startButton.interactable = interactable;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
