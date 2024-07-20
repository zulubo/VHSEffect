using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Collections.Generic;

[Serializable]
[PostProcess(typeof(VHSRenderer), PostProcessEvent.AfterStack, "Custom/VHS", true)]
public sealed class VHS : PostProcessEffectSettings
{
    [Range(0f, 1f)]
    public FloatParameter colorBleedingIntensity = new FloatParameter { value = 0.5f };
    [Range(0, 1), Tooltip("Color bleed iterations")]
    public FloatParameter colorBleedRadius = new FloatParameter { value = 0.5f };
    [Range(-1, 1), Tooltip("Color bleed direction")]
    public FloatParameter colorBleedDirection = new FloatParameter { value = 0.0f };
    [Range(0, 0.8f), Tooltip("Smear Intensity")]
    public FloatParameter smearIntensity = new FloatParameter { value = 0.0f };
    [Range(0f, 1f)]
    public FloatParameter grainIntensity = new FloatParameter { value = 0.1f };
    [Range(0.01f, 2f)]
    public FloatParameter grainScale = new FloatParameter { value = 0.1f };
    [Range(0f, 1f)]
    public FloatParameter stripeNoiseDensity = new FloatParameter { value = 0.1f };
    [Range(0f, 1f)]
    public FloatParameter stripeNoiseOpacity = new FloatParameter { value = 1f };
    [Range(0f, 2f)]
    public FloatParameter edgeIntensity = new FloatParameter { value = 0.5f };
    [Range(0f, 0.005f)]
    public FloatParameter edgeDistance = new FloatParameter { value = 0.002f };

    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {
        return base.IsEnabledAndSupported(context) && (colorBleedingIntensity > 0 || edgeIntensity > 0 || (stripeNoiseDensity > 0 && stripeNoiseOpacity > 0) || grainIntensity > 0);
    }
}

public sealed class VHSRenderer : PostProcessEffectRenderer<VHS>
{
    int[] blurPyramid;

    Texture2D grainTex
    {
        get
        {
            if (_grainTex == null) _grainTex = (Texture2D)Resources.Load("vhsGrain", typeof(Texture2D));
            return _grainTex;
        }
    }
    private Texture2D _grainTex;
    Texture2D horizontalNoiseTex
    {
        get
        {
            if (_horizontalNoiseTex == null) _horizontalNoiseTex = (Texture2D)Resources.Load("horizontalNoise", typeof(Texture2D));
            return _horizontalNoiseTex;
        }
    }
    private Texture2D _horizontalNoiseTex;
    Texture2D speckNoiseTex
    {
        get
        {
            if (_speckNoiseTex == null) _speckNoiseTex = (Texture2D)Resources.Load("speckNoise", typeof(Texture2D));
            return _speckNoiseTex;
        }
    }
    private Texture2D _speckNoiseTex;
    Texture2D stripeNoiseTex
    {
        get
        {
            if (_stripeNoiseTex == null) _stripeNoiseTex = (Texture2D)Resources.Load("stripeNoise", typeof(Texture2D));
            return _stripeNoiseTex;
        }
    }
    private Texture2D _stripeNoiseTex;

    Shader shader_downsample
    {
        get
        {
            if (_shader_downsample == null) _shader_downsample = Shader.Find("Hidden/VHSDownsample");
            return _shader_downsample;
        }
    }
    private Shader _shader_downsample;
    Shader shader_noiseGen
    {
        get
        {
            if (_shader_noiseGen == null) _shader_noiseGen = Shader.Find("Hidden/VHSNoiseGen");
            return _shader_noiseGen;
        }
    }
    private Shader _shader_noiseGen;
    Shader shader_smear
    {
        get
        {
            if (_shader_smear == null) _shader_smear = Shader.Find("Hidden/VHSSmear");
            return _shader_smear;
        }
    }
    private Shader _shader_smear;
    Shader shader_composite
    {
        get
        {
            if (_shader_composite == null) _shader_composite = Shader.Find("Hidden/VHSComposite");
            return _shader_composite;
        }
    }
    private Shader _shader_composite;

