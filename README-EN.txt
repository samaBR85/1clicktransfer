========================================
  1-CLICK TRANSFER  (Windows 10/11)
========================================

A simple app: one big button that transfers a pre-chosen file to a
pre-chosen destination (local/network folder OR an FTP server).
Bilingual interface (Portuguese / English). Nothing to install.

--------------------------------
HOW TO USE (first time)
--------------------------------
1. Double-click  "1clickTransfer.exe"  to open the app.
2. Click  "Settings":
   - Choose the FILE to transfer.
   - Choose the destination:
       * "Local / network folder"  -> pick a folder, or
       * "FTP server"              -> fill host, port, folder, user, password.
         (Use "Test connection" and "Browse" to check/navigate.)
   - Optionally set the keyboard shortcut, Theme and Language.
   - Click "Save".
3. Done! From now on just open the app and click  TRANSFER.

--------------------------------
PROFILES (multiple source/destination presets)
--------------------------------
- The "Profile" selector at the top of the home lets you switch between
  saved presets (source + destination together) with one click.
- Manage them in "Settings" -> "Saved profiles": Save current as...,
  Rename, Delete. Selecting "(none)" clears the home fields.

--------------------------------
ACTION (when the file already exists at the destination)
--------------------------------
- Replace ...................... always sends, overwriting.
- Replace if newer ............. only sends if the source is newer.
- Don't replace ............... does not send if it already exists.

--------------------------------
NOTES
--------------------------------
- The FTP password is stored ENCRYPTED (DPAPI - only your Windows user
  can read it). It is machine/user specific; reconfigure it on another PC.
- settings.json is created next to the .exe (portable).
- The .exe is not code-signed, so Windows SmartScreen may show a warning
  on first run: click "More info" -> "Run anyway". It is safe (source code
  is public on GitHub).

--------------------------------
LINKS
--------------------------------
- Repository: https://github.com/samaBR85/1clicktransfer
- Website:    https://samabr85.github.io/1clicktransfer/
