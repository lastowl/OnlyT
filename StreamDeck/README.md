# OnlyT Stream Deck Plugin

Control the OnlyT meeting timer from your Elgato Stream Deck.

## Features

- **Start Timer** - Start the timer for a specific talk
- **Stop Timer** - Stop the currently running timer
- **Toggle Timer** - Start if stopped, stop if running (with visual state)
- **Ring Bell** - Trigger the bell sound
- **Next Talk** - Automatically start the next talk in the schedule

## Installation

### Manual Installation

1. Close Stream Deck application
2. Copy the `com.onlyt.timer.sdPlugin` folder to the Stream Deck plugins directory:
   - **macOS:** `~/Library/Application Support/com.elgato.StreamDeck/Plugins/`
   - **Windows:** `%APPDATA%\Elgato\StreamDeck\Plugins\`
3. Restart Stream Deck application
4. The "OnlyT" category should appear in the action list

### From Release (Future)

Download the `.streamDeckPlugin` file and double-click to install.

## Configuration

Each action can be configured with:

- **OnlyT Host** - The hostname/IP where OnlyT is running (default: `localhost`)
- **Port** - The HTTP API port (default: `8096`)
- **Select Talk** - Optionally specify which talk to control (default: auto-selects next available)

## Requirements

- OnlyT application running with HTTP API enabled (Settings > API Enabled)
- Stream Deck software v6.0 or later
- Network access between Stream Deck and OnlyT (if not on same machine)

## API Endpoints Used

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/timers/` | GET | Get all talks and current status |
| `/api/v1/timers/{id}` | POST | Start timer for talk |
| `/api/v1/timers/{id}` | DELETE | Stop timer for talk |
| `/api/v1/bell/` | POST | Ring the bell |

## Troubleshooting

### Plugin doesn't appear in Stream Deck
- Ensure the folder is named exactly `com.onlyt.timer.sdPlugin`
- Check that `manifest.json` is valid JSON
- Restart Stream Deck application

### Actions don't work
1. Verify OnlyT is running
2. Check that the API is enabled in OnlyT settings
3. Verify the host/port settings in the action configuration
4. Test the API directly: `curl http://localhost:8096/api/v1/timers/`

### Bell action doesn't work
- Ensure bell is enabled in OnlyT settings
- Check the OnlyT logs for any errors

## Development

### Building the Plugin

Stream Deck plugins don't require compilation, but to create a distributable package:

```bash
# Package as .streamDeckPlugin
cd StreamDeck
zip -r com.onlyt.timer.streamDeckPlugin com.onlyt.timer.sdPlugin
```

### Testing

Use the Stream Deck software's developer mode to reload the plugin during development.

## License

This plugin is part of the OnlyT project.
