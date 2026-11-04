# Game updates

The community edition of Opus Magnum is updated often, and every update shuffles the internal
names the mod relies on. Magnum Opus keeps working through that on its own:

- **Right after an update**, the mod matches the new game build against the one it was made for.
  You hear "the game has updated since this mod version; using automatically matched names". If
  anything could not be matched, you also hear how many game members are missing; the features
  that use them stay silent, the rest work.
- **In the background**, the mod downloads the names the Opus Magnum modding community publishes
  for each new build (usually within a day). You hear "downloading names for the new game build",
  and then "mod updated for the new game build" once they are in use, or that none have been
  published yet. The download is kept, so the next launch uses it straight away.
- **A new release of the mod** is announced at launch ("update X available"); the installer
  updates it.

To turn the background download off, set `"mappings.download": "false"` in
`%LOCALAPPDATA%\MagnumOpus\settings.json`.
