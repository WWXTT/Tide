using UnityEngine;
using System.Collections.Generic;

namespace TobyFoliageEngine.Tools
{
	[AddComponentMenu("Toby Fredson/The Toby Foliage Engine/Tools/Interaction/Plant Interaction Physics")]
	public class PlantInteractionPhysics : MonoBehaviour
	{
		[Header("Bending Settings")]
		public float bendAmount = 90f;
		public float bendSpeed = 4f; // Smooth transitions
		public float resetSpeed = 1.6f; // Smooth reset
		public float interactionRadius = 5f;
		public float resetDistance = 7f;
		public float debounceTime = 0.2f;
		public bool usePlayerVelocity = true;
		public float downwardInfluence = 0f;
		public bool useLocalZAsForward = true;
		public float velocitySensitivity = 0.5f; // Scales velocity influence

		[Header("Random Movement Settings")]
		public bool enableWind = false; // Toggle in Inspector to enable wind
		public float windAmplitude = 3f;
		public float windFrequency = 1f;
		public Vector3 windAxis = new Vector3(1, 1, 1);
		public float windDistance = 10f;
		public bool restrictWindToPlayers = true; // Disable wind for plants far from players
		public float windCullDistance = 50f; // Distance beyond which wind is disabled

		[Header("Shader Settings")]
		public float heightMax = 1f; // Set in Inspector: ~5 for trees

		[Header("Performance Settings")]
		public float cullDistance = 50f; // Distance beyond which script is disabled
		public bool cullByCamera = true; // Cull based on camera distance
		public bool cullByPlayer = true; // Cull based on closest player distance
		public bool disableGameObject = false; // Disable entire GameObject instead of just script

		private MaterialPropertyBlock propBlock;
		private Renderer[] foliageRenderers;
		private Vector3 currentBendAxis;
		private float currentBendAmount;
		private Vector3 currentWindAxis;
		private float currentWindAmplitude;
		private float lastTriggerTime;
		private List<Transform> playerTransforms = new List<Transform>();
		private Transform closestPlayer;
		private Vector3 lastPlayerPosition;
		private bool hasLastPosition;
		private Dictionary<Transform, (Rigidbody rb, CharacterController cc)> playerComponents = new Dictionary<Transform, (Rigidbody, CharacterController)>();
		private Vector3 smoothedVelocity;
		private bool needsUpdate;
		private Quaternion lastBendRotation = Quaternion.identity;
		private bool wasCulled; // Track if the object was previously culled

		void Start()
		{
			foliageRenderers = GetComponentsInChildren<Renderer>(false);
			if (foliageRenderers == null || foliageRenderers.Length == 0)
			{
				enabled = false;
				return;
			}

			propBlock = new MaterialPropertyBlock();
			foreach (var renderer in foliageRenderers)
			{
				renderer.GetPropertyBlock(propBlock);
				propBlock.SetFloat("_HeightMax", heightMax);
				renderer.SetPropertyBlock(propBlock);
			}

			lastTriggerTime = -debounceTime;
			hasLastPosition = false;
			smoothedVelocity = Vector3.zero;
			currentBendAmount = 0f;
			currentBendAxis = Vector3.zero;
			currentWindAxis = windAxis.normalized;
			currentWindAmplitude = 0f;
			needsUpdate = false;
			wasCulled = false;
		}

