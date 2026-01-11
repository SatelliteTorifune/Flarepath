Shader "Jundroo/ReEntry/ReEntryEffectFixed" {
    Properties {
        _MainTex ("Base Image", 2D) = "white" {}
        _effectMaskTex ("Re Entry Effect Strength", 2D) = "white" {}
        _reEntryTint ("Re Entry Effect Tint", Color) = (1,0.5,0,1)  // 橙红色
        _vaporTint ("Vapor Effect Tint", Color) = (0.8,0.9,1,1)     // 蓝白色
        _reentryBloomScale ("Re-entry Bloom Scale", Range(0,10)) = 2
        _vaporBloomScale ("Vapor Bloom Scale", Range(0,10)) = 1
        _temperature ("Temperature", Range(0,1)) = 0.5              // 温度强度
    }
    
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;
            float4 _MainTex_ST;

            struct Vertex_Stage_Input {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct Vertex_Stage_Output {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            Vertex_Stage_Output vert(Vertex_Stage_Input input) {
                Vertex_Stage_Output output;
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float4 worldPos = mul(unity_ObjectToWorld, input.pos);
                output.worldPos = worldPos.xyz;
                output.normal = mul((float3x3)unity_ObjectToWorld, input.normal);
                output.pos = mul(unity_MatrixVP, worldPos);
                return output;
            }

            Texture2D<float4> _MainTex;
            Texture2D<float4> _effectMaskTex;
            SamplerState sampler_MainTex;
            SamplerState sampler_effectMaskTex;

            float4 _reEntryTint;
            float4 _vaporTint;
            float _reentryBloomScale;
            float _vaporBloomScale;
            float _temperature;

            struct Fragment_Stage_Input {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            // 模拟真实再入效果的函数
            float3 CalculateReentryColor(float intensity, float edgeFactor) {
                // 根据温度强度计算颜色 - 从红到黄到白
                float3 color1 = float3(1, 0, 0);      // 红色
                float3 color2 = float3(1, 0.5, 0);    // 橙色
                float3 color3 = float3(1, 1, 0.8);    // 黄白色
                
                float3 finalColor;
                if (intensity < 0.5) {
                    finalColor = lerp(color1, color2, intensity * 2);
                } else {
                    finalColor = lerp(color2, color3, (intensity - 0.5) * 2);
                }
                
                // 边缘增强效果
                finalColor *= (1.0 + edgeFactor * 0.5);
                
                return finalColor;
            }

            float4 frag(Fragment_Stage_Input input) : SV_Target {
                float4 baseColor = _MainTex.Sample(sampler_MainTex, input.uv);
                float4 effectMask = _effectMaskTex.Sample(sampler_effectMaskTex, input.uv);
                
                // 获取遮罩强度
                float reentryMask = effectMask.r;
                float vaporMask = effectMask.g;
                
                // 计算边缘因子（模拟高温边缘发光）
                float edgeFactor = saturate(1.0 - abs(dot(normalize(input.normal), float3(0,0,1))));
                edgeFactor = pow(edgeFactor, 3); // 强化边缘效果
                
                // 计算温度强度（结合遮罩和边缘）
                float tempIntensity = _temperature * reentryMask;
                
                // 再入效果 - 模拟高温等离子体
                float3 reEntryEffect = CalculateReentryColor(tempIntensity, edgeFactor);
                reEntryEffect *= reentryMask * _reentryBloomScale * tempIntensity;
                
                // 蒸汽效果 - 模拟低温蒸汽
                float3 vaporEffect = _vaporTint.rgb;
                vaporEffect *= vaporMask * _vaporBloomScale * (1.0 - tempIntensity);
                
                // 综合效果
                float3 glowEffect = reEntryEffect + vaporEffect;
                
                // 添加HDR效果（让亮部更亮）
                float hdrFactor = 1.0 + saturate(length(glowEffect) - 1.0) * 2.0;
                glowEffect *= hdrFactor;
                
                // 最终颜色合成
                float3 finalColor = baseColor.rgb + glowEffect;
                
                return float4(finalColor, baseColor.a);
            }

            ENDHLSL
        }
    }
    
    // 使用标准fallback
    Fallback "Diffuse"
}
