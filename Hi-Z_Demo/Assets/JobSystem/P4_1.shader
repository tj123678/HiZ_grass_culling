Shader "MyShader/URP/P4_1"
{
    Properties 
    {
        _Color("Color",Color) = (0,0,0,0)
        _MainTex("MainTex",2D) = "white"{}
    }
    SubShader
    {
        Tags
        {
            //告诉引擎，该Shader只用于 URP 渲染管线
            "RenderPipeline"="UniversalPipeline"
            //渲染类型
            "RenderType"="Transparent"
            //渲染队列
            "Queue"="Transparent"
        }
        //Blend One One
        ZWrite Off
        Pass
        {
            Name "Unlit"
          
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Pragmas
            #pragma target 2.0
            
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END

            
            //纹理的定义，如果是编译到GLES2.0平台，则相当于sample2D _MainTex;否则相当于 Texture2D _MainTex;
            TEXTURE2D(_MainTex);SAMPLER(SamplerState_linear_mirrorU_ClampV); float4 _MainTex_ST;
            TEXTURE2D(_CameraDepthTexture);SAMPLER(sampler_CameraDepthTexture);
            
            //struct appdata
            //顶点着色器的输入
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            //struct v2f
            //片元着色器的输入
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            //v2f vert(Attributes v)
            //顶点着色器
            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(v.uv,_MainTex);
                return o;
            }
            //fixed4 frag(v2f i) : SV_TARGET
            //片元着色器
            half4 frag(Varyings i) : SV_TARGET
            {
                half4 c;
                float4 mainTex = SAMPLE_TEXTURE2D(_MainTex,SamplerState_linear_mirrorU_ClampV,i.uv);
                //c = _Color *  mainTex;
                float4 cameraDepthTex = SAMPLE_TEXTURE2D(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv);
                float depthTex = Linear01Depth(cameraDepthTex,_ZBufferParams);
                return depthTex;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Shader Graph/FallbackError"
}
