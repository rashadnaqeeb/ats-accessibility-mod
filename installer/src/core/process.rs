use std::time::{Duration, Instant};

use sysinfo::System;

const GAME_PROCESS_EXE: &str = "against the storm.exe";
const GAME_PROCESS_NAME: &str = "against the storm";

fn is_game_process(name: &str) -> bool {
    let name = name.to_ascii_lowercase();
    name == GAME_PROCESS_EXE || name == GAME_PROCESS_NAME
}

pub fn is_game_running() -> bool {
    let system = System::new_all();
    system
        .processes()
        .values()
        .any(|p| is_game_process(&p.name().to_string_lossy()))
}

/// Terminate any running Against the Storm process. Returns the number signalled.
pub fn kill_game() -> usize {
    let system = System::new_all();
    let mut killed = 0;
    for process in system.processes().values() {
        if is_game_process(&process.name().to_string_lossy()) {
            if process.kill() {
                killed += 1;
            }
        }
    }
    killed
}

/// Kill the game and wait until it's actually gone, up to `timeout`. `kill()`
/// only signals; the process (and its locks on the plugin DLL and managed
/// assemblies) can take a moment to release, so we poll, then add a short settle
/// for file-handle release before returning. Returns false if it's still running
/// at timeout.
pub fn kill_game_and_wait(timeout: Duration) -> bool {
    kill_game();
    let start = Instant::now();
    while is_game_running() {
        if start.elapsed() >= timeout {
            return false;
        }
        std::thread::sleep(Duration::from_millis(200));
    }
    // The process is gone; give Windows a brief moment to release file handles
    // that the just-terminated process held open before we overwrite them.
    std::thread::sleep(Duration::from_millis(400));
    true
}
