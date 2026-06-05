//! Library crate for the installer. All logic lives here so unit tests run in
//! the library's test harness, which does NOT get the requireAdministrator
//! manifest embedded into the binary (that would make `cargo test` fail with
//! os error 740). `main.rs` is a thin wrapper that calls [`run`].

pub mod cli;
pub mod core;
pub mod gui;
pub mod i18n;

/// Parsed command-line options. The mod launches the installer with
/// `--update --game-dir "<path>" --lang <code>`; users launch it with no args
/// (GUI) or `--cli` (text menu).
pub struct Args {
    pub cli: bool,
    pub update: bool,
    pub game_dir: Option<String>,
    pub lang: Option<String>,
}

pub fn parse_args() -> Args {
    let mut args = Args {
        cli: false,
        update: false,
        game_dir: None,
        lang: None,
    };
    let raw: Vec<String> = std::env::args().skip(1).collect();
    let mut i = 0;
    while i < raw.len() {
        match raw[i].as_str() {
            "--cli" => args.cli = true,
            "--update" => args.update = true,
            "--game-dir" => {
                i += 1;
                args.game_dir = raw.get(i).cloned();
            }
            "--lang" => {
                i += 1;
                args.lang = raw.get(i).cloned();
            }
            other => {
                // Allow `--game-dir=...` / `--lang=...` forms too.
                if let Some(v) = other.strip_prefix("--game-dir=") {
                    args.game_dir = Some(v.to_string());
                } else if let Some(v) = other.strip_prefix("--lang=") {
                    args.lang = Some(v.to_string());
                }
            }
        }
        i += 1;
    }
    args
}

pub fn run() {
    let args = parse_args();

    // Set the UI language as early as possible: explicit --lang from the mod
    // wins, otherwise auto-detect from the OS. The GUI can still switch later.
    i18n::init(args.lang.as_deref());

    if args.cli {
        attach_console();
        cli::run(&args);
    } else {
        gui::run(&args);
    }
}

fn attach_console() {
    #[cfg(target_os = "windows")]
    unsafe {
        windows_sys::Win32::System::Console::AttachConsole(
            windows_sys::Win32::System::Console::ATTACH_PARENT_PROCESS,
        );
    }
}
