using Skyplan.Models.dto;
using UnityEngine;

namespace Skyplan.Models{
	public class Shape {
		public string id;
		public string type;
		public LayerDefDto layer;
		public List<Vector3> pts = [];
	}
}
