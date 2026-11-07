# Tang.VideoThrimalgoAI

图像 / 视频修复智能工作台（WPF，.NET 6）。图像页提供画质增强、高清修复、2x 放大、ONNX 超分、去马赛克、去水印、水平校正与批量处理；视频页基于本地 ffmpeg 提供高清修复、去码、去水印、并排/画中画、拼接、M3U8 转 mp4、音轨提取、裁剪与 GIF。

## 文档

| 文档 | 面向 | 内容 |
|---|---|---|
| [01-功能使用说明](docs/01-功能使用说明.md) | 最终用户 | 界面结构、图像页与视频页全部功能、语言切换、状态栏消息、操作示例、输出命名与格式支持、已知限制 |
| [02-开发与环境部署](docs/02-开发与环境部署.md) | 开发者 | 技术栈与版本、目录结构、环境与 ffmpeg 查找顺序、ONNX 模型部署、首次运行配置、调试方式、依赖还原 |
| [03-编译打包发布](docs/03-编译打包发布.md) | 发布者 | 还原/编译/publish 实命令、分发取舍与体积、发布前检查清单、包结构与命名、临时产物约定、故障排查 |

## 快速开始

```powershell
dotnet build Tang.VideoThrimalgoAI.csproj -c Debug
dotnet publish Tang.VideoThrimalgoAI.csproj -c Release -r win-x64 --self-contained false -o .\out
```

前置条件：.NET 6 Desktop Runtime（x64）、本地 ffmpeg（默认查找 `D:\tang\software\ffmpeg\bin`）、`Models\RealESRGAN_x4plus.onnx`（67,051,973 字节，校验方式见文档 02/03）。模型缺失时超分自动回退纯算法。
