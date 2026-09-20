namespace TobyFredson
{
	public static class TobyConstants
	{
		// Shader property names (already global in your setup)
		public const string SHADER_VAR_FLOAT_SEASON = "_SeasonChangeGlobal";
		public const string SHADER_VAR_FLOAT_WIND_TYPE = "_WindType"; // Optional if using keywords
		public const string SHADER_VAR_FLOAT_WIND_STRENGTH = "_GlobalWindStrength";
		public const string SHADER_VAR_FLOAT_WIND_SPEED = "_StrongWindSpeed";
		public const string SHADER_VAR_FLOAT_WIND_MOTION = "_WindMotion";
		public const string SHADER_VAR_FLOAT_SNOW = "_SnowAmount";
		public const string SHADER_VAR_FLOAT_WETNESS = "_Wetness"; // Added wetness property
		public const string SHADER_VAR_VECTOR_WIND_DIRECTION = "_WindDirection";

		// Shader keywords for wind type
		public const string SHADER_WIND_TYPE_VALUE_STRONGWIND = "_WINDTYPE_STRONGWIND";
		public const string SHADER_WIND_TYPE_VALUE_GENTLEBREEZE = "_WINDTYPE_GENTLEBREEZE";
		public const string SHADER_WIND_TYPE_VALUE_WINDOFF = "_WINDTYPE_WINDOFF";

		// Added for debug visualize wind mask (global keyword for static switch)
		public const string SHADER_KEYWORD_DEBUG_VISUALIZE_WIND_MASK = "_DEBUGVISUALIZEWINDMASK_ON";

		// Added for save performance deactivate wind (corrected to match shader)
		public const string SHADER_KEYWORD_SAVE_PERFORMANCE_DEACTIVATE_WIND = "_SAVEPERFORMANCEDEACTIVATEWIND_ON";
	}
}