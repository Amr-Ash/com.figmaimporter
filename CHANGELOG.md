# Changelog

All notable changes to this package will be documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/).

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
