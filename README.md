<h1 align="center">
  Ez2ReAction
</h1>

## ♿ 这个Mod能干什么

允许你无需打开制铺器    
临时覆写当前选中铺面的音符跳动速度(`Note Jump Speed`)    
以及音符生成偏移(`Note Spawn Offset`)    
简单来说就是铺面调流速 慢铺调快爽打 快铺调慢给反应   
~~🐉b和普通玩家都可以爽用~~


## 🚀 快速开始

> [!IMPORTANT]
> Mod仅在 `1.40.6` 以及 `1.44.0` 的版本上测试可用  
> 游戏版本低于 `1.40.6` 可能用不了()

0. 确认游戏版本
1. 下载最新的发行版构建 [`Ez2ReAction.dll`](https://github.com/ZiHaoSaMa66/Ez2ReAction/releases).
2. 放到你的游戏文件夹里 `Beat Saber/Plugins/`.
3. 启动游戏 , 享受游戏

> [!TIP]
> 卸载此mod只需要删除dll即可

## 🛠️ 从源码进行构建

### 构建需要的依赖

| 依赖名 | 版本 | 来源 |
|------------|---------|--------|
| .NET SDK | 8.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com) |
| Beat Saber | 1.40.6 | Steam / Oculus |
| BSIPA | ^4.3.0 | [BeatMods](https://beatmods.com) |
| BSML | ^1.12.0 | [BeatMods](https://beatmods.com) |

### 步骤


```powershell
# 克隆仓库
git clone https://github.com/ZiHaoSaMa66/Ez2ReAction.git
cd Ez2ReAction

# 修改 Ez2ReAction\Directory.Build.props
# 下的 BeatSaberDir
# 为你游戏实际安装的目录
# 如 F:\BSManager\BSInstances\Beat Saber

# 构建
dotnet build Ez2ReAction.csproj -c Release
```

> [!IMPORTANT]
> 为了解决构建所需的依赖
> 你可能需要本地安装 **旧版本的 Beat Saber** 并且安装好了 BSML 和 BSIPA mod 在 `Libs/` 和 `Plugins/` 文件夹里面


<p align="center">
  Made with ❤️ by <a href="https://github.com/ZiHaoSaMa66">ZiHao</a><br>
  <sub>如果你喜欢这个插件 请考虑给<a href="https://github.com/ZiHaoSaMa66/Ez2ReAction">我</a>点颗Star⭐</sub>
</p>