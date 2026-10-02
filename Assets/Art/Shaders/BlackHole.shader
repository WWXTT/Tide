// Derived from tantaneity/urp-black-hole (MIT, Copyright (c) 2026 tantaneity).
// Trimmed fork: spiral arms, dust lanes, turbulence warp and 4x supersample removed.
// Fork changes:
//  - binary system: two horizons orbit a common barycenter, dual pseudo-geodesic bending,
//    independently tiltable disks (a volumetric streamer bridge was tried and removed
//    2026-10-02 on art grounds — it read as a hard filament, not as mass transfer);
//  - escaped geodesics sample the procedural flowing sky analytically along the bent exit
//    direction (shared with BlackHole/Skybox) so lensing is always visible over a moving
//    starfield, with no dependence on the low-res camera opaque copy;
//  - perf: the whole system is ONE raymarch pass (mutual lensing and occlusion come free —
//    two separate meshes would double cost and could not lens each other), step length
//    adapts to the nearest horizon, the companion is skipped behind a uniform branch, and
//    the march exits early once transmittance is exhausted.
//  - perf (2026-10-02): mobile (GLES / Android Vulkan) builds cap the march at 96 steps and
//    drop to single-octave filaments plus a 3-octave sky FBM (ProceduralSky.hlsl), so desktop
//    quality is untouched while phones get a cheaper tier automatically.
//  - perf round 2 (same day, still ~70% GPU on desktop): the march now enters/exits at a
//    content sphere (system reach x1.6) instead of the full mesh bound, far-field steps
//    stretch to 0.10 of that radius, rays missing the content cost one iteration, and the
//    per-step normalize moved into the disk-crossing branches; material _StepCount 192->128.
Shader "BlackHole/Gargantua"
{
    Properties
    {
        _HorizonScale("Event Horizon Scale", Range(0.02, 0.2)) = 0.06
        _StepCount("Raymarch Steps", Range(64, 320)) = 192
        _StepSize("Raymarch Step Scale", Range(0.02, 0.2)) = 0.15
        _DiskInnerRadius("Disk Inner Radius", Range(1.2, 6)) = 1.6
        _DiskOuterRadius("Disk Outer Radius", Range(4, 12)) = 4.6
        _DiskBrightness("Disk Brightness", Range(0, 30)) = 7
        _DiskFalloff("Disk Radial Falloff", Range(0.5, 4)) = 2.2
        [HDR] _DiskColor("Disk Color", Color) = (1, 0.55, 0.38, 1)
        [HDR] _DiskHotColor("Disk Hot Color", Color) = (1, 0.93, 0.85, 1)
        _DiskDetail("Disk Ring Detail", Range(0.5, 8)) = 5.5
        _DiskRotationSpeed("Disk Rotation Speed", Range(0, 3)) = 1.2
        _DopplerStrength("Doppler Strength", Range(0, 1)) = 0.55
        _ParticleIntensity("Particle Intensity", Range(0, 8)) = 3
        _ParticleThreshold("Particle Sparsity", Range(0, 0.95)) = 0.62
        _SecondaryHorizonScale("Companion Horizon Scale", Range(0, 1)) = 0.5
        _SecondaryDiskScale("Companion Disk Scale", Range(0.1, 1)) = 0.55
        _SecondaryDiskTilt("Companion Disk Tilt (rad)", Range(-1.5708, 1.5708)) = 0.45
        _BinarySeparation("Binary Separation", Range(2, 12)) = 5.5
        _BinaryPeriod("Binary Orbit Period (s)", Range(2, 120)) = 24
        _BinaryPhase("Binary Orbit Phase (rad)", Range(0, 6.2832)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BlackHoleRaymarch"

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex VertexStage
            #pragma fragment FragmentStage
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ProceduralSky.hlsl"

            #define MAX_RAYMARCH_STEPS 320
            #define MESH_BOUND_RADIUS 0.5
            #define CAPTURE_RADIUS 0.95
            #define GEODESIC_BEND_FACTOR 1.5
            #define MIN_STEP_LENGTH 0.03
            #define MAX_STEP_FRACTION_OF_BOUND 0.10
            #define CONTENT_EXIT_MARGIN 1.6
            #define INNER_EDGE_SOFTNESS 1.18
            #define OUTER_FADE_START 0.55
            #define STREAK_ANGULAR_FREQUENCY 26.0
            #define STREAK_BASE 0.25
            #define STREAK_AMPLITUDE 1.2
            #define STREAK_FINE_RADIAL 4.5
            #define STREAK_FINE_ANGULAR 0.72
            #define STREAK_FINE_BASE 0.32
            #define STREAK_FINE_AMPLITUDE 1.35
            #define STREAK_FINE_OFFSET 19.3
            #define RIDGE_OCTAVE2_SCALE 2.3
            #define RIDGE_OCTAVE1_WEIGHT 0.62
            #define RIDGE_OCTAVE2_WEIGHT 0.38
            #define GRAIN_SCALE 7.5
            #define GRAIN_BASE 0.7
            #define GRAIN_AMPLITUDE 0.6
            #define CLOUD_NOISE_SCALE 1.2
            #define CLOUD_SPEED_RATIO 0.7
            #define CLOUD_BASE 0.5
            #define CLOUD_AMPLITUDE 0.95
            #define CLOUD_NOISE_OFFSET float2(31.7, 17.3)
            #define CLUMP_RADIAL_FREQUENCY 2.2
            #define CLUMP_ANGULAR_FREQUENCY 26.0
            #define CLUMP_CONTRAST_GAIN 3.0
            #define DOPPLER_BEAMING_EXPONENT 3.0
            #define DOPPLER_SHIFT_GAIN 1.0
            #define HEAT_FALLOFF_EXPONENT 3.0
            #define DISK_COVERAGE_GAIN 1.4
            #define TRANSMITTANCE_CUTOFF 0.01

            CBUFFER_START(UnityPerMaterial)
                float _HorizonScale;
                float _StepCount;
                float _StepSize;
                float _DiskInnerRadius;
                float _DiskOuterRadius;
                float _DiskBrightness;
                float _DiskFalloff;
                half4 _DiskColor;
                half4 _DiskHotColor;
                float _DiskDetail;
                float _DiskRotationSpeed;
                float _DopplerStrength;
                float _ParticleIntensity;
                float _ParticleThreshold;
                float _SecondaryHorizonScale;
                float _SecondaryDiskScale;
                float _SecondaryDiskTilt;
                float _BinarySeparation;
                float _BinaryPeriod;
                float _BinaryPhase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float SeamlessAngularNoise(float2 coordsA, float2 coordsB, float blend)
            {
                return lerp(SkySmoothValueNoise(coordsA), SkySmoothValueNoise(coordsB), blend);
            }

            float RidgedValueNoise(float2 p)
            {
                float n = SkySmoothValueNoise(p);
                n = 1.0 - abs(2.0 * n - 1.0);
                return n * n;
            }

            float SeamlessRidgedFilament(float2 coordsA, float2 coordsB, float blend)
            {
                float octave1 = lerp(RidgedValueNoise(coordsA), RidgedValueNoise(coordsB), blend);
                #if BLACKHOLE_MOBILE_TIER
                return octave1;
                #else
                float octave2 = lerp(RidgedValueNoise(coordsA * RIDGE_OCTAVE2_SCALE), RidgedValueNoise(coordsB * RIDGE_OCTAVE2_SCALE), blend);
                return RIDGE_OCTAVE1_WEIGHT * octave1 + RIDGE_OCTAVE2_WEIGHT * octave2;
                #endif
            }

            // planar/basisU/basisV describe the disk plane around its own hole, so the same
            // emission code serves both the primary and the companion disk.
            float3 EvaluateDiskEmission(
                float2 planar,
                float3 basisU,
                float3 basisV,
                float3 rayDirection,
                float innerRadius,
                float outerRadius,
                out float coverage)
            {
                float orbitRadius = length(planar);
                float innerFade = smoothstep(innerRadius, innerRadius * INNER_EDGE_SOFTNESS, orbitRadius);
                float outerFade = 1.0 - smoothstep(outerRadius * OUTER_FADE_START, outerRadius, orbitRadius);
                float radialMask = innerFade * outerFade;

                float angularVelocity = rsqrt(orbitRadius * orbitRadius * orbitRadius + 0.001);
                float orbitAngle = atan2(planar.y, planar.x);
                float rotationPhase = orbitAngle - _Time.y * _DiskRotationSpeed * angularVelocity;
                float normalizedPhase = frac(rotationPhase / TWO_PI);

                float radialCoordinate = orbitRadius * _DiskDetail;

                float2 streakCoordsA = float2(radialCoordinate, normalizedPhase * STREAK_ANGULAR_FREQUENCY);
                float2 streakCoordsB = float2(radialCoordinate, (normalizedPhase - 1.0) * STREAK_ANGULAR_FREQUENCY);
                float streaks = lerp(
                    SkyFractionalBrownianMotion(streakCoordsA),
                    SkyFractionalBrownianMotion(streakCoordsB),
                    normalizedPhase);
                streaks *= streaks;

                float2 fineStreakCoordsA = float2(
                    radialCoordinate * STREAK_FINE_RADIAL,
                    normalizedPhase * STREAK_ANGULAR_FREQUENCY * STREAK_FINE_ANGULAR) + STREAK_FINE_OFFSET;
                float2 fineStreakCoordsB = float2(
                    radialCoordinate * STREAK_FINE_RADIAL,
                    (normalizedPhase - 1.0) * STREAK_ANGULAR_FREQUENCY * STREAK_FINE_ANGULAR) + STREAK_FINE_OFFSET;
                float fineStreaks = SeamlessRidgedFilament(fineStreakCoordsA, fineStreakCoordsB, normalizedPhase);
                streaks *= STREAK_FINE_BASE + STREAK_FINE_AMPLITUDE * fineStreaks;

                float grain = SeamlessRidgedFilament(streakCoordsA * GRAIN_SCALE, streakCoordsB * GRAIN_SCALE, normalizedPhase);

                float2 clumpCoordsA = float2(orbitRadius * CLUMP_RADIAL_FREQUENCY, normalizedPhase * CLUMP_ANGULAR_FREQUENCY);
                float2 clumpCoordsB = float2(orbitRadius * CLUMP_RADIAL_FREQUENCY, (normalizedPhase - 1.0) * CLUMP_ANGULAR_FREQUENCY);
                float clumps = SeamlessAngularNoise(clumpCoordsA, clumpCoordsB, normalizedPhase);
                clumps = saturate((clumps - _ParticleThreshold) * CLUMP_CONTRAST_GAIN);
                clumps *= clumps;

                float cloudRotation = _Time.y * _DiskRotationSpeed * CLOUD_SPEED_RATIO * angularVelocity;
                float sinRotation;
                float cosRotation;
                sincos(cloudRotation, sinRotation, cosRotation);
                float2 rotatedOrbit = float2(
                    planar.x * cosRotation - planar.y * sinRotation,
                    planar.x * sinRotation + planar.y * cosRotation);
                float clouds = SkyFractionalBrownianMotion(rotatedOrbit * CLOUD_NOISE_SCALE + CLOUD_NOISE_OFFSET);

                float density = radialMask
                    * (STREAK_BASE + STREAK_AMPLITUDE * streaks)
                    * (GRAIN_BASE + GRAIN_AMPLITUDE * grain)
                    * (CLOUD_BASE + CLOUD_AMPLITUDE * clouds);

                float particleDensity = radialMask * clumps;

                float radialBrightness = pow(saturate(innerRadius / orbitRadius), _DiskFalloff);

                float3 orbitTangent = normalize(planar.x * basisV - planar.y * basisU);
                float dopplerShift = dot(orbitTangent, -rayDirection);
                float beaming = pow(max(1.0 + _DopplerStrength * dopplerShift * DOPPLER_SHIFT_GAIN, 0.0), DOPPLER_BEAMING_EXPONENT);

                float heat = pow(saturate(innerRadius / orbitRadius), HEAT_FALLOFF_EXPONENT);
                float3 diskColor = lerp(_DiskColor.rgb, _DiskHotColor.rgb, saturate(heat + max(dopplerShift, 0.0) * _DopplerStrength));

                float3 baseEmission = diskColor * density * radialBrightness * beaming;
                float3 particleEmission = _DiskHotColor.rgb * particleDensity * radialBrightness * beaming * _ParticleIntensity;

                coverage = saturate((density + particleDensity) * DISK_COVERAGE_GAIN);
                return baseEmission + particleEmission;
            }

            Varyings VertexStage(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            struct BlackHoleSample
            {
                float3 emission;
                float transmittance;
                float3 background;
            };

            BlackHoleSample TraceBlackHole(float3 rayDirectionWS)
            {
                float simulationScale = 1.0 / (MESH_BOUND_RADIUS * _HorizonScale);
                float boundRadiusSim = MESH_BOUND_RADIUS * simulationScale;

                float3 rayOriginSim = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz * simulationScale;
                float3 rayDirectionSim = normalize(mul((float3x3)unity_WorldToObject, rayDirectionWS));

                // Binary layout, evaluated once per fragment. Masses follow horizon volume,
                // so the primary barely wobbles while the companion carries the orbit.
                bool hasCompanion = _SecondaryHorizonScale > 0.001;
                float companionMass = _SecondaryHorizonScale * _SecondaryHorizonScale * _SecondaryHorizonScale;
                float theta = _BinaryPhase + _Time.y * TWO_PI / max(_BinaryPeriod, 0.001);
                float3 lineOfCenters = float3(cos(theta), 0.0, sin(theta));
                float massSum = 1.0 + companionMass;
                float3 primaryCenter = -lineOfCenters * (_BinarySeparation * companionMass / massSum);
                float3 secondaryCenter = hasCompanion
                    ? lineOfCenters * (_BinarySeparation / massSum)
                    : float3(1e5, 1e5, 1e5);
                float companionCaptureRadius = CAPTURE_RADIUS * _SecondaryHorizonScale;
                float companionBend = GEODESIC_BEND_FACTOR * _SecondaryHorizonScale * sqrt(_SecondaryHorizonScale);

                float companionInner = _DiskInnerRadius * _SecondaryDiskScale;
                float companionOuter = _DiskOuterRadius * _SecondaryDiskScale;

                // Companion disk plane: local +Y tilted around local X.
                float tiltSin;
                float tiltCos;
                sincos(_SecondaryDiskTilt, tiltSin, tiltCos);
                float3 companionNormal = float3(0.0, tiltCos, tiltSin);
                float3 companionBasisU = float3(1.0, 0.0, 0.0);
                float3 companionBasisV = float3(0.0, tiltSin, -tiltCos);
                float3 primaryBasisU = float3(1.0, 0.0, 0.0);
                float3 primaryBasisV = float3(0.0, 0.0, 1.0);

                // The march enters and exits at the content sphere rather than the mesh
                // bound: nothing emissive or strongly bendable lives in the empty outer
                // shell, so rays start and stop at reach * CONTENT_EXIT_MARGIN instead.
                float companionOffset = _BinarySeparation / massSum;
                float systemReach = max(_DiskOuterRadius, hasCompanion ? companionOffset + companionOuter : 0.0);
                float exitRadius = min(boundRadiusSim, systemReach * CONTENT_EXIT_MARGIN);
                float exitRadiusSq = exitRadius * exitRadius;
                float maxStepLength = exitRadius * MAX_STEP_FRACTION_OF_BOUND;

                float midpointDistance = dot(-rayOriginSim, rayDirectionSim);
                float closestApproachSq = dot(rayOriginSim, rayOriginSim) - midpointDistance * midpointDistance;
                float halfChord = sqrt(max(exitRadiusSq - closestApproachSq, 0.0));
                // Rays that never approach the content sphere start already escaped: the
                // out-of-range entry fails the bound test on the first iteration, so the
                // sphere's edge annulus costs one step instead of a full empty chord.
                float entryDistance = closestApproachSq < exitRadiusSq
                    ? max(midpointDistance - halfChord, 0.0)
                    : 1e9;

                float3 position = rayOriginSim + rayDirectionSim * entryDistance;
                float3 velocity = rayDirectionSim;

                // Mobile halves the march budget; _StepCount still rules on desktop.
                #if BLACKHOLE_MOBILE_TIER
                float stepBudget = min(_StepCount, 96.0);
                #else
                float stepBudget = _StepCount;
                #endif

                float3 diskEmission = 0.0;
                float transmittance = 1.0;
                bool captured = false;

                [loop]
                for (int stepIndex = 0; stepIndex < MAX_RAYMARCH_STEPS; stepIndex++)
                {
                    if (stepIndex >= (int)stepBudget)
                    {
                        break;
                    }

                    float3 relPrimary = position - primaryCenter;
                    float3 relSecondary = position - secondaryCenter;
                    float primaryDistance = length(relPrimary);
                    float secondaryDistance = length(relSecondary);

                    if (primaryDistance < CAPTURE_RADIUS || secondaryDistance < companionCaptureRadius)
                    {
                        captured = true;
                        break;
                    }

                    if (dot(position, position) > exitRadiusSq && dot(position, velocity) > 0.0)
                    {
                        break;
                    }

                    // Steps shrink near the nearest horizon, where all the bending happens,
                    // and stretch out in the empty reaches between and beyond the holes.
                    float nearestHorizon = hasCompanion ? min(primaryDistance, secondaryDistance) : primaryDistance;
                    float stepLength = clamp(nearestHorizon * _StepSize, MIN_STEP_LENGTH, maxStepLength);

                    float3 momentum = cross(relPrimary, velocity);
                    float primaryDistanceSq = primaryDistance * primaryDistance;
                    float3 acceleration = -GEODESIC_BEND_FACTOR * dot(momentum, momentum)
                        * relPrimary / (primaryDistanceSq * primaryDistanceSq * primaryDistance);

                    if (hasCompanion)
                    {
                        float3 companionMomentum = cross(relSecondary, velocity);
                        float secondaryDistanceSq = secondaryDistance * secondaryDistance;
                        acceleration += -companionBend * dot(companionMomentum, companionMomentum)
                            * relSecondary / (secondaryDistanceSq * secondaryDistanceSq * secondaryDistance);
                    }

                    float3 previousPosition = position;
                    velocity += acceleration * stepLength;
                    position += velocity * stepLength;

                    if (transmittance <= TRANSMITTANCE_CUTOFF)
                    {
                        break;
                    }

                    if (previousPosition.y * position.y < 0.0)
                    {
                        float3 rayDirection = normalize(velocity);
                        float interpolant = previousPosition.y / (previousPosition.y - position.y);
                        float3 crossingPoint = lerp(previousPosition, position, interpolant);
                        float2 planar = crossingPoint.xz - primaryCenter.xz;
                        float coverage;
                        float3 crossingEmission = EvaluateDiskEmission(
                            planar, primaryBasisU, primaryBasisV, rayDirection,
                            _DiskInnerRadius, _DiskOuterRadius, coverage);
                        diskEmission += crossingEmission * transmittance;
                        transmittance *= 1.0 - coverage;
                    }

                    if (hasCompanion)
                    {
                        float previousSide = dot(previousPosition - secondaryCenter, companionNormal);
                        float currentSide = dot(position - secondaryCenter, companionNormal);
                        if (previousSide * currentSide < 0.0)
                        {
                            float3 rayDirection = normalize(velocity);
                            float interpolant = previousSide / (previousSide - currentSide);
                            float3 crossingPoint = lerp(previousPosition, position, interpolant);
                            float3 local = crossingPoint - secondaryCenter;
                            float2 planar = float2(dot(local, companionBasisU), dot(local, companionBasisV));
                            float coverage;
                            float3 crossingEmission = EvaluateDiskEmission(
                                planar, companionBasisU, companionBasisV, rayDirection,
                                companionInner, companionOuter, coverage);
                            diskEmission += crossingEmission * transmittance;
                            transmittance *= 1.0 - coverage;
                        }
                    }
                }

                BlackHoleSample result;
                result.emission = diskEmission;
                result.transmittance = transmittance;
                result.background = 0.0;

                if (!captured)
                {
                    // Escaped geodesic: sample the flowing sky along the bent exit direction.
                    // Same function as the skybox, so the silhouette edge is seamless and the
                    // drifting stars visibly smear into lensing arcs around the shadows.
                    float3 exitDirectionWS = normalize(mul((float3x3)unity_ObjectToWorld, normalize(velocity)));
                    result.background = SampleProceduralSky(SkyFlowDirection(exitDirectionWS, _Time.y));
                }

                return result;
            }

            half4 FragmentStage(Varyings input) : SV_Target
            {
                float3 rayDirectionWS = normalize(input.positionWS - _WorldSpaceCameraPos);
                BlackHoleSample result = TraceBlackHole(rayDirectionWS);
                float3 finalColor = result.emission * _DiskBrightness + result.transmittance * result.background;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
