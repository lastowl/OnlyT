'use strict';

/**
 * OnlyT Timer Control - Stream Deck plugin (Node.js).
 *
 * Stream Deck starts this with: -port <n> -pluginUUID <id> -registerEvent <name> -info <json>
 * and talks to it over a local WebSocket. Uses only Node's built-in fetch and WebSocket, so there
 * are no dependencies to install or bundle.
 */

const {
    ACTIONS,
    OnlyTClient,
    OnlyTError,
    describeError,
    normaliseConnection,
    performAction,
} = require('./onlyt');

const POLL_INTERVAL_MS = 2000;

// OnlyT starts a timer at the next second boundary and its status catches up after that, so after a
// key press trust the expected state for a little while rather than flicker back
const STATE_SETTLE_MS = 2500;

const TOGGLE_STATE_START = 0;
const TOGGLE_STATE_STOP = 1;

function parseArgs(argv) {
    const args = {};
    for (let i = 0; i + 1 < argv.length; i += 2) {
        args[argv[i].replace(/^-+/, '')] = argv[i + 1];
    }
    return args;
}

class Plugin {
    /**
     * @param {object} args parsed launch arguments
     * @param {(url: string) => WebSocket} [openSocket] for tests
     */
    constructor(args, openSocket = url => new WebSocket(url)) {
        this.pluginUUID = args.pluginUUID;
        this.registerEvent = args.registerEvent;
        this.connection = normaliseConnection({});
        this.instances = new Map(); // context -> { action, settings, state }
        this.pollTimer = null;
        this.refreshing = false;
        this.refreshAgain = false;
        this.settleUntil = 0;

        this.socket = openSocket(`ws://127.0.0.1:${args.port}`);
        this.socket.addEventListener('open', () => this.onOpen());
        this.socket.addEventListener('message', evt => this.onMessage(evt.data));
        this.socket.addEventListener('close', () => this.onClose());
    }

    get client() {
        return new OnlyTClient(this.connection);
    }

    onClose() {
        this.dispose();
        process.exit(0);
    }

    dispose() {
        clearInterval(this.pollTimer);
        this.pollTimer = null;
    }

    send(message) {
        if (this.socket.readyState === 1 /* OPEN */) {
            this.socket.send(JSON.stringify(message));
        }
    }

    onOpen() {
        this.send({ event: this.registerEvent, uuid: this.pluginUUID });
        this.send({ event: 'getGlobalSettings', context: this.pluginUUID });
    }

    onMessage(data) {
        let message;
        try {
            message = JSON.parse(data);
        } catch {
            return;
        }

        const { event, action, context, payload = {} } = message;
        switch (event) {
            case 'didReceiveGlobalSettings':
                this.setConnection(payload.settings);
                break;

            case 'willAppear':
                this.instances.set(context, { action, settings: payload.settings || {}, state: payload.state ?? null });
                this.updatePolling();
                break;

            case 'willDisappear':
                this.instances.delete(context);
                this.updatePolling();
                break;

            case 'didReceiveSettings': {
                const instance = this.instances.get(context);
                if (instance) {
                    instance.settings = payload.settings || {};
                }
                break;
            }

            case 'keyDown':
                this.onKeyDown(action, context, payload.settings || {});
                break;

            case 'sendToPlugin':
                this.onPropertyInspectorMessage(action, context, payload);
                break;
        }
    }

    setConnection(settings) {
        this.connection = normaliseConnection(settings);
        this.refreshToggles();
    }

    async onKeyDown(action, context, settings) {
        try {
            const result = await performAction(action, settings, this.client);
            this.send({ event: result.ok ? 'showOk' : 'showAlert', context });
            if (!result.ok) {
                console.warn(`${action}: ${result.reason}`);
            } else if (result.running !== undefined) {
                this.settleUntil = Date.now() + STATE_SETTLE_MS;
                this.setToggleStates(result.running);
                return;
            }
        } catch (error) {
            console.warn(`${action}: ${describeError(error)}`);
            this.send({ event: 'showAlert', context });
        }

        this.refreshToggles();
    }

    async onPropertyInspectorMessage(action, context, payload) {
        const reply = response => this.send({ event: 'sendToPropertyInspector', action, context, payload: response });

        switch (payload.request) {
            case 'connectionChanged':
                // The inspector also saves the global settings; applying them here avoids waiting
                // for Stream Deck to echo them back.
                this.setConnection(payload.connection);
                break;

            case 'testConnection':
                try {
                    const system = await this.client.getSystem();
                    reply({ response: 'connection', ok: true, version: system.onlyTVersion || null });
                } catch (error) {
                    reply({ response: 'connection', ok: false, message: describeError(error) });
                }
                break;

            case 'getTalks':
                try {
                    const state = await this.client.getState();
                    reply({ response: 'talks', ok: true, talks: state.talks.map(t => ({ id: t.id, title: t.title })) });
                } catch (error) {
                    reply({ response: 'talks', ok: false, message: describeError(error) });
                }
                break;
        }
    }

    hasToggles() {
        for (const instance of this.instances.values()) {
            if (instance.action === ACTIONS.toggle) {
                return true;
            }
        }
        return false;
    }

    setToggleStates(running) {
        const wanted = running ? TOGGLE_STATE_STOP : TOGGLE_STATE_START;
        for (const [context, instance] of this.instances) {
            if (instance.action === ACTIONS.toggle && instance.state !== wanted) {
                instance.state = wanted;
                this.send({ event: 'setState', context, payload: { state: wanted } });
            }
        }
    }

    /** Poll OnlyT only while a Toggle key is visible; it's the only key that shows timer state. */
    updatePolling() {
        if (this.hasToggles()) {
            if (!this.pollTimer) {
                this.pollTimer = setInterval(() => this.refreshToggles(), POLL_INTERVAL_MS);
            }
            this.refreshToggles();
        } else if (this.pollTimer) {
            clearInterval(this.pollTimer);
            this.pollTimer = null;
        }
    }

    async refreshToggles() {
        if (!this.hasToggles()) {
            return;
        }

        if (this.refreshing) {
            // A poll that started before a key press could report the old state; check again after it
            this.refreshAgain = true;
            return;
        }

        if (Date.now() < this.settleUntil) {
            return;
        }

        this.refreshing = true;
        try {
            const state = await this.client.getState();
            if (Date.now() >= this.settleUntil) {
                this.setToggleStates(state.isRunning);
            }
        } catch (error) {
            // OnlyT isn't reachable; leave the keys as they are until it is
            if (!(error instanceof OnlyTError)) {
                console.warn(`Refreshing timer state: ${error.message}`);
            }
        } finally {
            this.refreshing = false;
        }

        if (this.refreshAgain) {
            this.refreshAgain = false;
            this.refreshToggles();
        }
    }
}

if (require.main === module) {
    new Plugin(parseArgs(process.argv.slice(2)));
}

module.exports = { Plugin, parseArgs };
