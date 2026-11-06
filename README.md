# 🎬 TorrServer STRM Manager for Jellyfin

**Turn magnet links into Jellyfin-ready libraries — powered by TorrServer.**

[![Jellyfin 10.10](https://img.shields.io/badge/Jellyfin-10.10-00A4DC?logo=jellyfin&logoColor=white)](https://jellyfin.org)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Latest release](https://img.shields.io/github/v/release/fryt3ch/Jellyfin-TorrServer-STRM-Manager-Plugin)](https://github.com/fryt3ch/Jellyfin-TorrServer-STRM-Manager-Plugin/releases)

TorrServer STRM Manager is a Jellyfin plugin that imports magnet links through TorrServer and writes `.strm` files into your libraries. Jellyfin treats them as regular movies and series, while TorrServer handles the torrenting and streaming behind the scenes.

---

## ✨ What it does

- 🧲 **Magnet → library in one paste** — no manual file management.
- 🏷️ **Automatic metadata** — title, year, and type are detected from the magnet and TorrServer.
- 📚 **Smart library selection** — only compatible movie or series libraries are offered.
- 📺 **Proper series layout** — seasons and episodes are named and organized for Jellyfin.
- 🩹 **Self-healing index** — `.strm` addresses are repaired and missing torrents are re-added automatically.
- 🧹 **Automatic cleanup** — torrents are removed from TorrServer after the last related `.strm` file is deleted.
- 🔍 **Tracked files view** — search imported files and spot missing entries.
- 🔐 **Optional TorrServer auth** — username/password via HTTP Basic.
- ⚡ **Optional library scan** — queue a Jellyfin scan after every import.

---

## 📸 Screenshots

<p align="center">
  <img src="https://github.com/user-attachments/assets/06967ee7-a4a7-4420-9856-e6bbdd89480a" width="48%" alt="Import magnet page" />
  <img src="https://github.com/user-attachments/assets/b5555fe6-5685-40d2-9f52-587579c9a8e2" width="48%" alt="Settings and tracked files" />
</p>

<p align="center"><sub>Import a magnet · Configure TorrServer and inspect tracked files</sub></p>

---

## 📋 Requirements

- **Jellyfin 10.10.x**
- **.NET 8 SDK** (only for building from source)
- A running **TorrServer** instance reachable from the Jellyfin server
- Write access to your Jellyfin library folders

### 🐳 Full deployment example

Need a ready-to-run Jellyfin + TorrServer setup? See the companion
[docker-compose stack](https://github.com/fryt3ch/jellyfin-torrserver-stack),
which also includes an optional Gluetun/WARP VPN profile for regions with strict laws.

---

## 🚀 Installation

### From the plugin repository (recommended)

1. In Jellyfin, open **Dashboard → Plugins → Repositories**.
2. Click **+** and add a repository:
   - **Name:** `TorrServer STRM Manager`
   - **URL:** `https://raw.githubusercontent.com/fryt3ch/Jellyfin-TorrServer-STRM-Manager-Plugin/main/manifest.json`
3. Save the repository, then open **Catalog → General**.
4. Install **TorrServer STRM Manager** and restart Jellyfin when prompted.

The repository is updated with every release, so Jellyfin can also offer future updates automatically.

### Build from source (manual)

```bash
dotnet publish src/Jellyfin.Plugin.TorrServerStrm/Jellyfin.Plugin.TorrServerStrm.csproj -c Release
```

Copy the resulting DLL:

```text
src/Jellyfin.Plugin.TorrServerStrm/bin/Release/net8.0/publish/Jellyfin.Plugin.TorrServerStrm.dll
```

into a versioned plugin folder and restart Jellyfin:

| Setup | Plugins folder |
| --- | --- |
| 🐧 Linux | `/var/lib/jellyfin/plugins/TorrServerStrm_<version>/` |
| 🐳 Docker | `/config/plugins/TorrServerStrm_<version>/` |
| 🪟 Windows | `%ProgramData%\Jellyfin\Server\plugins\TorrServerStrm_<version>/` |

Replace `<version>` with the plugin version, for example `1.0.0.0`.

---

## ⚙️ Configuration

Open **Dashboard → Plugins → TorrServer STRM Manager → Settings**.

| Setting | Description |
| --- | --- |
| 🌐 **TorrServer URL** | Used by Jellyfin for API calls and written into `.strm` files. It must be reachable from the Jellyfin server — in Docker, use the container address such as `http://torrserver:8090`. |
| 🔐 **Username / Password** | Optional HTTP Basic authentication for TorrServer. Leave blank if authentication is disabled. |
| 🔄 **Scan libraries after adding** | Queues a Jellyfin library scan after each successful import. |

Changing **TorrServer URL** and saving settings automatically rewrites the address in existing `.strm` files.

---

## ➕ Adding media

1. Open **Import .magnet link** from the Jellyfin sidebar.
2. Paste a magnet link and wait for the automatic metadata lookup.
3. Review the title, year, type, and library.
4. Click **Add**.

The plugin fills in the title, year, type, and library automatically. **Movie** is selected by default, and the detected type replaces it when a magnet is pasted. If the title is left empty, the plugin tries to determine it from TorrServer metadata and file names.

The library list is loaded from Jellyfin. Movie and series libraries of the wrong type are disabled — for example, a movie cannot be added to a series library. The first suitable library is selected automatically. Use the **Settings** button on the add page to return to the plugin configuration.

The plugin waits for TorrServer to fetch torrent metadata before writing files. If metadata cannot be received, the import fails with an error instead of creating broken entries.

---

## 🎬 Library layout

### Movies

```text
<Library>/
└── Movie Name (Year)/
    └── Movie Name (Year).strm
```

### Series

```text
<Library>/
└── Show Name (Year)/
    ├── Season 01/
    │   ├── Show Name S01E01.strm
    │   └── Show Name S01E02.strm
    └── Season 02/
        └── Show Name S02E01.strm
```

Series episodes are detected from `SxxExx` or `NxM` patterns and grouped into `Season <NN>` folders. If no episode marker is found, the plugin falls back to the source folder season and sequential numbering.

Only video files are imported: `.mkv`, `.mp4`, `.avi`, `.mov`, `.m4v`, `.ts`, `.m2ts`, `.wmv`, `.webm`, `.mpg`, and `.mpeg`.

---

## ▶️ Playback modes

By default, Jellyfin plays the `.strm` file directly from TorrServer. That means **both Jellyfin and the client** must be able to reach the configured TorrServer URL.

| Mode | How to enable | Who needs TorrServer access? |
| --- | --- | --- |
| 🚀 **Direct Play / Direct Stream** | Leave “Force transcoding” disabled | Jellyfin **and** the playback client |
| 🔒 **Jellyfin only** | Dashboard → Users → user → Media playback → enable **“Force transcoding of remote media sources (like Live TV)”** | Only Jellyfin |

The **Jellyfin only** mode makes Jellyfin fetch and transcode the stream, so clients only need access to Jellyfin. This disables Direct Play/Direct Stream and uses server CPU/GPU resources.

---

## 🗑️ Removing media

Delete movies, series, seasons, and episodes with the standard Jellyfin delete button. Jellyfin removes the `.strm` file and media folder, and the plugin automatically removes the related torrent from TorrServer.

To show the delete button, grant the user permission to delete media:

**Dashboard → Users → user → “Allow media deletion” → “All libraries”**

If a torrent is referenced by multiple `.strm` files — for example, one torrent for all seasons — it is removed only after the last related file is deleted.

---

## 🔧 Index and auto-repair

The plugin does not require a separate database. The in-memory index is built from existing `.strm` files that contain `/stream/` and have `link`, `index`, and `play` parameters.

The index is rebuilt at Jellyfin startup and after every library scan, manual or scheduled. During each rebuild the plugin:

1. **Fixes the base address** in `.strm` files when it does not match the configured TorrServer URL. Only the prefix up to `/stream/` is changed; the file name and the `link`, `index`, and `play` parameters are preserved.
2. **Re-adds missing torrents** by comparing hashes with TorrServer. The plugin requests the torrent list and adds any missing infohash. TorrServer fetches the metadata itself, and trackers are applied according to its Retrackers setting.

If TorrServer is unavailable, the plugin writes a warning to the log and lets Jellyfin startup and library scans continue. `.strm` files without the expected `/stream/`, `link`, `index`, or `play` data are ignored and left untouched.

---

## 📂 Tracked files

The plugin settings page includes a **Tracked files** section showing the current index:

- Movies and series grouped by title.
- Every `.strm` file with its path and torrent hash.
- A marker when a file is missing on disk.
- Search by title, path, or hash.
- A **Refresh** button to reload the index.

---

## 🩺 Troubleshooting

| Problem | Solution |
| --- | --- |
| Jellyfin cannot reach TorrServer | Use an address reachable from inside the Jellyfin container/process, not `localhost` from another container. Check the Jellyfin log for warnings. |
| Playback fails on some clients | Clients cannot reach TorrServer directly. Enable **“Force transcoding of remote media sources”** for those users. |
| The delete button is missing | Grant the user **“Allow media deletion”**. |
| Import fails with a metadata error | Verify TorrServer is reachable, the magnet has peers, and TorrServer can fetch metadata. Retry after TorrServer warms up. |
| `.strm` addresses did not update | Save the settings again or trigger a library scan, then check the Jellyfin log. |

---

## 🧑‍💻 Development

```bash
dotnet build
dotnet test
dotnet publish src/Jellyfin.Plugin.TorrServerStrm/Jellyfin.Plugin.TorrServerStrm.csproj -c Release
```

The plugin targets **.NET 8** and references **Jellyfin.Controller 10.10.0**.

### Publishing a release

1. Update the `changelog` in `src/Jellyfin.Plugin.TorrServerStrm/build.yaml`.
2. Tag the release and push it:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The GitHub Actions release workflow builds the plugin ZIP, publishes it as a GitHub Release, and updates `manifest.json` for the custom repository.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
