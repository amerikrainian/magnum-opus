# Before starting

Magnum Opus is a screen reader accessibility mod for Opus Magnum. It speaks through your screen
reader (NVDA, JAWS and others, through Prism; SAPI when none is running) and adds keyboard
navigation to the game's screens.

## The community-edition branch

Magnum Opus supports the **community-edition** Steam branch of Opus Magnum, not the default one.
To switch:

1. In your Steam library, open Opus Magnum's **Properties**.
2. Choose **Game Versions and Betas**.
3. Pick **community-edition**. Steam downloads the branch.

The branch is an official Steam branch of the same game: achievements, saves and Steam features
work as usual. The installer tells you when the game is on another branch.

## Installing

Run `MagnumOpusInstaller.exe`. It finds the game, downloads the newest release, and installs it
into the game folder, keeping a record so it can update, repair and uninstall cleanly. Nothing of
the game itself is changed: the mod is a set of extra files, and deleting `Lightning.exe.config`
from the game folder turns it off.

To install by hand, extract the release zip into the game folder (the zip's root is the game
folder).

## First launch

Start the game from Steam as usual. When the mod is up you hear "Magnum Opus" and its version,
then "Press any key to continue" on the title screen.
