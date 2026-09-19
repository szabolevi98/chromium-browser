# Chromium Browser 2.1.0

This release improves everyday window, tab and navigation behaviour, fixes private-window routing and adds live download controls.

## Changes

- Native title-bar hit testing for double-click maximise/restore and dragging a maximised window; a draggable area remains available with many tabs. The maximise button now exposes the Windows Snap target.
- A fixed new-tab button and automatic scrolling to keep the selected tab visible after resizing or closing tabs.
- Ctrl+Shift+T reopens closed tabs. Tab menus offer duplicate, mute, close other tabs and close tabs to the right.
- F11 full screen with a visible exit button; Alt+Left/Right, Ctrl+1–9, F6, Escape and cache-bypassing reload shortcuts.
- Address selection and focus fixes, plus bookmark/history suggestions. Private windows exclude normal history suggestions.
- Live downloads with pause, resume, cancel and retry controls. Private downloads use a separate in-memory list.
- Private launches retain their mode when handed to a running instance; external links still work after the first window closes.
- Session saving follows tab selection and window-state changes; bookmarks refresh across windows.
- Tab media-activity indicators, muting, tooltips and accessible names/actions for painted controls.

## Download and update

Download `ChromiumBrowser-2.1.0-win-x64.zip`, extract it and run `ChromiumBrowser.exe`. Windows x64, self-contained: no separate .NET installation is needed.

To keep an existing portable profile, close the browser, back up its `Data` folder, then extract the new files into the existing browser folder. The archive contains no user profile. Downloaded files remain on disk even when a private window closes.

`SHA256SUMS.txt` contains the archive's SHA-256 checksum.

## Validation

105 core checks and 146 UI/native Chromium integration checks passed. The build completed without warnings or errors.

Native tests cover title-bar hit-test and double-click messages, Chromium focus, private internal pages, live download rendering, WebAudio activity and full-screen transitions. Physical drag gestures, the visible Windows 11 Snap flyout, mixed-DPI monitor movement and screen-reader use still require manual validation. Download progress/control tests use synthetic records and callbacks, rather than a full network download.

Built with CefSharp 152.0.60 and .NET 9.