		void Update()
		{
			// Check for null Camera.main when culling by camera
			if (cullByCamera && Camera.main == null)
			{
				enabled = false;
				return;
			}

			// Check culling conditions with hysteresis
			bool shouldCull = true;
			float cameraDistance = cullByCamera ? Vector3.Distance(transform.position, Camera.main.transform.position) : float.MinValue;
			closestPlayer = playerTransforms.Count > 0 ? GetClosestPlayer() : null;
			float playerDistance = closestPlayer != null ? Vector3.Distance(transform.position, closestPlayer.position) : float.MaxValue;

			float enableDistance = cullDistance + 5f; // Increased hysteresis buffer
			float fadeDistance = cullDistance * 0.9f; // Start fading before culling
			float cullFadeFactor = 1f;

			if (cullByCamera && cameraDistance < enableDistance)
			{
				shouldCull = false;
				if (cameraDistance > fadeDistance)
				{
					cullFadeFactor = Mathf.Clamp01(1f - (cameraDistance - fadeDistance) / (enableDistance - fadeDistance));
				}
			}
			if (cullByPlayer && playerDistance < enableDistance)
			{
				shouldCull = false;
				if (playerDistance > fadeDistance)
				{
					cullFadeFactor = Mathf.Min(cullFadeFactor, Mathf.Clamp01(1f - (playerDistance - fadeDistance) / (enableDistance - fadeDistance)));
				}
			}

			if (shouldCull && enabled)
			{
				wasCulled = true;
				if (disableGameObject)
				{
					gameObject.SetActive(false);
				}
				else
				{
					enabled = false;
				}
				return;
			}
			else if (!enabled && !shouldCull)
			{
				enabled = true;
				if (disableGameObject)
				{
					gameObject.SetActive(true);
				}
				// Smoothly reset shader properties on re-enable
				if (wasCulled)
				{
					currentBendAmount = 0f;
					currentBendAxis = Vector3.zero;
					currentWindAmplitude = 0f;
					lastBendRotation = Quaternion.identity;
					needsUpdate = true;
					wasCulled = false;
				}
			}

			// Early exit and deactivate if idle
			bool windActive = enableWind && (!restrictWindToPlayers || playerDistance < windCullDistance);
			if (playerTransforms.Count == 0 && !windActive && currentBendAmount < 0.01f && currentWindAmplitude < 0.01f)
			{
				needsUpdate = false;
				enabled = false;
				return;
			}

			// Only process if affected by player or wind
			float influence = 0f;
			if (playerDistance < interactionRadius)
			{
				float t = 1f - (playerDistance / interactionRadius);
				influence = Mathf.Min(Mathf.Pow(t, 1.5f), 0.6f);
			}

			// Wind effect
			float targetWindAmplitude = 0f;
			if (windActive && playerDistance < windDistance)
			{
				float windScale = playerDistance < interactionRadius ? 1f : 0.5f;
				targetWindAmplitude = windAmplitude * windScale * cullFadeFactor;
			}

			// Player bending
			Quaternion targetBendRotation = Quaternion.identity;
			bool isResetting = true;

			if (playerTransforms.Count > 0 && playerDistance < interactionRadius)
			{
				if (usePlayerVelocity && closestPlayer != null)
				{
					Vector3 playerVelocity = GetPlayerVelocity(closestPlayer);
					if (playerVelocity.magnitude > 0.3f)
					{
						isResetting = false;
						playerVelocity.y = 0;
						if (playerVelocity.magnitude > 0)
						{
							Vector3 localVelocity = transform.InverseTransformDirection(playerVelocity);
							localVelocity.y = 0;
							localVelocity = localVelocity.normalized * velocitySensitivity;

							Vector3 bendAxis;
							if (useLocalZAsForward)
							{
								bendAxis = new Vector3(-localVelocity.z, 0f, localVelocity.x);
							}
							else
							{
								bendAxis = new Vector3(-localVelocity.x, 0f, localVelocity.z);
							}

							float bendAngle = bendAmount * influence * cullFadeFactor;
							targetBendRotation = Quaternion.AngleAxis(bendAngle, bendAxis);
						}
					}
				}

				if (downwardInfluence > 0)
				{
					Vector3 downAxis = transform.InverseTransformDirection(Vector3.down);
					targetBendRotation = Quaternion.Slerp(targetBendRotation, Quaternion.AngleAxis(bendAmount * influence * cullFadeFactor, downAxis), downwardInfluence);
				}
			}

			// Smooth rotation
			float lerpStep = Time.deltaTime * (isResetting ? resetSpeed : bendSpeed);
			lastBendRotation = Quaternion.Slerp(lastBendRotation, targetBendRotation, lerpStep);

			// Convert to axis-angle
			lastBendRotation.ToAngleAxis(out float angle, out Vector3 axis);
			float newBendAmount = angle;
			Vector3 newBendAxis = float.IsNaN(axis.x) ? Vector3.zero : axis;

			float newWindAmplitude = Mathf.Lerp(currentWindAmplitude, targetWindAmplitude, lerpStep);

			// Check if update is needed
			if (Mathf.Abs(newBendAmount - currentBendAmount) > 0.001f ||
				Vector3.Distance(newBendAxis, currentBendAxis) > 0.001f ||
				Mathf.Abs(newWindAmplitude - currentWindAmplitude) > 0.001f)
			{
				needsUpdate = true;
				currentBendAmount = newBendAmount;
				currentBendAxis = newBendAxis;
				currentWindAmplitude = newWindAmplitude;
			}
			else
			{
				needsUpdate = false;
			}

			// Update shader properties for all renderers
			if (needsUpdate)
			{
				propBlock.SetVector("_BendAxis", currentBendAxis);
				propBlock.SetFloat("_BendAmount", currentBendAmount * cullFadeFactor);
				propBlock.SetVector("_WindAxis", currentWindAxis);
				propBlock.SetFloat("_WindAmplitude", currentWindAmplitude);
				propBlock.SetFloat("_WindFrequency", windFrequency);

				foreach (var renderer in foliageRenderers)
				{
					if (renderer != null)
					{
						renderer.SetPropertyBlock(propBlock);
					}
				}
			}
		}

