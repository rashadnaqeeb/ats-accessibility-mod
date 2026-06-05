use std::rc::Rc;
use std::time::Duration;

use wxdragon::prelude::*;

use crate::Args;
use crate::core::detect::{self, GameInstall, GameSource};
use crate::core::github::{self, Asset};
use crate::core::install::{self, InstallOutcome, InstallProgress, InstallState};
use crate::core::manifest;
use crate::core::process;
use crate::core::uninstall;
use crate::i18n::{self, t, tf};

pub fn run(args: &Args) {
    // wxdragon closures need 'static data; copy what we need out of args.
    let arg_update = args.update;
    let arg_game_dir = args.game_dir.clone();
    let arg_lang = args.lang.clone();

    wxdragon::main(move |_app| {
        let frame = Frame::builder()
            .with_title(&t("app.title"))
            .with_size(Size::new(700, 460))
            .build();

        // Resolve the UI language before building the window. Priority:
        //   saved manifest pref > --lang from the mod > a one-time dialog (OS
        //   default). initial_dir is reused below to prefill the path.
        let initial_dir = arg_game_dir
            .as_deref()
            .map(std::path::PathBuf::from)
            .filter(|p| detect::validate_game_dir(p))
            .or_else(|| detect::detect_game().map(|g| g.path));
        match initial_dir.as_ref().and_then(|d| manifest::saved_language(d)) {
            Some(saved) => i18n::set_language(&saved),
            None => {
                if arg_lang.is_none() {
                    // First run, no saved choice and no language from the game.
                    if let Some(code) = prompt_language(&frame) {
                        i18n::set_language(&code);
                        if let Some(d) = &initial_dir {
                            manifest::set_saved_language(d, &code);
                        }
                    }
                }
            }
        }
        frame.set_title(&t("app.title"));

        let panel = Panel::builder(&frame).build();
        let main_sizer = BoxSizer::builder(Orientation::Vertical).build();

        let status = StaticText::builder(&panel)
            .with_label(&t("status.detecting"))
            .build();

        // Game directory row.
        let path_sizer = BoxSizer::builder(Orientation::Horizontal).build();
        let path_label = StaticText::builder(&panel)
            .with_label(&t("label.game_dir"))
            .build();
        let path_input = TextCtrl::builder(&panel).build();
        let browse_btn = Button::builder(&panel).with_label(&t("button.browse")).build();
        path_sizer.add(&path_label, 0, SizerFlag::All | SizerFlag::AlignCenterVertical, 4);
        path_sizer.add(&path_input, 1, SizerFlag::Expand | SizerFlag::All, 4);
        path_sizer.add(&browse_btn, 0, SizerFlag::All, 4);

        let log = TextCtrl::builder(&panel)
            .with_style(TextCtrlStyle::MultiLine | TextCtrlStyle::ReadOnly | TextCtrlStyle::WordWrap)
            .build();

        // Action buttons. The language button shows the current language in its
        // own name and reopens the one-time picker so the choice can be changed.
        let btn_sizer = BoxSizer::builder(Orientation::Horizontal).build();
        let lang_btn = Button::builder(&panel)
            .with_label(&i18n::display_name(&i18n::active_language()))
            .build();
        let install_btn = Button::builder(&panel).with_label(&t("button.install")).build();
        let reinstall_btn = Button::builder(&panel).with_label(&t("button.reinstall")).build();
        let uninstall_btn = Button::builder(&panel).with_label(&t("button.uninstall")).build();
        let close_btn = Button::builder(&panel).with_label(&t("button.close")).build();
        btn_sizer.add(&lang_btn, 0, SizerFlag::All, 4);
        btn_sizer.add_stretch_spacer(1);
        btn_sizer.add(&install_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&reinstall_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&uninstall_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&close_btn, 0, SizerFlag::All, 4);

        main_sizer.add(&status, 0, SizerFlag::Expand | SizerFlag::All, 8);
        main_sizer.add_sizer(&path_sizer, 0, SizerFlag::Expand | SizerFlag::Left | SizerFlag::Right, 8);
        main_sizer.add(&log, 1, SizerFlag::Expand | SizerFlag::All, 8);
        main_sizer.add_sizer(&btn_sizer, 0, SizerFlag::Expand | SizerFlag::All, 4);
        panel.set_sizer(main_sizer, true);

        install_btn.enable(false);
        reinstall_btn.enable(false);
        uninstall_btn.enable(false);

        // Warn about a Microsoft Store / Xbox copy up front.
        if let Some(store_path) = detect::detect_store_warning() {
            log_append(&log, &format!("{}\n  {}", t("dialog.store_warning"), store_path.display()));
            show_warning(&frame, &t("dialog.store_warning"), &t("dialog.store_warning_title"));
        }

        // Fetch latest release once; share via Rc.
        let release = match github::fetch_latest_release() {
            Ok(r) => {
                log_append(&log, &t("log.connected"));
                Some(r)
            }
            Err(e) => {
                log_append(&log, &tf("log.github_failed", &[&e]));
                None
            }
        };
        let asset: Rc<Option<Asset>> = Rc::new(release.as_ref().and_then(github::find_mod_zip));
        if let Some(a) = asset.as_ref() {
            log_append(&log, &tf("log.latest_asset", &[&a.name]));
        } else {
            log_append(&log, &t("log.no_asset"));
        }

        // Prefill the path (initial_dir was resolved above for the language lookup).
        if let Some(path) = &initial_dir {
            path_input.set_value(&path.to_string_lossy());
            log_append(&log, &tf("log.detected_dir", &[&path.to_string_lossy()]));
        } else {
            status.set_label(&t("status.not_found"));
            log_append(&log, &t("log.cannot_detect"));
        }

        refresh_state(&path_input, &status, &install_btn, &reinstall_btn, &uninstall_btn, asset.as_ref().as_ref(), &log);

        // --- Language button: reopen the picker and re-apply all labels. ---
        {
            let frame_c = frame.clone();
            let status_c = status.clone();
            let lang_btn_c = lang_btn.clone();
            let path_label_c = path_label.clone();
            let browse_c = browse_btn.clone();
            let install_c = install_btn.clone();
            let reinstall_c = reinstall_btn.clone();
            let uninstall_c = uninstall_btn.clone();
            let close_c = close_btn.clone();
            let path_input_c = path_input.clone();
            let log_c = log.clone();
            let asset_c = Rc::clone(&asset);

            lang_btn.on_click(move |_| {
                let Some(code) = prompt_language(&frame_c) else {
                    return;
                };
                i18n::set_language(&code);
                // Persist into the manifest if this is a managed install.
                if let Some(game) = game_from_input(&path_input_c) {
                    manifest::set_saved_language(&game.path, &code);
                }
                frame_c.set_title(&t("app.title"));
                lang_btn_c.set_label(&i18n::display_name(&code));
                path_label_c.set_label(&t("label.game_dir"));
                browse_c.set_label(&t("button.browse"));
                reinstall_c.set_label(&t("button.reinstall"));
                uninstall_c.set_label(&t("button.uninstall"));
                close_c.set_label(&t("button.close"));
                // Status + primary-button label depend on install state.
                refresh_state(&path_input_c, &status_c, &install_c, &reinstall_c, &uninstall_c, asset_c.as_ref().as_ref(), &log_c);
            });
        }

        // --- Browse handler ---
        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_c = install_btn.clone();
            let reinstall_c = reinstall_btn.clone();
            let uninstall_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = Rc::clone(&asset);

            browse_btn.on_click(move |_| {
                let dialog = DirDialog::builder(&frame_c, &t("browse.title"), "").build();
                if dialog.show_modal() != ID_OK {
                    return;
                }
                let Some(path_str) = dialog.get_path() else {
                    return;
                };
                let path = std::path::PathBuf::from(&path_str);
                if !detect::validate_game_dir(&path) {
                    log_append(&log_c, &tf("log.invalid_dir", &[&path.to_string_lossy()]));
                    status_c.set_label(&t("dialog.select_valid_dir"));
                    return;
                }
                path_input_c.set_value(&path.to_string_lossy());
                log_append(&log_c, &tf("log.game_dir", &[&path.to_string_lossy()]));
                refresh_state(&path_input_c, &status_c, &install_c, &reinstall_c, &uninstall_c, asset_c.as_ref().as_ref(), &log_c);
            });
        }

        // --- Primary Install/Update/Repair handler ---
        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_c = install_btn.clone();
            let reinstall_c = reinstall_btn.clone();
            let uninstall_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = Rc::clone(&asset);

            install_btn.on_click(move |_| {
                let Some(asset) = asset_c.as_ref().as_ref() else {
                    show_logged_error(&frame_c, &log_c, &t("dialog.no_asset"));
                    return;
                };
                let Some(game) = game_from_input(&path_input_c) else {
                    show_logged_error(&frame_c, &log_c, &t("dialog.select_valid_dir"));
                    return;
                };
                install_asset(&frame_c, &game, asset, false, &log_c);
                refresh_state(&path_input_c, &status_c, &install_c, &reinstall_c, &uninstall_c, Some(asset), &log_c);
            });
        }

        // --- Reinstall (force) handler ---
        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_c = install_btn.clone();
            let reinstall_c = reinstall_btn.clone();
            let uninstall_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = Rc::clone(&asset);

            reinstall_btn.on_click(move |_| {
                let Some(asset) = asset_c.as_ref().as_ref() else {
                    show_logged_error(&frame_c, &log_c, &t("dialog.no_asset"));
                    return;
                };
                let Some(game) = game_from_input(&path_input_c) else {
                    show_logged_error(&frame_c, &log_c, &t("dialog.select_valid_dir"));
                    return;
                };
                install_asset(&frame_c, &game, asset, true, &log_c);
                refresh_state(&path_input_c, &status_c, &install_c, &reinstall_c, &uninstall_c, Some(asset), &log_c);
            });
        }

        // --- Uninstall handler ---
        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_c = install_btn.clone();
            let reinstall_c = reinstall_btn.clone();
            let uninstall_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = Rc::clone(&asset);

            uninstall_btn.on_click(move |_| {
                let Some(game) = game_from_input(&path_input_c) else {
                    show_logged_error(&frame_c, &log_c, &t("dialog.select_valid_dir"));
                    return;
                };
                let InstallState::Managed(manifest) = install::classify_install(&game.path) else {
                    show_logged_error(&frame_c, &log_c, &t("dialog.uninstall_only_managed"));
                    return;
                };
                if confirm(&frame_c, &t("dialog.confirm_uninstall"), &t("dialog.confirm_uninstall_title")) != ID_YES {
                    return;
                }
                if !ensure_game_closed(&frame_c, &log_c) {
                    return;
                }
                match uninstall::uninstall(&game.path, &manifest) {
                    Ok(()) => {
                        log_append(&log_c, &t("dialog.uninstall_complete"));
                        show_info(&frame_c, &t("dialog.uninstall_complete"));
                    }
                    Err(e) => show_logged_error(&frame_c, &log_c, &tf("dialog.uninstall_failed", &[&e])),
                }
                refresh_state(&path_input_c, &status_c, &install_c, &reinstall_c, &uninstall_c, asset_c.as_ref().as_ref(), &log_c);
            });
        }

        // --- Close handler ---
        {
            let frame_c = frame.clone();
            close_btn.on_click(move |_| {
                frame_c.close(true);
            });
        }

        frame.show(true);

        // --- Auto-update flow when launched by the mod ---
        if arg_update {
            if let (Some(asset), Some(game)) = (asset.as_ref().as_ref(), game_from_input(&path_input)) {
                log_append(&log, &t("log.update_launch"));
                if confirm(&frame, &t("dialog.update_prompt"), &t("dialog.update_prompt_title")) == ID_YES {
                    install_asset(&frame, &game, asset, false, &log);
                    refresh_state(&path_input, &status, &install_btn, &reinstall_btn, &uninstall_btn, Some(asset), &log);
                }
            }
        }
    })
    .expect("Failed to start installer UI");
}

