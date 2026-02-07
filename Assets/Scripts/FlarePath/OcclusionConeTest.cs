using System;
using FlarePath;
using UnityEngine;

/// <summary>
/// 锥形遮挡类，用于计算飞行器部件在气动加热环境中的遮挡效果
/// </summary>
public class OcclusionConeTest:MonoBehaviour
{
    // 关联的遮挡数据
    public OcclusionData part;

    // 锥体中心点
    public Vector3 center;

    // 锥体范围
    public Vector2 extents;

    // 偏移量
    public Vector2 offset;

    // 锥体半径
    public double radius;

    // 圆柱鼻锥点
    public double cylNoseDot;

    // 冲击波鼻锥点
    public double shockNoseDot;

    // 冲击波角度
    public double shockAngle;

    // 冲击波对流温度倍数
    public double shockConvectionTempMult;

    // 冲击波对流系数倍数
    public double shockConvectionCoeffMult;

    // 遮挡对流温度倍数
    public double occludeConvectionTempMult;

    // 遮挡对流系数倍数
    public double occludeConvectionCoeffMult;

    // 遮挡对流面积倍数
    public double occludeConvectionAreaMult;

    // 分离冲击波热传导倍数
    public static double detachedShockHeatMult = 0.5;

    // 分离冲击波系数倍数
    public static double detachedShockCoeffMult = 1.0;

    // 分离后冲击波热传导倍数
    public static double detachedBehindShockHeatMult = 0.4;

    // 分离后冲击波系数倍数
    public static double detachedBehindShockCoeffMult = 1.0;

    // 分离冲击波马赫角倍数
    public static double detachedShockMachAngleMult = 0.05;

    // 分离冲击波起始角度
    public static double detachedShockStartAngle = Math.PI * 49.0 / 100.0;

    // 分离冲击波结束角度
    public static double detachedShockEndAngle = Math.PI * 9.0 / 25.0;

    // 斜激波角度倍数
    public static double obliqueShockAngleMult = 0.8;

    // 斜激波部件角度倍数
    public static double obliqueShockPartAngleMult = 0.25;

    // 斜激波最小角度倍数
    public static double obliqueShockMinAngleMult = 1.05;

    // 斜激波锥体热传导倍数
    public static double obliqueShockConeHeatMult = 0.75;

    // 斜激波锥体系数倍数
    public static double obliqueShockConeCoeffMult = 1.0;

    // 斜激波圆柱热传导倍数
    public static double obliqueShockCylHeatMult = 0.55;

    // 斜激波圆柱系数倍数
    public static double obliqueShockCylCoeffMult = 1.0;

    /// <summary>
    /// 设置锥形遮挡参数
    /// </summary>
    /// <param name="part">关联的遮挡数据</param>
    /// <param name="sqrtMach">马赫数平方根</param>
    /// <param name="sqrtMachAngle">马赫角平方根</param>
    /// <param name="detachAngle">分离角度</param>
    public void Setup(OcclusionData part, double sqrtMach, double sqrtMachAngle, double detachAngle)
    {
        this.part = part;
        center = part.projectedCenter;
        radius = part.projectedRadius;
        offset = -part.center;
        extents = part.extents;
        
        // 获取当前对流参数
        double tempMultiplier = part.ptd.convectionTempMultiplier;
        double coeffMultiplier = part.ptd.convectionCoeffMultiplier;
        
        // 计算圆柱鼻锥点位置
        cylNoseDot = part.maximumDot - part.maxWidthDepth;
        
        // 计算逆细长比对应的角度
        double finenessAngle = Math.Asin(part.invFineness);

        // 判断是否为斜激波情况（细长比小于1且角度小于分离角度）
        if (part.invFineness < 1.0 && finenessAngle <= detachAngle)
        {
            // 斜激波配置
            shockNoseDot = part.maximumDot;
            shockAngle = Math.Max(finenessAngle * obliqueShockMinAngleMult, 
                                sqrtMachAngle * obliqueShockAngleMult + finenessAngle * obliqueShockPartAngleMult);
            occludeConvectionAreaMult = 1.0;

            // 根据当前传热系数调整激波参数
            if (tempMultiplier >= detachedShockHeatMult - 0.05 && 
                coeffMultiplier >= detachedShockCoeffMult - 0.05)
            {
                // 使用标准斜激波参数
                occludeConvectionTempMult = obliqueShockCylHeatMult;
                shockConvectionTempMult = obliqueShockConeHeatMult;
                occludeConvectionCoeffMult = obliqueShockCylCoeffMult;
                shockConvectionCoeffMult = obliqueShockConeCoeffMult;
            }
            else
            {
                // 混合参数：结合分离后冲击波和斜激波特性
                double heatVal = Math.Max(tempMultiplier, detachedBehindShockHeatMult);
                occludeConvectionTempMult = Math.Min(heatVal, obliqueShockCylHeatMult);
                shockConvectionTempMult = Math.Min(heatVal, obliqueShockConeHeatMult);
                
                double coeffVal = Math.Max(coeffMultiplier, detachedBehindShockCoeffMult);
                occludeConvectionCoeffMult = Math.Min(coeffVal, obliqueShockCylCoeffMult);
                shockConvectionCoeffMult = Math.Min(coeffVal, obliqueShockConeCoeffMult);
            }
        }
        else
        {
            // 分离冲击波配置
            shockNoseDot = part.maximumDot + radius * part.invFineness;
            shockAngle = Mathf.Lerp((float)detachedShockEndAngle, (float)detachedShockStartAngle, 
                (float)sqrtMach * (float)detachedShockMachAngleMult);
            
            // 分离冲击波的遮挡效应更强
            occludeConvectionTempMult = 0.0;
            occludeConvectionAreaMult = 0.0;

            // 根据当前传热系数决定使用哪种分离冲击波模型
            if (tempMultiplier >= detachedShockHeatMult - 0.05 && 
                coeffMultiplier >= detachedShockCoeffMult - 0.05)
            {
                // 标准分离冲击波参数
                tempMultiplier = detachedShockHeatMult;
                coeffMultiplier = detachedShockCoeffMult;
                shockConvectionTempMult = detachedBehindShockHeatMult;
                shockConvectionCoeffMult = detachedBehindShockCoeffMult;
            }
            else
            {
                // 弱化分离冲击波参数
                tempMultiplier = shockConvectionTempMult = detachedBehindShockHeatMult;
                coeffMultiplier = shockConvectionCoeffMult = detachedBehindShockCoeffMult;
            }
        }

        // 更新部件的对流传热参数
        part.ptd.convectionTempMultiplier = tempMultiplier;
        part.ptd.convectionCoeffMultiplier = coeffMultiplier;
    }

    /// <summary>
    /// 根据点的位置计算对应的冲击波半径
    /// </summary>
    /// <param name="dot">点的投影位置</param>
    /// <returns>该位置的冲击波半径</returns>
    public double GetShockRadius(double dot)
    {
        double distanceFromShockNose = shockNoseDot - dot;
        return radius + distanceFromShockNose * Math.Tan(shockAngle);
    }
}
