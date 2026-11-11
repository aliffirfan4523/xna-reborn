# xna40-effects

Original synthetic effect files written for this repository. They contain no third-party code or assets and are released under the repository license. Each file is under 40 lines.

## Purpose

A tiny XNA 4.0 project whose content project lists small `.fx` files, one for each construct the effect rewriters care about:

- `Sm2.fx`: fixed `vs_2_0` and `ps_2_0` in `compile` statements.
- `Sm3.fx`: fixed `vs_3_0` and `ps_3_0`.
- `Template.fx`: unconditional `#define VS_SHADERMODEL` and `PS_SHADERMODEL`.
- `Portable.fx`: models already chosen per target with `#if OPENGL`.
- `MipBias.fx`: the sampler state `MipMapLodBias`.
- `Gaussian.fx`: `GaussianQuad` and `PyramidalQuad` filters.
- `AlphaTest.fx`: `AlphaTestEnable`, `AlphaFunc` and `AlphaRef` pass states.
- `Annotated.fx`: an annotation, arguments in `compile`, and `SetPixelShader`.
- `Vpos.fx`: a `VPOS` pixel input.
- `Defaults.fx`: a parameter with a default value and unused parameters.
- `WithInclude.fx`: an `#include` of `Common.fxh`.

It is never built.

## What the analyzer should find

- XNA017 for the `.fx` content items.
- Fixed shader models, unsupported pass states and old sampler names (rules added later in the audio-and-shaders spec).