// Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
// https://github.com/rashadnaqeeb/NonVisualCalculus — by way of the Harkest
// Dungeon installer, https://github.com/amerikrainian/harkest-dungeon.

use std::path::{Path, PathBuf};

// The full release list, newest first: one call serves both the latest-version
// lookup and the per-version release notes shown after an update.
pub const GITHUB_RELEASES_URL: &str =
    "https://api.github.com/repos/amerikrainian/magnum-opus/releases?per_page=100";
pub const MOD_ZIP_PREFIX: &str = "MagnumOpus-v";
pub const MOD_ZIP_SUFFIX: &str = ".zip";
pub const GAME_EXES: &[&str] = &["Lightning.exe"];
pub const GAME_FOLDERS: &[&str] = &["Opus Magnum"];
// Opus Magnum is one self-contained managed exe on Zachtronics' own engine; its
// D3D11 renderer dll sits next to the exe in every install, so together they
// identify the game dir (SDL2.dll alone would match any SDL game).
pub const GAME_ASSEMBLY_MARKER: &str = "Renderer_D3D11.dll";
// The host dll Lightning.exe.config makes the CLR load: the mod's presence marker.
pub const PLUGIN_REL: &str = "MagnumOpus.dll";
// Installer state lives in the mod's own folder, next to mappings\ and lang\.
pub const MANIFEST_REL: &str = "MagnumOpus/install.json";
pub const BACKUPS_REL: &str = "MagnumOpus/backups";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_REL)
}

pub fn normalize_rel(path: &str) -> String {
    path.replace('\\', "/").trim_start_matches("./").to_string()
}

/// Any of these present without a manifest = a hand-copied install (repair offered).
/// steam_appid.txt and Mono.Cecil.dll are deliberately not markers: too generic.
pub fn required_loader_files() -> &'static [&'static str] {
    &[
        "Lightning.exe.config",
        PLUGIN_REL,
        "MagnumOpus/MagnumOpus.Module.dll",
        "0Harmony.dll",
        "prism.dll",
    ]
}
