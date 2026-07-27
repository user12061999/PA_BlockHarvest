Shader "Unlit/WaterSurface"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    	
        _BaseColor ("Base Color", Color) = (0,0.6,1)
        _RippleColor ("Ripple Color", Color) = (0,0.85,1)
        _RippleSpeed ("Ripple Speed", float) = 0.5
        _Ripple1SpeedX ("Ripple1 SpeedX", Range(-1.0, 1.0)) = 1.0
        _Ripple1SpeedY ("Ripple1 SpeedY", Range(-1.0, 1.0)) = -0.5
        _Ripple1Tex ("Ripple1 Tex", 2D) = "black" {}
        _Ripple2SpeedX ("Ripple2 SpeedX", Range(-1.0, 1.0)) = -0.5
        _Ripple2SpeedY ("Ripple2 SpeedY", Range(-1.0, 1.0)) = 1.0
        _Ripple2Tex ("Rippl2 Tex", 2D) = "black" {}
        
    }
    SubShader
    {
		Tags
		{ 
			"Queue"="Transparent" 
			"IgnoreProjector"="True" 
			"RenderType"="Transparent" 
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend One OneMinusSrcAlpha
		
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
				float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
				fixed4 color    : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD2;
            };

			fixed4 _Color;
            
            float4 _BaseColor;
            float4 _RippleColor;
            float _RippleSpeed;

            float _Ripple1SpeedX;
            float _Ripple1SpeedY;
            sampler2D _Ripple1Tex;
            float4 _Ripple1Tex_ST;

            float _Ripple2SpeedX;
            float _Ripple2SpeedY;
            sampler2D _Ripple2Tex;
            float4 _Ripple2Tex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _Ripple1Tex);
                o.uv2 = TRANSFORM_TEX(v.uv, _Ripple2Tex);
            	
                return o;
            }

			sampler2D _MainTex;
			sampler2D _AlphaTex;
			float _AlphaSplitEnabled;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _BaseColor * i.color;
            	col.rgb *= col.a;
                
#if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
				if (_AlphaSplitEnabled)
					color.a = tex2D (_AlphaTex, uv).r;
#endif //UNITY_TEXTURE_ALPHASPLIT_ALLOWED

                float2 offset = float2(_Ripple1SpeedX, _Ripple1SpeedY) * _Time.x * _RippleSpeed;
                fixed4 ripple = tex2D(_Ripple1Tex, i.uv + offset) * _RippleColor;
                col = lerp(col, _RippleColor, ripple.a * _RippleColor.a * i.color.a);

                offset = float2(_Ripple2SpeedX, _Ripple2SpeedY) * _Time.x * _RippleSpeed;
                ripple = tex2D(_Ripple2Tex, i.uv2 + offset) * _RippleColor;
                col = lerp(col, _RippleColor, ripple.a * _RippleColor.a * i.color.a);
            	
                
                return col;
            }
            ENDCG
        }
    }
}
