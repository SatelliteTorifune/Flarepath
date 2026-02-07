## 正在进行的项目

### 遮罩优化?

> 直接用这玩意(BodyScript.ReEntryEffectStrength)解决不了问题,考虑到你游狗屎一样,聊胜于无的遮挡计算,要解决这个问题似乎只有两条路

>1.优化shader面数,即便多个(50?100?)个效果instance的性能消耗也不会太大

>2.给jundroo擦屁股,把他们没做的遮挡功能给做了 


### 得想出个法子解决遮挡计算的问题
> 目前在偷ksp的锥体遮挡系统,不用FAR那种体素是因为JNO的craft的p数太几把多了,退一步讲体素法吃性能也难得写,所以用这个玩意凑合用

> 1.得搞懂demo里面是咋用的

> 2.这个OcclusionData的具体数据如何计算?



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
### 你妈的Look rotation viewing vector is zero 是个什么几把玩意??
> 这个东西疑似和unity的Update函数有关,我觉得是OcclusionSampler.Update把unity mono的那个Update搞坏了

> 不是为什么我写在FlightFixedUpdate里面也不行啊

> 在给OcclusionSampler的debug的线段创建啥的加入了一些防止空值的判断后解决

>据说是rigidBody在速度为0是会出现这种问题
 
> 主要是现在又出现了Look rotation viewing vector is zero,得找然后修

>这个玩意会严重影响性能,直接给我吃了50多fps,必须修

>问题在ReEntryEffect里面,velocityWorld.normalized 在 velocityWorld 为零时会变成无效值,Quaternion.LookRotation()调用这个Vector3的时候就会Look rotation viewing vector is zero
> 
### 效果prefab和craft没对齐
> Effect更新要写在IFlightUpdate里面,不能写在IFlightFixedUpdate里面

## Feature

### UI和配置文件创建
### 性能优化
### 参数调整自动化
> 先把你那逼 ReEntryEffectManager.GetEntryStrength()写了吧
### 自带参数调整
### 增加粒子效果以及对应的config参数

