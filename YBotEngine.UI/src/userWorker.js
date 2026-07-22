import * as monaco from 'monaco-editor';

self.MonacoEnvironment = {
    getWorker: function (_, label) {
        return new Worker(
            new URL('monaco-editor/esm/vs/editor/editor.worker.js', import.meta.url),
            { type: 'module' }
        );
    }
};