// 天空盒：黑洞引力透镜效果
// ----------------------------------------------------------------------------
// 透镜模型：点质量引力透镜（爱因斯坦）弱场近似
//     beta = theta - thetaE^2 / theta
//     theta  = 视线与黑洞方向的世界角距
//     thetaE = 爱因斯坦半径（ Properties 中以角度配置）
//   theta > thetaE        → 主像（放大、被推离黑洞）
//   theta < thetaE        → 反向副像（倒像，向阴影边缘压缩），beta<0 沿大圆翻到另一侧
//   theta = thetaE        → 爱因斯坦环（正后方恒星被拉成环）
//   theta < 阴影半径       → 事件视界，纯黑
// 星空来源三种模式：Cubemap / 等距圆柱全景图 / 程序化星空（默认，贴图未到位也能预览透镜）
// 吸积盘：以黑洞为单位距离的平面求交得到角半径/盘面方位，程序化螺旋条纹 + 多普勒增亮。
// 建议相机开 HDR + Bloom，吸积盘/光子环输出大于 1 的 HDR 值。
// ----------------------------------------------------------------------------
Shader "Tide/Skybox/GravitationalLens"
{
    Properties
    {
        [Header(Star Source)]
        [Enum(Cubemap,0,Equirectangular,1,Procedural,2)] _StarMode ("Star Source", Float) = 2
        [NoScaleOffset] _StarCube ("Star Cubemap", Cube) = "" {}
        [NoScaleOffset] _StarMap ("Star Equirectangular Map (2D)", 2D) = "black" {}
        _MapYaw ("Equirect Yaw Offset (Degrees)", Float) = 0
        _StarExposure ("Star Exposure", Range(0.0, 8.0)) = 1.0

        [Header(Procedural Starfield)]
        _StarDensity ("Star Density", Range(0.0, 1.0)) = 0.28
        _StarSize ("Star Size (Degrees)", Range(0.01, 0.6)) = 0.1
        _StarScaleA ("Layer A Scale", Range(8.0, 90.0)) = 38
        _StarScaleB ("Layer B Scale", Range(20.0, 220.0)) = 120
        _StarTwinkle ("Star Twinkle", Range(0.0, 1.0)) = 0.4
        _NebulaIntensity ("Nebula Intensity", Range(0.0, 1.0)) = 0.16
        _NebulaColor ("Nebula Color", Color) = (0.32, 0.44, 0.8, 1)

        [Header(Black Hole Direction)]
        _LensYaw ("Azimuth (Degrees)", Range(-180.0, 180.0)) = 45
        _LensPitch ("Elevation (Degrees)", Range(-89.0, 89.0)) = 16

        [Header(Lens)]
        _EinsteinDeg ("Einstein Radius (Degrees)", Range(0.05, 30.0)) = 3.0
        _ShadowDeg ("Shadow Radius (Degrees)", Range(0.01, 30.0)) = 1.1
        _Chroma ("Chromatic Aberration", Range(0.0, 0.08)) = 0.008
        _PhotonRingIntensity ("Photon Ring Intensity", Range(0.0, 10.0)) = 1.6

        [Header(Einstein Ring Glow)]
        _RingColor ("Ring Glow Color", Color) = (0.62, 0.74, 1.0, 1)
        _RingIntensity ("Ring Glow Intensity", Range(0.0, 10.0)) = 0.5
        _RingWidthDeg ("Ring Glow Width (Degrees)", Range(0.05, 5.0)) = 0.55

        [Header(Accretion Disk)]
        [Toggle(_DISK_ON)] _DiskOn ("Enable Accretion Disk", Float) = 1
        _DiskNormal ("Disk Plane Normal (World)", Vector) = (0.28, 1.0, 0.12, 0)
        _DiskInnerDeg ("Inner Radius (Degrees)", Range(0.05, 40.0)) = 1.4
        _DiskOuterDeg ("Outer Radius (Degrees)", Range(0.2, 60.0)) = 6.5
        _DiskIntensity ("Disk Intensity", Range(0.0, 20.0)) = 3.0
        _DiskTwist ("Spiral Twist", Range(0.0, 10.0)) = 2.8
        _DiskSpeed ("Flow Speed", Range(-4.0, 4.0)) = 0.3
        _DiskNoiseScale ("Noise Scale", Range(0.3, 10.0)) = 2.2
        _DiskDoppler ("Doppler Beaming", Range(-1.0, 1.0)) = 0.55
        _DiskColorHot ("Hot Color", Color) = (1.0, 0.93, 0.75, 1)
        _DiskColorCool ("Cool Color", Color) = (1.0, 0.42, 0.14, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "IgnoreProjector" = "True"
            "PreviewType" = "Skybox"
        }

        Pass
        {
            Name "GravitationalLensSkybox"
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma shader_feature_local _DISK_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 注意：本管线（dbdrp）UnityPerMaterial 只允许 float/float4，
            // 且声明顺序必须与 Properties 块一致（贴图属性用 NoScaleOffset，不需要 _ST）。
            CBUFFER_START(UnityPerMaterial)
                float  _StarMode;
                float  _MapYaw;
                float  _StarExposure;
                float  _StarDensity;
                float  _StarSize;
                float  _StarScaleA;
                float  _StarScaleB;
                float  _StarTwinkle;
                float  _NebulaIntensity;
                float4 _NebulaColor;
                float  _LensYaw;
                float  _LensPitch;
                float  _EinsteinDeg;
                float  _ShadowDeg;
                float  _Chroma;
                float  _PhotonRingIntensity;
                float4 _RingColor;
                float  _RingIntensity;
                float  _RingWidthDeg;
                float  _DiskOn;
                float4 _DiskNormal;
                float  _DiskInnerDeg;
                float  _DiskOuterDeg;
                float  _DiskIntensity;
                float  _DiskTwist;
                float  _DiskSpeed;
                float  _DiskNoiseScale;
                float  _DiskDoppler;
                float4 _DiskColorHot;
                float4 _DiskColorCool;
            CBUFFER_END

            TEXTURECUBE(_StarCube);
            SAMPLER(sampler_StarCube);
            TEXTURE2D(_StarMap);
            SAMPLER(sampler_StarMap);

            #define TL_PI 3.14159265359
            #define TL_DEG2RAD (TL_PI / 180.0)

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirWS      : TEXCOORD0;
            };

            // 天空盒网格以相机为中心、世界固定朝向绘制，物体空间坐标即世界空间方向
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToClipPos(input.positionOS.xyz);
                output.dirWS = input.positionOS.xyz;
                return output;
            }

            // ---------------- 哈希与噪声 ----------------

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash13(i);
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));
                return lerp(
                    lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                    lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
                    f.z);
            }

            float Fbm3(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += a * ValueNoise(p);
                    p = p * 2.03 + float3(11.7, 5.1, 7.3);
                    a *= 0.5;
                }
                return v;
            }

            // ---------------- 程序化星空 ----------------

            float3 ProceduralStarLayer(float3 dir, float scale, float density, float sizeDeg,
                                       float brightness, float twinkle, float t, float seed)
            {
                float3 p = dir * scale + seed;
                float3 cell = floor(p);
                float3 hA = Hash33(cell + 7.31);
                float3 hB = Hash33(cell + 41.7);
                // 星点留在单元格中央区域内，避免跨格截断
                float3 starDir = normalize(cell + 0.5 + (hB - 0.5) * 0.44);
                float ang = acos(clamp(dot(dir, starDir), -1.0, 1.0));
                float sizeRad = sizeDeg * TL_DEG2RAD * (0.55 + 0.9 * hA.y);
                float core = saturate(1.0 - ang / sizeRad);
                core = core * core * core;
                float on = step(1.0 - density, hA.x);
                float tw = 1.0 - twinkle + twinkle * (0.5 + 0.5 * sin(t * (1.5 + 4.0 * hA.z) + hA.x * 44.0));
                float b = brightness * (0.2 + 0.8 * hA.z);
                float3 tint = lerp(float3(1.0, 0.82, 0.62), float3(0.70, 0.80, 1.0), hA.y);
                return tint * (core * on * tw * b);
            }

            float3 ProceduralStarfield(float3 dir)
            {
                float t = _Time.y;
                float3 col = ProceduralStarLayer(dir, _StarScaleA, _StarDensity, _StarSize * 1.4, 1.0, _StarTwinkle, t, 0.0);
                col += ProceduralStarLayer(dir, _StarScaleB, _StarDensity * 0.7, _StarSize * 0.8, 0.5, _StarTwinkle, t, 100.0);
                float3 neb = _NebulaColor.rgb * Fbm3(dir * 3.1) * (0.75 + 0.25 * Fbm3(dir * 6.7 + 19.0));
                return (col + neb * _NebulaIntensity) * _StarExposure;
            }

            // ---------------- 星空采样 ----------------

            float2 DirectionToEquirect(float3 d, float yawRad)
            {
                float u = atan2(d.x, -d.z) / (2.0 * TL_PI) + 0.5 + yawRad / (2.0 * TL_PI);
                u = frac(u);
                float v = asin(clamp(d.y, -1.0, 1.0)) / TL_PI + 0.5;
                return float2(u, v);
            }

            float3 SampleStarSource(float3 dir)
            {
                if (_StarMode < 0.5)
                    return SAMPLE_TEXTURECUBE(_StarCube, sampler_StarCube, dir).rgb * _StarExposure;
                if (_StarMode < 1.5)
                {
                    float2 uv = DirectionToEquirect(dir, _MapYaw * TL_DEG2RAD);
                    return SAMPLE_TEXTURE2D(_StarMap, sampler_StarMap, uv).rgb * _StarExposure;
                }
                return ProceduralStarfield(dir);
            }

            // ---------------- 引力透镜 ----------------

            float3 LensDirectionFromAngles(float yawDeg, float pitchDeg)
            {
                float cp = cos(pitchDeg * TL_DEG2RAD);
                return float3(cp * sin(yawDeg * TL_DEG2RAD),
                              sin(pitchDeg * TL_DEG2RAD),
                              cp * cos(yawDeg * TL_DEG2RAD));
            }

            float3 AnyPerpendicular(float3 v)
            {
                float3 a = (abs(v.y) < 0.9) ? float3(0, 1, 0) : float3(1, 0, 0);
                return normalize(cross(a, v));
            }

            // 点质量透镜方程重映射。输出 theta 为原始视线角距（供阴影/辉光使用）。
            float3 ApplyPointLens(float3 dir, float3 lensDir, float thetaE, out float theta)
            {
                float cosT = dot(dir, lensDir);
                theta = acos(clamp(cosT, -1.0, 1.0));
                // 大圆切向基：u 指向原视线一侧；beta<0 时 sin 为负，自动翻到黑洞另一侧（副像）
                float3 u = dir - lensDir * cosT;
                float ul = length(u);
                u = (ul > 1e-5) ? u / ul : AnyPerpendicular(lensDir);
                float beta = theta - thetaE * thetaE / max(theta, 1e-5);
                beta = clamp(beta, -TL_PI, TL_PI);
                return lensDir * cos(beta) + u * sin(beta);
            }

            // ---------------- 吸积盘 ----------------

