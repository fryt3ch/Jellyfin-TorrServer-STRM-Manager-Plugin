# Jellyfin.Plugin.TorrServerStrm

<img width="1321" height="996" alt="image" src="https://github.com/user-attachments/assets/06967ee7-a4a7-4420-9856-e6bbdd89480a" />
<img width="1201" height="990" alt="image" src="https://github.com/user-attachments/assets/b5555fe6-5685-40d2-9f52-587579c9a8e2" />



## Build
    dotnet publish -c Release

## Installation
Copy `src/Jellyfin.Plugin.TorrServerStrm/bin/Release/net8.0/publish/Jellyfin.Plugin.TorrServerStrm.dll` into the Jellyfin plugins folder:
    /var/lib/jellyfin/plugins/TorrServerStrm_1.0.0.0/
(Docker: `/config/plugins/TorrServerStrm_1.0.0.0/`), then restart Jellyfin.

## Usage
1. Dashboard → Plugins → TorrServer STRM Manager → Settings: set the TorrServer URL.

   The same address is used by the Jellyfin server for API calls and for streaming,
   and it is written to `.strm` files. It must be reachable from the Jellyfin
   container/process (for example, `http://torrserver:8090` in Docker).
   When the address changes and settings are saved, existing `.strm` files are updated automatically.

   To play only through Jellyfin (clients never open the TorrServer address), enable
   **"Force transcoding of remote media sources (like Live TV)"** for the users:
   Dashboard → Users → user → Media playback. This makes Jellyfin fetch the stream
   and transcode it, so clients only need access to Jellyfin. Note that this disables
   Direct Play/Direct Stream and uses server CPU/GPU.

   If that option is disabled, clients can Direct Play directly from TorrServer and
   therefore must be able to reach the configured TorrServer URL.
2. Open the "Import .magnet link" sidebar item (or the "Settings" button on the add page
   if you need to return to the configuration). Paste a magnet link and click "Add".
   Title, year, type, and library are filled in automatically from the magnet (`dn`). "Movie" is
   selected by default; when a magnet is pasted, the type is replaced with the detected one.
   You can correct the values before adding. If the title is left empty, the plugin will try
   to determine it from TorrServer data and file names.

   The library list is loaded from Jellyfin. Each library folder is a separate item.
   "Movies" and "Series" libraries are unavailable when the magnet type is different (for example,
   you cannot add a movie to a series library). Libraries of other types can be selected.
   By default, the first suitable library is selected, otherwise the first available one.
3. Series are laid out as `Series/<Show>/Season <NN>/<Show> S<NN>E<NN>.strm`.

## Removal
Delete movies, series, seasons, and episodes with the standard Jellyfin delete button. Jellyfin deletes
the `.strm` file and the media folder, and the plugin automatically removes the related torrent from TorrServer.

To make the delete button appear, grant the user permission to delete:
Dashboard → Users → user → "Allow media deletion" → "All libraries".

If a torrent is used by multiple `.strm` files (for example, one torrent for all seasons),
it is removed only after the last related file is deleted.

## `.strm` index and auto-repair

The plugin does not keep a separate `library.json`. The in-memory index is built from existing
`.strm` files that contain `/stream/` and have `link`, `index`, and `play` in the link parameters.

The index is rebuilt at Jellyfin startup and after every library scan
(manual or scheduled). At both points the plugin:

1. Fixes the address in `.strm` files when it does not match the TorrServer URL from settings.
   Only the prefix up to `/stream/` is changed; the `link`, `index`, and `play` parameters
   and the file name are preserved.
2. Compares torrent hashes with TorrServer and re-adds missing ones
   (`POST /torrents` with `action=list`, then `action=add` by infohash).
   TorrServer fetches the metadata itself; trackers are applied according to its Retrackers setting.

If TorrServer is unavailable, the plugin writes a warning to the log, and Jellyfin startup
and the library scan continue. `.strm` files without `/stream/`, `link`, `index`,
or `play` are not indexed and are not modified.

Old `library.json` files left by previous versions are no longer used and can be deleted.

## Tracked files

The plugin settings page has a **Tracked files** section. It shows the current
index: movies and series grouped by title, with a list of `.strm` files, paths, and hashes.
For each file it shows whether the file exists on disk. There is a search by title,
path, and hash, and a **Refresh** button.
