import type React from 'react';

// Colour value used by the game's ColorField: channels 0-1.
export interface Rgba { r: number; g: number; b: number; a: number; }

// SVG attribute names → React inline style keys: stroke-width → strokeWidth.
export function toInlineStyle(style: Record<string, string>): React.CSSProperties {
	const out: Record<string, string> = {};
	for (const [k, v] of Object.entries(style))
		out[k.replace(/-([a-z])/g, (_, c: string) => c.toUpperCase())] = v;
	return out as React.CSSProperties;
}

// Accepts #rgb and #rrggbb; anything else ("none", named colours) falls back to white so the
// picker still opens on something sensible.
export function hexToRgba(hex: string | undefined, alpha: number): Rgba {
	let h = (hex ?? '').trim().replace(/^#/, '');
	if (h.length === 3) h = h.split('').map(c => c + c).join('');
	const n = /^[0-9a-fA-F]{6}$/.test(h) ? parseInt(h, 16) : 0xffffff;
	return { r: ((n >> 16) & 255) / 255, g: ((n >> 8) & 255) / 255, b: (n & 255) / 255, a: alpha };
}

export function rgbaToHex(c: Rgba): string {
	const ch = (v: number) => Math.round(Math.min(1, Math.max(0, v)) * 255).toString(16).padStart(2, '0');
	return `#${ch(c.r)}${ch(c.g)}${ch(c.b)}`;
}

// Opacity keys are strings in layer styles ("0.8"); a missing key means fully opaque.
export function parseOpacity(v: string | undefined): number {
	const n = parseFloat(v ?? '');
	return Number.isFinite(n) ? n : 1;
}

export function formatOpacity(a: number): string {
	return String(Math.round(a * 100) / 100);
}
