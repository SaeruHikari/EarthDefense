# 1.17 轨道基础设施资产

本目录记录原生程序模型的参考与尺度；所有模型由项目GDScript生成，不下载网络模型或图片纹理。

望远镜采用**Hubble式单一构型**：筒形光学组件、前端遮光罩和开放镜口、主/次镜与支撑、侧置开启的保护门、两翼刚性太阳能阵列、后部仪器总线和小型天线。银白、石墨与少量金色隔热面上加入克制的青色设备线。不是JWST分段镜与Hubble筒身的混搭。

官方结构参考：

- [NASA：Hubble整体设计](https://science.nasa.gov/mission/hubble/observatory/design/)
- [NASA：双镜Cassegrain光学系统](https://science.nasa.gov/mission/hubble/observatory/design/optics/)
- [NASA：双太阳能翼供电系统](https://science.nasa.gov/mission/hubble/observatory/design/electrical-power/)
- [ESA/Hubble：太阳能板](https://esahubble.org/about/general/solar_panels/)

卫星太阳能翼总跨度约0.44世界单位。刚性零件首次合并为共享PBR网格，使用项目既有ORM材质体系，无单零件每帧逻辑。船坞为开放式支架，原母舰及其真实工厂仍由现有远征渲染器独立绘制。

内轨道半径5.45，外轨道6.85，使用不同倾角；均在惯性三维世界中进行正常深度遮挡。点击容错为卫星15逻辑像素、轨道7.5逻辑像素，仅接受相机前方且未被地球遮住的几何。卫星运动取已保存的天体模拟时钟，暂停与恢复不另起一套计时。
