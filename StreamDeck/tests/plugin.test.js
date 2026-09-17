'use strict';

// Run with: node --test StreamDeck/tests/plugin.test.js
// Exercises the plugin against a fake OnlyT HTTP API and a fake Stream Deck socket.

const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');

const {
    ACTIONS,
    OnlyTClient,
    describeError,
    normaliseConnection,
    performAction,
} = require('../com.onlyt.timer.sdPlugin/onlyt');
const { Plugin, parseArgs } = require('../com.onlyt.timer.sdPlugin/plugin');

/** A minimal stand-in for OnlyT's API, mirroring the rules OperatorPageViewModel applies. */
function createFakeOnlyT({ accessCode = '', apiEnabled = true, bellEnabled = true } = {}) {
    const onlyt = {
        talks: [
            { talkId: 1, talkTitle: 'Opening Comments', completedTimeSecs: null },
            { talkId: 2, talkTitle: 'Treasures', completedTimeSecs: null },
            { talkId: 3, talkTitle: 'Spiritual Gems', completedTimeSecs: null },
        ],
        runningTalkId: null,
        requests: [],
        bellRings: 0,
    };

    const server = http.createServer((req, res) => {
        onlyt.requests.push({ method: req.method, url: req.url, apiKey: req.headers['x-api-key'] });
        const json = (status, body) => {
            res.writeHead(status, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(body));
        };

        if (accessCode && req.headers['x-api-key'] !== accessCode) {
            return json(401, { error: 'Unauthorized' });
        }
        if (!apiEnabled) {
            return json(503, { error: 'API is disabled' });
        }

        const status = () => ({ talkId: onlyt.runningTalkId, isRunning: onlyt.runningTalkId !== null });
        const timerMatch = req.url.match(/^\/api\/v1\/timers\/(\d+)$/);

        if (req.method === 'GET' && req.url === '/api/v1/system/') {
            return json(200, { onlyTVersion: '2.5.0.11' });
        }
        if (req.method === 'GET' && req.url === '/api/v1/timers/') {
            return json(200, { status: status(), timerInfo: onlyt.talks });
        }
        if (timerMatch && (req.method === 'POST' || req.method === 'DELETE')) {
            const talkId = Number(timerMatch[1]);
            const talk = onlyt.talks.find(t => t.talkId === talkId);
            let success = false;
            if (talk && req.method === 'POST' && onlyt.runningTalkId === null) {
                onlyt.runningTalkId = talkId;
                success = true;
            } else if (talk && req.method === 'DELETE' && onlyt.runningTalkId === talkId) {
                onlyt.runningTalkId = null;
                talk.completedTimeSecs = 60;
                success = true;
            }
            return json(200, { success, talkId, currentStatus: status() });
        }
        if (req.method === 'POST' && req.url === '/api/v1/bell/') {
            if (bellEnabled) {
                onlyt.bellRings++;
            }
            return json(200, { success: bellEnabled, message: bellEnabled ? 'Bell triggered' : 'Bell is disabled in settings' });
        }

        json(404, { error: 'Unknown endpoint' });
    });

    return new Promise(resolve => {
        server.listen(0, '127.0.0.1', () => {
            onlyt.connection = { host: '127.0.0.1', port: server.address().port, accessCode };
            onlyt.close = () => new Promise(done => server.close(done));
            resolve(onlyt);
        });
    });
}

async function withOnlyT(options, body) {
    const onlyt = await createFakeOnlyT(options);
    try {
        await body(onlyt, new OnlyTClient(onlyt.connection));
    } finally {
        await onlyt.close();
    }
}

test('normaliseConnection fills in defaults and rejects bad ports', () => {
    assert.deepEqual(normaliseConnection({}), { host: 'localhost', port: 8096, accessCode: '' });
    assert.deepEqual(normaliseConnection({ host: ' 10.0.0.5 ', port: '9000', accessCode: ' abc ' }),
        { host: '10.0.0.5', port: 9000, accessCode: 'abc' });
    assert.equal(normaliseConnection({ port: 70000 }).port, 8096);
    assert.equal(new OnlyTClient({ host: '::1', port: 8096 }).baseUrl, 'http://[::1]:8096');
});

test('Start with automatic talk starts the first talk not yet timed', () => withOnlyT({}, async (onlyt, client) => {
    onlyt.talks[0].completedTimeSecs = 120;
    const result = await performAction(ACTIONS.start, {}, client);
    assert.deepEqual(result, { ok: true, running: true });
    assert.equal(onlyt.runningTalkId, 2);
}));

