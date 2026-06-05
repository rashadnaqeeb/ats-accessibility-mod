fn main() {
    if std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default() == "windows" {
        // Embed the manifest only into the actual binary, not test harnesses.
        // The requireAdministrator manifest would otherwise make `cargo test`
        // fail to launch the unit-test exe (os error 740: requires elevation).
        let _ = embed_resource::compile_for(
            "app.rc",
            &["ATSAccessibilityInstaller"],
            embed_resource::NONE,
        );
    }
}
