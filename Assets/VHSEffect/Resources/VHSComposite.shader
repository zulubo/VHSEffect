Shader "Hidden/VHSComposite"
{
    HLSLINCLUDE

    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

    TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
    float4 _MainTex_TexelSize;
    TEXTURE2D_SAMPLER2D(_VHSSlightBlurredTex, sampler_VHSSlightBlurredTex);
    TEXTURE2D_SAMPLER2D(_VHSBlurredTex, sampler_VHSBlurredTex);
    TEXTURE2D_SAMPLER2D(_VHSSmearedTex, sampler_VHSSmearedTex);
    TEXTURE2D_SAMPLER2D(_Grain, sampler_Grain);
    float _ColorBleedIntensity;
    float _SmearIntensity;
    float _EdgeIntensity;
    float _EdgeDistance;
    float _GrainIntensity;
    float4 _GrainScaleOffset;
    

    float3 RGBToYCbCr(float3 rgb) {
        return float3(0.0625 + 0.257 * rgb.r + 0.50412 * rgb.g + 0.0979 * rgb.b,
            0.5 - 0.14822 * rgb.r - 0.290 * rgb.g + 0.43921 * rgb.b,
            0.5 + 0.43921 * rgb.r - 0.3678 * rgb.g - 0.07142 * rgb.b);
    }
    float3 YCbCrToRGB(float3 ycbcr) {
        
        ycbcr -= float3(0.0625, 0.5, 0.5);
        return float3(1.164 * ycbcr.x + 1.596 * ycbcr.z,
            1.164 * ycbcr.x - 0.392 * ycbcr.y - 0.813 * ycbcr.z,
            1.164 * ycbcr.x + 2.017 * ycbcr.y);
    }

    float4 Frag(VaryingsDefault i) : SV_Target
    {
        float2 quarterpixel = _MainTex_TexelSize.xy * 0.25;

        float4 sharpColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + quarterpixel);

        float3 edges = sharpColor.rgb + 0.5 - (SAMPLE_TEXTURE2D(_VHSSlightBlurredTex, sampler_VHSSlightBlurredTex, i.texcoord - float2(_EdgeDistance, 0)).rgb);
        sharpColor.rgb += (edges - 0.5) * _EdgeIntensity;
        
        float3 smearedColor = SAMPLE_TEXTURE2D(_VHSSmearedTex, sampler_VHSSmearedTex, i.texcoord).rgb;
        sharpColor.rgb = lerp(sharpColor.rgb, smearedColor.rgb, _SmearIntensity);
        
        sharpColor.xyz = RGBToYCbCr(sharpColor.rgb);
        
        float3 blurredColor = RGBToYCbCr(SAMPLE_TEXTURE2D(_VHSBlurredTex, sampler_VHSBlurredTex, i.texcoord).rgb).xyz;
        float2 colorGrain = RGBToYCbCr(SAMPLE_TEXTURE2D(_Grain, sampler_Grain, (i.texcoord - _GrainScaleOffset.zw) * _GrainScaleOffset.xy).rgb).yz;
        float lumGrain = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, (i.texcoord - _GrainScaleOffset.zw) * _GrainScaleOffset.xy * 4 - 0.5).g;
        sharpColor.yz = lerp(sharpColor.yz, blurredColor.yz, _ColorBleedIntensity);
        sharpColor.yz += (colorGrain.xy - 0.5) * _GrainIntensity * sharpColor.x;
        sharpColor.x *= 1 + (lumGrain - 0.5) * _GrainIntensity * 0.5;

        return float4(YCbCrToRGB(sharpColor.rgb), sharpColor.a);
    }

        ENDHLSL

        SubShader
    {
        Cull Off ZWrite Off ZTest Always

            Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment Frag

            ENDHLSL
        }
    }
}
