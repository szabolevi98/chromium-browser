# Chromium Browser 2.1.2

Fixes menus staying open when the page is clicked: the main menu, the tab menu and the bookmark menus now close on a click into the web page, not only on a click in the window's own title bar, tabs or toolbar.

The page runs on Chromium's own thread, which a menu does not watch. While a menu is open, a mouse hook now closes it when the page is pressed. The click still reaches the page.

## Download and update

Extract `ChromiumBrowser-2.1.2-win-x64.zip` and run `ChromiumBrowser.exe`. Windows x64; no separate .NET installation is needed. To update an existing portable copy, close it, back up its `Data` folder, then extract the new files into its folder. No user data is included in the archive.

`SHA256SUMS.txt` contains the archive's SHA-256 checksum.

## Validation

154 UI/native Chromium integration checks passed; the Release build completed with no warnings or errors.

Closing a menu by clicking into the page needs real mouse input, so it was checked by hand on the desktop rather than by the automated checks.
