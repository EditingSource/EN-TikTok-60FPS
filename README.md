
<div align="center">

<img src="https://github.com/EditingSource.png" width="150" alt="EditingNews" />

</div>

<h1 align="center">EN-TikTok-60FPS</h1>
<div align="center">

<b>A WPF UI application in C# designed to enhance video quality for smooth playback at 60 frames per second on a well-known platform, working together with our extension.</b>

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/) 
[![WPF](https://img.shields.io/badge/WPF-UI-lightgrey.svg)](https://learn.microsoft.com/dotnet/desktop/wpf/) 
[![License](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0) 
[![Releases](https://img.shields.io/github/v/release/EditingSource/EN-TikTok-60FPS?include_prereleases)](https://github.com/EditingNews/EN-TikTok-60FPS/releases)
[![Telegram](https://img.shields.io/badge/Telegram-Channel-blue.svg?logo=telegram)](https://t.me/editing_news) 
[![Discord](https://img.shields.io/badge/Discord-Server-7289DA.svg?logo=discord)](https://discord.gg/usVCEAPyF8)

</div>

---

<div align="center">

### 💖 Support Editings News

If you find this project useful and want to support its development, you can donate via one of the following methods:
<!-- Бейджи в ряд -->
<div style="display: flex; gap: 12px; justify-content: center; margin-bottom: 12px;">
  <img src="https://img.shields.io/badge/PayPal-%23FFC439.svg?style=for-the-badge&logo=paypal&logoColor=white" alt="PayPal" height="28" />
  <img src="https://img.shields.io/badge/Monobank-%230083FF.svg?style=for-the-badge&logo=visa&logoColor=white" alt="Monobank" height="28" />
  <img src="https://img.shields.io/badge/МИР-%23285A98.svg?style=for-the-badge&logo=mir&logoColor=white" alt="МИР (RU)" height="28" />
  <img src="https://img.shields.io/badge/USDT-%2300FF00.svg?style=for-the-badge&logo=tether&logoColor=white" alt="USDT" height="28" />
  <img src="https://img.shields.io/badge/Toncoin-%23000000.svg?style=for-the-badge&logo=toncoin&logoColor=white" alt="Toncoin" height="28" />
</div>

<!-- Реквизиты под бейджами -->
<div style="background:#e0e0e0; padding: 12px 16px; border-radius: 6px; max-width: 600px; margin: 16px auto 0; text-align: left; user-select: text;">
  <details>
    <summary style="cursor:pointer; user-select:none; font-weight: 600; margin-bottom: 8px;">Our Payment Details</summary>
    <ul style="margin: 8px 0 0 16px; padding: 0; list-style: none; font-family: monospace; font-size: 14px;">
      <strong>PayPal:</strong> <code>yoomoney2010@gmail.com</code><br>
      <strong>Monobank (Visa):</strong> <code>4441 1144 8118 1137</code><br>
      <strong>МИР (RU):</strong> <code>2204 1201 1755 8540</code><br>
      <strong>USDT (TRC20):</strong> <code>TNXzbveQq7LUaoDGhCrgdp9DqY5VHgPMcp</code><br>
      <strong>Toncoin:</strong> <code>UQBm45oB1lioZi9RB7Zj8wJPgJanZPHb6QeH9DUD9UhugRzc</code><br>
    </ul>
  </details>
</div>

*Thank you for your support! 🙏*
</div>







## [Download](https://github.com/EditingSource/EN-TikTok-60FPS/)
> This release package includes the UI version of the application with pre-installed RIFE models and FFmpeg for seamless video interpolation and processing.

💡 <b>You can install the latest version of the application using the installer (recommended) or select the desired release in the [releases](https://github.com/EditingSource/EN-TikTok-60FPS/releases) tab.</b>

### System Requirements:
* Windows 10/11
* Intel Core i5 4570  _(recommended, or higher)_
* GTX 1060  _(minimun, or higher)_
* 16GB RAM  _(recommended, or higher)_
* 20GB Disk Space  _(minimum, or higher)_

# Usage
> The following is an example of how to use the application<br>
> It is recommended to select the parameters manually

💡 <b>It is highly recommended to watch the latest tutorial in our telegram channel for a full understanding of the process</b>

1. Import your video
2. Configure interpolation (RIFE 4.6, X2 by default) or disable it
3. Choose the scale factor manually or leave it automatic
4. Upload your video to the platform on the website using the extension
5. Make your video publicly accessible

# MP4 Atom Patching
> Atom patching is used to improve video smoothness and maintain stable FPS and quality

💡 <b>Patching atoms in MP4 is needed to locate and modify specific data structures (atoms) in the MP4 file — specifically the mvhd and mdhd atoms. The application reads their timing parameters (timescale and duration), scales them by a given factor, and writes them back to the file. This allows changing video playback speed in the player without re-encoding</b>

### Scale Factor Examples:
| Input FPS | Scale Factor | Output FPS | 
|---|---|---|
| 120fps | 0.5 | 60fps |
| 120fps | 0.25 | 30fps |
| 90fps | 0.5 | 60fps |
| 90fps | 0.25 | 30fps |
| 60fps | 1.0 | 60fps |
| 60fps | 0.5 | 30fps |

* You can find the source code for atom patching [here](https://github.com/EditingSource/EN-TikTok-60FPS/blob/main/src/VideoPatcher.cs)

# RIFE Interpoation
> Interpolation is used to improve the smoothness and fps of video using AI

💡 <b>The application uses the [rife-ncnn-vulkan](https://github.com/nihui/rife-ncnn-vulkan) library by [nihui](https://github.com/nihui/) for video interpolation (MIT License)</b>

### Available Models:
| model | upstream version | Description |
|---|---|---|
| rife-v4 | 4.0 | Fast General Model |
| rife-v4.6 | 4.6 | General Model, Recommended | 

❗ <b>If you encounter a crash or error, try upgrading your GPU driver:</b>

* Intel: https://downloadcenter.intel.com/product/80939/Graphics-Drivers
* AMD: https://www.amd.com/en/support
* NVIDIA: https://www.nvidia.com/Download/index.aspx <br><br>

* Original RIFE Project - https://github.com/hzwer/arXiv2020-RIFE<br> 
* You can find the source code for interpolation [here](https://github.com/EditingSource/EN-TikTok-60FPS/blob/main/src/VideoInterpolator.cs)<br>


# FFmpeg Video Decoding/Encoding
> FFmpeg is used to split a video into frames and then re-encode it back into video for interpolation

💡 <b>The application uses the [codexffmpeg](https://github.com/GyanD/codexffmpeg) build by [GyanD](https://github.com/GyanD/)</b>

### Supported Codecs:
| Codec | Description | Support |
|---|---|---|
| libx264 |	CPU-based H.264 encoding | All |
| h264_nvenc |	NVIDIA GPU-accelerated H.264 encoding | Nvidia Only

❗ Make sure you have the latest version of your graphics card drivers to avoid errors and visual artifacts.

# ⚠️ Important
> Please read this information before use.

* We are not responsible for your accounts.
* After processing, your video may not play correctly in most players.
* Your video will be in slow motion on the web version of the platform.
* Our software does not access your accounts in any way.
* Before reporting an error, please make sure all requirements for proper application operation are met.
* To date, no accounts have been blocked or banned.
* On very sharp transitions in video, interpolation can cause warps

# Other Open-Source Code Used

* https://github.com/Kinnara/ModernWpf - For styles in App (MIT License)
* https://github.com/Kryptos-FR/markdig.wpf - For Markdown Formatting (MIT License)

---
  
<div align="center">
  <em>© 2025 EditingNews, Evilfy — Licensed under the <a href="https://www.gnu.org/licenses/gpl-3.0">GPLv3 License</a></em>
</div>

