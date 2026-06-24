# PixelThis

A small, fast pixel-art editor built with [Avalonia](https://avaloniaui.net/)
(.NET 10) — layers, a custom palette, a native `.pxt` project format, and
transparent PNG export aimed at Godot sprites.

## Develop

Run the app from source (hot dev loop, does **not** touch the installed app):

```bash
./run.sh
```

or directly:

```bash
dotnet run --project src/PixelThis/PixelThis.csproj
```

Build / quick checks:

```bash
dotnet build src/PixelThis/PixelThis.csproj
```

## Deploy to Launchpad (macOS)

The version shown in **Launchpad** is the installed app bundle at
`/Applications/PixelThis.app`. `dotnet run` never updates it — to publish a new
build to Launchpad, run:

```bash
./bundle.sh
```

This script:

1. Publishes a self-contained `osx-arm64` **Release** build to `./publish`.
2. Packages it into `PixelThis.app` (with `Info.plist`).
3. Installs it to `/Applications/PixelThis.app`, replacing the previous copy.

Takes ~1–2 minutes (it bundles the whole .NET runtime). After it finishes, quit
PixelThis if it's open (⌘-Q) and relaunch it from Launchpad to get the new build.

Notes:

- If you see `permission denied`, make the script executable once: `chmod +x bundle.sh`.
- The `NU1903` advisory printed during publish comes from a transitive Avalonia
  dependency (`Tmds.DBus.Protocol`, used only on Linux) — harmless on macOS.

## Project files

Work is saved in the native **`.pxt`** format (JSON) via the toolbar **Save** /
**Open** buttons. It captures the full session — every layer (pixels, name,
visibility, opacity), canvas size, palette, and the current color — so you can
reopen and keep editing. Use **Export PNG** to produce a transparent PNG for Godot.
