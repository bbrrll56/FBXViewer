using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FBXViewer.Viewer
{
    /// <summary>
    /// Displays a converted package and plays description audio.
    /// </summary>
    public class PackageViewer : MonoBehaviour
    {
        private class HighlightMaterialSet
        {
            public readonly List<Material> TargetMaterials = new List<Material>();
            public readonly List<Material> OtherMaterials = new List<Material>();
        }

        private class MaterialHighlightState
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

        [SerializeField] private string packageFolderPath = string.Empty;
        [SerializeField] private Transform modelContainer;
        [SerializeField] private Text projectNameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button playAudioButton;
        [SerializeField] private Dropdown partDropdown;
        [SerializeField] private Dropdown descriptionDropdown;
        [SerializeField] private WAVPlayer wavPlayer;

        private readonly List<Material> highlightedMaterials = new List<Material>();
        private readonly Dictionary<Material, MaterialHighlightState> highlightStates = new Dictionary<Material, MaterialHighlightState>();
        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.15f, 1f);
        private static readonly Color HighlightEmissionColor = new Color(1.2f, 0.9f, 0.2f, 1f);
        private const float HighlightDurationSeconds = 2f;
        private const float HighlightPulseFrequency = 2.5f;
        private const float DimAlpha = 0.2f;

        private ProjectMetadataData projectMetadata;
        private PackageManifest packageManifest;
        private AssetBundle loadedModelBundle;
        private GameObject currentModelInstance;
        private int currentPartIndex;
        private int currentDescriptionIndex;
        private Coroutine highlightCoroutine;
        private Coroutine audioSequenceCoroutine;

        private void Start()
        {
            EnsureWavPlayer();

            if (string.IsNullOrEmpty(packageFolderPath))
            {
                packageFolderPath = ResolveDefaultPackagePath();
            }

            Debug.Log($"[PackageViewer] Package path: {packageFolderPath}");
            Debug.Log($"[PackageViewer] Path exists: {Directory.Exists(packageFolderPath)}");

            RegisterUiCallbacks();
            LoadPackage();
        }

        private static string ResolveDefaultPackagePath()
        {
            string packagesRoot = Path.Combine(Application.persistentDataPath, "Packages");
            string packagePath = FindFirstPackagePath(packagesRoot);
            if (!string.IsNullOrEmpty(packagePath))
            {
                return packagePath;
            }

            string streamingAssetsPackage = Path.Combine(Application.streamingAssetsPath, "Package");
            if (IsPackagePath(streamingAssetsPackage))
            {
                return streamingAssetsPackage;
            }

            return packagesRoot;
        }

        private static string FindFirstPackagePath(string packagesRoot)
        {
            if (IsPackagePath(packagesRoot))
            {
                return packagesRoot;
            }

            if (!Directory.Exists(packagesRoot))
            {
                return string.Empty;
            }

            string[] packageDirectories = Directory.GetDirectories(packagesRoot);
            for (int i = 0; i < packageDirectories.Length; i++)
            {
                if (IsPackagePath(packageDirectories[i]))
                {
                    return packageDirectories[i];
                }
            }

            string nestedPackagePath = FindFirstNestedPackagePath(packagesRoot);
            if (!string.IsNullOrEmpty(nestedPackagePath))
            {
                return nestedPackagePath;
            }

            return string.Empty;
        }

        private static string FindFirstNestedPackagePath(string root)
        {
            if (!Directory.Exists(root))
            {
                return string.Empty;
            }

            string[] directories = Directory.GetDirectories(root, "Package", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                if (IsPackagePath(directories[i]))
                {
                    return directories[i];
                }
            }

            return string.Empty;
        }

        private static bool IsPackagePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }

            return File.Exists(Path.Combine(path, "manifest.json")) &&
                   File.Exists(Path.Combine(path, "Metadata", "project_metadata.json"));
        }

        private void EnsureWavPlayer()
        {
            if (wavPlayer != null)
            {
                return;
            }

            GameObject wavPlayerGO = new GameObject("WAVPlayer");
            wavPlayerGO.transform.SetParent(transform, false);
            wavPlayerGO.AddComponent<AudioSource>();
            wavPlayer = wavPlayerGO.AddComponent<WAVPlayer>();
        }

        private void RegisterUiCallbacks()
        {
            if (previousButton != null)
            {
                previousButton.onClick.AddListener(PreviousPart);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(NextPart);
            }

            if (playAudioButton != null)
            {
                playAudioButton.onClick.AddListener(PlayAudio);
            }

            if (partDropdown != null)
            {
                partDropdown.onValueChanged.AddListener(OnPartSelected);
            }

            if (descriptionDropdown != null)
            {
                descriptionDropdown.onValueChanged.AddListener(OnDescriptionSelected);
            }
        }

        private void LoadPackage()
        {
            packageManifest = PackageReader.ReadManifest(packageFolderPath);
            if (packageManifest == null)
            {
                Debug.LogWarning("[PackageViewer] manifest.json was not loaded. Falling back to metadata-only loading.");
            }

            projectMetadata = PackageReader.ReadProjectMetadata(packageFolderPath);
            if (projectMetadata == null)
            {
                Debug.LogError("[PackageViewer] Failed to load package metadata.");
                return;
            }

            if (projectNameText != null)
            {
                projectNameText.text = $"Project: {projectMetadata.projectName}";
            }

            InitializePartDropdown();
            LoadModel();

            if (projectMetadata.parts != null && projectMetadata.parts.Length > 0)
            {
                DisplayPart(0);
            }
        }

        private void LoadModel()
        {
            if (projectMetadata == null || modelContainer == null)
            {
                return;
            }

            if (TryLoadAssetBundleModel())
            {
                return;
            }

            string fbxPath = projectMetadata.fbxPath;
            Debug.Log($"[PackageViewer] Raw FBX path: {fbxPath}");

            int assetsIndex = fbxPath.IndexOf("Assets");
            if (assetsIndex >= 0)
            {
                fbxPath = fbxPath.Substring(assetsIndex).Replace("\\", "/");
            }

            Debug.Log($"[PackageViewer] Asset FBX path: {fbxPath}");

            #if UNITY_EDITOR
            GameObject fbxModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxModel == null)
            {
                Debug.LogWarning($"[PackageViewer] FBX model was not found: {fbxPath}");
                return;
            }

            if (currentModelInstance != null)
            {
                Destroy(currentModelInstance);
            }

            currentModelInstance = Instantiate(fbxModel, modelContainer);
            currentModelInstance.name = "ModelInstance";
            Debug.Log($"[PackageViewer] Model loaded: {fbxPath}");
            #else
            Debug.LogWarning("[PackageViewer] Runtime FBX loading is not implemented outside the editor.");
            #endif
        }

        private bool TryLoadAssetBundleModel()
        {
            if (packageManifest == null)
            {
                return false;
            }

            PackageBundle bundleEntry = SelectBundleForCurrentPlatform(packageManifest);
            string bundleRelativePath = bundleEntry != null ? bundleEntry.bundle : packageManifest.modelBundle;
            string assetName = bundleEntry != null ? bundleEntry.assetName : packageManifest.modelAssetName;

            if (string.IsNullOrEmpty(bundleRelativePath))
            {
                Debug.LogWarning("[PackageViewer] No model bundle was listed in manifest.");
                return false;
            }

            string bundlePath = Path.Combine(packageFolderPath, bundleRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(bundlePath))
            {
                Debug.LogWarning($"[PackageViewer] Model bundle was not found: {bundlePath}");
                return false;
            }

            UnloadModelBundle();
            loadedModelBundle = AssetBundle.LoadFromFile(bundlePath);
            if (loadedModelBundle == null)
            {
                Debug.LogWarning($"[PackageViewer] Failed to load model AssetBundle: {bundlePath}");
                return false;
            }

            GameObject modelAsset = LoadModelAssetFromBundle(loadedModelBundle, assetName);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[PackageViewer] Model asset was not found in bundle: {assetName}");
                UnloadModelBundle();
                return false;
            }

            if (currentModelInstance != null)
            {
                Destroy(currentModelInstance);
            }

            currentModelInstance = Instantiate(modelAsset, modelContainer);
            currentModelInstance.name = "ModelInstance";
            Debug.Log($"[PackageViewer] Model loaded from AssetBundle: {bundlePath}");
            return true;
        }

        private static PackageBundle SelectBundleForCurrentPlatform(PackageManifest manifest)
        {
            if (manifest.bundles == null || manifest.bundles.Length == 0)
            {
                return null;
            }

            string preferredPlatform = GetPreferredBundlePlatform();
            for (int i = 0; i < manifest.bundles.Length; i++)
            {
                PackageBundle bundle = manifest.bundles[i];
                if (bundle != null && string.Equals(bundle.platform, preferredPlatform, System.StringComparison.OrdinalIgnoreCase))
                {
                    return bundle;
                }
            }

            for (int i = 0; i < manifest.bundles.Length; i++)
            {
                PackageBundle bundle = manifest.bundles[i];
                if (bundle != null && !string.IsNullOrEmpty(bundle.bundle))
                {
                    return bundle;
                }
            }

            return null;
        }

        private static string GetPreferredBundlePlatform()
        {
#if UNITY_ANDROID
            return "Quest";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return "Windows";
#else
            return "Quest";
#endif
        }

        private static GameObject LoadModelAssetFromBundle(AssetBundle bundle, string assetName)
        {
            if (!string.IsNullOrEmpty(assetName))
            {
                GameObject explicitAsset = bundle.LoadAsset<GameObject>(assetName);
                if (explicitAsset != null)
                {
                    return explicitAsset;
                }
            }

            string[] assetNames = bundle.GetAllAssetNames();
            for (int i = 0; i < assetNames.Length; i++)
            {
                GameObject asset = bundle.LoadAsset<GameObject>(assetNames[i]);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private void UnloadModelBundle()
        {
            if (loadedModelBundle == null)
            {
                return;
            }

            loadedModelBundle.Unload(false);
            loadedModelBundle = null;
        }

        private void InitializePartDropdown()
        {
            if (partDropdown == null || projectMetadata.parts == null)
            {
                return;
            }

            var partNames = new List<string>();
            foreach (PartMetadataData part in projectMetadata.parts)
            {
                partNames.Add(part.partName);
            }

            partDropdown.ClearOptions();
            partDropdown.AddOptions(partNames);
            partDropdown.SetValueWithoutNotify(0);
        }

        private void DisplayPart(int partIndex)
        {
            if (projectMetadata?.parts == null || partIndex < 0 || partIndex >= projectMetadata.parts.Length)
            {
                return;
            }

            currentPartIndex = partIndex;
            PartMetadataData part = projectMetadata.parts[partIndex];
            Debug.Log($"[PackageViewer] Display part: {part.partName}");

            if (descriptionDropdown != null)
            {
                var descriptions = new List<string>(part.descriptions ?? new string[0]);
                descriptionDropdown.ClearOptions();
                descriptionDropdown.AddOptions(descriptions);
                descriptionDropdown.SetValueWithoutNotify(0);
            }

            StopHighlightBlinking();
            StopAudioSequence();
            DisplayDescription(0);
        }

        private HighlightMaterialSet CollectHighlightMaterials(PartMetadataData part)
        {
            var materialSet = new HighlightMaterialSet();
            var targetMaterials = new HashSet<Material>();

            if (currentModelInstance == null)
            {
                return materialSet;
            }

            ResetPartHighlight();

            string[] targetNames = GetHighlightTargetNames(part);
            if (targetNames.Length == 0)
            {
                Debug.LogWarning($"[PackageViewer] No bound object names were found for part '{part.partName}'.");
                return materialSet;
            }

            Transform[] allTransforms = currentModelInstance.GetComponentsInChildren<Transform>(true);
            var matchedTransforms = new HashSet<Transform>();

            foreach (string targetName in targetNames)
            {
                bool foundAny = false;

                foreach (Transform transformNode in allTransforms)
                {
                    if (!IsTransformMatch(transformNode, targetName))
                    {
                        continue;
                    }

                    matchedTransforms.Add(transformNode);
                    foundAny = true;
                }

                if (!foundAny)
                {
                    Debug.LogWarning($"[PackageViewer] Bound FBX object was not found: {targetName}");
                }
            }

            foreach (Transform matchedTransform in matchedTransforms)
            {
                foreach (Renderer renderer in matchedTransform.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.materials)
                    {
                        if (material == null)
                        {
                            continue;
                        }

                        targetMaterials.Add(material);
                    }
                }
            }

            foreach (Renderer renderer in currentModelInstance.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.materials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    CacheHighlightState(material);
                    RegisterModifiedMaterial(material);

                    if (targetMaterials.Contains(material))
                    {
                        materialSet.TargetMaterials.Add(material);
                    }
                    else
                    {
                        materialSet.OtherMaterials.Add(material);
                    }
                }
            }

            Debug.Log($"[PackageViewer] Found {materialSet.TargetMaterials.Count} target material(s) for part '{part.partName}'.");
            return materialSet;
        }

        private void StartHighlightBlink(PartMetadataData part)
        {
            StopHighlightBlinking();

            HighlightMaterialSet materialSet = CollectHighlightMaterials(part);
            if (materialSet.TargetMaterials.Count == 0)
            {
                return;
            }

            highlightCoroutine = StartCoroutine(BlinkHighlightRoutine(materialSet, part.partName));
        }

        private IEnumerator BlinkHighlightRoutine(HighlightMaterialSet materialSet, string partName)
        {
            float elapsed = 0f;

            while (elapsed < HighlightDurationSeconds)
            {
                foreach (Material material in materialSet.OtherMaterials)
                {
                    ApplyDim(material);
                }

                float pulse = 0.5f + (0.5f * Mathf.Sin(elapsed * HighlightPulseFrequency * Mathf.PI * 2f));
                float highlightStrength = Mathf.Lerp(0.35f, 1f, pulse);

                foreach (Material material in materialSet.TargetMaterials)
                {
                    ApplyHighlight(material, highlightStrength);
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            RestoreMaterials(materialSet.TargetMaterials);
            foreach (Material material in materialSet.OtherMaterials)
            {
                ApplyDim(material);
            }

            highlightCoroutine = null;

            Debug.Log($"[PackageViewer] Blink highlight finished for part '{partName}'.");
        }

        private string[] GetHighlightTargetNames(PartMetadataData part)
        {
            if (part.selectedObjectNames != null && part.selectedObjectNames.Length > 0)
            {
                return part.selectedObjectNames;
            }

            if (!string.IsNullOrEmpty(part.partName))
            {
                return new[] { part.partName };
            }

            return new string[0];
        }

        private bool IsTransformMatch(Transform transformNode, string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                return false;
            }

            if (targetName.Contains("/"))
            {
                string relativePath = GetRelativePath(transformNode);
                return relativePath == targetName;
            }

            return transformNode.name == targetName;
        }

        private string GetRelativePath(Transform transformNode)
        {
            var pathSegments = new List<string>();
            Transform current = transformNode;

            while (current != null && current != currentModelInstance.transform)
            {
                pathSegments.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", pathSegments);
        }

        private void ApplyHighlight(Material material, float strength)
        {
            if (material == null)
            {
                return;
            }

            RestoreMaterialState(material);

            if (material.HasProperty("_BaseColor"))
            {
                Color original = highlightStates[material].BaseColor;
                Color highlighted = Color.Lerp(original, HighlightColor, Mathf.Lerp(0.15f, 0.7f, strength));
                highlighted.a = 1f;
                material.SetColor("_BaseColor", highlighted);
            }
            else if (material.HasProperty("_Color"))
            {
                Color original = highlightStates[material].Color;
                Color highlighted = Color.Lerp(original, HighlightColor, Mathf.Lerp(0.15f, 0.7f, strength));
                highlighted.a = 1f;
                material.SetColor("_Color", highlighted);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", HighlightEmissionColor * Mathf.Lerp(0.2f, 1f, strength));
            }
        }

        private void CacheHighlightState(Material material)
        {
            if (highlightStates.ContainsKey(material))
            {
                return;
            }

            var state = new MaterialHighlightState
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

            highlightStates[material] = state;
        }

        private void RegisterModifiedMaterial(Material material)
        {
            if (!highlightedMaterials.Contains(material))
            {
                highlightedMaterials.Add(material);
            }
        }

        private void ApplyDim(Material material)
        {
            if (material == null)
            {
                return;
            }

            RestoreMaterialState(material);
            SetMaterialTransparent(material);

            if (material.HasProperty("_BaseColor"))
            {
                Color color = highlightStates[material].BaseColor;
                color.a = DimAlpha;
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                Color color = highlightStates[material].Color;
                color.a = DimAlpha;
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }
        }

        private void SetMaterialTransparent(Material material)
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

        private void RestoreMaterials(IEnumerable<Material> materials)
        {
            foreach (Material material in materials)
            {
                RestoreMaterialState(material);
            }
        }

        private void RestoreMaterialState(Material material)
        {
            if (material == null || !highlightStates.TryGetValue(material, out MaterialHighlightState state))
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

        private void ResetPartHighlight()
        {
            foreach (Material material in highlightedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                RestoreMaterialState(material);
            }

            highlightedMaterials.Clear();
            highlightStates.Clear();
        }

        private void StopHighlightBlinking()
        {
            if (highlightCoroutine != null)
            {
                StopCoroutine(highlightCoroutine);
                highlightCoroutine = null;
            }

            ResetPartHighlight();
        }

        private void StopAudioSequence()
        {
            if (audioSequenceCoroutine != null)
            {
                StopCoroutine(audioSequenceCoroutine);
                audioSequenceCoroutine = null;
            }

            if (wavPlayer != null)
            {
                wavPlayer.StopPlayback();
            }
        }

        private void DisplayDescription(int descIndex)
        {
            if (projectMetadata?.parts == null || currentPartIndex >= projectMetadata.parts.Length)
            {
                return;
            }

            PartMetadataData part = projectMetadata.parts[currentPartIndex];
            if (part.descriptions == null || descIndex < 0 || descIndex >= part.descriptions.Length)
            {
                return;
            }

            currentDescriptionIndex = descIndex;

            string description = part.descriptions[descIndex];
            if (descriptionText != null)
            {
                descriptionText.text = $"{part.partName}: {description}";
            }

            Debug.Log($"[PackageViewer] Description: {description}");
        }

        private void PlayAudio()
        {
            if (projectMetadata?.parts == null || currentPartIndex >= projectMetadata.parts.Length)
            {
                Debug.LogWarning("[PackageViewer] Invalid package metadata or part index.");
                return;
            }

            PartMetadataData part = projectMetadata.parts[currentPartIndex];
            if (part.audioFiles == null || currentDescriptionIndex < 0 || currentDescriptionIndex >= part.audioFiles.Length)
            {
                Debug.LogWarning("[PackageViewer] Audio file index is out of range.");
                return;
            }

            if (wavPlayer == null)
            {
                Debug.LogError("[PackageViewer] WAVPlayer is not assigned.");
                return;
            }

            StopAudioSequence();
            audioSequenceCoroutine = StartCoroutine(PlayAudioSequence(part, currentDescriptionIndex));
        }

        private IEnumerator PlayAudioSequence(PartMetadataData part, int startDescriptionIndex)
        {
            for (int i = startDescriptionIndex; i < part.audioFiles.Length; i++)
            {
                string audioFileName = new FileInfo(part.audioFiles[i]).Name;
                string audioPath = Path.Combine(packageFolderPath, "Audio", audioFileName);

                Debug.Log($"[PackageViewer] Audio file: {audioPath}");
                if (!File.Exists(audioPath))
                {
                    Debug.LogWarning($"[PackageViewer] Audio file was not found: {audioPath}");
                    continue;
                }

                currentDescriptionIndex = i;

                if (descriptionDropdown != null)
                {
                    descriptionDropdown.SetValueWithoutNotify(i);
                }

                DisplayDescription(i);
                if (i == startDescriptionIndex)
                {
                    StartHighlightBlink(part);
                }
                yield return wavPlayer.PlayWAVAndWait(audioPath);
            }

            audioSequenceCoroutine = null;
        }

        private void PreviousPart()
        {
            if (currentPartIndex <= 0)
            {
                return;
            }

            DisplayPart(currentPartIndex - 1);
            if (partDropdown != null)
            {
                partDropdown.SetValueWithoutNotify(currentPartIndex);
            }
        }

        private void NextPart()
        {
            if (projectMetadata?.parts == null || currentPartIndex >= projectMetadata.parts.Length - 1)
            {
                return;
            }

            DisplayPart(currentPartIndex + 1);
            if (partDropdown != null)
            {
                partDropdown.SetValueWithoutNotify(currentPartIndex);
            }
        }

        private void OnPartSelected(int index)
        {
            DisplayPart(index);
        }

        private void OnDescriptionSelected(int index)
        {
            DisplayDescription(index);
        }

        private void OnDestroy()
        {
            StopAudioSequence();
            StopHighlightBlinking();

            if (currentModelInstance != null)
            {
                Destroy(currentModelInstance);
            }

            UnloadModelBundle();
        }

        public void SetPackagePath(string path)
        {
            packageFolderPath = path;
        }
    }
}
