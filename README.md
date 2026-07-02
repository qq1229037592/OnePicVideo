# 一张图 · 压制工具 v3.0

Apache-2.0 | C# .NET 7 + WinForms + FFmpeg | Windows 10/11 | 2026.07

---

## 项目简介

**一张图 · 压制工具** 是一个轻量级桌面一图流视频合成工具，专为音乐工程、PV 制作场景设计，让您只需一张图片 + 一段音频即可快速压制出高质量 MP4 视频，并内置 163 音乐搜歌下封面功能，省去手动找封面的繁琐步骤。

### 主要功能

| 功能模块             | 说明                                                                   |
| -------------------- | ---------------------------------------------------------------------- |
| **一图流压制**       | 图片 + 音频 → MP4，支持拖放、12+ 图片格式、12+ 音频格式                |
| **GPU 硬件加速**     | 自动检测 NVENC / QSV / AMF / Vulkan，支持 H.264/H.265，默认优选 GPU    |
| **163 搜歌下封面**   | 粘贴歌曲链接或 ID → 搜索 → 预览封面 → 一键填入/下载，退出自动清理缓存 |
| **预设联动**         | 中文化预设（极速→质量最佳），切换预设时画质值自动联动调整               |
| **实时进度**         | 解析 FFmpeg 编码进度，百分比进度条 + 暗色终端日志                       |

---

## 系统要求

- **操作系统：** Windows 10（21H2+）/ Windows 11
- **运行环境：** [.NET 7 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/7.0)（约 50MB，一次性安装）
- **依赖工具：** [FFmpeg](https://ffmpeg.org/download.html)（需加入系统 PATH，或放置在 `C:\Program Files\FFmpeg\ffmpeg.exe`）
- **磁盘空间：** 300KB

---

## 快速开始

1. **安装环境**（如已安装可跳过）
   - 下载安装 .NET 7 Desktop Runtime（Win10/11 自带或 Windows Update 推送）
   - 下载 FFmpeg Windows 构建版并配置 PATH

2. **运行程序**
   - 双击 `OnePicVideo.exe` 启动

3. **一图流压制**
   - 在「一图流压制」标签页拖入/浏览选择图片和音频
   - 选择编码器（默认已选 GPU 加速），根据需要调整预设和画质
   - 点击「▶ 开始压制」等待完成

4. **搜索封面**
   - 切换到「搜索封面」标签页
   - 粘贴 163 音乐链接（如 `https://music.163.com/song?id=769609`，或直接填 ID `769609`）
   - 点击「搜索」→ 点击「填入图片 → 压制页」自动衔接到压制流程

---

## 编译指南

```bash
# 还原依赖
dotnet restore src/OnePicVideo.csproj

# 编译
dotnet build src/OnePicVideo.csproj -c Release

# 发布单文件 exe
dotnet publish src/OnePicVideo.csproj -c Release -o publish
```

---

## 技术架构

```
OnePicVideo/
├── Program.cs           # [STAThread] 入口
├── MainForm.cs          # TabControl 双标签页 WinForms UI
├── FfmpegEncoder.cs     # FFmpeg 进程管理 + GPU 编码器自动检测
├── MusicApiClient.cs    # 163 weapi AES/RSA 加密 + 封面下载
└── OnePicVideo.csproj   # net7.0-windows 框架依赖
```

- **163 API：** weapi 内部接口，双重 AES-CBC 加密 + RSA(BigInteger.ModPow) 签名
- **GPU 加速：** `ffmpeg -encoders` 检测可用编码器 → `nvidia-smi` 识别 GPU → 自动优选 NVENC
- **进度解析：** 正则匹配 stderr `time=HH:MM:SS.ms` → 除以音频总时长得百分比

---

## 许可证

本软件采用 [Apache-2.0](https://opensource.org/licenses/Apache-2.0) 协议开源。

本项目参考了以下开源项目：
- [jitwxs/163MusicLyrics](https://github.com/jitwxs/163MusicLyrics)（Apache-2.0）— 163 音乐 API weapi 加密实现
- [pphh77/MeGui](https://github.com/pphh77/MeGui) — C# WinForms + FFmpeg 视频压制架构
- [FFmpeg](https://ffmpeg.org)（LGPL/GPL）— 底层的音视频编解码引擎

使用本软件生成的视频内容和下载的封面图片，请遵守对应平台的版权规定。

---

## 联系方式

- **开源仓库：** https://github.com/qq1229037592/OnePicVideo`
- **问题反馈：** 提交 Issue 至项目仓库
