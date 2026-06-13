namespace Skyplan.Models {
	public enum SridOption {
		Epsg4326  = 4326,   // WGS84
		Epsg25832 = 25832,  // ETRS89 / UTM zone 32N
		Epsg25833 = 25833,  // ETRS89 / UTM zone 33N
		Epsg32632 = 32632,  // WGS84 / UTM zone 32N
		Epsg32633 = 32633,  // WGS84 / UTM zone 33N
		Epsg27700 = 27700,  // British National Grid
		Epsg2154  = 2154,   // RGF93 / Lambert-93 (France)
		Epsg3857  = 3857,   // Web Mercator
	}
}
