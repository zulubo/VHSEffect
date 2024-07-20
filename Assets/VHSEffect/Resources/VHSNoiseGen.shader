Shader "Hidden/VHSNoiseGen"
{
    HLSLINCLUDE

        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

        TEXTURE2D_SAMPLER2D(_HorizontalNoise, sampler_HorizontalNoise);
        TEXTURE2D_SAMPLER2D(_StripeNoise, sampler_StripeNoise);
        float _HorizontalNoisePos;
        float _HorizontalNoisePower;
        float4 _StripeNoiseScaleOffset;
        float _Blend;

        float NoiseFrag(VaryingsDefault i) : SV_Target
        {
            float horizontalNoise = SAMPLE_TEXTURE2D(_HorizontalNoise, sampler_HorizontalNoise, float2(_HorizontalNoisePos, i.texcoord.y)).r;
            float2 stripeNoise = SAMPLE_TEXTURE2D(_StripeNoise, sampler_StripeNoise, (i.texcoord - _StripeNoiseScaleOffset.zw) * _StripeNoiseScaleOffset.xy).rg;
            return stripeNoise.r * (stripeNoise.g > pow((1-horizontalNoise)* (1 - horizontalNoise), _HorizontalNoisePower));
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment NoiseFrag
            ENDHLSL
        }
    }
}
