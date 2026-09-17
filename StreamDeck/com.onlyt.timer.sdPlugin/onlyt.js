'use strict';

/**
 * OnlyT API client and the Stream Deck action logic, kept free of any Stream Deck plumbing so it
 * can be tested with a fake fetch (see StreamDeck/tests).
 *
 * API endpoints used:
 *   GET    /api/v1/system/       - OnlyT version and API state (connection test)
 *   GET    /api/v1/timers/       - talks and current timer status
 *   POST   /api/v1/timers/{id}   - start the timer for a talk
 *   DELETE /api/v1/timers/{id}   - stop the timer for a talk
 *   POST   /api/v1/bell/         - ring the bell
 */

const ACTIONS = Object.freeze({
    start: 'com.onlyt.timer.start',
    stop: 'com.onlyt.timer.stop',
    toggle: 'com.onlyt.timer.toggle',
    bell: 'com.onlyt.timer.bell',
    next: 'com.onlyt.timer.next',
});

const DEFAULT_HOST = 'localhost';
const DEFAULT_PORT = 8096;
const REQUEST_TIMEOUT_MS = 3000;

/** Connection settings shared by every key, with defaults for anything missing or invalid. */
function normaliseConnection(settings) {
    const s = settings || {};
    const port = Number.parseInt(s.port, 10);
    return {
        host: typeof s.host === 'string' && s.host.trim() ? s.host.trim() : DEFAULT_HOST,
        port: Number.isInteger(port) && port > 0 && port <= 65535 ? port : DEFAULT_PORT,
        accessCode: typeof s.accessCode === 'string' ? s.accessCode.trim() : '',
    };
}

class OnlyTError extends Error {
    /**
     * @param {'unreachable'|'disabled'|'unauthorized'|'throttled'|'http'} kind
     */
    constructor(kind, message) {
        super(message);
        this.name = 'OnlyTError';
        this.kind = kind;
    }
}

/** A short explanation for the property inspector. */
function describeError(error) {
    if (!(error instanceof OnlyTError)) {
        return `Unexpected error: ${error && error.message ? error.message : error}`;
    }

    switch (error.kind) {
        case 'unreachable':
            return 'Can\'t reach OnlyT. Check that it\'s running and the host and port match OnlyT\'s settings.';
        case 'disabled':
            return 'OnlyT\'s API is turned off. Enable it under Remote apps in OnlyT\'s settings.';
        case 'unauthorized':
            return 'The access code doesn\'t match the one in OnlyT\'s settings.';
        case 'throttled':
            return 'OnlyT is limiting requests. Wait a minute, or turn off Throttled in OnlyT\'s settings.';
        default:
            return error.message;
    }
}

class OnlyTClient {
    constructor(connection, fetchImpl = globalThis.fetch, timeoutMs = REQUEST_TIMEOUT_MS) {
        this.connection = normaliseConnection(connection);
        this.fetch = fetchImpl;
        this.timeoutMs = timeoutMs;
    }

    get baseUrl() {
        const { host, port } = this.connection;
        // Bracket IPv6 literals
        const hostPart = host.includes(':') && !host.startsWith('[') ? `[${host}]` : host;
        return `http://${hostPart}:${port}`;
    }

    async request(method, path) {
        const headers = { Accept: 'application/json' };
        if (this.connection.accessCode) {
            headers['X-Api-Key'] = this.connection.accessCode;
        }

        let response;
        try {
            response = await this.fetch(`${this.baseUrl}${path}`, {
                method,
                headers,
                signal: AbortSignal.timeout(this.timeoutMs),
            });
        } catch (error) {
            throw new OnlyTError('unreachable', `Can't reach OnlyT at ${this.baseUrl}: ${error.message}`);
        }

        switch (response.status) {
            case 401:
                throw new OnlyTError('unauthorized', 'Access code required or incorrect');
            case 429:
                throw new OnlyTError('throttled', 'Too many requests');
            case 503:
                throw new OnlyTError('disabled', 'OnlyT API is disabled');
        }

        if (!response.ok) {
            throw new OnlyTError('http', `OnlyT returned HTTP ${response.status} for ${method} ${path}`);
        }

        try {
            return await response.json();
        } catch {
            throw new OnlyTError('http', `OnlyT returned an invalid response for ${method} ${path}`);
        }
    }

    getSystem() {
        return this.request('GET', '/api/v1/system/');
    }

    async getState() {
        return parseState(await this.request('GET', '/api/v1/timers/'));
    }

    startTalk(talkId) {
        return this.request('POST', `/api/v1/timers/${talkId}`);
    }

    stopTalk(talkId) {
        return this.request('DELETE', `/api/v1/timers/${talkId}`);
    }

    ringBell() {
        return this.request('POST', '/api/v1/bell/');
    }
}

