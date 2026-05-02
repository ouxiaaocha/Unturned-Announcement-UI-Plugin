# Unturned-Announcement-UI-Plugin

> Unturned 服务器公告 UI 插件：把服务器规则、轮播公告和临时强制公告显示在自定义 UI 中，也可以同步发送聊天公告。

## ✨ 功能亮点

- 🖥️ **UI 公告面板**：玩家进服后自动加载公告 UI。
- 📜 **服务器规则展示**：从配置文件读取规则文本并显示在 UI 中。
- 📢 **公告轮播**：按配置时间间隔循环展示公告内容。
- ⚡ **临时强制公告**：管理员可以用命令临时覆盖轮播公告。
- 🎨 **富文本支持**：支持颜色、字号、加粗、斜体和换行。
- 🧩 **可自定义 UI**：可使用默认创意工坊 UI，也可以导入 Unity 自行修改后重新上传。

## 📦 下载与安装

1. 打开 GitHub Releases，下载 `Unturned-Announcement-UI-Plugin-v1.0.0.zip`。
2. 解压后可以看到 DLL 文件：`[基佬]UI公告.dll`。
3. 将 DLL 放入服务器的 Rocket 插件目录，例如：

```text
Servers/<服务器名>/Rocket/Plugins/
```

4. 重启服务器，插件会自动加载并生成配置文件。
5. 确保服务器已加载对应的 UI Effect 资源。

## 🧱 默认 UI

如果你不想自己上传 UI 到创意工坊，可以直接使用我已经上传好的默认 UI：

🔗 [Steam 创意工坊：公告UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3663166920)

默认配置中的 `EffectId` 为 `45685`。如果你使用默认 UI，通常不需要修改这个值。

如果 Steam 创意工坊链接无法订阅、失效，或服务器环境无法自动下载创意工坊内容，可以下载 Release 附件中的 `Effect.unity3d`，再按下方自定义 UI 流程自行导入和上传。

## 🚀 命令与权限

| 命令 | 权限 | 说明 |
| --- | --- | --- |
| `/annui` | 无 | 开启或关闭全部 UI |
| `/annui 01` | 无 | 开启或关闭服务器规则 UI |
| `/annui 02` | 无 | 开启或关闭服务器公告 UI |
| `/ann <文字> <秒数>` | `ann` | 强制广播一条临时公告 |

示例：

```text
/ann {color=#ff0000}服务器将在 5 分钟后维护{/color} 60
```

## ⚙️ 配置说明

插件首次启动后会生成配置文件，可按服务器需求修改。

| 配置项 | 说明 |
| --- | --- |
| `EffectId` | UI Effect ID，默认 `45685` |
| `ShowUIByDefault` | 玩家进服后是否默认显示 UI |
| `EnableUIAnnouncements` | 是否启用 UI 公告 |
| `EnableChatAnnouncements` | 是否同时发送聊天公告 |
| `ChatMessageColor` | 聊天公告颜色 |
| `ChatAvatarURL` | 聊天公告头像 URL，留空则不显示头像 |
| `AnnouncementIntervalSeconds` | 公告轮播间隔，单位秒 |
| `EnableRulesUI` | 是否启用服务器规则 UI |
| `ServerTitleText` | 规则 UI 标题 |
| `RulesFieldCount` | 规则文本字段数量 |
| `Rules` | 服务器规则列表 |
| `HelpText` | UI 底部提示文本 |
| `Announcements` | 公告轮播列表 |

## 🧾 富文本与变量

配置文件中使用 `{}` 书写富文本，插件会自动转换为 Unity/TMP 可识别的 `<>`。

| 写法 | 效果 |
| --- | --- |
| `{b}文字{/b}` | 加粗 |
| `{i}文字{/i}` | 斜体 |
| `{color=#3498db}文字{/color}` | 设置颜色 |
| `{size=20}文字{/size}` | 设置字号 |
| `{br}` | 换行 |

可用变量：

| 变量 | 说明 |
| --- | --- |
| `{player_name}` | 玩家名称 |
| `{player_id}` | 玩家 Steam ID |
| `{server_name}` | 服务器名称 |
| `{server_players}` | 当前在线人数 |
| `{server_maxplayers}` | 最大人数 |
| `{server_map}` | 地图名 |
| `{server_mode}` | 游戏模式 |

## 🎨 自定义 UI 并上传创意工坊

1. 从 GitHub Release 下载 `Effect.unity3d`，并导入到你的 Unity 项目中。
2. 推荐使用 Unity `2022.3.62f3` 打开仓库中的 `UI/Unturned` 工程。
3. 根据需要调整 UI 的图片、字体、布局和样式。
4. 不要重命名层级结构中的任何对象，因为插件会通过对象名称查找 UI 元素。
5. 重新上传到 Steam 创意工坊时，请使用唯一的 GUID 和 ID。
6. 上传后，将新 UI 的 Effect ID 写入插件配置文件中的 `EffectId`。
7. 重启服务器并验证 UI 是否正常显示。

插件依赖的关键对象名包括：

```text
Canvas
ServerRulesUI
AnnouncementUI
ServerText
ServerRulesText
ServerRulesText1
ServerRulesText2
ServerRulesText3
HelpText
AnnouncementText
```

## 🛠️ 开发构建

本项目使用 `.NET Framework 4.8`，依赖 `RestoreMonarchy.RocketRedist`。

```powershell
dotnet restore
dotnet build "服务器公告带UI.sln" -c Release
```

Release DLL 输出位置：

```text
bin/Release/net48/[基佬]UI公告.dll
```

## 📄 License

本项目使用 [MIT License](LICENSE) 开源。
