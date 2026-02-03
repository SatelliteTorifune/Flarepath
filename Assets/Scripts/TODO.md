## 正在进行的项目
### 你妈的Look rotation viewing vector is zero 是个什么几把玩意??
> 这个东西疑似和unity的Update函数有关,我觉得是OcclusionSampler.Update把unity mono的那个Update搞坏了

> 不是为什么我写在FlightFixedUpdate里面也不行啊
### OcclusionSampler的采样点分布算法修复
> OcclusionSampler.GenerateUniformSurfaceSamples()



## 优先修复的项目
### 面对复杂模型时会穿模
> 原因未知,mesh的尺寸和实际效果不匹配的问题疑似也和这个有关?
### mesh的尺寸和实际效果不匹配
> 疑似和size和partScale有关
### 迎风面高亮效果重写或优化
### 神秘蓝色效果
## 已修复的项目

### 特定角度或者时间下效果消失
### OcclusionSampler速度矢量更新问题

## Feature

### UI和配置文件创建
### 性能优化
### 参数调整自动化
> 先把你那逼 ReEntryEffectManager.GetEntryStrength()修了吧
### 自带参数调整
### 增加粒子效果以及对应的config参数

