// FigmaModels.cs
// Place in: Assets/Editor/FigmaImporter/
using System.Collections.Generic;
using UnityEngine;

namespace FigmaImporter
{
    public class FigmaColor
    {
        public float r, g, b, a = 1f;
        public Color ToUnityColor() => new Color(r, g, b, a);
    }

    public class FigmaBoundingBox
    {
        public float x, y, width, height;
    }

    public class FigmaPaint
    {
        public string type;       // SOLID, IMAGE, GRADIENT_LINEAR, etc.
        public FigmaColor color;
        public float opacity = 1f;
        public string imageRef;   // filled for IMAGE type
        public bool visible = true;
    }

    public class FigmaTextStyle
    {
        public string fontFamily = "Arial";
        public float fontSize = 14f;
        public string fontWeight = "400";
        public float letterSpacing = 0f;
        public float lineHeightPx = 0f;
        public string textAlignHorizontal = "LEFT";  // LEFT | CENTER | RIGHT | JUSTIFIED
        public string textAlignVertical   = "TOP";   // TOP  | CENTER | BOTTOM
    }

    public class FigmaNode
    {
        public string id;
        public string name;
        public string type;                             // FRAME, GROUP, RECTANGLE, TEXT, etc.
        public bool visible = true;
        public float opacity = 1f;
        public float cornerRadius = 0f;
        public float strokeWeight = 0f;
        public bool clipsContent = false;

        // Auto-layout
        public string layoutMode = "NONE";             // NONE | HORIZONTAL | VERTICAL
        public float paddingLeft, paddingRight, paddingTop, paddingBottom;
        public float itemSpacing;

        public FigmaBoundingBox absoluteBoundingBox;
        public List<FigmaPaint> fills   = new List<FigmaPaint>();
        public List<FigmaPaint> strokes = new List<FigmaPaint>();

        // TEXT nodes only
        public string characters;
        public FigmaTextStyle style;

        public List<FigmaNode> children = new List<FigmaNode>();
    }
}
