type Pt = { x: number; y: number };

export function buildPath(pts: Pt[]): string {
	if (pts.length < 2) return '';
	return `M ${pts[0].x} ${pts[0].y} ` + pts.slice(1).map(p => `L ${p.x} ${p.y}`).join(' ');
}

export function buildCurve(pts: Pt[]): string {
	if (pts.length < 2) return '';
	const [p0, p1] = pts;
	const mx = (p0.x + p1.x) / 2, my = (p0.y + p1.y) / 2;
	const dx = p1.x - p0.x, dy = p1.y - p0.y;
	const len = Math.hypot(dx, dy) || 1;
	const bulge = Math.min(len * 0.25, 120);
	// perpendicular offset from midpoint bows the line into a quadratic curve
	const cx = mx - (dy / len) * bulge;
	const cy = my + (dx / len) * bulge;
	return `M ${p0.x} ${p0.y} Q ${cx} ${cy} ${p1.x} ${p1.y}`;
}

export function buildPolygon(pts: Pt[]): string {
	if (pts.length < 3) return '';
	return pts.map(p => `${p.x},${p.y}`).join(' ');
}

export function centroid(pts: Pt[]): Pt {
	const sum = pts.reduce((a, p) => ({ x: a.x + p.x, y: a.y + p.y }), { x: 0, y: 0 });
	return { x: sum.x / pts.length, y: sum.y / pts.length };
}