test('Start fails when another talk is running, but not for the same talk', () => withOnlyT({}, async (onlyt, client) => {
    onlyt.runningTalkId = 1;
    assert.equal((await performAction(ACTIONS.start, { talkId: 2 }, client)).ok, false);
    assert.equal((await performAction(ACTIONS.start, { talkId: 1 }, client)).ok, true);
    assert.equal(onlyt.runningTalkId, 1);
}));

test('Start reports a configured talk that is no longer in the schedule', () => withOnlyT({}, async (onlyt, client) => {
    const result = await performAction(ACTIONS.start, { talkId: 99 }, client);
    assert.equal(result.ok, false);
    assert.match(result.reason, /no longer/);
    assert.equal(onlyt.runningTalkId, null);
}));

test('Stop stops the running talk, and fails when nothing is running', () => withOnlyT({}, async (onlyt, client) => {
    assert.equal((await performAction(ACTIONS.stop, {}, client)).ok, false);
    onlyt.runningTalkId = 3;
    assert.equal((await performAction(ACTIONS.stop, { talkId: 2 }, client)).ok, false, 'a key set to another talk leaves it running');
    assert.equal((await performAction(ACTIONS.stop, {}, client)).ok, true);
    assert.equal(onlyt.runningTalkId, null);
}));

test('Toggle stops a running talk and starts one otherwise', () => withOnlyT({}, async (onlyt, client) => {
    assert.equal((await performAction(ACTIONS.toggle, { talkId: 2 }, client)).ok, true);
    assert.equal(onlyt.runningTalkId, 2);
    assert.equal((await performAction(ACTIONS.toggle, { talkId: 3 }, client)).ok, true);
    assert.equal(onlyt.runningTalkId, null);
}));

test('Next stops the running talk and starts the one after it', () => withOnlyT({}, async (onlyt, client) => {
    assert.equal((await performAction(ACTIONS.next, {}, client)).ok, true);
    assert.equal(onlyt.runningTalkId, 1);
    assert.equal((await performAction(ACTIONS.next, {}, client)).ok, true);
    assert.equal(onlyt.runningTalkId, 2);
    assert.equal(onlyt.talks[0].completedTimeSecs, 60);

    onlyt.runningTalkId = 3;
    assert.deepEqual(await performAction(ACTIONS.next, {}, client), { ok: true, running: false }, 'stopping the last talk is enough');
    assert.equal(onlyt.runningTalkId, null);
}));

test('Bell reads the success field of the response', () => withOnlyT({}, async (onlyt, client) => {
    assert.deepEqual(await performAction(ACTIONS.bell, {}, client), { ok: true });
    assert.equal(onlyt.bellRings, 1);
}));

test('Bell reports OnlyT\'s reason when it is disabled', () => withOnlyT({ bellEnabled: false }, async (onlyt, client) => {
    assert.deepEqual(await performAction(ACTIONS.bell, {}, client), { ok: false, reason: 'Bell is disabled in settings' });
}));

test('The access code is sent in the X-Api-Key header', () => withOnlyT({ accessCode: 'secret' }, async (onlyt, client) => {
    await performAction(ACTIONS.start, {}, client);
    assert.ok(onlyt.requests.every(r => r.apiKey === 'secret'));

    const wrong = new OnlyTClient({ ...onlyt.connection, accessCode: 'nope' });
    await assert.rejects(() => wrong.getState(), error => /access code/.test(describeError(error)));
}));

