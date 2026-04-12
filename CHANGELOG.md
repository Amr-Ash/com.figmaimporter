# Changelog

All notable changes to this package will be documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/).

## [1.1.0] - 2026-04-11

### Fixed
- Added `com.unity.textmeshpro` to `package.json` dependencies — fixes compile error when importing the package into a fresh project
- `spriteMap` lookup now uses node ID (was incorrectly using `imageRef`) — images now render correctly
- File key regex now accepts hyphens and underscores in Figma file keys
- Downloaded sprites are saved as persistent PNG assets under `Assets/FigmaImporter/Images/` instead of in-memory only

### Added
- Rotation support: Figma node `rotation` field parsed and applied to `localEulerAngles`
- Auto-layout: `HORIZONTAL`/`VERTICAL` layout modes now generate `HorizontalLayoutGroup`/`VerticalLayoutGroup` with correct padding and spacing
- `clipsContent` now adds `RectMask2D` to clip overflowing children

## [1.0.0] - 2026-04-09

### Added
- Fetch all pages from a Figma file via the REST API
- Import any page as a Canvas + uGUI hierarchy into the active scene
- Support for FRAME, GROUP, COMPONENT, INSTANCE, RECTANGLE, ELLIPSE, VECTOR, TEXT nodes
- Solid color fills → Image.color
- Image fills → downloaded PNG sprites
- Text nodes → legacy Text component with font size, alignment, color
- Opacity → CanvasGroup.alpha
- Full parent/child hierarchy preservation
- Automatic retry with exponential back-off on 429 rate limit errors
- Undo support (Ctrl+Z removes the entire import)
- Option to import all frames or just the first frame