/// Show the language picker and return the chosen game loca code, or None if
/// cancelled. Lists every shipped language by its own name; defaults to the
/// currently active language.
fn prompt_language(parent: &impl WxWidget) -> Option<String> {
    let langs = i18n::available_languages();
    let names: Vec<String> = langs.iter().map(|(_, name)| name.clone()).collect();
    let name_refs: Vec<&str> = names.iter().map(|s| s.as_str()).collect();
    let active = i18n::active_language();
    let default_idx = langs
        .iter()
        .position(|(code, _)| code.eq_ignore_ascii_case(&active))
        .unwrap_or(0) as i32;

    let dialog =
        SingleChoiceDialog::builder(parent, &t("dialog.choose_language"), &t("app.title"), &name_refs)
            .build();
    dialog.set_selection(default_idx);
    if dialog.show_modal() != ID_OK {
        return None;
    }
    let sel = dialog.get_selection();
    if sel < 0 {
        return None;
    }
    langs.get(sel as usize).map(|(code, _)| code.clone())
}

fn game_from_input(path_input: &TextCtrl) -> Option<GameInstall> {
    let path = std::path::PathBuf::from(path_input.get_value());
    if detect::validate_game_dir(&path) {
        Some(GameInstall {
            path,
            source: GameSource::Manual,
        })
    } else {
        None
    }
}