#if defined(_DISK_ON)
            // 视线与"过黑洞、以 _DiskNormal 为法线"的平面求交（黑洞取单位距离），
            // 得到盘面角半径与盘面方位角。diskPhi 同时供环辉光的多普勒相位调制。
            float3 AccretionDisk(float3 dir, float3 lensDir, float3 diskNormal, out float diskPhi)
            {
                diskPhi = 0.0;
                float dn = dot(dir, diskNormal);
                if (abs(dn) < 1e-4)
                    return float3(0.0, 0.0, 0.0);
                float t = dot(lensDir, diskNormal) / dn;
                if (t <= 0.0)
                    return float3(0.0, 0.0, 0.0);

                float3 p = dir * t - lensDir; // 盘面内相对黑洞的偏移（弧度近似）
                float rDeg = length(p) / TL_DEG2RAD;
                if (rDeg >= _DiskOuterDeg)
                    return float3(0.0, 0.0, 0.0);

                float3 e1v = cross(diskNormal, lensDir);
                float3 e1 = (length(e1v) > 1e-5) ? normalize(e1v) : AnyPerpendicular(lensDir);
                float3 e2 = cross(diskNormal, e1);
                diskPhi = atan2(dot(p, e2), dot(p, e1));

                float u = saturate((rDeg - _DiskInnerDeg) / max(_DiskOuterDeg - _DiskInnerDeg, 1e-3));
                float env = smoothstep(0.0, 0.1, u) * (1.0 - smoothstep(0.45, 1.0, u));

                // 对数螺旋条纹：内圈转速快（开普勒差速）
                float sp = diskPhi + _DiskTwist * (1.1 - u) + _Time.y * _DiskSpeed / max(u, 0.12);
                float streak = 0.45 + 0.55 * Fbm3(float3(cos(sp), sin(sp), 0.35) * (length(p) * _DiskNoiseScale)
                                                  + float3(0.0, 0.0, length(p) * 1.7));

                // 多普勒增束：接近侧更亮偏蓝，远离侧更暗偏红
                float dopp = _DiskDoppler * sin(diskPhi);
                float3 baseCol = lerp(_DiskColorHot.rgb, _DiskColorCool.rgb, pow(u, 0.75));
                float3 shift = lerp(float3(1.18, 0.82, 0.62), float3(0.82, 0.95, 1.35), saturate(dopp * 0.5 + 0.5));
                return baseCol * shift * (env * streak * (1.0 + dopp) * _DiskIntensity);
            }
