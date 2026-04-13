// FigmaUGUIBuilder.cs
// Place in: Assets/Editor/FigmaImporter/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaImporter
{
    /// <summary>
    /// Converts a parsed FigmaNode tree into a uGUI (Canvas-based) GameObject hierarchy.
    ///
    /// Coordinate conversion:
    ///   Figma  →  origin top-left,  Y increases downward,  absolute positions
    ///   Unity  →  origin depends on anchor, Y increases upward, positions relative to parent
    ///
    /// Strategy used here: anchor = parent top-left (0,1), pivot = element center (0.5,0.5).
    /// anchoredPosition = (relX + w/2, -(relY + h/2)) — points from parent top-left to element center.
    /// </summary>
    public static class FigmaUGUIBuilder
    {
        // ─── Public entry point ──────────────────────────────────────────────────────

        /// <summary>
        /// Build a single top-level Figma FRAME as a Canvas + uGUI hierarchy.
        /// </summary>
        public static GameObject BuildFromNode(FigmaNode root,
            Dictionary<string, Sprite> spriteMap = null)
        {
            // Root Canvas — sized to match the Figma node
            var canvasGO = new GameObject(root.name);
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                root.absoluteBoundingBox?.width  ?? 1920,
                root.absoluteBoundingBox?.height ?? 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(
                root.absoluteBoundingBox?.width  ?? 1920,
                root.absoluteBoundingBox?.height ?? 1080);

            // If the root itself has a fill, apply it
            var solidFill = root.fills.Find(f => f.visible && f.type == "SOLID");
            if (solidFill != null)
            {
                var bg   = canvasGO.AddComponent<Image>();
                bg.color = ApplyFillOpacity(solidFill);
            }

            // Build all children
            if (root.children != null)
                foreach (var child in root.children)
                    if (child.visible)
                        BuildNode(child, canvasGO.transform, root.absoluteBoundingBox, spriteMap);

            return canvasGO;
        }

        // ─── Recursive builder ───────────────────────────────────────────────────────

        private static GameObject BuildNode(FigmaNode node, Transform parent,
            FigmaBoundingBox parentBBox, Dictionary<string, Sprite> spriteMap)
        {
            if (!node.visible) return null;

            GameObject go;

            if (node.type == "TEXT")
            {
                // Text nodes are always rendered by TextMeshPro for editability
                go = BuildTextNode(node);
            }
            else if (spriteMap != null && spriteMap.ContainsKey(node.id))
            {
                // A pre-rendered PNG from Figma is available — show it as a flat Image.
                // Children are already baked into this PNG so we skip recursion entirely,
                // giving pixel-perfect results for buttons, icons, frames, and shapes.
                go = BuildFlatPngNode(node, spriteMap[node.id]);
            }
            else
            {
                // Fallback: no PNG available, reconstruct from fill data
                switch (node.type)
                {
                    case "RECTANGLE":
                    case "ELLIPSE":
                    case "VECTOR":
                    case "BOOLEAN_OPERATION":
                    case "STAR":
                    case "POLYGON":
                        go = BuildImageNode(node, spriteMap);
                        break;
                    default:
                        go = BuildContainerNode(node, spriteMap);
                        break;
                }
            }

            if (go == null) return null;

            // Parent & layout
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            ApplyRectTransform(rt, node, parentBBox);

            // Opacity via CanvasGroup (only add when needed to avoid overhead)
            if (node.opacity < 0.999f)
            {
                var cg   = go.AddComponent<CanvasGroup>();
                cg.alpha = node.opacity;
            }

            return go;
        }

        // ─── Node type builders ──────────────────────────────────────────────────────

        private static GameObject BuildContainerNode(FigmaNode node,
            Dictionary<string, Sprite> spriteMap)
        {
            var go = new GameObject(SanitizeName(node));

            // Add Image if there is a background color
            var solidFill = GetFirstSolidFill(node.fills);
            if (solidFill != null)
            {
                var img  = go.AddComponent<Image>();
                img.color = ApplyFillOpacity(solidFill);

                // Unity's built-in Image doesn't support corner radius natively.
                // Tag the name so the developer knows to swap it with a sprite or
                // the Rounded Corners package if needed.
                if (node.cornerRadius > 0)
                    go.name += $" [radius={node.cornerRadius:F0}]";
            }
            else
            {
                // Container with no background still needs a RectTransform
                go.AddComponent<RectTransform>();
            }

            // Clip children to this container's bounds
            if (node.clipsContent)
                go.AddComponent<RectMask2D>();

            // NOTE: We intentionally do NOT add HorizontalLayoutGroup/VerticalLayoutGroup here.
            // Figma's absoluteBoundingBox already encodes the final pixel positions of every node,
            // including the result of any auto-layout. Adding a Unity LayoutGroup would override
            // those positions with its own calculation and cause misalignment.

            // Recurse into children
            foreach (var child in node.children)
                if (child.visible)
                    BuildNode(child, go.transform, node.absoluteBoundingBox, spriteMap);

            return go;
        }

        /// <summary>
        /// Creates a single Image component showing a pre-rendered Figma PNG.
        /// No children are added — everything is baked into the sprite.
        /// </summary>
        private static GameObject BuildFlatPngNode(FigmaNode node, Sprite sprite)
        {
            var go  = new GameObject(SanitizeName(node));
            var img = go.AddComponent<Image>();
            img.sprite         = sprite;
            img.type           = Image.Type.Simple;
            img.preserveAspect = false;
            img.color          = Color.white;
            return go;
        }

        private static GameObject BuildImageNode(FigmaNode node,
            Dictionary<string, Sprite> spriteMap)
        {
            var go  = new GameObject(SanitizeName(node));
            var img = go.AddComponent<Image>();

            // Image fill takes priority.
            // spriteMap is keyed by node ID (from the Figma render API), not by imageRef.
            var imageFill = GetFirstImageFill(node.fills);
            if (imageFill != null
                && spriteMap != null
                && spriteMap.TryGetValue(node.id, out var sprite))
            {
                img.sprite          = sprite;
                img.type            = Image.Type.Simple;
                img.preserveAspect  = true;
                img.color           = Color.white;
            }
            else
            {
                // Fall back to solid colour
                var solidFill = GetFirstSolidFill(node.fills);
                img.color = solidFill != null ? ApplyFillOpacity(solidFill) : new Color(1, 1, 1, 0);
            }

            if (node.cornerRadius > 0)
                go.name += $" [radius={node.cornerRadius:F0}]";

            return go;
        }

        private static GameObject BuildTextNode(FigmaNode node)
        {
            var go   = new GameObject(SanitizeName(node));
            var text = go.AddComponent<TMPro.TextMeshProUGUI>();

            text.text         = node.characters ?? "";
            text.richText     = false;
            text.enableWordWrapping = true;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;

            if (node.style != null)
            {
                text.fontSize  = node.style.fontSize;
                text.alignment = ToTMPAlignment(node.style.textAlignHorizontal,
                                                node.style.textAlignVertical);
                text.fontStyle = ToTMPFontStyle(node.style.fontWeight);

                // Store the original Figma font name in the GameObject name so
                // you know which TMP font asset to assign manually.
                if (!string.IsNullOrEmpty(node.style.fontFamily))
                    go.name += $" [{node.style.fontFamily}]";
            }

            // Text colour
            var solidFill = GetFirstSolidFill(node.fills);
            text.color = solidFill != null ? ApplyFillOpacity(solidFill) : Color.black;

            return go;
        }

        // ─── RectTransform positioning ───────────────────────────────────────────────

        private static void ApplyRectTransform(RectTransform rt,
            FigmaNode node, FigmaBoundingBox parentBBox)
        {
            var bbox = node.absoluteBoundingBox;
            rt.sizeDelta = new Vector2(bbox.width, bbox.height);

            // Anchor fixed at parent's top-left corner.
            // Pivot at element center — matches Figma's transform origin so that
            // rotation and scale operate around the correct point.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            // Offset from parent top-left to the element's top-left (Figma absolute coords)
            float relX = bbox.x - parentBBox.x;
            float relY = bbox.y - parentBBox.y;

            // anchoredPosition points to the PIVOT (center), so shift by half the size.
            // Unity Y is upward, Figma Y is downward → negate relY.
            rt.anchoredPosition = new Vector2(
                relX + bbox.width  * 0.5f,
              -(relY + bbox.height * 0.5f));

            // Figma rotation is clockwise degrees; Unity localEulerAngles Z is counter-clockwise
            if (node.rotation != 0f)
                rt.localEulerAngles = new Vector3(0f, 0f, -node.rotation);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────────

        private static FigmaPaint GetFirstSolidFill(List<FigmaPaint> fills)
        {
            foreach (var f in fills)
                if (f.visible && f.type == "SOLID") return f;
            return null;
        }

        private static FigmaPaint GetFirstImageFill(List<FigmaPaint> fills)
        {
            foreach (var f in fills)
                if (f.visible && f.type == "IMAGE" && !string.IsNullOrEmpty(f.imageRef)) return f;
            return null;
        }

        private static Color ApplyFillOpacity(FigmaPaint fill)
        {
            var c = fill.color.ToUnityColor();
            c.a  *= fill.opacity;
            return c;
        }

        private static string SanitizeName(FigmaNode node) =>
            string.IsNullOrWhiteSpace(node.name) ? node.type : node.name;

        private static TMPro.TextAlignmentOptions ToTMPAlignment(string h, string v) => (h, v) switch
        {
            ("LEFT",      "TOP")    => TMPro.TextAlignmentOptions.TopLeft,
            ("LEFT",      "CENTER") => TMPro.TextAlignmentOptions.Left,
            ("LEFT",      "BOTTOM") => TMPro.TextAlignmentOptions.BottomLeft,
            ("CENTER",    "TOP")    => TMPro.TextAlignmentOptions.Top,
            ("CENTER",    "CENTER") => TMPro.TextAlignmentOptions.Center,
            ("CENTER",    "BOTTOM") => TMPro.TextAlignmentOptions.Bottom,
            ("RIGHT",     "TOP")    => TMPro.TextAlignmentOptions.TopRight,
            ("RIGHT",     "CENTER") => TMPro.TextAlignmentOptions.Right,
            ("RIGHT",     "BOTTOM") => TMPro.TextAlignmentOptions.BottomRight,
            ("JUSTIFIED", "TOP")    => TMPro.TextAlignmentOptions.TopJustified,
            ("JUSTIFIED", "CENTER") => TMPro.TextAlignmentOptions.Justified,
            ("JUSTIFIED", "BOTTOM") => TMPro.TextAlignmentOptions.BottomJustified,
            _                       => TMPro.TextAlignmentOptions.TopLeft
        };

        private static TMPro.FontStyles ToTMPFontStyle(string weight)
        {
            if (string.IsNullOrEmpty(weight)) return TMPro.FontStyles.Normal;
            var lower = weight.ToLowerInvariant();
            bool bold   = (int.TryParse(weight, out int w) && w >= 700) || lower.Contains("bold");
            bool italic = lower.Contains("italic");
            if (bold && italic) return TMPro.FontStyles.Bold | TMPro.FontStyles.Italic;
            if (bold)   return TMPro.FontStyles.Bold;
            if (italic) return TMPro.FontStyles.Italic;
            return TMPro.FontStyles.Normal;
        }
    }
}
