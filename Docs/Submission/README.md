# 提交资料

本目录保存本次提交文档的可编辑源文件。最终可玩入口只有 `Assets/Scenes/CombatTrainingArena.unity`。

- `Game_Design_zh-CN.pdf`：可直接阅读的游戏设计说明。
- `Game_Design_zh-CN.md`：设计说明的可编辑源文件。
- `Play_and_Project_Guide_zh-CN.md`：运行、按键、Unity 版本、工程打开及重新构建说明。
- `Third_Party_Notice_zh-CN.md`：素材与插件目录、制作范围及许可边界。

## 工程归档

在项目根目录执行 `python Tools/Package_Submission.py`，会在已被 Git 忽略的 `Builds/Submission_时间戳/` 生成提交目录：设计文档 PDF、可编辑文档、运行说明、第三方说明、完整工程 ZIP、校验报告和 SHA-256 清单。

归档直接读取当前磁盘上已保存的源文件，包含尚未提交的文件；不使用旧 Git 提交导出，也不删除或精简资源。默认包含 Assets、Packages、ProjectSettings、Docs、Tools、Bin 以及根目录说明／版本控制规则，不包含 Library、Temp、Logs、obj、UserSettings、.git、IDE 生成文件与构建输出。脚本只使用 Python 标准库，不修改 Unity 依赖。

打包时不要同时保存或修改工程；如果文件发生变化，脚本会中止而不是把不同状态混成一个已通过校验的包。完成后会逐项解压读取并核对 SHA-256，验证压缩内容完整；这不是干净机器的 Unity 导入或运行验收。

## 最终组合

提交者另外放入：

1. 完整 Windows 游戏输出文件夹（exe、Data 目录、DLL 等一起保留）。
2. 本次最终游戏包录制的流程视频（建议 MP4）。

录制视频与游戏构建不由工程归档脚本生成。若录像后又修改代码、场景或设置，应重新构建并重新打工程包，避免工程、游戏、文档和视频版本不一致。

第三方工程源文件的转交应符合各产品实际许可；本说明不代替素材取得者的授权确认。
