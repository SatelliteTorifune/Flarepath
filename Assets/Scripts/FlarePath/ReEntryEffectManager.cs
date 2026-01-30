using System;
using Assets.Scripts;
using Assets.Scripts.Craft.Parts.Modifiers.Fuselage;
using Assets.Scripts.FlarePath;
using ModApi.Craft.Parts;
using UnityEngine;

public class ReEntryEffectManager:MonoBehaviour
{
        public ReEntryEffect Effect;
        public ParticleSystem ParticleSystem;
        public IPartScript part;
        private float minTemp;
        private float ignitionTemp;
        private float maxTemp;
    
        private void Awake()
        {
            minTemp = Effect.minTemp;
            ignitionTemp = Effect.ignitionTemp;
            maxTemp = Effect.maxTemp;
            
        }

        public void Update()
        {
            //Debug.LogFormat($"{part.Data.Name} is occluded by{Effect.occlusionSampler.Occlusion}");
            ParticleEffectUpdate();
            ReEntryEffectUpdate();
            
        }

        private void ReEntryEffectUpdate()
        {
            this.gameObject.transform.position=part.GameObject.transform.position;
            this.gameObject.transform.rotation=part.GameObject.transform.rotation;
            this.gameObject.transform.localScale=part.GameObject.transform.localScale;
            Effect.velocityWorld = part.CraftScript.FlightData.SurfaceVelocity.ToVector3();
            Effect.lengthMultiplier = 5;
            Effect.entryStrength = GetEntryStrength();
        }

        private void ParticleEffectUpdate()
        {
            
        }
    
        private float GetEntryStrength()
        {
            return 3000f;
            float temp = part.Temperature;
            if (temp < ignitionTemp)
            {
                return 300f;
            }

            // 归一化
            float t = Mathf.Clamp((temp - ignitionTemp) / (maxTemp - ignitionTemp), 0f, 1f);

            // 用 2.5 次方曲线（高温爆发感强）
            float curve = Mathf.Pow(t, 2.5f);
            float strength = Mathf.Lerp(300f, 3000f, curve);
            return strength;
        }

       
        private Vector3 lastVelocityDir = Vector3.zero;  // 上次速度方向
        private float lastSamplerCheck = 0f;  // 定时器
        private float samplerSwitchInterval = 0.5f;  // 每 0.5s 检查一次方向变化
        
