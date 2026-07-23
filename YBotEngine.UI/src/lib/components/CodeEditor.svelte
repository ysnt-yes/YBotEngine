<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import { EditorState, Compartment } from '@codemirror/state';
    import { EditorView, lineNumbers } from '@codemirror/view';
    import { autocompletion } from '@codemirror/autocomplete';
    import { linter } from '@codemirror/lint';
    import { csharp } from '@replit/codemirror-lang-csharp';
    import * as themes from '@uiw/codemirror-themes-all';
    import { Select } from 'bits-ui';

    import { editorCache } from '$lib/editorCache';

    interface Props {
        documentName: string;
    }
    let { documentName }: Props = $props();

    let editorElement = $state<HTMLElement | null>(null);
    let view: EditorView | null = null;
    const themeCompartment = new Compartment();

    let events = $state<Array<{ name: string; payloadType: string }>>([]);
    let selectedEventName = $state('');
    let selectedPayloadType = $state('');
    let isLoading = $state(true);
    let isSaving = $state(false);
    let userSessionId = $state('');

    let currentThemeId = $state(
        (typeof window !== 'undefined' && localStorage.getItem('codemirror_theme')) || 'oneDark'
    );

    const cleanThemesList = Object.keys(themes)
        .filter(key => key !== 'default' && typeof themes[key] !== 'function' && !/(style|settings|init)/i.test(key))
        .sort()
        .map(key => ({ id: key, name: key.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase()) }));

    const fullSessionKey = $derived(`${userSessionId}_${documentName}`);

    const isCurrentFileDirty = $derived(editorCache.get(documentName)?.isDirty ?? false);

    let currentEventLabel = $derived(events.find(e => e.payloadType === selectedPayloadType)?.name || 'Select Event...');
    let currentThemeLabel = $derived(cleanThemesList.find(t => t.id === currentThemeId)?.name || 'Select Theme...');

    $effect(() => {
        if (documentName) {
            loadDocumentWorkspace(documentName);
        }
    });

    async function loadDocumentWorkspace(scriptId: string) {
        isLoading = true;
        const cachedFile = editorCache.get(scriptId);

        if (cachedFile) {
            initEditor(cachedFile.targetEvent, cachedFile.codeText);
            isLoading = false;
            return;
        }

        try {
            const res = await fetch(`/api/scripts/${scriptId}`);
            const data = await res.json();

            editorCache.set(scriptId, {
                scriptId: data.scriptId,
                codeText: data.initialCode,
                targetEvent: data.eventName
            });

            initEditor(data.eventName, data.initialCode);
        } catch (err) {
            console.error(err);
        } finally {
            isLoading = false;
        }
    }

    onMount(async () => {
        userSessionId = crypto.randomUUID();
        try {
            const res = await fetch(`${apiBaseUrl}/api/events`);
            events = await res.json();
            if (events.length > 0 && !selectedPayloadType) {
                selectedEventName = events[0].name;
                selectedPayloadType = events[0].payloadType;
            }
        } catch (err) {
            console.error(err);
        }
    });

    onDestroy(() => {
        if (view) view.destroy();
    });

    $effect(() => {
        if (view && currentThemeId) {
            const targetThemeExport = (themes as any)[currentThemeId];
            if (targetThemeExport) {
                const resolvedExtension = typeof targetThemeExport === 'function' ? targetThemeExport() : targetThemeExport;
                const validExtension = resolvedExtension?.extension || resolvedExtension?.theme || resolvedExtension;
                view.dispatch({ effects: themeCompartment.reconfigure(validExtension) });
            }
        }
    });

    $effect(() => {
        if (currentThemeId) {
            localStorage.setItem('codemirror_theme', currentThemeId);
        }
    });

    function initEditor(eventName: string, initialCodeText: string) {
        if (view) view.destroy();
        if (!editorElement) return;

        const lspAutocomplete = autocompletion({
            activateOnTyping: true,
            override: [async (context) => {
                const match = context.matchBefore(/\w+$/) || context.matchBefore(/\.\w*$/);
                if (!context.explicit && !match) return null;
                try {
                    const response = await fetch(`${apiBaseUrl}/api/lsp?lang=csharp&session=${fullSessionKey}`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ code: context.state.doc.toString(), cursorPosition: context.pos, payloadType: selectedPayloadType })
                    });
                    const data = await response.json();
                    return {
                        from: match ? match.from : context.pos,
                        options: data.completions.map((c: any) => ({ label: c.label, type: c.type, detail: c.detail })),
                        validForChars: /^[\w]$/
                    };
                } catch (err) { return null; }
            }]
        });

        const lspLinter = linter(async (editorView) => {
            try {
                const response = await fetch(`${apiBaseUrl}/api/lsp?lang=csharp&session=${fullSessionKey}`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ code: editorView.state.doc.toString(), cursorPosition: editorView.state.selection.main.head, payloadType: selectedPayloadType })
                });
                const data = await response.json();
                return data.errors.map((err: any) => ({ from: err.from, to: err.to, severity: err.severity === 'error' ? 'error' : 'warning', message: err.message }));
            } catch (err) { return []; }
        }, { delay: 400 });

        const targetThemeExport = (themes as any)[currentThemeId];
        const initialThemeExtension = typeof targetThemeExport === 'function' ? targetThemeExport() : targetThemeExport;

        view = new EditorView({
            state: EditorState.create({
                doc: initialCodeText,
                extensions: [
                    themeCompartment.of(initialThemeExtension || []),
                    csharp(),
                    lspAutocomplete,
                    lspLinter,
                    lineNumbers(),
                    EditorView.updateListener.of((update) => {
                        if (update.docChanged) {
                            editorCache.updateCode(documentName, update.state.doc.toString());
                        }
                    })
                ]
            }),
            parent: editorElement
        });
    }

    function handleEventChange(value: string | undefined) {
        if (!value) return;
        const matched = events.find(ev => ev.payloadType === value);
        if (matched) {
            selectedEventName = matched.name;
            selectedPayloadType = matched.payloadType;
        }
    }

    async function handleSave() {
        if (!view) return;
        isSaving = true;
        const cleanCodeBody = view.state.doc.toString();
        try {
            await fetch(`${apiBaseUrl}/api/scripts/save`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ scriptId: documentName, eventName: selectedEventName, code: cleanCodeBody })
            });
            editorCache.markClean(documentName);
        } catch (err) {
            console.error(err);
        } finally {
            isSaving = false;
        }
    }
