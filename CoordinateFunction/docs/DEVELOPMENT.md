# WPF 多渲染坐标绘图架构

## 界面结构

主窗口采用左侧渲染方式菜单、右侧 `ContentControl` 工作区。点击菜单会创建并切换独立的 `RendererPage` 用户控件页面，所有页面共享同一个 `DrawingDocument`，因此切换渲染机制不会丢失图形。

## 五种渲染实现

- `CanvasSurface`：以 Canvas 作为页面宿主，并嵌入绘制控件。
- `FrameworkElementSurface`：重写 `OnRender`，使用 WPF `DrawingContext` 即时绘制。
- `DrawingVisualSurface`：使用独立 `DrawingVisual` 批量提交绘制命令。
- `SkiaSharpSurface`：使用 `SKElement` 和 SkiaSharp 绘制坐标轴、图形和 Tag。
- `WriteableBitmapSurface`：先绘制到位图源，再写入 `WriteableBitmap`，适合像素输出和快照。

## 共享能力

`CoordinateTransform` 统一处理世界坐标、屏幕坐标、锚点缩放和平移，并支持四象限、第一象限、第二象限配置。

`WpfCoordinateTools` 独立封装坐标轴、网格、刻度、正负方向箭头、Tag 和矩形/三角形/多边形/直线/箭头/圆弧/圆环/椭圆等图形绘制。

## 运行

```bash
dotnet restore
dotnet run
```