#endif

            float4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dirWS);

                float3 lensDir = LensDirectionFromAngles(_LensYaw, _LensPitch);
                float thetaE = _EinsteinDeg * TL_DEG2RAD;
                float shadowR = _ShadowDeg * TL_DEG2RAD;

                // ---- 被透镜弯折后的星空采样 ----
                float theta;
                float3 remapped = ApplyPointLens(dir, lensDir, thetaE, theta);

                float3 col;
                if (_Chroma > 0.0005)
                {
                    // 透镜本身无色差，这里按通道微调爱因斯坦半径做艺术化色散
                    float tIgnored;
                    float3 dirR = ApplyPointLens(dir, lensDir, thetaE * (1.0 - _Chroma), tIgnored);
                    float3 dirB = ApplyPointLens(dir, lensDir, thetaE * (1.0 + _Chroma), tIgnored);
                    col.r = SampleStarSource(dirR).r;
                    col.g = SampleStarSource(remapped).g;
                    col.b = SampleStarSource(dirB).b;
                }
                else
                {
                    col = SampleStarSource(remapped);
                }

                // ---- 爱因斯坦环辉光 + 阴影边缘光子环 ----
                float ringWidth = max(_RingWidthDeg * TL_DEG2RAD, 1e-4);
                float ring = exp(-pow((theta - thetaE) / ringWidth, 2.0)) * _RingIntensity;
                float photonWidth = max(shadowR * 0.12, 0.05 * TL_DEG2RAD);
                float photonRing = exp(-pow((theta - shadowR * 1.05) / photonWidth, 2.0)) * _PhotonRingIntensity;

                // ---- 吸积盘（画在星空之上，自身不参与透镜弯折） ----
                float3 diskCol = float3(0.0, 0.0, 0.0);
                float diskPhi = 0.0;
#if defined(_DISK_ON)
                diskCol = AccretionDisk(dir, lensDir, normalize(_DiskNormal.xyz), diskPhi);
                // 艺术近似：被透镜弯到阴影上方的盘背光，用环辉光随多普勒相位起伏来暗示
                ring *= 1.0 + 0.8 * _DiskDoppler * sin(diskPhi);
#endif

                float3 sky = col
                           + _RingColor.rgb * ring
                           + lerp(_DiskColorHot.rgb, _RingColor.rgb, 0.4) * photonRing
                           + diskCol;

                // ---- 事件视界阴影：视线落入即纯黑 ----
                float shadowMask = smoothstep(shadowR, shadowR * 1.03 + 1e-6, theta);
                return float4(sky * shadowMask, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