test('A disabled API and an unreachable OnlyT give helpful errors', async () => {
    await withOnlyT({ apiEnabled: false }, async (onlyt, client) => {
        await assert.rejects(() => client.getSystem(), error => /Remote apps/.test(describeError(error)));
    });

    const onlyt = await createFakeOnlyT();
    await onlyt.close();
    const client = new OnlyTClient(onlyt.connection, fetch, 1000);
    await assert.rejects(() => client.getState(), error => /Can't reach OnlyT/.test(describeError(error)));
});

/** Records what the plugin sends and lets the test deliver Stream Deck events. */
class FakeStreamDeckSocket extends EventTarget {
    constructor() {
        super();
        this.readyState = 1;
        this.sent = [];
    }

    send(data) {
        this.sent.push(JSON.parse(data));
    }

    open() {
        this.dispatchEvent(new Event('open'));
    }

    deliver(message) {
        const event = new Event('message');
        event.data = JSON.stringify(message);
        this.dispatchEvent(event);
    }

    async waitFor(predicate, timeoutMs = 2000) {
        const started = Date.now();
        while (Date.now() - started < timeoutMs) {
            const match = this.sent.find(predicate);
            if (match) {
                return match;
            }
            await new Promise(resolve => setTimeout(resolve, 10));
        }
        assert.fail(`Timed out waiting for a message; sent: ${JSON.stringify(this.sent)}`);
    }
}

function startPlugin() {
    const socket = new FakeStreamDeckSocket();
    const args = parseArgs(['-port', '28196', '-pluginUUID', 'plugin-uuid', '-registerEvent', 'registerPlugin', '-info', '{}']);
    const plugin = new Plugin(args, url => {
        assert.equal(url, 'ws://127.0.0.1:28196');
        return socket;
    });
    socket.open();
    return { plugin, socket };
}

test('The plugin registers and asks for the shared connection settings', () => {
    const { plugin, socket } = startPlugin();
    assert.deepEqual(socket.sent, [
        { event: 'registerPlugin', uuid: 'plugin-uuid' },
        { event: 'getGlobalSettings', context: 'plugin-uuid' },
    ]);
    plugin.dispose();
});

test('Key presses use the global connection and show OK or an alert', () => withOnlyT({ accessCode: 'code' }, async (onlyt) => {
    const { plugin, socket } = startPlugin();
    socket.deliver({ event: 'didReceiveGlobalSettings', payload: { settings: onlyt.connection } });

    socket.deliver({ event: 'keyDown', action: ACTIONS.start, context: 'key1', payload: { settings: {} } });
    await socket.waitFor(m => m.event === 'showOk' && m.context === 'key1');
    assert.equal(onlyt.runningTalkId, 1);

    socket.deliver({ event: 'keyDown', action: ACTIONS.start, context: 'key2', payload: { settings: { talkId: 2 } } });
    await socket.waitFor(m => m.event === 'showAlert' && m.context === 'key2');
    plugin.dispose();
}));

test('Toggle keys follow the timer state, and polling stops when they disappear', () => withOnlyT({}, async (onlyt) => {
    const { plugin, socket } = startPlugin();
    socket.deliver({ event: 'didReceiveGlobalSettings', payload: { settings: onlyt.connection } });

    onlyt.runningTalkId = 2;
    socket.deliver({ event: 'willAppear', action: ACTIONS.toggle, context: 'toggle', payload: { settings: {}, state: 0 } });
    await socket.waitFor(m => m.event === 'setState' && m.context === 'toggle' && m.payload.state === 1);
    assert.ok(plugin.pollTimer, 'polls while a toggle key is visible');

    socket.deliver({ event: 'keyDown', action: ACTIONS.toggle, context: 'toggle', payload: { settings: {} } });
    await socket.waitFor(m => m.event === 'setState' && m.context === 'toggle' && m.payload.state === 0);
    assert.equal(onlyt.runningTalkId, null);

    // OnlyT's status can lag just after a start; the key keeps the expected state meanwhile
    socket.sent.length = 0;
    const realGetState = OnlyTClient.prototype.getState;
    OnlyTClient.prototype.getState = async function () {
        const state = await realGetState.call(this);
        return onlyt.laggingStatus ? { ...state, isRunning: false, runningTalkId: null } : state;
    };
    try {
        socket.deliver({ event: 'keyDown', action: ACTIONS.toggle, context: 'toggle', payload: { settings: {} } });
        await socket.waitFor(m => m.event === 'setState' && m.payload.state === 1);
        onlyt.laggingStatus = true;
        await plugin.refreshToggles();
        assert.ok(!socket.sent.some(m => m.event === 'setState' && m.payload.state === 0), 'no flicker back to Start');
    } finally {
        OnlyTClient.prototype.getState = realGetState;
    }

    socket.deliver({ event: 'willDisappear', action: ACTIONS.toggle, context: 'toggle', payload: {} });
    assert.equal(plugin.pollTimer, null);
    plugin.dispose();
}));

test('The property inspector can test the connection and load talks', () => withOnlyT({}, async (onlyt) => {
    const { plugin, socket } = startPlugin();
    const fromInspector = payload => socket.deliver({ event: 'sendToPlugin', action: ACTIONS.start, context: 'key1', payload });

    fromInspector({ request: 'connectionChanged', connection: onlyt.connection });
    fromInspector({ request: 'testConnection' });
    const connection = await socket.waitFor(m => m.event === 'sendToPropertyInspector' && m.payload.response === 'connection');
    assert.deepEqual(connection, {
        event: 'sendToPropertyInspector', action: ACTIONS.start, context: 'key1',
        payload: { response: 'connection', ok: true, version: '2.5.0.11' },
    });

    fromInspector({ request: 'getTalks' });
    const talks = await socket.waitFor(m => m.event === 'sendToPropertyInspector' && m.payload.response === 'talks');
    assert.deepEqual(talks.payload.talks.map(t => t.title), ['Opening Comments', 'Treasures', 'Spiritual Gems']);
    plugin.dispose();
}));
