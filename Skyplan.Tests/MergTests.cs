using Newtonsoft.Json.Linq;
using Skyplan.Models.Enums;
using Skyplan.Persistence.Helpers;

namespace Skyplan.Tests {
	public class MergTests {
		JObject DefaultLayers() => JObject.Parse("""
			{
				"labelStyle": {
					"color": "#ffffff",
					"fontSize": 15,
					"fontWeight": "normal",
					"opacity": 1
				},
				"layers": [
					{
						"id": "train",
						"label": "Train",
						"allowedTools": ["path"],
						"style": {
							"stroke": "#333333",
							"stroke-width": 5
						},
						"labelStyle": {}
					},
					{
						"id": "residential",
						"label": "Residential",
						"allowedTools": ["polygon"],
						"style": {
							"stroke": "#44cc88",
							"fill": "#44cc88"
						},
						"labelStyle": {}
					}
				]
			}
			""");

		JObject UserLayers() => JObject.Parse("""
			{
				"labelStyle": {
					"color": "#000000",
					"fontSize": 10,
					"fontWeight": "normal",
					"opacity": 0.5
				},
				"layers": [
					{
						"id": "train",
						"label": "My Train",
						"allowedTools": ["path"],
						"style": {
							"stroke": "#ff0000"
						},
						"labelStyle": {
							"fontSize": 20
						}
					},
					{
						"id": "custom",
						"label": "My Custom Layer",
						"allowedTools": ["polygon"],
						"style": {
							"stroke": "#aabbcc"
						}
					}
				]
			}
			""");

		List<JObject> DefaultLayerList() =>
			[.. DefaultLayers()["layers"]!.OfType<JObject>()];

		List<JObject> UserLayerList() =>
			[.. UserLayers()["layers"]!.OfType<JObject>()];

		// MergeObjects 

		[Fact]
		public void MergeObjects_UserOverridesPerKey() {
			var def  = (JObject)DefaultLayers()["labelStyle"]!;
			var user = (JObject)UserLayers()["labelStyle"]!;

			var result = LayerMerger.MergeObjects(def, user);

			Assert.Equal("#000000", result["color"]?.Value<string>());
			Assert.Equal(10, result["fontSize"]?.Value<int>());
			Assert.Equal(0.5, result["opacity"]?.Value<double>());
		}

		[Fact]
		public void MergeObjects_DefaultFillsMissingKeys() {
			var def  = (JObject)DefaultLayers()["labelStyle"]!;
			var user = new JObject { ["fontSize"] = 20 };

			var result = LayerMerger.MergeObjects(def, user);

			Assert.Equal("#ffffff", result["color"]?.Value<string>());
			Assert.Equal(20, result["fontSize"]?.Value<int>());
			Assert.Equal("normal", result["fontWeight"]?.Value<string>());
		}

		[Fact]
		public void MergeObjects_DoesNotMutateDefault() {
			var def  = (JObject)DefaultLayers()["labelStyle"]!;
			var user = new JObject { ["color"] = "#ff0000" };

			LayerMerger.MergeObjects(def, user);

			Assert.Equal("#ffffff", def["color"]?.Value<string>());
		}

		// MergeLayers 

		[Fact]
		public void MergeLayers_MatchingId_UserLabelWins() {
			var result = LayerMerger.MergeLayers(DefaultLayerList(), UserLayerList(), MergePolicies.UserWins);

			var train = result.First(l => l["id"]?.Value<string>() == "train");
			Assert.Equal("My Train", train["label"]?.Value<string>());
		}

		[Fact]
		public void MergeLayers_MatchingId_StyleMerged() {
			var result = LayerMerger.MergeLayers(DefaultLayerList(), UserLayerList(), MergePolicies.UserWins);

			var train = result.First(l => l["id"]?.Value<string>() == "train");
			var style = (JObject)train["style"]!;

			Assert.Equal("#ff0000", style["stroke"]?.Value<string>());
			Assert.Equal(5, style["stroke-width"]?.Value<int>());
		}

		[Fact]
		public void MergeLayers_MatchingId_LabelStyleMerged() {
			var result = LayerMerger.MergeLayers(DefaultLayerList(), UserLayerList(), MergePolicies.UserWins);

			var train = result.First(l => l["id"]?.Value<string>() == "train");
			var ls    = (JObject)train["labelStyle"]!;

			Assert.Equal(20, ls["fontSize"]?.Value<int>());
		}

		[Fact]
		public void MergeLayers_DefaultOnly_KeptAsIs() {
			var result = LayerMerger.MergeLayers(DefaultLayerList(), UserLayerList(), MergePolicies.UserWins);

			var residential = result.First(l => l["id"]?.Value<string>() == "residential");
			Assert.Equal("Residential", residential["label"]?.Value<string>());
		}

		[Fact]
		public void MergeLayers_DefaultOrder_Preserved() {
			var result = LayerMerger.MergeLayers(DefaultLayerList(), UserLayerList(), MergePolicies.UserWins);

			Assert.Equal("train", result[0]["id"]?.Value<string>());
			Assert.Equal("residential", result[1]["id"]?.Value<string>());
		}

		[Fact]
		public void MergeLayers_UserOnly_Appended() {
			var result = LayerMerger.MergeLayers(DefaultLayerList(), UserLayerList(), MergePolicies.UserWins);

			Assert.True(result.Any(l => l["id"]?.Value<string>() == "custom"));
		}

		// AppendMissingLayers

		static LayerMerger.UsedLayer Used(string id, string tool, string stroke = "#1a1a1a") =>
			new(id, id, new Dictionary<string, string> { ["stroke"] = stroke }, tool);

		[Fact]
		public void AppendMissingLayers_KnownId_NotAdded() {
			var merged = DefaultLayers();

			int added = LayerMerger.AppendMissingLayers(merged, [Used("train", "path"), Used("TRAIN", "curve")]);

			Assert.Equal(0, added);
			Assert.Equal(2, ((JArray)merged["layers"]!).Count);
		}

		[Fact]
		public void AppendMissingLayers_UnknownId_AddedOnceWithAllTools() {
			var merged = DefaultLayers();

			int added = LayerMerger.AppendMissingLayers(merged,
				[Used("imported", "path", "#111111"), Used("imported", "curve", "#222222"), Used("imported", "path")]);

			Assert.Equal(1, added);
			var layer = ((JArray)merged["layers"]!).OfType<JObject>().Single(l => l["id"]?.Value<string>() == "imported");
			Assert.Equal(["path", "curve"], layer["allowedTools"]!.Values<string>());
			// Style comes from the first shape.
			Assert.Equal("#111111", layer["style"]?["stroke"]?.Value<string>());
			Assert.NotNull(layer["labelStyle"]);
		}

		[Fact]
		public void AppendMissingLayers_NoLayersArray_Created() {
			var merged = new JObject();

			LayerMerger.AppendMissingLayers(merged, [Used("imported", "path")]);

			Assert.Single((JArray)merged["layers"]!);
		}
	}
}
