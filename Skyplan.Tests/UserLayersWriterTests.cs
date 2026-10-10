using Newtonsoft.Json.Linq;
using Skyplan.Models.Enums;
using Skyplan.Models.dto;
using Skyplan.Persistence.Helpers;

namespace Skyplan.Tests {
	public class UserLayersWriterTests {
		// No fontWeight, so that key falls through to the writer's hard default.
		JObject GlobalLabelStyle() => JObject.Parse("""
			{ "color": "#ffffff", "fontSize": 15, "opacity": 1 }
			""");

		JObject DefaultTrain() => JObject.Parse("""
			{
				"id": "train",
				"label": "Train",
				"allowedTools": ["path"],
				"style": { "stroke": "#333333", "stroke-width": "5" },
				"labelStyle": { "color": "#ff00ff" }
			}
			""");

		JObject UserRoot() => JObject.Parse("""
			{
				"layers": [
					{
						"id": "train",
						"label": "Train",
						"allowedTools": ["path"],
						"style": { "stroke": "#333333", "stroke-width": "5" },
						"labelStyle": { "fontSize": 20 }
					}
				]
			}
			""");

		JObject SourceMerged() => JObject.Parse("""
			{
				"id": "landmark",
				"label": "Landmark",
				"allowedTools": ["point"],
				"style": { "fill": "#ff4488" },
				"labelStyle": { "color": "#ff4488" },
				"icon": { "path": "M0 0L1 1", "color": "#000000" }
			}
			""");

		// The fully resolved label style JS sends, with one key overridden.
		static JObject Draft(string key, JToken value) {
			JObject draft = new() { ["color"] = "#ff00ff", ["fontSize"] = 15, ["fontWeight"] = "normal", ["opacity"] = 1 };
			draft[key] = value;
			return draft;
		}

		static SaveLayerStyleDto SaveDto(JObject labelStyle) => new() {
			LayerId = "train",
			Style = new() { ["stroke"] = "#ff0000", ["stroke-width"] = "5" },
			LabelStyle = labelStyle,
		};

		static JObject UserLabelStyle(JObject root, string id) =>
			(JObject)UserLayersWriter.FindLayer(root, id)!["labelStyle"]!;

		// Label diff

		[Fact]
		public void SaveStyle_LabelEqualToDefaultLayer_NotWritten() {
			JObject root = UserRoot();
			UserLayersWriter.SaveStyle(root, SaveDto(Draft("color", "#ff00ff")), DefaultTrain(), GlobalLabelStyle());

			Assert.Null(UserLabelStyle(root, "train")["color"]);
		}

		[Fact]
		public void SaveStyle_LabelEqualToGlobal_NotWritten() {
			JObject root = UserRoot();
			UserLayersWriter.SaveStyle(root, SaveDto(Draft("opacity", 1)), DefaultTrain(), GlobalLabelStyle());

			Assert.Null(UserLabelStyle(root, "train")["opacity"]);
		}

		[Fact]
		public void SaveStyle_LabelEqualToHardDefault_NotWritten() {
			JObject root = UserRoot();
			UserLayersWriter.SaveStyle(root, SaveDto(Draft("fontWeight", "normal")), DefaultTrain(), GlobalLabelStyle());

			Assert.Null(UserLabelStyle(root, "train")["fontWeight"]);
		}

		[Fact]
		public void SaveStyle_LabelDifferent_Written() {
			JObject root = UserRoot();
			UserLayersWriter.SaveStyle(root, SaveDto(Draft("fontWeight", "bold")), DefaultTrain(), GlobalLabelStyle());

			Assert.Equal("bold", UserLabelStyle(root, "train")["fontWeight"]?.Value<string>());
		}

		[Fact]
		public void SaveStyle_LabelResetToInherited_RemovesExistingOverride() {
			JObject root = UserRoot();
			UserLayersWriter.SaveStyle(root, SaveDto(Draft("fontSize", 15)), DefaultTrain(), GlobalLabelStyle());

			Assert.Null(UserLabelStyle(root, "train")["fontSize"]);
		}

		// Round trip through the merge

		[Fact]
		public void SaveStyle_ThenMerge_GivesTheDraft() {
			JObject root = UserRoot();
			SaveLayerStyleDto dto = SaveDto(Draft("fontWeight", "bold"));
			UserLayersWriter.SaveStyle(root, dto, DefaultTrain(), GlobalLabelStyle());

			JObject merged = LayerMerger.MergeLayers([DefaultTrain()], [.. root["layers"]!.OfType<JObject>()], MergePolicies.UserWins)[0];

			Assert.Equal("#ff0000", merged["style"]!["stroke"]?.Value<string>());
			JObject layerLabel = (JObject)merged["labelStyle"]!;
			JObject global = GlobalLabelStyle();
			foreach (JProperty prop in dto.LabelStyle.Properties()) {
				JToken? resolved = layerLabel[prop.Name] ?? global[prop.Name];
				// fontWeight "normal" comes from the hard default, which isn't in either file.
				if (resolved is null) Assert.Equal("normal", prop.Value.Value<string>());
				else Assert.True(JToken.DeepEquals(prop.Value, resolved), $"{prop.Name}: {prop.Value} vs {resolved}");
			}
		}

		// Save as

		[Fact]
		public void SaveStyleAs_CopiesSourceWithNewIdLabelAndStyle() {
			JObject root = UserRoot();
			SaveLayerStyleAsDto dto = new() {
				LayerId = "landmark",
				NewId = "my-landmark",
				Label = "My Landmark",
				Style = new() { ["fill"] = "#00ff00" },
				LabelStyle = Draft("color", "#ff4488"),
			};

			Assert.True(UserLayersWriter.SaveStyleAs(root, dto, SourceMerged(), GlobalLabelStyle()));

			JObject copy = UserLayersWriter.FindLayer(root, "my-landmark")!;
			Assert.Equal("My Landmark", copy["label"]?.Value<string>());
			Assert.Equal(["point"], copy["allowedTools"]!.Values<string>());
			Assert.Equal("M0 0L1 1", copy["icon"]!["path"]?.Value<string>());
			Assert.Equal("#00ff00", copy["style"]!["fill"]?.Value<string>());
			// A new layer has no default entry, so the source's own label colour is kept as an override.
			Assert.Equal("#ff4488", copy["labelStyle"]!["color"]?.Value<string>());
		}

		[Fact]
		public void SaveStyleAs_DuplicateIdIgnoringCase_Rejected() {
			JObject root = UserRoot();
			SaveLayerStyleAsDto dto = new() {
				LayerId = "landmark",
				NewId = "TRAIN",
				Label = "Train",
				Style = new() { ["fill"] = "#00ff00" },
				LabelStyle = [],
			};

			Assert.False(UserLayersWriter.SaveStyleAs(root, dto, SourceMerged(), GlobalLabelStyle()));
			Assert.Single((JArray)root["layers"]!);
		}
	}
}
