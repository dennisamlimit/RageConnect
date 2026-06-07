# RageConnect

RageConnect is a small console launcher for RAGE:MP direct connect presets.

The tool does not patch or modify RAGE:MP.
It only writes the normal direct connect values, starts the RAGE:MP updater and lets you manage presets more comfortably.

## Patching

RageConnect currently does not include any RAGE patches.
As long as the normal Direct Connect method works, there is no need to patch or modify any RAGE files. The project is intentionally kept as simple as possible: set the connect values, manage presets, and launch the official RAGE updater.
If patches should become necessary in the future, they will be implemented. However, at the moment they are not required since direct connecting is still possible.

## Features

* GrandRP server presets
* Custom server presets
* Add and delete own presets
* RAGE:MP path detection
* Manual `updater.exe` selection
* Optional RAGE:MP installer download
* Simple status output
* Local preset storage

## Current RAGE:MP situation

RAGE:MP announced a structured shutdown after a request from Take-Two Interactive.

The public server list was planned to be shut down on June 1, 2026.
RAGE:MP end-of-support is planned for August 31, 2026.

Because of that, this tool is not meant to replace official launchers or bypass server restrictions.
It is only a small helper for the Direct Connect method while it still works.

## Can this connect without the server list?

Yes, but only in a normal Direct Connect scenario.

RageConnect can help if:

* the RAGE:MP client still starts
* the server is still online
* the server accepts Direct Connect
* the correct address and port are known

RageConnect cannot help if:

* RAGE:MP disables Direct Connect
* the RAGE:MP backend is offline
* the server requires its own launcher or bridge
* the server blocks normal direct connections

## Included GrandRP presets

```txt
GrandRP DE01 - de1.gta5grand.com:22005
GrandRP DE02 - de2.gta5grand.com:22005
GrandRP DE03 - de3.gta5grand.com:22005
GrandRP DE04 - de4.gta5grand.com:22005

GrandRP EN01 - rage.gta5grand.com:22005
GrandRP EN02 - rage2.gta5grand.com:22005
GrandRP EN03 - rage3.gta5grand.com:22005
```

## Custom presets

Custom presets are saved locally:

```txt
%AppData%\RageConnect\custom-presets.txt
```

Format:

```txt
Name|Address|Port
```

Example:

```txt
My Server|127.0.0.1|22005
Test Server|example.com|22005
```

## RAGE:MP detection

RageConnect searches common installation paths for:

```txt
updater.exe
```

If RAGE:MP is not found, the app lets you:

* download the RAGE:MP installer
* select the `updater.exe` manually
* enter the path yourself

The selected path is saved for the next start.

## Build

Recommended setup:

* .NET Framework 4.8

The project uses `System.Windows.Forms` for the file picker, so the reference has to be added.

## Usage

1. Start RageConnect
2. Select a preset
3. Let the app set the RAGE:MP direct connect values
4. Start RAGE:MP through RageConnect