fn refresh_state(
    path_input: &TextCtrl,
    status: &StaticText,
    install_btn: &Button,
    reinstall_btn: &Button,
    uninstall_btn: &Button,
    asset: Option<&Asset>,
    log: &TextCtrl,
) {
    let Some(game) = game_from_input(path_input) else {
        install_btn.enable(false);
        reinstall_btn.enable(false);
        uninstall_btn.enable(false);
        return;
    };

    let state = install::classify_install(&game.path);
    let has_asset = asset.is_some();

    match &state {
        InstallState::Fresh => {
            status.set_label(&t("status.ready_install"));
            install_btn.set_label(&t("button.install"));
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::Unmanaged => {
            status.set_label(&t("status.unmanaged"));
            install_btn.set_label(&t("button.repair"));
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::DamagedState(reason) => {
            status.set_label(&t("status.damaged"));
            log_append(log, &tf("log.damaged_detail", &[reason]));
            install_btn.set_label(&t("button.repair"));
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::Managed(manifest) => {
            install_btn.set_label(&t("button.update"));
            if install::update_available(&state, asset) {
                status.set_label(&tf("status.update_available", &[&manifest.mod_version]));
                install_btn.enable(has_asset);
            } else {
                status.set_label(&tf("status.up_to_date", &[&manifest.mod_version]));
                install_btn.enable(false);
            }
            reinstall_btn.enable(has_asset);
            uninstall_btn.enable(true);
        }
    }
}

/// Ensure the game is closed before touching its files. Confirms, then kills.
fn ensure_game_closed(parent: &impl WxWidget, log: &TextCtrl) -> bool {
    if !process::is_game_running() {
        return true;
    }
    if confirm(parent, &t("dialog.confirm_kill"), &t("dialog.confirm_kill_title")) != ID_YES {
        return false;
    }
    log_append(log, &t("log.killing_game"));
    if !process::kill_game_and_wait(Duration::from_secs(8)) {
        show_logged_error(parent, log, &t("dialog.game_running_cancel"));
        return false;
    }
    true
}

fn install_asset(parent: &impl WxWidget, game: &GameInstall, asset: &Asset, force: bool, log: &TextCtrl) {
    if !ensure_game_closed(parent, log) {
        return;
    }

    let outcome = install::perform_install(game, asset, force, |step| match step {
        InstallProgress::Downloading(name) => log_append(log, &tf("log.downloading", &[&name])),
        InstallProgress::Verifying => log_append(log, &t("log.verifying")),
        InstallProgress::Installing => log_append(log, &t("log.installing")),
    });

    match outcome {
        Ok(InstallOutcome::Installed) => {
            log_append(log, &t("dialog.install_complete"));
            show_info(parent, &t("dialog.install_complete"));
        }
        Ok(InstallOutcome::AlreadyUpToDate) => {
            log_append(log, &t("log.already_up_to_date"));
            show_info(parent, &t("log.already_up_to_date"));
        }
        Err(e) => show_logged_error(parent, log, &tf("dialog.install_failed", &[&e])),
    }
}

fn log_append(log: &TextCtrl, message: &str) {
    let current = log.get_value();
    if current.is_empty() {
        log.set_value(message);
    } else {
        log.set_value(&format!("{current}\n{message}"));
    }
}

fn show_info(parent: &impl WxWidget, message: &str) {
    MessageDialog::builder(parent, message, &t("app.title"))
        .with_style(MessageDialogStyle::OK | MessageDialogStyle::IconInformation)
        .build()
        .show_modal();
}

fn show_warning(parent: &impl WxWidget, message: &str, title: &str) {
    MessageDialog::builder(parent, message, title)
        .with_style(MessageDialogStyle::OK | MessageDialogStyle::IconWarning)
        .build()
        .show_modal();
}

fn show_error(parent: &impl WxWidget, message: &str) {
    MessageDialog::builder(parent, message, &t("app.title"))
        .with_style(MessageDialogStyle::OK | MessageDialogStyle::IconError)
        .build()
        .show_modal();
}

fn show_logged_error(parent: &impl WxWidget, log: &TextCtrl, message: &str) {
    log_append(log, message);
    show_error(parent, message);
}

fn confirm(parent: &impl WxWidget, message: &str, title: &str) -> i32 {
    MessageDialog::builder(parent, message, title)
        .with_style(MessageDialogStyle::YesNo | MessageDialogStyle::IconQuestion)
        .build()
        .show_modal()
}
