import styleManagerShot from './images/style-manager.jpg';

export interface WhatsNewMedia {
	type: 'image' | 'gif';
	src: string;
	alt: string;
	// Natural pixel dimensions of the source file - required so the panel can compute an explicit
	// display width/height in pixels. GameFace does not reliably resolve `height: auto` or a
	// percentage-padding aspect-ratio box on <img> (both confirmed broken 2026-09-12 - one
	// disappears the image entirely, the other breaks its layout position), so no auto/percentage
	// sizing is used at all - see WhatsNewPanel.tsx.
	width: number;
	height: number;
}

export interface WhatsNewEntry {
	version: string;
	date: string;
	bullets: string[];
	media?: WhatsNewMedia[];
}

// Newest first. Kept as a small manual duplicate of the changelog in
// Skyplan/Properties/PublishConfiguration.xml and README.md - not worth wiring cross-project
// auto-sync for a handful of lines per release.
export const WHATS_NEW: WhatsNewEntry[] = [
	{
		version: '0.2.0-beta: Style Manager',
		date: '17-10-2026',
		bullets: [
			'Added: Style Manager - edit any layer\'s colour, line width, dash pattern and label style, with a live preview. Open it from the toolbar.',
			'Added: Save / Save as in the Style Manager - Save writes your changes to the layer, Save as creates a new layer from them. Styles are stored in your layers.json and survive restarts.',
			'Added: multi-line text - labels with line breaks now render on several lines.',
			'UX: panel titles adjust to their length.',
			'UI: hover on the Shape Manager collapse toggle.',
			'UI: delete button on each shape row in the Shape Manager.',
			'Bug fix: label styles from the layers file were ignored and overwritten.',
			'Bug fix: SVG export used the wrong layer styles.',
			'Bug fix: points and shapes no longer sink below the water surface.',
			'Bug fix: SVGs made in other programs no longer import invisibly; they go into the "Imported" layer and keep their styles.',
		],
		media: [
			{
				type: 'image',
				src: styleManagerShot,
				alt: 'Style Manager panel',
				width: 1040, height: 585
			},
		],

	},
	{
		version: '0.1.3-beta',
		date: '19-09-2026',
		bullets: [
			'Added: multi-layer parallel corridors - queue several layers (e.g. Train, Subway, Train) and draw once to get parallel lanes in that exact order, for both lines and curves. Each lane is independently erasable/editable.',
			'Added: Shift+scroll wheel adjusts corridor spacing live while drawing.',
			'Added: 3 new custom point layers - Telecom, Garbage, Cemetery.',
			'UX: layer picker now single-selects on plain click; Shift+click adds another lane to a corridor.',
			"UX: world clicks (building select/placement) are now blocked while in Draw mode, so a draw click doesn't also interact with the game underneath (toggleable in mod settings).",
		],
	},
	{
		version: '0.1.2-beta',
		date: '06-09-2026',
		bullets: [
			'Added: Curve Tool.',
			'Added: Custom Icons for the Point Tool.',
			'Added: snapping - lines and polygons snap to other shapes, with an indicator on the overview.',
			'UX: you can now deselect tools and layers with right-click.',
			'Improved performance.',
			'Bug fix: shapes no longer become distorted when zooming.',
		],
	},
];
