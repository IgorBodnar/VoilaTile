# Installation

Installing **VoilaTile** takes less than a minute. You can get the latest version from the official GitHub Releases page.

---

## 🪄 Step 1 — Download the Installer

1. Visit the [VoilaTile Releases](https://github.com/IgorBodnar/VoilaTile/releases) page.  
2. Under the latest version, download **`VoilaTile.Installer.exe`**.  
3. (Optional) Verify its integrity using the included `.sha256.txt` file:

   ```powershell
   Get-FileHash VoilaTile.Installer.exe -Algorithm SHA256
   ```

   Compare the resulting hash with the contents of  
   `VoilaTile.Installer.exe.sha256.txt`.

> 💡 Tip: You can view what changed in each version in the [Release Notes](../release-notes/index.md).

---

## 💽 Step 2 — Run the Installer

1. Double-click the downloaded installer.  
2. Follow the on-screen instructions.

When the installation finishes, the **Exit** page offers two optional checkboxes:

| Option | Description |
|--------|--------------|
| **Launch Snapper on Windows startup** | Adds VoilaTile’s Snapper component to Windows startup so it runs automatically after login. |
| **Open Settings after installation** | Opens the VoilaTile **Settings** app immediately after setup, letting you configure layouts, hints, and appearance. |

You can safely enable both — these are the recommended defaults for new users.

---

## ⚙️ Step 3 — Verify the Installation

After installation completes:

- Press **++Alt+Space++** to open the **Snap Overlay**.  
- Window hints should appear over your active screen.  
- If nothing happens, open the **Settings** app and confirm that *Snapper* is running in the background.

If Snapper doesn’t start automatically, enable it in  
**Settings → General → Launch on startup**.

---

## 🔄 Step 4 — Updating VoilaTile

Updating is as easy as reinstalling:

1. Download the latest version from the [Releases page](https://github.com/IgorBodnar/VoilaTile/releases).  
2. Run the installer — it will automatically upgrade your current installation.

No need to uninstall first; your layouts, hints, and preferences remain intact.

> 🧠 Tip: Re-running the same version of the installer performs a **Repair** — useful if a file or registry entry ever becomes corrupted.

---

## 🧹 Step 5 — Uninstallation

If you ever wish to remove VoilaTile:

1. Open **Add or Remove Programs** in Windows.  
2. Find **VoilaTile**, select **Uninstall**, and follow the wizard.  
   — or —  
   Run the installer again and choose **Remove** when prompted.

Uninstalling automatically removes any startup entries created by the installer.

---

## 🧰 Advanced — Verifying the Installer Manually

Each release includes a `.sha256.txt` file containing the installer’s checksum.  
You can manually verify it as follows:

```powershell
# From the folder containing both files
Get-FileHash VoilaTile.Installer.exe -Algorithm SHA256 |
    ForEach-Object { $_.Hash -eq (Get-Content VoilaTile.Installer.exe.sha256.txt).Trim() }
```

If the result prints `True`, the file is valid and unmodified.

---

## 🚀 Next Steps

🎯 Continue to [Setup →](setup.md) to configure layouts, hints, and your first snap.

---

*Having trouble installing?*  
Check the [FAQ](../faq.md) or open an issue on [GitHub](https://github.com/IgorBodnar/VoilaTile/issues).  

