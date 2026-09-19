# Chromium Browser 2.1.1

Fixes the maximise/restore button introduced in 2.1.0: one click now immediately changes the window state, without displaying an extra native button that requires a second click.

The button retains native Windows Snap hover integration. Releasing the pointer outside the button or cancelling the click leaves the window state unchanged.

## Download and update

Extract `ChromiumBrowser-2.1.1-win-x64.zip` and run `ChromiumBrowser.exe`. Windows x64; no separate .NET installation is needed. To update an existing portable copy, close it, back up its `Data` folder, then extract the new files into its folder. No user data is included in the archive.

`SHA256SUMS.txt` contains the archive's SHA-256 checksum.

## Validation

154 UI/native Chromium integration checks passed; the Release build completed with no warnings or errors.

Native regression checks cover a single maximise/restore click, hover without resizing, release outside the button, cancelled tracking and lost mouse capture. The visible Windows 11 Snap flyout still requires a manual desktop check.