		Transform GetClosestPlayer()
		{
			Transform closest = null;
			float minDistance = float.MaxValue;

			foreach (Transform player in playerTransforms)
			{
				if (player == null)
					continue;
				float dist = Vector3.Distance(transform.position, player.position);
				if (dist < minDistance)
				{
					minDistance = dist;
					closest = player;
				}
			}

			return closest;
		}

		Vector3 GetPlayerVelocity(Transform player)
		{
			if (player == null)
				return Vector3.zero;

			if (!playerComponents.ContainsKey(player))
			{
				playerComponents[player] = (player.GetComponent<Rigidbody>(), player.GetComponent<CharacterController>());
			}

			var (rb, cc) = playerComponents[player];

			Vector3 velocity = Vector3.zero;
			if (rb != null && !rb.isKinematic)
			{
				velocity = rb.linearVelocity;
			}
			else if (cc != null)
			{
				velocity = cc.velocity;
			}
			else if (hasLastPosition)
			{
				velocity = (player.position - lastPlayerPosition) / Time.deltaTime;
			}

			smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, Time.deltaTime * 5f);
			lastPlayerPosition = player.position;
			hasLastPosition = true;
			return smoothedVelocity;
		}

		void OnTriggerEnter(Collider other)
		{
			if (other != null && other.CompareTag("Player") && Time.time - lastTriggerTime >= debounceTime)
			{
				if (!playerTransforms.Contains(other.transform))
				{
					playerTransforms.Add(other.transform);
					lastTriggerTime = Time.time;
					hasLastPosition = false;
					needsUpdate = true;
					if (disableGameObject)
					{
						gameObject.SetActive(true);
					}
					enabled = true;
				}
			}
		}

		void OnTriggerExit(Collider other)
		{
			if (other != null && other.CompareTag("Player") && Time.time - lastTriggerTime >= debounceTime)
			{
				playerTransforms.Remove(other.transform);
				playerComponents.Remove(other.transform);
				lastTriggerTime = Time.time;
				if (playerTransforms.Count == 0)
				{
					hasLastPosition = false;
					needsUpdate = true;
				}
			}
		}

		void OnTriggerStay(Collider other)
		{
			if (other != null && other.CompareTag("Player") && !playerTransforms.Contains(other.transform))
			{
				playerTransforms.Add(other.transform);
				lastTriggerTime = Time.time;
				needsUpdate = true;
				if (disableGameObject)
				{
					gameObject.SetActive(true);
				}
				enabled = true;
			}
		}

		void OnBecameVisible()
		{
			if (cullByCamera)
			{
				enabled = true;
				if (disableGameObject)
				{
					gameObject.SetActive(true);
				}
				if (wasCulled)
				{
					currentBendAmount = 0f;
					currentBendAxis = Vector3.zero;
					currentWindAmplitude = 0f;
					lastBendRotation = Quaternion.identity;
					needsUpdate = true;
					wasCulled = false;
				}
			}
		}

		void OnEnable()
		{
			if (playerTransforms == null)
				playerTransforms = new List<Transform>();
			if (playerComponents == null)
				playerComponents = new Dictionary<Transform, (Rigidbody, CharacterController)>();
			// Ensure shader properties are initialized
			if (propBlock != null && foliageRenderers != null)
			{
				propBlock.SetVector("_BendAxis", currentBendAxis);
				propBlock.SetFloat("_BendAmount", currentBendAmount);
				propBlock.SetVector("_WindAxis", currentWindAxis);
				propBlock.SetFloat("_WindAmplitude", currentWindAmplitude);
				propBlock.SetFloat("_WindFrequency", windFrequency);

				foreach (var renderer in foliageRenderers)
				{
					if (renderer != null)
					{
						renderer.SetPropertyBlock(propBlock);
					}
				}
			}
		}

		void OnDisable()
		{
			playerTransforms.Clear();
			playerComponents.Clear();
			hasLastPosition = false;
			needsUpdate = false;
			wasCulled = true;
		}
	}
}