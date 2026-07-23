export interface CachedDocument {
    scriptId: string;
    baselineCode: string;
    codeText: string;
    targetEvent: string;
    isDirty: boolean;
}

let cacheMap = $state<Record<string, CachedDocument>>({});

export const editorCache = {
    get(scriptId: string): CachedDocument | null {
        return cacheMap[scriptId] || null;
    },

    set(scriptId: string, data: { scriptId: string; codeText: string; targetEvent: string }) {
        cacheMap[scriptId] = {
            scriptId: data.scriptId,
            baselineCode: data.codeText,
            codeText: data.codeText,
            targetEvent: data.targetEvent,
            isDirty: false
        };
    },

    updateCode(scriptId: string, currentText: string) {
        const doc = cacheMap[scriptId];
        if (doc) {
            doc.codeText = currentText;
            doc.isDirty = doc.baselineCode !== currentText;
        }
    },

    markClean(scriptId: string) {
        const doc = cacheMap[scriptId];
        if (doc) {
            doc.baselineCode = doc.codeText;
            doc.isDirty = false;
        }
    },

    invalidate(scriptId: string) {
        delete cacheMap[scriptId];
    },

    hasUnsavedChanges(): boolean {
        return Object.values(cacheMap).some(doc => doc.isDirty);
    }
};
