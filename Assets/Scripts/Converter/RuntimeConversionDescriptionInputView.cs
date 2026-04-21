using System;
using UnityEngine;
using UnityEngine.UI;

namespace FBXViewer.Converter
{
    public class RuntimeConversionDescriptionInputView : MonoBehaviour
    {
        [SerializeField] private InputField inputField;
        [SerializeField] private Text validationText;

        public bool HasRequiredReferences()
        {
            return inputField != null;
        }

        public void Initialize(Action onInputChanged)
        {
            inputField.onValueChanged.RemoveAllListeners();
            inputField.onValueChanged.AddListener(_ =>
            {
                RefreshValidation();
                onInputChanged?.Invoke();
            });

            RefreshValidation();
        }

        public void SetText(string value)
        {
            inputField.text = value ?? string.Empty;
            RefreshValidation();
        }

        public string GetText()
        {
            return inputField.text.Trim();
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(GetText());
        }

        public void RefreshValidation()
        {
            if (validationText == null || inputField == null || validationText == inputField.textComponent)
            {
                return;
            }

            bool isValid = IsValid();
            validationText.text = isValid ? string.Empty : "Enter a description.";
            validationText.gameObject.SetActive(!isValid);
        }
    }
}
