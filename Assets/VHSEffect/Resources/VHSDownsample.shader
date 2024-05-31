Shader "Hidden/VHSDownsample"
{
    HLSLINCLUDE

    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

    TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
    float4 _MainTex_TexelSize;
    float _Blend;
    TEXTURE2D(_Noise);
    float _NoiseOpacity;
    uniform float4 _OddScale;
    uniform float _BlurBias;

    float4 FragDown(VaryingsDefault i) : SV_Target
    {
        i.texcoord.xy /= _OddScale.xy;
        float2 offset = _MainTex_TexelSize.xy * _OddScale.xy;
        float2 halfPixel = _MainTex_TexelSize.xy * -0.5 * _OddScale.xy;
        float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(offset.x, offset.y) + halfPixel)
                     + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(0, offset.y) + halfPixel)
                     + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(offset.x, 0) + halfPixel)
                     + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(0, 0) + halfPixel);
        color *= 0.25;
        color.rgb += SAMPLE_TEXTURE2D(_Noise, sampler_MainTex, i.texcoord).r * _NoiseOpacity;
        return color;
    }

    float4 FragBlur(VaryingsDefault i) : SV_Target
    {
        float left = -1 - _BlurBias;
        float right = 1 - _BlurBias;
        float2 blur = _MainTex_TexelSize.xy * float2(1, 0.5);
        float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(blur.x * left, -blur.y))
                     + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(blur.x * right, -blur.y))
                     + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(blur.x * left, blur.y))
                     + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + float2(blur.x * right, blur.y));
        color *= 0.25;
        return color;
    }

    float _UpsampleBlend;
    float4 FragUp(VaryingsDefault i) : SV_Target
    {
        float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
        color.a = _UpsampleBlend;
        return color;
    }

        ENDHLSL

        SubShader
    {
        Cull Off ZWrite Off ZTest Always

         Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment FragDown
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment FragUp
            ENDHLSL
        }
    }
}
