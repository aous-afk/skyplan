using System;

namespace SkyPlan.Export {
	public static class CoordinateConverter {
		public static (double x, double y) Convert(
			float worldX, float worldZ,
			int srid, double originX, double originY)
		{
			if (srid == 4326) {
				double lat = originY + worldZ / 111320.0;
				double lon = originX + worldX / (111320.0 * Math.Cos(originY * Math.PI / 180.0));
				return (lon, lat);
			}
			return (originX + worldX, originY + worldZ);
		}
	}
}