function parseState(data) {
    const status = (data && data.status) || {};
    const talks = Array.isArray(data && data.timerInfo) ? data.timerInfo : [];
    return {
        isRunning: status.isRunning === true,
        runningTalkId: status.isRunning === true && Number.isInteger(status.talkId) ? status.talkId : null,
        talks: talks.map(talk => ({
            id: talk.talkId,
            title: talk.talkTitle || `Talk ${talk.talkId}`,
            // OnlyT records how long a talk ran once its timer has been stopped
            timed: Number.isInteger(talk.completedTimeSecs) && talk.completedTimeSecs > 0,
        })),
    };
}

/** The first talk that hasn't been timed yet. */
function firstUntimedTalk(state) {
    return state.talks.find(talk => !talk.timed) || null;
}

/** The talk after the given one in the schedule. */
function talkAfter(state, talkId) {
    const index = state.talks.findIndex(talk => talk.id === talkId);
    return index >= 0 && index + 1 < state.talks.length ? state.talks[index + 1] : null;
}

/** The talk configured on the key, if it still exists in OnlyT's schedule. */
function configuredTalkId(settings, state) {
    const id = Number.parseInt(settings && settings.talkId, 10);
    if (!Number.isInteger(id)) {
        return null;
    }
    return state.talks.some(talk => talk.id === id) ? id : undefined;
}

/**
 * { ok, reason } for an API command response, with the reason only kept on failure. On success,
 * `running` is whether a timer should now be running: OnlyT starts timers at the next second
 * boundary, so its status can lag behind for a moment.
 */
function outcome(result, reason, running) {
    const ok = result?.success === true;
    if (!ok) {
        return { ok, reason: result?.message || result?.errorMessage || reason };
    }
    return running === undefined ? { ok } : { ok, running };
}

async function start(client, settings, state) {
    const configured = configuredTalkId(settings, state);
    if (configured === undefined) {
        return { ok: false, reason: 'The talk chosen for this key is no longer in OnlyT\'s schedule' };
    }

    if (state.isRunning) {
        // Pressing Start for the talk that's already running isn't an error
        return configured !== null && configured === state.runningTalkId
            ? { ok: true, running: true }
            : { ok: false, reason: 'Another talk is already running' };
    }

    const talk = configured !== null ? { id: configured } : firstUntimedTalk(state);
    if (!talk) {
        return { ok: false, reason: 'Every talk has already been timed' };
    }

    return outcome(await client.startTalk(talk.id), 'OnlyT didn\'t start the timer', true);
}

async function stop(client, settings, state) {
    const configured = configuredTalkId(settings, state);
    if (configured === undefined) {
        return { ok: false, reason: 'The talk chosen for this key is no longer in OnlyT\'s schedule' };
    }

    if (!state.isRunning) {
        return { ok: false, reason: 'No timer is running' };
    }

    if (configured !== null && configured !== state.runningTalkId) {
        return { ok: false, reason: 'A different talk is running' };
    }

    return outcome(await client.stopTalk(state.runningTalkId), 'OnlyT didn\'t stop the timer', false);
}

function toggle(client, settings, state) {
    // Stop whatever is running, even if the key is set to a different talk
    return state.isRunning ? stop(client, {}, state) : start(client, settings, state);
}

async function ringBell(client) {
    return outcome(await client.ringBell(), 'OnlyT didn\'t ring the bell');
}

async function next(client, state) {
    let nextTalk;
    if (state.isRunning) {
        const stopped = outcome(await client.stopTalk(state.runningTalkId), 'OnlyT didn\'t stop the running timer');
        if (!stopped.ok) {
            return stopped;
        }

        nextTalk = talkAfter(state, state.runningTalkId);
        if (!nextTalk) {
            // That was the last talk, so stopping it is all there is to do
            return { ok: true, running: false };
        }
    } else {
        nextTalk = firstUntimedTalk(state);
        if (!nextTalk) {
            return { ok: false, reason: 'Every talk has already been timed' };
        }
    }

    return outcome(await client.startTalk(nextTalk.id), 'OnlyT didn\'t start the next talk', true);
}

/**
 * Runs a key press. Resolves to { ok, reason?, running? }; API failures reject with an OnlyTError.
 */
async function performAction(action, settings, client) {
    if (action === ACTIONS.bell) {
        return ringBell(client);
    }

    const state = await client.getState();
    switch (action) {
        case ACTIONS.start:
            return start(client, settings, state);
        case ACTIONS.stop:
            return stop(client, settings, state);
        case ACTIONS.toggle:
            return toggle(client, settings, state);
        case ACTIONS.next:
            return next(client, state);
        default:
            return { ok: false, reason: `Unknown action ${action}` };
    }
}

module.exports = {
    ACTIONS,
    DEFAULT_HOST,
    DEFAULT_PORT,
    OnlyTClient,
    OnlyTError,
    describeError,
    firstUntimedTalk,
    normaliseConnection,
    parseState,
    performAction,
    talkAfter,
};
