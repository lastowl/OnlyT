# OnlyT Stream Deck Plugin

Control the OnlyT meeting timer from an Elgato Stream Deck.

## Actions

- **Start Timer** - Start the timer for a chosen talk, or the first talk not yet timed
- **Stop Timer** - Stop the running timer (optionally only a chosen talk)
- **Start/Stop Timer** - Stop the running timer, or start one when none is running; the key shows which it will do
- **Next Talk** - Stop the running talk and start the one after it
- **Ring Bell** - Ring OnlyT's bell

## Requirements

- Stream Deck 7.1 or later on Windows 10+ or macOS 12+ (the Stream Deck app isn't available for Linux)
- OnlyT with its API turned on: **Settings > Remote apps > Enabled**

## Installation

- **Windows installer:** choose "Install Stream Deck plugin"; the installer opens the plugin package so
  Stream Deck installs or updates it.
- **macOS DMG:** double-click `OnlyT-StreamDeck-Plugin.streamDeckPlugin`.
- **Otherwise:** double-click `com.onlyt.timer-<version>.streamDeckPlugin` from the release.

## Configuration

The connection is shared by every OnlyT key. Select any OnlyT key in Stream Deck to set:

- **Host** - where OnlyT is running (default `localhost`)
- **Port** - OnlyT's web server port (default `8096`)
- **Access code** - only if one is set in OnlyT's Remote apps settings

**Test connection** shows whether OnlyT can be reached and, if not, why (not running, API turned off,
wrong access code, or requests throttled).

Start, Stop and Start/Stop keys can each be set to a specific talk, or left on Automatic.

## API Endpoints Used

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/system/` | GET | Connection test |
| `/api/v1/timers/` | GET | Talks and current status |
| `/api/v1/timers/{id}` | POST | Start timer for talk |
| `/api/v1/timers/{id}` | DELETE | Stop timer for talk |
| `/api/v1/bell/` | POST | Ring the bell |

The access code is sent in the `X-Api-Key` header. Only visible Start/Stop keys poll OnlyT (every 2 seconds).

## Troubleshooting

- **Keys show a warning triangle:** open any OnlyT key's settings and use **Test connection**.
- **Start does nothing:** another talk may already be running, or every talk has been timed.
- **Bell doesn't ring:** make sure the bell is enabled in OnlyT's settings.

## Development

The plugin is plain Node.js with no dependencies (`plugin.js` handles Stream Deck, `onlyt.js` the OnlyT
API and action logic), so there's no build step.

```bash
# Tests (fake OnlyT API and fake Stream Deck connection)
node --test StreamDeck/tests/plugin.test.js

# Validate the manifest and package (version from SolutionInfo.cs)
npx @elgato/cli validate StreamDeck/com.onlyt.timer.sdPlugin
./Installer/StreamDeck/pack-streamdeck.sh
```

To try changes, link the folder into Stream Deck with `npx @elgato/cli link StreamDeck/com.onlyt.timer.sdPlugin`
and restart it with `npx @elgato/cli restart com.onlyt.timer`.

## License

This plugin is part of the OnlyT project.