        private void CreateOrUpdateOcclusionSampler()
        {
            Vector3 velocityDir = part.CraftScript.FlightData.SurfaceVelocity.ToVector3().normalized;
            if (part.CraftScript.FlightData.SurfaceVelocity.ToVector3().magnitude < 1f)
            {
                Effect.occlusionSampler = null;
                return;
            }
        
            // 每 N 秒检查一次方向变化（省性能）
            if (Time.time - lastSamplerCheck < 0.5f) return;
            lastSamplerCheck = Time.time;
        
            // 方向变化小于 36°（cos > 0.8）就不重新创建
            if (Vector3.Dot(velocityDir, lastVelocityDir) > 0.8f) return;
            lastVelocityDir = velocityDir;
        
            // 6 个面法线（本地坐标系）
            Vector3[] faceNormals = new Vector3[]
            {
                Vector3.right, Vector3.left,    // ±X
                Vector3.up, Vector3.down,       // ±Y
                Vector3.forward, Vector3.back   // ±Z
            };
        
            float maxDot = -1f;
            int faceIndex = -1;
            for (int i = 0; i < 6; i++)
            {
                float dot = Vector3.Dot(faceNormals[i], -velocityDir); // -velocity = 迎风
                if (dot > maxDot)
                {
                    maxDot = dot;
                    faceIndex = i;
                }
            }
        
            // 如果最迎风 dot < 0.5（太斜），不采样
            if (maxDot < 0.5f)
            {
                Effect.occlusionSampler = null;
                return;
            }
        
            Vector2 scale = Vector2.one;
            Vector3 localCenter = Vector3.zero;
            Vector3 localDirection = faceNormals[faceIndex]; // 迎风法线
        
            // 判断是否是 Fuselage 部件（根据你的 mod 结构）
            FuselageScript fuselageScript = GetComponent<FuselageScript>(); // 或 GetComponentInChildren 等
            bool isFuselage = fuselageScript != null;
        
            if (isFuselage)
            {
                FuselageData fuselageData = fuselageScript.Data;
        
                // 根据迎风面轴选择对应的 Scale 和 Offset
                switch (faceIndex)
                {
                    case 0: case 1: // ±X 面（侧面，迎风面是 X 轴方向）
                        // X 面：采样宽 = Y 轴尺寸（左右），高 = Z 轴尺寸（上下）
                        // 但 Fuselage 没有 z 尺寸 → 用 Offset.y 或固定值近似
                        scale = new Vector2(
                            fuselageData.TopScale.y * 2f * 0.9f,  // 宽：Y 方向（高度）
                            fuselageData.Offset.y * 2f * 0.9f     // 高：Z 方向用 Offset.y 近似深度
                        );
                        localCenter = (faceIndex == 0 ? Vector3.right : -Vector3.right) * fuselageData.Offset.x;
                        break;

                    case 2: case 3: // ±Y 面（上下，迎风面是 Y 轴方向）
                        // Y 面：采样宽 = X 轴尺寸（左右），高 = Z 轴尺寸（前后）
                        scale = new Vector2(
                            fuselageData.TopScale.x * 2f * 0.9f,  // 宽：X 方向（宽度）
                            fuselageData.Offset.y * 2f * 0.9f     // 高：Z 方向用 Offset.y 近似
                        );
                        localCenter = (faceIndex == 2 ? Vector3.up : -Vector3.up) * fuselageData.Offset.y;
                        break;

                    case 4: case 5: // ±Z 面（前后，迎风面是 Z 轴方向）
                        // Z 面：采样宽 = X 轴尺寸（左右），高 = Y 轴尺寸（上下）
                        scale = new Vector2(
                            fuselageData.TopScale.x * 2f * 0.9f,  // 宽：X 方向
                            fuselageData.TopScale.y * 2f * 0.9f   // 高：Y 方向
                        );
                        localCenter = (faceIndex == 4 ? Vector3.forward : -Vector3.forward) * fuselageData.Offset.z;
                        break;

                    default:
                        scale = new Vector2(1f, 1f); // 兜底
                        break;
                }
        
                // 向内偏移，避免采样点打到机身自身
                float offsetAmount = Mathf.Min(Mathf.Abs(localCenter.magnitude) * 0.5f, 0.05f);
                localCenter -= offsetAmount * localDirection.normalized;
            }
            else
            {
                // 普通 part：用 _EnvelopeScaleFactor 反向缩放（固定大小）
                Vector3 invScale = new Vector3(
                    1f / Mathf.Max(0.01f, transform.lossyScale.x),
                    1f / Mathf.Max(0.01f, transform.lossyScale.y),
                    1f / Mathf.Max(0.01f, transform.lossyScale.z)
                );
                scale = new Vector2(invScale.x * 2f * 0.9f, invScale.y * 2f * 0.9f);
                localCenter = Vector3.zero;
            }
        
            // 创建/更新 sampler
            if (Effect.occlusionSampler != null)
            {
                // 如果方向/scale 变化大，就重建
                Effect.occlusionSampler = new OcclusionSampler(
                    scale,
                    5,  // 采样密度
                    transform,
                    localCenter,
                    localDirection
                );
            }
        
            Effect.occlusionSampler.MaxDistance = Mathf.Max(scale.x, scale.y) * 2f;
            Effect.occlusionSampler.SkipCorners = true;
            Effect.occlusionSampler.AddIgnore(gameObject); // 忽略自身
        
            // 重置采样状态，准备新一轮
            Effect.occlusionSampler.Ready = false;
        }
}