using UnityEngine;
using UnityEngine.SceneManagement;

namespace TobyFredson
{
	[ExecuteInEditMode]
	public class TobyGlobalShadersController : MonoBehaviour
	{
		#region Inspector Fields
		[SerializeField] private TobyWindType windType;
		[SerializeField] [Range(0f, 1f)] private float windStrength;
		[SerializeField] [Range(1f, 3f)] private float windSpeed;
		[SerializeField] [Range(0f, 1f)] private float windMotion;
		[SerializeField] [Range(-2f, 2f)] private float season;
		[SerializeField] [Range(0f, 10f)] private float snow;
		[SerializeField] [Range(0f, 1f)] private float wetness; // Added wetness field
		[SerializeField] private bool _DEBUGVisualizeWindMask; // Debug toggle for wind mask
		[SerializeField] private bool _showWindDirectionGizmo; // Toggle for wind direction gizmo
		[SerializeField] private bool _savePerformanceDeactivateWind; // Toggle for save performance
		#endregion

		#region Private Fields
		private TobyShaderValuesModel lastInputValues;
		private float lastUpdateTime; // Note: This is unused now but kept in case you need it later
		#endregion

		#region Unity Callbacks
		protected void Awake()
		{
			// Ensure only one controller exists
			TobyGlobalShadersController[] controllers = FindObjectsOfType<TobyGlobalShadersController>(true);
			if (controllers.Length > 1)
			{
				Debug.LogWarning($"Multiple controllers found in scene {SceneManager.GetActiveScene().name}. Destroying duplicates.");
				for (int i = 1; i < controllers.Length; i++)
				{
					Destroy(controllers[i].gameObject);
				}
			}
		}

		protected void Start()
		{
			SetGlobalShaderProperties();
		}

		protected void Update()
		{
			// Restrict rotation to Y-axis for wind direction
			transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);

			var newValues = GetNewValues();
			if (!newValues.Equals(lastInputValues))
			{
				lastInputValues = newValues;
				SetGlobalShaderProperties(); // Update every frame when values change
			}
		}

		private void OnDrawGizmos()
		{
			// Draw gizmo for wind direction only if toggle is enabled
			if (_showWindDirectionGizmo)
			{
				Gizmos.color = Color.green;
				Vector3 windDirection = transform.forward;
				Vector3 position = transform.position;
				float gizmoLength = 3f;
				Vector3 endPoint = position + windDirection * gizmoLength;

				Gizmos.DrawLine(position, endPoint);
				float arrowSize = 0.5f;
				Vector3 arrowPoint1 = endPoint - windDirection * arrowSize + Quaternion.Euler(0, 30, 0) * windDirection * arrowSize;
				Vector3 arrowPoint2 = endPoint - windDirection * arrowSize + Quaternion.Euler(0, -30, 0) * windDirection * arrowSize;
				Gizmos.DrawLine(endPoint, arrowPoint1);
				Gizmos.DrawLine(endPoint, arrowPoint2);
			}
		}
		#endregion

		#region Private Methods
		private void SetGlobalShaderProperties()
		{
			// Set global float properties
			Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_SEASON, season);
			Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_SNOW, snow);
			Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WETNESS, wetness); // Set wetness property

			// Handle wind properties based on windType
			if (windType == TobyWindType.WindOff)
			{
				// Disable wind effects completely
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_STRENGTH, 0f);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_SPEED, 0f);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_MOTION, 0f);
				Shader.SetGlobalVector(TobyConstants.SHADER_VAR_VECTOR_WIND_DIRECTION, Vector4.zero);
			}
			else
			{
				// Use user-defined values for GentleBreeze or StrongWind
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_STRENGTH, windStrength);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_SPEED, windSpeed);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_MOTION, windMotion);
				Shader.SetGlobalVector(TobyConstants.SHADER_VAR_VECTOR_WIND_DIRECTION, new Vector4(transform.forward.x, transform.forward.y, transform.forward.z, 0f));
			}

			// Set debug keyword globally for static switch
			if (_DEBUGVisualizeWindMask)
			{
				Shader.EnableKeyword(TobyConstants.SHADER_KEYWORD_DEBUG_VISUALIZE_WIND_MASK);
			}
			else
			{
				Shader.DisableKeyword(TobyConstants.SHADER_KEYWORD_DEBUG_VISUALIZE_WIND_MASK);
			}

			// Set save performance keyword globally for static switch
			if (_savePerformanceDeactivateWind)
			{
				Shader.EnableKeyword(TobyConstants.SHADER_KEYWORD_SAVE_PERFORMANCE_DEACTIVATE_WIND);
			}
			else
			{
				Shader.DisableKeyword(TobyConstants.SHADER_KEYWORD_SAVE_PERFORMANCE_DEACTIVATE_WIND);
			}

			// Set wind type keywords
			Shader.DisableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_GENTLEBREEZE);
			Shader.DisableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_STRONGWIND);
			Shader.DisableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_WINDOFF);
			switch (windType)
			{
			case TobyWindType.GentleBreeze:
				Shader.EnableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_GENTLEBREEZE);
				break;
			case TobyWindType.StrongWind:
				Shader.EnableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_STRONGWIND);
				break;
			case TobyWindType.WindOff:
				Shader.EnableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_WINDOFF);
				break;
			}
		}

		private TobyShaderValuesModel GetNewValues()
		{
			return new TobyShaderValuesModel
			{
				season = season,
				snow = snow,
				wetness = wetness, // Add wetness to model
				windType = windType,
				windStrength = windStrength,
				windSpeed = windSpeed,
				windMotion = windMotion,
				windDirection = transform.forward,
				_DEBUGVisualizeWindMask = _DEBUGVisualizeWindMask,
				_savePerformanceDeactivateWind = _savePerformanceDeactivateWind
			};
		}
		#endregion
	}
}