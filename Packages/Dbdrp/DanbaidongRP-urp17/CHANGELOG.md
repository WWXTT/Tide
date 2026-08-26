# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- High Quality Screen Space Ambient Occlusion (XeGTAO based), including denoiser and integration into the Screen Space Lighting pipeline.
- Ray Traced Ambient Occlusion path and related shader/texture resources.
- Ambient Occlusion volume override and inspector support.

### Changed

- Updated blue noise / STBN sampling textures and import settings for improved temporal stability.
- Updated render pipeline resources to register new AO shaders, textures and history buffers.

### Fixed

- Minor issues in related Screen Space Lighting and Ray Tracing integration.

## [6000.0.30f1 ver4] - 2025-09-02

This version is compatible with Unity 6000.0.30f1.

### Added

- RayTracing Shadows. (Character use half dir for face&hair)

### Changed

### Fixed

- RayTracing reflection color bug. LitGBufferPass GI only enable when LIGHTMAP_ON keyword enable. see GlobalIlumation.
- PBRToonFace add FLAG_FACE for raytracing face shadows.

##
