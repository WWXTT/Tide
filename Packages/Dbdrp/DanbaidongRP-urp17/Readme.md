<div align="center">
<img src="https://img.shields.io/badge/Unity ver-6000.0.30f1-red"/>
<img src="https://img.shields.io/github/last-commit/danbaidong1111/DanbaidongRP" alt="last-commit" />
<img src="https://img.shields.io/badge/Author-Danbaidong-pink"/>

<a href="https://www.zhihu.com/people/danbaidong1111" target="_blank">
  <img src="https://img.shields.io/badge/Zhihu-%E8%9B%8B%E7%99%BD%E8%83%A8-gray?style=flat&logo=zhihu&logoSize=auto&labelColor=white&link=https%3A%2F%2Fwww.zhihu.com%2Fpeople%2Fdanbaidong1111"/>
</a>

<a href="https://space.bilibili.com/39694821" target="_blank">
  <img src="https://img.shields.io/badge/bilibili-%E5%AF%8C%E5%90%AB%E8%8A%9D%E5%A3%AB%E7%9A%84%E8%9B%8B%E7%99%BD%E8%83%A8-gray?style=flat&logo=bilibili&logoColor=white&logoSize=auto&labelColor=%23FF69B4&link=https%3A%2F%2Fspace.bilibili.com%2F39694821"/>
</a>

# **Danbaidong Render Pipeline**

## Announcement

Just add something I need.

`DanbaidongRP` is a custom render pipeline built based on `Universal RP 17.0.3`, `Unity 6 (6000.0.30f1)`. This pipeline is optimized for both PBR and NPR Toon Rendering, offering enhanced flexibility for various rendering needs. Future updates will include additional features tailored to my specific requirements.

</div>

Subscribe:

* https://space.bilibili.com/39694821
* https://www.zhihu.com/people/danbaidong1111

Roadmap & Features:

- [x] PBR Toon Shader
- [ ] Cel shading
- [x] Danbaidong Shader GUI
- [x] PerObjectShadow
- [x] PCSS/PCF Soft Shadows
- [x] Shadow Scattering
- [x] Ray Tracing Shadow
- [ ] Transparent Shadows
- [x] Toon Toonmapping
- [x] Toon Bloom
- [x] Visual Sky
- [x] High Quality SSR (Ray Tracing)
- [ ] High Quality SSAO
- [ ] High Quality SSGI
- [x] Cluster Deferred lighting
- [x] Character Forward lighting
- [ ] Atmosphere Fog
- [ ] Idol Live アイドル!!!!!

# Usage

* Create a new Unity 6 URP project <span style="color: #FF8C00;">(6000.0.30f1)</span>.
* *Window -> Package Manager*, remove *Universal RP* and delete the contents of the *Assets/Settings* folder.
* Add *DanbaidongRP* through the *Package Manager*. It is recommended to use the "Install package from disk..." option to individually add the [Core](https://github.com/danbaidong1111/DanbaidongRPCore), [Config](https://github.com/danbaidong1111/DanbaidongRPConfig), and [DanbaidongRP](https://github.com/danbaidong1111/DanbaidongRP) packages (select the `package.json` file for each).
* In the *Settings* directory, recreate the DanbaidongRP Asset by right-clicking and selecting *Create -> Rendering -> Danbaidong RP Asset and Renderer*. Then, in *Edit -> Project Settings -> Graphics*, set the *Default Renderer Pipeline* to the pipeline Asset you just created. The scene should now render correctly.
* Next, restart the project. A Wizard window will pop up, which contains the necessary configurations for the pipeline to use correctly.

# Documents

[DanbaidongRP Documents](https://miusjun13qu.feishu.cn/docx/EXPtdrNmnox8hkx4mnCcy8QNn2b?from=from_copylink) (CN)

# Character Rendering

![ToonRenderingSuomi](ReadmeAssets~/202409011.png)

![ToonRenderingDirect](ReadmeAssets~/202311071.PNG)

![ToonRenderingPunctual](ReadmeAssets~/202311072.PNG)

# Danbaidong Shader GUI

CustomEditor "UnityEditor.DanbaidongGUI.DanbaidongGUI"`

![ShaderGUI](ReadmeAssets~/202311073.PNG)

![GradientEditor](ReadmeAssets~/202311074.PNG)

# Shadows

**Shadow Scattering**

![ShadowScatter](ReadmeAssets~/202409012.png)

**Per Object Shadow**

![PerObjectShadow](ReadmeAssets~/202311075.PNG)

![PerObjectShadowmap](ReadmeAssets~/202311076.PNG)

# Reflections (RayTracing)

![Reflection](ReadmeAssets~/202409013.png)

![ReflectionScene](ReadmeAssets~/202410021.png)

# GPU Lights (Cluster)

![GPULights](ReadmeAssets~/202409014.png)

# Others

<br/>
<table><tr><td valign="top" width="50%">
<div align="center" valign="top">

