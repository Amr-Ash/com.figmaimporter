# Figma → uGUI Importer

A Unity Editor tool that imports Figma designs directly into your scene as a Canvas/uGUI hierarchy.

---

## Installation

### Option A — Git URL (recommended for teams)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL**
3. Paste:
   ```
   https://github.com/YOURUSERNAME/com.figmaimporter.git
   ```
4. Click **Add**

Unity will automatically install the two required dependencies:
- `com.unity.editorcoroutines`
- `com.unity.nuget.newtonsoft-json`

To install a specific version tag:
```
https://github.com/YOURUSERNAME/com.figmaimporter.git#v1.0.0
```

---

### Option B — Local package (for personal use / offline)

1. Clone or download this repo anywhere on your machine
2. Open **Window → Package Manager**
3. Click **+** → **Add package from disk**
4. Navigate to the folder and select `package.json`

---

### Option C — .unitypackage (for one-off sharing)

Export from Unity via **Assets → Export Package**, then share the `.unitypackage` file.
Recipients import it via **Assets → Import Package → Custom Package**.

---

## Setup

### Get a Figma Personal Access Token

1. Log into [figma.com](https://figma.com)
2. Click your profile icon → **Settings → Security**
3. Under **Personal Access Tokens** → **Generate new token**
4. Set a name, choose an expiration, and enable these scopes:
   - ✅ `file_content:read`
   - ✅ `current_user:read`
5. Copy the token immediately (shown only once)

---

## How to Use

1. Open **Tools → Figma Importer** in Unity
2. Paste your **Figma File URL**
   - Example: `https://www.figma.com/design/ABC123XYZ/MyApp`
3. Paste your **API Token**
4. Click **1. Fetch Pages**
5. Select a page from the dropdown
6. Click **2. Import Selected Page**
7. The Canvas hierarchy appears in your **Hierarchy** panel

> Use **Ctrl+Z** to undo the entire import at once.

---

## What gets imported

| Figma element | Unity uGUI |
|---|---|
| FRAME / COMPONENT / INSTANCE | RectTransform (+ Image if background color) |
| RECTANGLE / ELLIPSE / VECTOR | Image component |
| TEXT | Text component |
| GROUP / SECTION | Empty RectTransform |
| Solid fills | `Image.color` |
| Image fills | `Image.sprite` (PNG downloaded) |
| Opacity | `CanvasGroup.alpha` |
| Nesting | Full parent/child hierarchy |
| Positions & sizes | `RectTransform` with top-left anchor |

---

## Known Limitations

| Issue | Workaround |
|---|---|
| Corner radius | Tagged as `[radius=N]` in name — use a rounded sprite or Rounded Corners shader |
| Text rendering | Uses legacy `Text` — upgrade to TextMeshProUGUI manually after import |
| Gradients | Not supported — node will be transparent |
| Auto-layout | Positions are baked — Unity Layout Groups not generated |
| Figma components | Treated as regular frames — no Unity prefab linking |

---

## Updating the package (for maintainer)

1. Make your changes
2. Bump the version in `package.json`
3. Add an entry to `CHANGELOG.md`
4. Commit and push
5. Tag the release: `git tag v1.1.0 && git push --tags`

Team members update via **Package Manager → Update**.
