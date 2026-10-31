# MindMapWpf48

基于 WPF + C# + .NET Framework 4.8 的流程图 / 思维导图编辑器示例。

## 环境
- Windows
- Visual Studio 2019/2022
- .NET Framework 4.8 Developer Pack

## 运行
1. 使用 Visual Studio 打开 `MindMapWpf48.csproj`
2. 选择 .NET Framework 4.8，生成并运行
3. 顶部工具栏添加图形、连接箭头、编辑文字、添加 Tag
4. 拖动节点调整位置；保存项目为 XML；导出 PNG

## 当前示例包含
- 矩形、正方形、圆角矩形、椭圆、菱形
- 节点文字、任务状态、多个 Tag
- 节点拖动、节点间箭头连接、连接文字
- XML 项目保存 / 打开
- PNG 图片导出

## 可继续增强
- 当前连线为中心方向的直线示例；生产版本建议改为边界交点 + 正交折线路由
- 增加端口吸附、连接线拖动编辑、撤销/重做、框选、多选、缩放平移
- 图片导出建议根据节点实际边界计算导出区域，而不是固定画布尺寸
- 可增加 SVG、PDF、XPS 导出，以及自动布局算法
