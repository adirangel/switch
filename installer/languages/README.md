# Vendored Inno Setup translations

Inno Setup ships official wizard translations for English, Spanish, French, Portuguese, Hebrew and
Japanese, so those six are referenced straight out of the compiler's own `Languages\` folder.

**Chinese (Simplified) and Arabic are unofficial** Inno Setup translations and are not part of a
standard install. Drop them here to have the wizard speak them too:

- `ChineseSimplified.isl`
- `Arabic.isl`

Both are listed on the [Inno Setup translations page](https://jrsoftware.org/files/istrans/).

`ScreenSwitch.iss` includes each one only if the file is present, so the installer builds either
way. When a file is missing, the *wizard* falls back to English for that language — the application
itself is still fully translated, because its strings come from its own resources rather than from
Inno Setup.
