# TVisionUpperDemp

WPF 上位机平面绘图 Demo：在可缩放、可平移的自定义坐标系中演示文字、矩形、三角形、多边形、直线、箭头、圆弧、圆环和椭圆，并支持 Tag 标注与批量绘制。

## 快速开始

需要 .NET 8 SDK 和 Windows 桌面环境：

```bash
dotnet restore
dotnet run
```

顶部可切换 Canvas、FrameworkElement、DrawingVisual、SkiaSharp、WriteableBitmap 五种渲染后端入口；当前核心交互统一由 `CoordinateSurface` 提供，便于后续替换渲染器而不影响数据与坐标变换。

## 交互

- 鼠标滚轮：以鼠标当前位置为锚点缩放，范围 0.15x～8x
- 鼠标左键拖动：平移坐标系与全部图形
- 鼠标右键：在当前位置添加带 Tag 的椭圆
- “添加到画布”：按选定类型追加图形；“清空”和“重置视图”用于演示

## 架构

- `Models/DrawShape.cs`：与 UI 无关的图元模型及类型枚举
- `Services/CoordinateTransform.cs`：世界坐标/屏幕坐标转换、锚点缩放、平移
- `Controls/CoordinateSurface.cs`：坐标轴、刻度、网格和 WPF DrawingContext 绘制
- `MainWindow`：演示壳层，可通过 `RendererMode` 扩展后端

### 后端演进建议

生产版本建议抽取 `IRenderer.Render(DrawingContext, IReadOnlyList<DrawShape>, CoordinateTransform)`，分别实现：Canvas（子元素）、FrameworkElement（保留当前实现）、DrawingVisual（批量 Visual）、SkiaSharp（SKCanvas）和 WriteableBitmap（像素栅格）。模型和变换服务保持共享；高频数据场景优先 DrawingVisual/SkiaSharp，像素后处理场景使用 WriteableBitmap。

## 发布

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```
