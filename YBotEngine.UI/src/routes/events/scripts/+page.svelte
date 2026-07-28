<script lang="ts">
    import { onMount } from 'svelte';
    import { type ApiEventsResponse, type ScriptTab } from '$lib/api';
	import CodeEditor from '$lib/components/CodeEditor.svelte';

    let events = $state<ApiEventsResponse[]>([]);
    let isLoading = $state<boolean>(true);

    let tabs = $state<ScriptTab[]>([
        {
            id: 'script_abc123',
            title: 'AuthHandler.cs',
            eventName: 'MessageCreated',
            payloadType: 'Message',
            docText: '// Context: UserLogin\n',
            cursorPosition: 0,
            lspData: { completions: [], errors: [] }
        }
    ]);
    
    let activeTabId = $state<string>('script_abc123');

    let activeTabIdx = $derived(tabs.findIndex(t => t.id === activeTabId));
    let activeTab = $derived(tabs[activeTabIdx]);

    onMount(async () => {
        try {
            const res = await fetch('/api/events');
            events = await res.json();
        } catch (err) {
            console.error('Failed to load global context records:', err);
        } finally {
            isLoading = false;
        }
    });

    function addNewTab() {
        const id = `script_${crypto.randomUUID().slice(0, 8)}`;
        const fallbackEvent = events[0] || { name: 'MessageCreated', payloadType: 'Message' };
        
        tabs.push({
            id,
            title: `ScriptModule_${tabs.length + 1}.cs`,
            eventName: fallbackEvent.name,
            payloadType: fallbackEvent.payloadType,
            docText: `// Context: ${fallbackEvent.name}\n`,
            cursorPosition: 0,
            lspData: { completions: [], errors: [] }
        });
        
        activeTabId = id;
    }

    function closeTab(id: string, event: MouseEvent) {
        event.stopPropagation(); 
        
        const index = tabs.findIndex(t => t.id === id);
        if (index === -1) return;

        tabs.splice(index, 1);

        if (activeTabId === id && tabs.length > 0) {
            const nextActiveIndex = Math.max(0, index - 1);
            activeTabId = tabs[nextActiveIndex].id;
        }
    }

    async function handleSaveActiveScript() {
        if (!activeTab) return;
        try {
            await fetch('/api/scripts/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    scriptId: activeTab.id,
                    eventName: activeTab.eventName,
                    code: activeTab.docText
                })
            });
            alert(`Successfully saved ${activeTab.title}!`);
        } catch (err) {
            console.error('Save routine crashed:', err);
        }
    }
</script>

<main class="page-layout">
    <div class="header-container">
        <h1>Developer Script Workspace</h1>
        <button class="add-tab-btn" onclick={addNewTab}>＋ New Tab</button>
    </div>
    
    <div class="tabs-nav-bar">
        {#each tabs as tab (tab.id)}
            <div 
                class="tab-item" 
                class:active={tab.id === activeTabId} 
                onclick={() => activeTabId = tab.id}
                role="button"
                tabindex="0"
                onkeydown={(e) => e.key === 'Enter' && (activeTabId = tab.id)}
            >
                <span>📄 {tab.title}</span>
                <button class="close-btn" onclick={(e) => closeTab(tab.id, e)} aria-label="Close tab">×</button>
            </div>
        {/each}
    </div>

    {#if activeTab}
        <CodeEditor 
            bind:tab={tabs[activeTabIdx]} 
            {events}
            {isLoading}
            onSave={handleSaveActiveScript}
        />
    {:else}
        <div class="empty-state">No open file streams found. Select "New Tab" to get started.</div>
    {/if}
</main>

<style>
    .page-layout {
        padding: 2rem;
        max-width: 1200px;
        margin: 0 auto;
        font-family: system-ui, -apple-system, sans-serif;
    }
    .header-container {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1rem;
    }
    .add-tab-btn {
        background: #28a745;
        color: white;
        border: none;
        padding: 8px 14px;
        border-radius: 4px;
        cursor: pointer;
        font-weight: 500;
    }
    .add-tab-btn:hover {
        background: #218838;
    }
    .tabs-nav-bar {
        display: flex;
        gap: 4px;
        background: #20232a;
        padding: 6px 6px 0 6px;
        border-radius: 6px 6px 0 0;
    }
    .tab-item {
        display: flex;
        align-items: center;
        gap: 10px;
        background: #2c3036;
        color: #abb2bf;
        padding: 8px 16px;
        border-radius: 4px 4px 0 0;
        cursor: pointer;
        font-size: 13px;
        user-select: none;
    }
    .tab-item:hover {
        background: #353b45;
    }
    .tab-item.active {
        background: #282c34;
        color: #fff;
        font-weight: bold;
    }
    .close-btn {
        background: transparent;
        border: none;
        color: #5c6370;
        font-size: 16px;
        cursor: pointer;
        padding: 0 4px;
        line-height: 1;
    }
    .close-btn:hover {
        color: #ff4c4c;
    }
    .empty-state {
        text-align: center;
        padding: 60px;
        border: 1px dashed #5c6370;
        color: #5c6370;
        border-radius: 6px;
        margin-top: -1px;
        font-style: italic;
    }
</style>
