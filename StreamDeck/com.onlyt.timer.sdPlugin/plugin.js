/**
 * OnlyT Timer Control - Stream Deck Plugin
 *
 * This plugin allows you to control the OnlyT meeting timer application
 * via its HTTP API from your Stream Deck.
 *
 * API Endpoints:
 * - GET  /api/v1/timers/      - Get all talks and current status
 * - POST /api/v1/timers/{id}  - Start timer for talk
 * - DELETE /api/v1/timers/{id} - Stop timer for talk
 * - POST /api/v1/bell/        - Ring the bell
 */

// Global WebSocket connection to Stream Deck
let websocket = null;

// Store action instances and their settings
const actionInstances = {};

// Default settings
const DEFAULT_HOST = 'localhost';
const DEFAULT_PORT = 8096;

// Current timer state (cached from polling)
let currentTimerState = {
    isRunning: false,
    activeTalkId: null,
    talks: []
};

// Polling interval for timer status
let pollInterval = null;

/**
 * Connect to Stream Deck
 */
function connectElgatoStreamDeckSocket(inPort, inPluginUUID, inRegisterEvent, inInfo) {
    websocket = new WebSocket(`ws://127.0.0.1:${inPort}`);

    websocket.onopen = () => {
        // Register plugin with Stream Deck
        const json = {
            event: inRegisterEvent,
            uuid: inPluginUUID
        };
        websocket.send(JSON.stringify(json));

        // Start polling timer status
        startPolling();
    };

    websocket.onmessage = (evt) => {
        const jsonObj = JSON.parse(evt.data);
        const event = jsonObj.event;
        const action = jsonObj.action;
        const context = jsonObj.context;
        const payload = jsonObj.payload || {};

        switch (event) {
            case 'keyDown':
                handleKeyDown(action, context, payload);
                break;

            case 'willAppear':
                // Action appeared on Stream Deck
                actionInstances[context] = {
                    action: action,
                    settings: payload.settings || {}
                };
                break;

            case 'willDisappear':
                // Action removed from Stream Deck
                delete actionInstances[context];
                break;

            case 'didReceiveSettings':
                // Settings updated from Property Inspector
                if (actionInstances[context]) {
                    actionInstances[context].settings = payload.settings || {};
                }
                break;
        }
    };

    websocket.onclose = () => {
        stopPolling();
    };
}

/**
 * Handle button press
 */
async function handleKeyDown(action, context, payload) {
    const settings = payload.settings || {};
    const host = settings.host || DEFAULT_HOST;
    const port = settings.port || DEFAULT_PORT;
    const baseUrl = `http://${host}:${port}`;

    try {
        switch (action) {
            case 'com.onlyt.timer.start':
                await startTimer(baseUrl, settings.talkId, context);
                break;

            case 'com.onlyt.timer.stop':
                await stopTimer(baseUrl, settings.talkId, context);
                break;

            case 'com.onlyt.timer.toggle':
                await toggleTimer(baseUrl, settings.talkId, context);
                break;

            case 'com.onlyt.timer.bell':
                await ringBell(baseUrl, context);
                break;

            case 'com.onlyt.timer.next':
                await startNextTalk(baseUrl, context);
                break;
        }
    } catch (error) {
        console.error('Error handling key press:', error);
        showAlert(context);
    }
}

/**
 * Start timer for a specific talk
 */
async function startTimer(baseUrl, talkId, context) {
    // If no specific talk ID, use the first available or currently selected
    if (!talkId && currentTimerState.talks.length > 0) {
        talkId = currentTimerState.talks[0].TalkId;
    }

    if (!talkId) {
        showAlert(context);
        return;
    }

    const response = await fetch(`${baseUrl}/api/v1/timers/${talkId}`, {
        method: 'POST'
    });

    if (response.ok) {
        showOk(context);
        await refreshTimerState(baseUrl);
    } else {
        showAlert(context);
    }
}

/**
 * Stop timer for a specific talk
 */
