Shader "Hidden/VHSSmear"
{
    HLSLINCLUDE

        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"


        TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
        //float4 _MainTex_TexelSize;
        float4 _TexelSize;
        float2 _SmearOffsetAttenuation;
#define SMEAR_LENGTH 4
        float4 SmearFrag(VaryingsDefault i) : SV_Target
        {
            float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
            float energy = 1;
            [unroll]
            for (uint o = 1; o <= SMEAR_LENGTH; o++)
            {
                float falloff = exp(-_SmearOffsetAttenuation.y * o);
                energy += falloff;
                float uvx = i.texcoord.x - _TexelSize.x * _SmearOffsetAttenuation.x * o;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(uvx, i.texcoord.y)) * falloff * (uvx > 0);
            }
            return color / energy;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment SmearFrag
            ENDHLSL
        }
    }
}