</script>

<div class="editor-wrapper">
    <header class="editor-header">

        <div class="select-group">
            <span>⚡ Event:</span>
            <Select.Root type="single" disabled={isLoading} value={selectedPayloadType} onValueChange={handleEventChange}>
                <Select.Trigger>
                    {currentEventLabel}
                </Select.Trigger>
                <Select.Content>
                    {#each events as ev}
                        <Select.Item value={ev.payloadType} label={ev.name}>
                            {ev.name}
                        </Select.Item>
                    {/each}
                </Select.Content>
            </Select.Root>
        </div>

        <div class="select-group">
            <span>🎨 Theme:</span>
            <Select.Root type="single" value={currentThemeId} onValueChange={(val) => { if(val) currentThemeId = val; }}>
                <Select.Trigger>
                    {currentThemeLabel}
                </Select.Trigger>
                <Select.Content>
                    {#each cleanThemesList as theme}
                        <Select.Item value={theme.id} label={theme.name}>
                            {theme.name}
                        </Select.Item>
                    {/each}
                </Select.Content>
            </Select.Root>
        </div>

        <button onclick={handleSave} disabled={isSaving || isLoading}>
            {#if isSaving}
                Saving...
            {:else}
                Save Script {isCurrentFileDirty ? '*' : ''}
            {/if}
        </button>
    </header>

    <div bind:this={editorElement}></div>
</div>
