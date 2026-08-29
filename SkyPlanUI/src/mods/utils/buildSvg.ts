type Pt = { x: number; y: number };

export function buildPath(pts: Pt[]): string {
	if (pts.length < 2) return '';
	return `M ${pts[0].x} ${pts[0].y} ` + pts.slice(1).map(p => `L ${p.x} ${p.y}`).join(' ');
}

export function buildPolygon(pts: Pt[]): string {
	if (pts.length < 3) return '';
	return pts.map(p => `${p.x},${p.y}`).join(' ');
}

export function centroid(pts: Pt[]): Pt {
	const sum = pts.reduce((a, p) => ({ x: a.x + p.x, y: a.y + p.y }), { x: 0, y: 0 });
	return { x: sum.x / pts.length, y: sum.y / pts.length };
}