    private class VHSState
    {
        public float horizontalNoisePos;
    }
    private static Dictionary<Camera, VHSState> cameraStates = new();

    int PyramidID(int i) => Shader.PropertyToID("_VHSPyramid" + i);

    static readonly int noiseBuffer = Shader.PropertyToID("_NoiseBuffer");
    static readonly int smearBuffer = Shader.PropertyToID("_SmearBuffer");
    static readonly int smearBuffer2 = Shader.PropertyToID("_SmearBuffer2");

    public override void Render(PostProcessRenderContext context)
    {
        // update state
        if (!cameraStates.TryGetValue(context.camera, out VHSState state))
        {
            state = new VHSState();
            cameraStates[context.camera] = state;
        }
        state.horizontalNoisePos += Time.deltaTime * 0.004f;
        if (UnityEngine.Random.value < 0.01f) state.horizontalNoisePos += UnityEngine.Random.value;
        state.horizontalNoisePos = Mathf.Repeat(state.horizontalNoisePos, 1);

        bool enableNoise = settings.stripeNoiseDensity > 0 && settings.stripeNoiseOpacity > 0;
        if(enableNoise)
        {
            // create noise buffer
            int nw = Mathf.Min(640, Mathf.RoundToInt(context.width * 0.5f));
            int nh = Mathf.Min(480, Mathf.RoundToInt(context.height * 0.5f));
            context.GetScreenSpaceTemporaryRT(context.command, noiseBuffer, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Default, FilterMode.Bilinear, nw, nh);
            var noiseSheet = context.propertySheets.Get(shader_noiseGen);
            noiseSheet.properties.SetTexture("_HorizontalNoise", horizontalNoiseTex);
            noiseSheet.properties.SetFloat("_HorizontalNoisePos", state.horizontalNoisePos);
            noiseSheet.properties.SetFloat("_HorizontalNoisePower", settings.stripeNoiseDensity * settings.stripeNoiseDensity);
            noiseSheet.properties.SetTexture("_StripeNoise", stripeNoiseTex);
            noiseSheet.properties.SetVector("_StripeNoiseScaleOffset", new Vector4(nw / (float)stripeNoiseTex.width, nh / (float)stripeNoiseTex.height, UnityEngine.Random.value, UnityEngine.Random.value));
            context.command.BlitFullscreenTriangle(Texture2D.blackTexture, noiseBuffer, noiseSheet, 0);
        }

        float blurAmount = Mathf.Clamp(Mathf.Log(context.width * settings.colorBleedRadius * 0.25f, 2f), 3, 8);
        int blurIterations = Mathf.FloorToInt(blurAmount);
        
        // create blur pyramid
        if (blurPyramid == null || blurPyramid.Length != blurIterations) 
        {
            blurPyramid = new int[blurIterations];
            for (int i = 0; i < blurIterations; i++)
            {
                blurPyramid[i] = PyramidID(i);
            }
        }

        int w = context.width;
        int h = context.height;

        // downsample
        var downsampleSheet = context.propertySheets.Get(shader_downsample);
        downsampleSheet.properties.SetFloat("_BlurBias", settings.colorBleedDirection);
        for (int i = 0; i < blurIterations; i++)
        {
            downsampleSheet.properties.SetVector("_OddScale", GetOddScale(w,h));
            w /= 2;
            h /= 2;
            
            context.GetScreenSpaceTemporaryRT(context.command, blurPyramid[i], 0, context.sourceFormat, RenderTextureReadWrite.Default, FilterMode.Bilinear, w, h);

            if(i == 0)
            {
                if(enableNoise) context.command.SetGlobalTexture("_Noise", noiseBuffer);
                downsampleSheet.properties.SetFloat("_NoiseOpacity", settings.stripeNoiseOpacity);
                context.command.BlitFullscreenTriangle(context.source, blurPyramid[0], downsampleSheet, 0);
            }
            else
            {
                context.command.BlitFullscreenTriangle(blurPyramid[i - 1], blurPyramid[i], downsampleSheet, 1);
            }
        }

        // upsample
        for (int i = blurIterations - 1; i > 2; i--)
        {
            float fac = 1;
            if (i == blurIterations - 1)
            {
                fac = blurAmount - blurIterations;
            }
            downsampleSheet.properties.SetFloat("_UpsampleBlend", 0.7f * fac);
            context.command.BlitFullscreenTriangle(blurPyramid[i], blurPyramid[i - 1], downsampleSheet, 2);
        }

        // apply smearing
        bool enableSmearing = settings.smearIntensity >  0;
        if(enableSmearing)
        {
            int sw = Mathf.Min(640, Mathf.RoundToInt(context.width * 0.5f));
            int sh = Mathf.Min(480, Mathf.RoundToInt(context.height * 0.5f));
            context.GetScreenSpaceTemporaryRT(context.command, smearBuffer, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, FilterMode.Bilinear, sw, sh);
            context.GetScreenSpaceTemporaryRT(context.command, smearBuffer2, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, FilterMode.Bilinear, sw, sh);
            var smearSheet = context.propertySheets.Get(shader_smear);
            smearSheet.properties.SetVector("_TexelSize", new Vector4(1f/sw, 1f/sh, sw, sh));
            smearSheet.properties.SetVector("_SmearOffsetAttenuation", new Vector4(1, 0.3f));
            context.command.BlitFullscreenTriangle(blurPyramid[1], smearBuffer, smearSheet, 0);
            smearSheet.properties.SetVector("_SmearOffsetAttenuation", new Vector4(5, 1.2f));
            context.command.BlitFullscreenTriangle(smearBuffer, smearBuffer2, smearSheet, 0);
        }

        var compositeSheet = context.propertySheets.Get(shader_composite);
        compositeSheet.properties.SetFloat("_ColorBleedIntensity", settings.colorBleedingIntensity);
        compositeSheet.properties.SetFloat("_BlurIntensity", settings.smearIntensity);
        compositeSheet.properties.SetTexture("_Grain", grainTex);
        compositeSheet.properties.SetFloat("_GrainIntensity", settings.grainIntensity);
        compositeSheet.properties.SetVector("_GrainScaleOffset", new Vector4(0.6f * settings.grainScale, settings.grainScale, UnityEngine.Random.value, UnityEngine.Random.value));
        if(enableNoise) context.command.SetGlobalTexture("_Noise", noiseBuffer);
        compositeSheet.properties.SetFloat("_NoiseOpacity", settings.stripeNoiseOpacity);
        compositeSheet.properties.SetFloat("_EdgeIntensity", settings.edgeIntensity);
        compositeSheet.properties.SetFloat("_EdgeDistance", -settings.edgeDistance);
        context.command.SetGlobalTexture("_VHSSlightBlurredTex", blurPyramid[1]);
        context.command.SetGlobalTexture("_VHSBlurredTex", blurPyramid[2]);
        if(enableSmearing) context.command.SetGlobalTexture("_VHSSmearedTex", smearBuffer2);
        compositeSheet.properties.SetFloat("_SmearIntensity", settings.smearIntensity);
        context.command.BlitFullscreenTriangle(blurPyramid[0], context.destination, compositeSheet, 0);

        // clean up
        for (int i = 0; i < blurPyramid.Length; i++)
        {
            context.command.ReleaseTemporaryRT(blurPyramid[i]);
        }
        if(enableNoise) 
        {
            context.command.ReleaseTemporaryRT(noiseBuffer);
        }
        if(enableSmearing)
        {
            context.command.ReleaseTemporaryRT(smearBuffer);
            context.command.ReleaseTemporaryRT(smearBuffer2);
        }
    }

    Vector2 GetOddScale(int w, int h)
    {
        bool we = w % 2 == 0;
        bool he = h % 2 == 0;
        int w2 = w / 2;
        int h2 = h / 2;
        return new Vector4(we ? 1 : ((w2 - 1f) / w2),
            he ? 1 : ((h2 - 1f) / h2),
            we ? 1f / w : 1f / (w - 1),
            he ? 1f / h : 1f / (h - 1));
    }
}
