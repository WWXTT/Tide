using UnityEngine;

namespace TobyFredson
{
	[System.Serializable]
	public struct TobyShaderValuesModel
	{
		public float season;
		public float snow;
		public float wetness; // Added wetness field
		public TobyWindType windType;
		public float windStrength;
		public float windSpeed;
		public float windMotion;
		public Vector3 windDirection;
		public bool _DEBUGVisualizeWindMask; // Debug toggle
		public bool _savePerformanceDeactivateWind; // Save performance toggle

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType()) return false;
			var other = (TobyShaderValuesModel)obj;
			return windType == other.windType &&
				Mathf.Approximately(windStrength, other.windStrength) &&
				Mathf.Approximately(windSpeed, other.windSpeed) &&
				Mathf.Approximately(windMotion, other.windMotion) &&
				Mathf.Approximately(season, other.season) &&
				Mathf.Approximately(snow, other.snow) &&
				Mathf.Approximately(wetness, other.wetness) && // Added wetness comparison
				windDirection == other.windDirection &&
				_DEBUGVisualizeWindMask == other._DEBUGVisualizeWindMask &&
				_savePerformanceDeactivateWind == other._savePerformanceDeactivateWind;
		}

		public override int GetHashCode()
		{
			// Combine first 8 fields
			var hash1 = System.HashCode.Combine(windType, windStrength, windSpeed, windMotion, season, snow, wetness, windDirection);
			// Combine hash1 with remaining 2 fields
			return System.HashCode.Combine(hash1, _DEBUGVisualizeWindMask, _savePerformanceDeactivateWind);
		}
	}

	public enum TobyWindType
	{
		GentleBreeze,
		StrongWind,
		WindOff
	}
}