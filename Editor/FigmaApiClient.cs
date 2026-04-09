// FigmaApiClient.cs
// Place in: Assets/Editor/FigmaImporter/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEngine.Networking;

namespace FigmaImporter
{
    public static class FigmaApiClient
    {
        private const string BASE_URL = "https://api.figma.com/v1";

        // ─── URL Parsing ────────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts the file key from a Figma URL.
        /// Supports both /file/KEY and /design/KEY formats.
        /// </summary>
        public static string ExtractFileKey(string url)
        {
            var match = Regex.Match(url, @"figma\.com/(?:file|design)/([a-zA-Z0-9]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        // ─── API Calls ───────────────────────────────────────────────────────────────

        /// <summary>Fetch the full Figma file JSON. Retries up to 3 times on 429.</summary>
        public static IEnumerator FetchFile(string fileKey, string token,
            Action<JObject, string> callback)
        {
            string url     = $"{BASE_URL}/files/{fileKey}";
            int    retries = 3;
            float  waitSec = 5f;

            for (int attempt = 0; attempt < retries; attempt++)
            {
                using var req = UnityWebRequest.Get(url);
                req.SetRequestHeader("X-Figma-Token", token);
                yield return req.SendWebRequest();

                // 429 → wait and retry
                if (req.responseCode == 429)
                {
                    Debug.Log($"[FigmaImporter] Rate limited (429). Waiting {waitSec}s before retry {attempt + 1}/{retries}…");
                    yield return new EditorWaitForSeconds(waitSec);
                    waitSec *= 2f; // exponential back-off
                    continue;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    callback(null, $"HTTP {req.responseCode}: {req.error}");
                    yield break;
                }

                try   { callback(JObject.Parse(req.downloadHandler.text), null); }
                catch (Exception e) { callback(null, "JSON parse error: " + e.Message); }
                yield break; // success — exit
            }

            callback(null, "Rate limit (429) persists after retries. Wait a minute and try again.");
        }

        /// <summary>
        /// Ask Figma to render a list of node IDs as PNGs and return their URLs.
        /// Use this to download image assets (icons, photos, vectors).
        /// </summary>
        public static IEnumerator FetchRenderedImageUrls(string fileKey, string token,
            List<string> nodeIds, Action<Dictionary<string, string>, string> callback)
        {
            if (nodeIds == null || nodeIds.Count == 0)
            {
                callback(new Dictionary<string, string>(), null);
                yield break;
            }

            string ids  = string.Join(",", nodeIds);
            string url  = $"{BASE_URL}/images/{fileKey}?ids={Uri.EscapeDataString(ids)}&format=png&scale=2";

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("X-Figma-Token", token);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                callback(null, req.error);
                yield break;
            }

            try
            {
                var json   = JObject.Parse(req.downloadHandler.text);
                var result = new Dictionary<string, string>();
                if (json["images"] is JObject imgs)
                    foreach (var prop in imgs.Properties())
                        if (!string.IsNullOrEmpty(prop.Value.ToString()))
                            result[prop.Name] = prop.Value.ToString();
                callback(result, null);
            }
            catch (Exception e) { callback(null, "JSON parse error: " + e.Message); }
        }

        /// <summary>Downloads a texture from a URL (e.g. the PNG URLs returned above).</summary>
        public static IEnumerator DownloadTexture(string url,
            Action<Texture2D, string> callback)
        {
            using var req = UnityWebRequestTexture.GetTexture(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                callback(null, req.error);
            else
                callback(DownloadHandlerTexture.GetContent(req), null);
        }

        // ─── JSON → FigmaNode ────────────────────────────────────────────────────────

        public static FigmaNode ParseNode(JToken t)
        {
            if (t == null) return null;

            var node = new FigmaNode
            {
                id           = t["id"]?.ToString(),
                name         = t["name"]?.ToString() ?? "Node",
                type         = t["type"]?.ToString() ?? "FRAME",
                visible      = t["visible"]?.Value<bool>()  ?? true,
                opacity      = t["opacity"]?.Value<float>() ?? 1f,
                cornerRadius = t["cornerRadius"]?.Value<float>() ?? 0f,
                strokeWeight = t["strokeWeight"]?.Value<float>() ?? 0f,
                clipsContent = t["clipsContent"]?.Value<bool>() ?? false,
                layoutMode   = t["layoutMode"]?.ToString() ?? "NONE",
                paddingLeft  = t["paddingLeft"]?.Value<float>()  ?? 0f,
                paddingRight = t["paddingRight"]?.Value<float>() ?? 0f,
                paddingTop   = t["paddingTop"]?.Value<float>()   ?? 0f,
                paddingBottom= t["paddingBottom"]?.Value<float>()    ?? 0f,
                itemSpacing  = t["itemSpacing"]?.Value<float>()  ?? 0f,
                characters   = t["characters"]?.ToString()
            };

            // Bounding box
            var bb = t["absoluteBoundingBox"];
            node.absoluteBoundingBox = bb != null
                ? new FigmaBoundingBox {
                    x      = bb["x"]?.Value<float>()      ?? 0,
                    y      = bb["y"]?.Value<float>()       ?? 0,
                    width  = bb["width"]?.Value<float>()   ?? 100,
                    height = bb["height"]?.Value<float>()  ?? 100
                  }
                : new FigmaBoundingBox { width = 100, height = 100 };

            // Fills & strokes
            if (t["fills"]   is JArray fills)   foreach (var f in fills)   { var p = ParsePaint(f); if (p != null) node.fills.Add(p);   }
            if (t["strokes"] is JArray strokes)  foreach (var s in strokes) { var p = ParsePaint(s); if (p != null) node.strokes.Add(p); }

            // Text style
            if (t["style"] is JToken sty)
            {
                node.style = new FigmaTextStyle
                {
                    fontFamily           = sty["fontFamily"]?.ToString()       ?? "Arial",
                    fontSize             = sty["fontSize"]?.Value<float>()     ?? 14f,
                    fontWeight           = sty["fontWeight"]?.ToString()       ?? "400",
                    letterSpacing        = sty["letterSpacing"]?.Value<float>()   ?? 0f,
                    lineHeightPx         = sty["lineHeightPx"]?.Value<float>()    ?? 0f,
                    textAlignHorizontal  = sty["textAlignHorizontal"]?.ToString() ?? "LEFT",
                    textAlignVertical    = sty["textAlignVertical"]?.ToString()   ?? "TOP"
                };
            }

            // Children (recursive)
            if (t["children"] is JArray children)
                foreach (var child in children)
                {
                    var childNode = ParseNode(child);
                    if (childNode != null) node.children.Add(childNode);
                }

            return node;
        }

        private static FigmaPaint ParsePaint(JToken t)
        {
            if (t == null) return null;
            var paint = new FigmaPaint
            {
                type     = t["type"]?.ToString()            ?? "SOLID",
                opacity  = t["opacity"]?.Value<float>()     ?? 1f,
                visible  = t["visible"]?.Value<bool>()      ?? true,
                imageRef = t["imageRef"]?.ToString()
            };

            var c = t["color"];
            paint.color = c != null
                ? new FigmaColor {
                    r = c["r"]?.Value<float>() ?? 1f,
                    g = c["g"]?.Value<float>() ?? 1f,
                    b = c["b"]?.Value<float>() ?? 1f,
                    a = c["a"]?.Value<float>() ?? 1f
                  }
                : new FigmaColor { r = 1, g = 1, b = 1, a = 1 };

            return paint;
        }
    }
}
