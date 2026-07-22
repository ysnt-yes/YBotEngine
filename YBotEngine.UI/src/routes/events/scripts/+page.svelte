<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as monaco from 'monaco-editor';
    import { MonacoLanguageClient } from 'monaco-languageclient';
    import { WebSocketMessageReader, WebSocketMessageWriter } from 'vscode-ws-jsonrpc';

    let eventsList = $state([]);
    let selectedEventIndex = $state(0);
    let code = $state('// Type your script here...\n// Use the global "Data" variable.\n\n');

    let currentEvent = $derived(eventsList[selectedEventIndex] || null);

    let editorElement = $state<HTMLDivElement | null>(null);
    let editor: monaco.editor.IStandaloneCodeEditor | null = null;
    let languageClient: MonacoLanguageClient | null = null;
    let webSocket: WebSocket | null = null;

    $effect(() => {
        if (!editor || !currentEvent) return;

        const modelUri = monaco.Uri.parse(
            `file:///workspace/script.cs?payloadType=${currentEvent.payloadType}`
        );

        let model = monaco.editor.getModel(modelUri);
        if (!model) {
            model = monaco.editor.createModel(code, 'csharp', modelUri);
        }

        const oldModel = editor.getModel();
        editor.setModel(model);
        if (oldModel && oldModel !== model) oldModel.dispose();

        model.onDidChangeModelContent(() => {
            code = model.getValue();
        });
    });

    onMount(async () => {
        monaco.languages.register({ id: 'csharp', extensions: ['.cs', '.csx'] });

        if (editorElement) {
            editor = monaco.editor.create(editorElement, {
                theme: 'vs-dark',
                automaticLayout: true,
                minimap: { enabled: false }
            });
        }

        try {
            const res = await fetch('/api/events');
            eventsList = await res.json();
        } catch (err) {
            console.error('Failed to load event definitions', err);
        }

        const protocol = window.location.protocol === 'https:' ? 'wss' : 'ws';
        const sessionId = "session_" + Math.random().toString(36).substring(2, 15);

        const socketUrl = `${protocol}://${window.location.host}/api/lsp?lang=csharp&session=${sessionId}`;
        webSocket = new WebSocket(socketUrl);

        webSocket.onopen = () => {
            if (!webSocket) return;

            const socketAdapter = {
                send: (content: string) => webSocket?.send(content),
                onMessage: (cb: (data: any) => void) => webSocket!.onmessage = event => cb(event.data),
                onError: (cb: (err: any) => void) => webSocket!.onerror = event => cb(event),
                onClose: (cb: (ev: any) => void) => webSocket!.onclose = event => cb(event),
                close: () => webSocket?.close()
            };

            const reader = new WebSocketMessageReader(socketAdapter);
            const writer = new WebSocketMessageWriter(socketAdapter);

            languageClient = new MonacoLanguageClient({
                name: 'C# Global Script LSP',
                clientOptions: {
                    documentSelector: ['csharp']
                },
                connectionProvider: {
                    get: () => Promise.resolve({ reader, writer })
                }
            });

            languageClient.start();
        };
    });

    onDestroy(() => {
        languageClient?.stop();
        webSocket?.close();
        editor?.getModel()?.dispose();
        editor?.dispose();
    });
</script>

<div class="workspace">
    <header class="toolbar">
        <label for="event-select">Triggering Event Context:</label>
        <select id="event-select" bind:value={selectedEventIndex}>
            {#each eventsList as evt, index}
                <option value={index}>{evt.name} ({evt.payloadType})</option>
            {/each}
        </select>
    </header>

    <div bind:this={editorElement} class="editor-frame"></div>
</div>

<style>
    .workspace {
        display: flex;
        flex-direction: column;
        height: 100vh;
        font-family: sans-serif;
    }
    .toolbar {
        padding: 12px;
        background: #1e1e1e;
        color: #fff;
        display: flex;
        align-items: center;
        gap: 10px;
        border-bottom: 1px solid #333;
    }
    select {
        padding: 6px 12px;
        background: #2d2d2d;
        color: white;
        border: 1px solid #555;
        border-radius: 4px;
    }
    .editor-frame {
        flex-grow: 1;
        width: 100%;
    }
</style>
