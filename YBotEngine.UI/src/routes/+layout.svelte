<script lang="ts">
	import favicon from '$lib/assets/favicon.svg';
	import { browser } from '$app/environment';

	if (browser) {
		import('../userWorker.js')
				.then(() => console.log('Monaco environment workers initialized.'))
				.catch(err => console.error('Failed to load Monaco environment:', err));
	}
	
	let { children } = $props();
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
</svelte:head>

{@render children()}