async function stopTimer(baseUrl, talkId, context) {
    // If no specific talk ID, stop the currently running one
    if (!talkId && currentTimerState.activeTalkId) {
        talkId = currentTimerState.activeTalkId;
    }

    if (!talkId) {
        showAlert(context);
        return;
    }

    const response = await fetch(`${baseUrl}/api/v1/timers/${talkId}`, {
        method: 'DELETE'
    });

    if (response.ok) {
        showOk(context);
        await refreshTimerState(baseUrl);
    } else {
        showAlert(context);
    }
}

/**
 * Toggle timer - start if stopped, stop if running
 */
async function toggleTimer(baseUrl, talkId, context) {
    if (currentTimerState.isRunning) {
        await stopTimer(baseUrl, currentTimerState.activeTalkId, context);
    } else {
        await startTimer(baseUrl, talkId, context);
    }

    // Update button state
    updateToggleState(context);
}

/**
 * Ring the bell
 */
async function ringBell(baseUrl, context) {
    const response = await fetch(`${baseUrl}/api/v1/bell/`, {
        method: 'POST'
    });

    const data = await response.json();

    if (data.Success) {
        showOk(context);
    } else {
        showAlert(context);
    }
}

/**
 * Start the next talk in the schedule
 */
async function startNextTalk(baseUrl, context) {
    await refreshTimerState(baseUrl);

    // Find the next talk that hasn't been completed
    const nextTalk = currentTimerState.talks.find(talk =>
        talk.CompletedTimeSecs === null || talk.CompletedTimeSecs === 0
    );

    if (nextTalk) {
        await startTimer(baseUrl, nextTalk.TalkId, context);
    } else {
        showAlert(context);
    }
}

/**
 * Refresh timer state from API
 */
async function refreshTimerState(baseUrl) {
    try {
        const response = await fetch(`${baseUrl}/api/v1/timers/`);
        const data = await response.json();

        currentTimerState.talks = data.TimerInfo || [];
        currentTimerState.isRunning = data.Status?.IsRunning || false;
        currentTimerState.activeTalkId = data.Status?.TalkId || null;

        // Update all toggle button states
        updateAllToggleStates();
    } catch (error) {
        console.error('Error refreshing timer state:', error);
    }
}

/**
 * Start polling for timer status
 */
function startPolling() {
    stopPolling();

    // Get base URL from first action instance settings
    const firstInstance = Object.values(actionInstances)[0];
    const settings = firstInstance?.settings || {};
    const host = settings.host || DEFAULT_HOST;
    const port = settings.port || DEFAULT_PORT;
    const baseUrl = `http://${host}:${port}`;

    // Poll every 2 seconds
    pollInterval = setInterval(() => {
        refreshTimerState(baseUrl);
    }, 2000);
}

/**
 * Stop polling
 */
function stopPolling() {
    if (pollInterval) {
        clearInterval(pollInterval);
        pollInterval = null;
    }
}

/**
 * Update toggle button state based on timer running state
 */
function updateToggleState(context) {
    const state = currentTimerState.isRunning ? 1 : 0;
    setActionState(context, state);
}

/**
 * Update all toggle button states
 */
function updateAllToggleStates() {
    for (const [context, instance] of Object.entries(actionInstances)) {
        if (instance.action === 'com.onlyt.timer.toggle') {
            updateToggleState(context);
        }
    }
}

/**
 * Set action state (for multi-state buttons)
 */
function setActionState(context, state) {
    if (websocket && websocket.readyState === WebSocket.OPEN) {
        websocket.send(JSON.stringify({
            event: 'setState',
            context: context,
            payload: { state: state }
        }));
    }
}

/**
 * Show OK checkmark on button
 */
function showOk(context) {
    if (websocket && websocket.readyState === WebSocket.OPEN) {
        websocket.send(JSON.stringify({
            event: 'showOk',
            context: context
        }));
    }
}

/**
 * Show alert on button (error indicator)
 */
function showAlert(context) {
    if (websocket && websocket.readyState === WebSocket.OPEN) {
        websocket.send(JSON.stringify({
            event: 'showAlert',
            context: context
        }));
    }
}

/**
 * Set title on button
 */
function setTitle(context, title) {
    if (websocket && websocket.readyState === WebSocket.OPEN) {
        websocket.send(JSON.stringify({
            event: 'setTitle',
            context: context,
            payload: { title: title }
        }));
    }
}
