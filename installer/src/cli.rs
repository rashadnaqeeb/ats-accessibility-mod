use std::io::{self, Write};
use std::path::PathBuf;
use std::time::Duration;

use crate::Args;
use crate::core::detect::{self, GameInstall, GameSource};
use crate::core::github;
use crate::core::install::{self, InstallOutcome, InstallProgress, InstallState};
use crate::core::process;
use crate::core::uninstall;
use crate::i18n::{t, tf};

pub fn run(args: &Args) {
    println!("=== {} ===", t("app.title"));

    if let Some(store) = detect::detect_store_warning() {
        println!("{}", t("dialog.store_warning"));
        println!("  {}", store.display());
    }

    let Some(game) = resolve_game_install(args) else {
        println!("{}", t("dialog.select_valid_dir"));
        return;
    };

    // Headless update mode: do it once and exit (launched by the mod with
    // --cli --update --game-dir ...).
    if args.update {
        install_latest(&game, false);
        return;
    }

    loop {
        let state = install::classify_install(&game.path);
        println!();
        println!("{}", tf("log.game_dir", &[&game.path.to_string_lossy()]));
        print_state(&state);
        println!();
        println!("1. {}", primary_label(&state));
        println!("2. {}", t("button.reinstall"));
        println!("3. {}", t("button.uninstall"));
        println!("4. {}", t("button.close"));

        match prompt("> ").as_str() {
            "1" => install_latest(&game, false),
            "2" => install_latest(&game, true),
            "3" => uninstall_managed(&game.path, &state),
            "4" => return,
            _ => {}
        }
    }
}

fn primary_label(state: &InstallState) -> String {
    match state {
        InstallState::Fresh => t("button.install"),
        InstallState::Unmanaged | InstallState::DamagedState(_) => t("button.repair"),
        InstallState::Managed(_) => t("button.update"),
    }
}

fn resolve_game_install(args: &Args) -> Option<GameInstall> {
    // Explicit path from the mod / command line wins.
    if let Some(dir) = &args.game_dir {
        let path = PathBuf::from(dir.trim_matches('"'));
        if detect::validate_game_dir(&path) {
            return Some(GameInstall {
                path,
                source: GameSource::Manual,
            });
        }
    }

    if let Some(detected) = detect::detect_game() {
        println!(
            "{}",
            tf("log.detected_dir", &[&detected.path.to_string_lossy()])
        );
        if args.update {
            return Some(detected);
        }
        let answer = prompt("Use this path? (Y/n): ");
        if answer != "n" && answer != "no" {
            return Some(detected);
        }
    }

    let input = prompt("Game directory path: ");
    let path = PathBuf::from(input.trim_matches('"'));
    if detect::validate_game_dir(&path) {
        Some(GameInstall {
            path,
            source: GameSource::Manual,
        })
    } else {
        None
    }
}

fn print_state(state: &InstallState) {
    match state {
        InstallState::Fresh => println!("{}", t("status.ready_install")),
        InstallState::Managed(m) => {
            println!("{}", tf("status.up_to_date", &[&m.mod_version]))
        }
        InstallState::Unmanaged => println!("{}", t("status.unmanaged")),
        InstallState::DamagedState(reason) => {
            println!("{}", tf("log.damaged_detail", &[reason]))
        }
    }
}

fn ensure_game_closed() -> bool {
    if !process::is_game_running() {
        return true;
    }
    println!("{}", t("dialog.confirm_kill"));
    let answer = prompt("(y/N): ");
    if answer != "y" && answer != "yes" {
        return false;
    }
    println!("{}", t("log.killing_game"));
    process::kill_game_and_wait(Duration::from_secs(8))
}

fn install_latest(game: &GameInstall, force: bool) {
    if !ensure_game_closed() {
        println!("{}", t("dialog.game_running_cancel"));
        return;
    }

    let release = match github::fetch_latest_release() {
        Ok(r) => r,
        Err(e) => {
            println!("{}", tf("log.github_failed", &[&e]));
            return;
        }
    };
    let Some(asset) = github::find_mod_zip(&release) else {
        println!("{}", t("log.no_asset"));
        return;
    };
    println!("{}", tf("log.latest_asset", &[&asset.name]));

    let outcome = install::perform_install(game, &asset, force, |step| match step {
        InstallProgress::Downloading(name) => println!("{}", tf("log.downloading", &[&name])),
        InstallProgress::Verifying => println!("{}", t("log.verifying")),
        InstallProgress::Installing => println!("{}", t("log.installing")),
    });

    match outcome {
        Ok(InstallOutcome::Installed) => println!("{}", t("dialog.install_complete")),
        Ok(InstallOutcome::AlreadyUpToDate) => println!("{}", t("log.already_up_to_date")),
        Err(e) => println!("{}", tf("dialog.install_failed", &[&e])),
    }
}

fn uninstall_managed(game_path: &std::path::Path, state: &InstallState) {
    let InstallState::Managed(manifest) = state else {
        println!("{}", t("dialog.uninstall_only_managed"));
        return;
    };
    let answer = prompt(&format!("{} (y/N): ", t("dialog.confirm_uninstall")));
    if answer != "y" && answer != "yes" {
        return;
    }
    if !ensure_game_closed() {
        println!("{}", t("dialog.game_running_cancel"));
        return;
    }
    match uninstall::uninstall(game_path, manifest) {
        Ok(()) => println!("{}", t("dialog.uninstall_complete")),
        Err(e) => println!("{}", tf("dialog.uninstall_failed", &[&e])),
    }
}

fn prompt(message: &str) -> String {
    print!("{message}");
    let _ = io::stdout().flush();
    let mut input = String::new();
    let _ = io::stdin().read_line(&mut input);
    input.trim().to_lowercase()
}
