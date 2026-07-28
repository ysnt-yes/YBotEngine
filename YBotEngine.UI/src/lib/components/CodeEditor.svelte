<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { EditorState, Compartment } from '@codemirror/state';
	import { EditorView, lineNumbers } from '@codemirror/view';
	import { autocompletion } from '@codemirror/autocomplete';
	import { linter } from '@codemirror/lint';
	import { csharp } from '@replit/codemirror-lang-csharp';
	import * as themes from '@uiw/codemirror-themes-all';

	import { type ApiEventsResponse, type ApiLspResponse, type ScriptTab } from '$lib/api';

	let {
		tab = $bindable(),
		events = [],
		isLoading = false,
		onSave = async () => {}
	}: {
		tab: ScriptTab;
		events: ApiEventsResponse[];
		isLoading: boolean;
		onSave?: () => Promise<void> | void;
	} = $props();

	let editorElement = $state<HTMLElement | null>(null);
	let view: EditorView | null = null;
	const themeCompartment = new Compartment();
	let isSaving = $state<boolean>(false);

	let fullSessionKey = $derived(`${tab.eventName}_${tab.id}`);

	let currentThemeId = $state<string>(
		(typeof window !== 'undefined' && localStorage.getItem('codemirror_theme')) || 'oneDark'
	);

	const cleanThemesList = Object.keys(themes)
		.filter((key) => {
			if (key === 'default' || typeof (themes as any)[key] === 'function') return false;
			if (/(style|settings|init)/i.test(key)) return false;
			return true;
		})
		.sort()
		.map((key) => ({
			id: key,
			name: key.replace(/([A-Z])/g, ' $1').replace(/^./, (str) => str.toUpperCase())
		}));

	$effect(() => {
		if (editorElement && !view) {
			initEditor();
		}
	});

	const lspAutocomplete = autocompletion({
		activateOnTyping: true,
		override: [
			async (context) => {
				const match = context.matchBefore(/\w+$/) || context.matchBefore(/\.\w*$/);
				if (!context.explicit && !match) return null;
				if (!context.explicit && match && match.text.length < 2 && !context.matchBefore(/\.\w*$/))
					return null;

				return {
					from: match ? match.from : context.pos,
					options: tab.lspData.completions,
					validForChars: /^[\w]$/
				};
			}
		]
	});

	const lspLinter = linter(() => {
		return tab.lspData.errors;
	});

	const stateUpdater = EditorView.updateListener.of((update) => {
		if (update.docChanged || update.selectionSet) {
			tab.docText = update.state.doc.toString();
			tab.cursorPosition = update.state.selection.main.head;
		}
	});

	let extensions = $derived([
		themeCompartment.of((themes as any)[currentThemeId] || []),
		csharp(),
		lspAutocomplete,
		lspLinter,
		stateUpdater,
		lineNumbers()
	]);

	$effect(() => {
		if (view && tab.id) {
			if (view.state.doc.toString() !== tab.docText) {
				const newState = EditorState.create({
					doc: tab.docText,
					extensions: extensions
				});

				view.setState(newState);

				view.dispatch({
					selection: { anchor: tab.cursorPosition }
				});
			}
		}
	});

	$effect(() => {
		if (currentThemeId) localStorage.setItem('codemirror_theme', currentThemeId);
	});

	$effect(() => {
		if (view && currentThemeId) {
			const target = (themes as any)[currentThemeId];
			if (target) view.dispatch({ effects: themeCompartment.reconfigure(target) });
		}
	});

	$effect(() => {
		const textToQuery = tab.docText;
		const cursorPos = tab.cursorPosition;
		const payload = tab.payloadType;

		if (!textToQuery || !payload) return;

		const timer = setTimeout(async () => {
			try {
				const response = await fetch(`/api/lsp?lang=csharp&session=${fullSessionKey}`, {
					method: 'POST',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify({
						code: textToQuery,
						cursorPosition: cursorPos,
						payloadType: payload
					})
				});

				const data: ApiLspResponse = await response.json();

				tab.lspData = {
					completions: (data.completions || []).map((c) => ({
						label: c.label,
						type: c.type,
						detail: c.detail
					})),
					errors: (data.errors || []).map((err) => ({
						from: Number(err.from),
						to: Number(err.to),
						severity: err.severity === 'error' ? 'error' : 'warning',
						message: err.message
					}))
				};
			} catch (err) {
				console.error('LSP Tab sync failure:', err);
			}
		}, 250);

		return () => clearTimeout(timer);
	});

	function initEditor() {
		if (!editorElement) return;

		view = new EditorView({
			state: EditorState.create({
				doc: tab.docText,
				extensions: extensions
			}),
			parent: editorElement
		});
	}

	function handleEventChange(e: Event) {
		const target = e.target as HTMLSelectElement;
		const matched = events.find((ev) => ev.payloadType === target.value);
		if (matched) {
			tab.eventName = matched.name;
			tab.payloadType = matched.payloadType;
		}
	}

	async function handleSaveClick() {
		isSaving = true;
		try {
			if (onSave) await onSave();
		} finally {
			isSaving = false;
		}
	}

	onDestroy(() => {
		if (view) view.destroy();
	});
</script>

<div class="editor-wrapper">
	<header class="editor-header">
		<div class="controls-left">
			<label for="event-select">⚡ Event Context:</label>
			{#if isLoading}
				<select id="event-select" disabled><option>Loading...</option></select>
			{:else}
				<select id="event-select" value={tab.payloadType} onchange={handleEventChange}>
					{#each events as ev (ev.name)}
						<option value={ev.payloadType}>{ev.name}</option>
					{/each}
				</select>
			{/if}

			<label for="theme-select" class="theme-label">🎨 Theme:</label>
			<select id="theme-select" bind:value={currentThemeId}>
				{#each cleanThemesList as theme (theme.id)}
					<option value={theme.id}>{theme.name}</option>
				{/each}
			</select>
		</div>

		<button class="save-btn" onclick={handleSaveClick} disabled={isSaving || isLoading}>
			{isSaving ? 'Saving...' : `Save Script`}
		</button>
	</header>

	<div class="editor-container" bind:this={editorElement}></div>
</div>

<style>
	.editor-wrapper {
		border: 1px solid #1c1e22;
		border-radius: 0 0 6px 6px;
		overflow: hidden;
		background: #282c34;
	}
	.editor-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		background: #282c34;
		padding: 10px;
		color: white;
		border-bottom: 1px solid #1c1e22;
	}
	.controls-left {
		display: flex;
		align-items: center;
		gap: 10px;
	}
	.theme-label {
		margin-left: 15px;
	}
	.editor-container {
		display: block;
		min-height: 350px;
	}
	.save-btn {
		padding: 6px 14px;
		background: #4c97ff;
		border: none;
		color: white;
		border-radius: 4px;
		cursor: pointer;
	}
	.save-btn:disabled {
		background: #5c6370;
		cursor: not-allowed;
	}
</style>
