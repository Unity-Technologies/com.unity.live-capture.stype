Shader "Hidden/Stype/Hdrp/LensDistortionBrownConrady"
{
    HLSLINCLUDE
    #pragma target 4.5
    #pragma multi_compile _ USING_OVERSCAN
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"
    #include "Packages/com.unity.film-internal-utilities/Shaders/Include/LensDistortion.cginc"

    float4 _ScaleAndBias;
    float4 _LensDistortionMainParams;
    float2 _SensorSize;

    float2 ApplyScaleAndBias(float2 xy)
    {
#if defined(USING_OVERSCAN)
        return xy * _ScaleAndBias.xy + _ScaleAndBias.zw;
#else
        return xy;
#endif
    }

    float2 ApplyScaleAndBiasInverse(float2 xy)
    {
#if defined(USING_OVERSCAN)
        return (xy - _ScaleAndBias.zw) / _ScaleAndBias.xy;
#else
        return xy;
#endif
    }

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vertex(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_InputTexture);

    float4 _OutOfViewportColor;
    float4 _GridColor;
    float _GridResolution;
    float _GridLineWidth;

    float EvaluateGrid(float2 uv)
    {
        float screenAspect = _ScreenSize.x / _ScreenSize.y;
        float2 steps = frac((uv + 0.5) * _GridResolution * float2(screenAspect, 1));
        float2 dSteps = fwidth(steps);
        float2 grid = pow(max(
            smoothstep(_GridLineWidth * dSteps * 0.5, 0, steps),
            smoothstep(1 - _GridLineWidth * dSteps * 0.5, 1, steps)), 4);

        return grid.x + grid.y;
    }

    float4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float depth = LoadCameraDepth(input.positionCS.xy);
        PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float2 position = ApplyScaleAndBiasInverse(posInput.positionNDC);
        float2 distortedPosition = CalculateDistortedPosition(position * _ScreenSize.xy, 1.0,
            _LensDistortionMainParams, _SensorSize.xy);
        distortedPosition /= _ScreenSize.xy;
        distortedPosition = ApplyScaleAndBias(distortedPosition);

        float4 distortedColor = SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(distortedPosition.xy));

        if (distortedPosition.x < 0 || distortedPosition.x > 1.0 || distortedPosition.y > 1.0 || distortedPosition.y < 0.0)
        {
            distortedColor = _OutOfViewportColor;
        }

        return distortedColor;
    }

    float4 FragmentWithGrid(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float depth = LoadCameraDepth(input.positionCS.xy);
        PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float2 distortedPosition = CalculateDistortedPosition(posInput.positionNDC * _ScreenSize.xy, 1.0,
            _LensDistortionMainParams, _SensorSize.xy);

        float4 distortedColor = SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(distortedPosition.xy));

        if (distortedPosition.x < 0 || distortedPosition.x > 1.0 || distortedPosition.y > 1.0 || distortedPosition.y < 0.0)
        {
            distortedColor = _OutOfViewportColor;
        }

        float grid = EvaluateGrid(input.texcoord);
        return lerp(distortedColor, _GridColor, grid);
    }

    ENDHLSL

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.high-definition" : "14.0"
        }
        Pass
        {
            Name "Standard"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
        Pass
        {
            Name "Grid"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentWithGrid
            ENDHLSL
        }
    }
    Fallback Off
}
