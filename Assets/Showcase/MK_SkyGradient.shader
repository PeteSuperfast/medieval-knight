Shader "MK/SkyGradient"
{
    Properties { _Top ("Top", Color) = (0.71, 0.77, 0.87, 1) _Bottom ("Horizon", Color) = (0.84, 0.75, 0.76, 1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Top; fixed4 _Bottom;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            fixed4 frag (v2f i) : SV_Target { float t = saturate(normalize(i.dir).y * 1.6); return lerp(_Bottom, _Top, t); }
            ENDCG
        }
    }
}
