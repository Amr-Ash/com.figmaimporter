// FigmaImporterWindow.cs
// Place in: Assets/Editor/FigmaImporter/
//
// DEPENDENCIES (add via Package Manager before using):
//   • com.unity.editorcoroutines          (Editor Coroutines)
//   • com.unity.nuget.newtonsoft-json     (Newtonsoft Json.NET)
//
// HOW TO USE:
//   1. Open via Unity menu → Tools → Figma Importer
//   2. Paste your Figma file URL  (e.g. https://www.figma.com/design/ABC123/MyApp)
//   3. Paste your Personal Access Token (Figma → Settings → Security → Personal Access Tokens)
//   4. Click "Fetch Pages" to load the page list
//   5. Select a page and click "Import Selected Page"
//   6. The Canvas + uGUI hierarchy will appear in your Scene hierarchy

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace FigmaImporter
{
    public class FigmaImporterWindow : EditorWindow
    {
        // ─── State ───────────────────────────────────────────────────────────────────
        private string figmaFileUrl     = "";
        private string apiToken         = "";
        private bool   downloadImages   = true;
        private bool   importAllFrames  = true;

        private string status           = "";
        private bool   isWorking        = false;

        private string            fileKey  = "";
        private List<string>      pageNames = new List<string>();
        private List<FigmaNode>   pages     = new List<FigmaNode>();
        private int               selectedPage = 0;

        private Vector2 scrollPos;

        // Track running coroutine so we can stop it before starting a new one
        private EditorCoroutine activeCoroutine = null;

        // ─── Menu item ───────────────────────────────────────────────────────────────
        [MenuItem("Tools/Figma Importer")]
        public static void ShowWindow()
        {
            var w = GetWindow<FigmaImporterWindow>("Figma Importer");
            w.minSize = new Vector2(420, 360);
        }

        // ─── GUI ─────────────────────────────────────────────────────────────────────
        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            DrawCredentials();
            DrawOptions();
            DrawFetchButton();

            if (pageNames.Count > 0)
                DrawPageSelector();

            DrawStatus();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Space(8);
            GUILayout.Label("🎨  Figma → uGUI Importer", EditorStyles.boldLabel);
            GUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Imports a Figma design page as a Canvas/uGUI hierarchy directly into your scene.",
                MessageType.None);
            GUILayout.Space(10);
        }

        private void DrawCredentials()
        {
            GUILayout.Label("Credentials", EditorStyles.boldLabel);
            figmaFileUrl = EditorGUILayout.TextField(
                new GUIContent("File URL", "Paste the full Figma file or design URL"),
                figmaFileUrl);
            apiToken = EditorGUILayout.PasswordField(
                new GUIContent("API Token", "Figma → Settings → Security → Personal Access Tokens"),
                apiToken);
            GUILayout.Space(8);
        }

        private void DrawOptions()
        {
            GUILayout.Label("Options", EditorStyles.boldLabel);
            downloadImages = EditorGUILayout.Toggle(
                new GUIContent("Download Images",
                    "Fetch PNG renders for nodes that have image fills"),
                downloadImages);
            importAllFrames = EditorGUILayout.Toggle(
                new GUIContent("Import All Frames",
                    "Import every top-level frame on the page. Uncheck to import only the first frame."),
                importAllFrames);
            GUILayout.Space(10);
        }

        private void DrawFetchButton()
        {
            EditorGUI.BeginDisabledGroup(isWorking);
            if (GUILayout.Button("1.  Fetch Pages", GUILayout.Height(32)))
                OnFetchPages();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawPageSelector()
        {
            GUILayout.Space(10);
            GUILayout.Label("Page", EditorStyles.boldLabel);
            selectedPage = EditorGUILayout.Popup("Select Page", selectedPage, pageNames.ToArray());
            GUILayout.Space(6);

            EditorGUI.BeginDisabledGroup(isWorking);
            if (GUILayout.Button("2.  Import Selected Page", GUILayout.Height(38)))
                OnImportPage();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawStatus()
        {
            if (string.IsNullOrEmpty(status)) return;
            GUILayout.Space(10);
            var msgType = status.StartsWith("✅") ? MessageType.None
                        : status.StartsWith("❌") ? MessageType.Error
                        : MessageType.Info;
            EditorGUILayout.HelpBox(status, msgType);
        }

        // ─── Actions ─────────────────────────────────────────────────────────────────

        private void OnFetchPages()
        {
            fileKey = FigmaApiClient.ExtractFileKey(figmaFileUrl);

            if (string.IsNullOrEmpty(fileKey))
            {
                SetStatus("❌ Invalid Figma URL. Example: https://www.figma.com/design/ABC123/MyFile");
                return;
            }
            if (string.IsNullOrEmpty(apiToken))
            {
                SetStatus("❌ Please enter your Figma Personal Access Token.");
                return;
            }

            // Stop any previous coroutine that's still running
            if (activeCoroutine != null)
                EditorCoroutineUtility.StopCoroutine(activeCoroutine);

            pageNames.Clear();
            pages.Clear();
            SetWorking(true, "⏳ Connecting to Figma…");
            activeCoroutine = EditorCoroutineUtility.StartCoroutine(FetchPagesRoutine(), this);
        }

        private void OnImportPage()
        {
            if (pages.Count == 0 || selectedPage >= pages.Count) return;

            // Stop any previous coroutine that's still running
            if (activeCoroutine != null)
                EditorCoroutineUtility.StopCoroutine(activeCoroutine);

            SetWorking(true, "⏳ Starting import…");
            activeCoroutine = EditorCoroutineUtility.StartCoroutine(ImportPageRoutine(pages[selectedPage]), this);
        }

        // ─── Coroutines ──────────────────────────────────────────────────────────────

        private IEnumerator FetchPagesRoutine()
        {
            JObject fileJson = null;
            string  error    = null;

            yield return FigmaApiClient.FetchFile(fileKey, apiToken, (j, e) =>
            {
                fileJson = j;
                error    = e;
            });

            if (error != null)
            {
                SetWorking(false, "❌ " + error);
                yield break;
            }

            var document = fileJson?["document"];
            if (document?["children"] is not JArray children || children.Count == 0)
            {
                SetWorking(false, "❌ No pages found in this file.");
                yield break;
            }

            foreach (var page in children)
            {
                var node = FigmaApiClient.ParseNode(page);
                if (node != null)
                {
                    pages.Add(node);
                    pageNames.Add(node.name);
                }
            }

            SetWorking(false, $"✅ Found {pages.Count} page(s). Select one and click Import.");
        }

        private IEnumerator ImportPageRoutine(FigmaNode page)
        {
            // ── Collect top-level nodes ───────────────────────────────────────────
            // Accept any visible node type — designs can use FRAME, SECTION,
            // COMPONENT, GROUP, or bare shapes placed directly on the canvas.
            var frames = new List<FigmaNode>();
            foreach (var child in page.children)
                if (child.visible) frames.Add(child);

            if (frames.Count == 0)
            {
                SetWorking(false, "❌ This page appears to be empty. Make sure your Figma page has content.");
                yield break;
            }

            // Log what we found so you can see it in the Console
            Debug.Log($"[FigmaImporter] Found {frames.Count} top-level node(s) on page \"{page.name}\":");
            foreach (var f in frames)
                Debug.Log($"  → [{f.type}] {f.name}  ({f.absoluteBoundingBox?.width}×{f.absoluteBoundingBox?.height})");

            var targetFrames = importAllFrames ? frames : new List<FigmaNode> { frames[0] };

            // ── Optionally download images ────────────────────────────────────────
            var spriteMap = new Dictionary<string, Sprite>();

            if (downloadImages)
            {
                var imageNodeIds = new List<string>();
                foreach (var frame in targetFrames)
                    CollectImageNodeIds(frame, imageNodeIds);

                if (imageNodeIds.Count > 0)
                {
                    SetStatus($"⏳ Requesting render URLs for {imageNodeIds.Count} image node(s)…");
                    Repaint();

                    Dictionary<string, string> imageUrls = null;
                    string imgError = null;

                    yield return FigmaApiClient.FetchRenderedImageUrls(
                        fileKey, apiToken, imageNodeIds,
                        (urls, err) => { imageUrls = urls; imgError = err; });

                    if (imgError != null)
                    {
                        Debug.LogWarning("[FigmaImporter] Image fetch error: " + imgError);
                    }
                    else if (imageUrls != null && imageUrls.Count > 0)
                    {
                        int done = 0;
                        foreach (var kvp in imageUrls)
                        {
                            Texture2D tex  = null;
                            string    dlErr = null;
                            yield return FigmaApiClient.DownloadTexture(kvp.Value,
                                (t, e) => { tex = t; dlErr = e; });

                            if (tex != null)
                            {
                                var sprite = Sprite.Create(
                                    tex,
                                    new Rect(0, 0, tex.width, tex.height),
                                    new Vector2(0.5f, 0.5f));
                                sprite.name = kvp.Key;
                                spriteMap[kvp.Key] = sprite;
                            }
                            else if (dlErr != null)
                            {
                                Debug.LogWarning($"[FigmaImporter] Couldn't download image {kvp.Key}: {dlErr}");
                            }

                            done++;
                            SetStatus($"⏳ Downloaded {done}/{imageUrls.Count} image(s)…");
                            Repaint();
                        }
                    }
                }
            }

            // ── Build GameObjects ─────────────────────────────────────────────────
            SetStatus("⏳ Building GameObject hierarchy…");
            Repaint();

            int built = 0;
            foreach (var frame in targetFrames)
            {
                var go = FigmaUGUIBuilder.BuildFromNode(frame, spriteMap);

                // Register with Undo so the developer can Ctrl+Z the import
                Undo.RegisterCreatedObjectUndo(go, "Import Figma Frame: " + frame.name);

                // Make it visible in Scene view
                UnityEditor.Selection.activeGameObject = go;
                built++;
            }

            SetWorking(false,
                $"✅ Imported {built} frame(s) from page \"{page.name}\". " +
                $"Check your Hierarchy! (Ctrl+Z to undo)");
        }

        // ─── Utility ─────────────────────────────────────────────────────────────────

        private void CollectImageNodeIds(FigmaNode node, List<string> ids)
        {
            bool hasImageFill = node.fills.Exists(f => f.type == "IMAGE" && f.visible);
            if (hasImageFill && !ids.Contains(node.id))
                ids.Add(node.id);
            foreach (var child in node.children)
                CollectImageNodeIds(child, ids);
        }

        private void SetStatus(string msg)
        {
            status = msg;
            Repaint();
        }

        private void SetWorking(bool working, string msg)
        {
            isWorking = working;
            status    = msg;
            Repaint();
        }
    }
}
