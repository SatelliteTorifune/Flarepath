using System.Collections.Generic;
using ModApi.Craft.Parts;
using UnityEngine;

/// <summary>
/// 部件热力学数据类，存储和管理单个部件的所有热力学相关信息
/// </summary>
public class PartThermalData
{
    // 整数比较器和蒙皮比较器实例
    public static PartThermalDataIntComparer intComparer = new PartThermalDataIntComparer();
    public static PartThermalDataSkinComparer skinComparer = new PartThermalDataSkinComparer();

    // 关联的部件对象
    public IPartScript part;

    // 热连接数量
    public int thermalLinkCount;

    // 身体辐射面积倍数
    public double bodyAreaMultiplier = 1.0;

    // 太阳辐射面积倍数
    public double sunAreaMultiplier = 1.0;

    // 对流面积倍数
    public double convectionAreaMultiplier = 1.0;

    // 对流温度倍数
    public double convectionTempMultiplier = 1.0;

    // 对流系数倍数
    public double convectionCoeffMultiplier = 1.0;
    
    // 对流遮挡数据
    public OcclusionData convectionData;

    // 太阳辐射遮挡数据
    public OcclusionData sunData;

    // 身体辐射遮挡数据
    public OcclusionData bodyData;

    // 上一帧温度记录
    public double previousTemperature;
    public double previousSkinTemperature;
    public double previousSkinUnexposedTemperature;

    // 是否暴露在外
    public bool exposed;

    // 辐射面积倒数
    public double radAreaRecip = 1.0;

    // 对流面积
    public double convectionArea;

    // 各种热传导通量
    public double intConductionFlux;                    // 内部传导通量
    public double skinConductionFlux;                   // 蒙皮传导通量
    public double localIntConduction;                   // 局部内部传导
    public double localSkinConduction;                  // 局部蒙皮传导
    public double skinSkinConductionFlux;               // 蒙皮间传导通量
    public double skinInteralConductionFlux;           // 蒙皮内部传导通量
    public double unexpSkinInternalConductionFlux;     // 未暴露蒙皮内部传导通量

    // 各种辐射通量
    public double radiationFlux;                        // 辐射通量
    public double unexpRadiationFlux;                  // 未暴露辐射通量
    public double convectionFlux;                      // 对流通量
    

    // 冲击波后外部温度
    public double postShockExtTemp;

    // 最终系数
    public double finalCoeff;

    // 部件伪雷诺数
    public double partPseudoRe;

    // 发射率标量
    public double emissScalar;

    // 吸收率标量
    public double absorbScalar;

    // 吸收发射比率
    public double absEmissRatio;

    // 各种热通量
    public double sunFlux;                             // 太阳通量
    public double bodyFlux;                            // 身体通量
    public double expFlux;                             // 暴露通量
    public double unexpFlux;                           // 未暴露通量

    // 亮度相关参数
    public double brtUnexposed;                        // 未暴露亮度
    public double brtExposed;                          // 暴露亮度

    // 计数器相关
    public int sCount;
    public int realSCount;
    public double sDivisor;

    // 传导倍数
    public double conductionMult;

    // 蒙皮间传递
    public double skinSkinTransfer;

    // 统一温度
    public double unifiedTemp;

    /// <summary>
    /// 构造函数，初始化部件热力学数据
    /// </summary>
    /// <param name="part">关联的部件</param>
    public PartThermalData(IPartScript part)
    {
        this.part = part;
        thermalLinkCount = 0;
        
        // 创建三种遮挡数据对象
        convectionData = new OcclusionData(this);
        sunData = new OcclusionData(this);
        bodyData = new OcclusionData(this);
        
    }
    
    
}
