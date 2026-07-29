import type { Completion } from '@codemirror/autocomplete';
import type { Diagnostic } from '@codemirror/lint';

export const apiBase = 'api'

export type CompletionResult = {
    label: string,
    type:string,
    detail: string
}

export type ErrorResult = {
    from: string,
    to: string,
    severity: 'error' | 'warning',
    message: string
}

export type ApiLspResponse = {
        completions: CompletionResult[];
        errors: ErrorResult[];
    };

export type ApiEventsResponse = {
    name: string,
    payloadType: string
}

export type ScriptTab = {
    id: string;
    eventName: string;
    payloadType: string;
    docText: string;
    cursorPosition: number;
    lspData: {
        completions: Completion[];
        errors: Diagnostic[];
    };
};