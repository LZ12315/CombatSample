# 完整开发工作区跨机器交接

## 目标与范围

本次按用户要求同步**全部已保存的项目开发状态**，而不是只挑选已完成功能。此前暂留的 Action／运动策略、编辑器复制粘贴、NodeCanvas Actor Target、弹反代码与测试、Katana／大剑／Kiana 资源、HyperGraph 场景和合同文档一并纳入版本控制。

这是一份制作中工程快照，不是“所有功能完成”的发布承诺。不根据文件日期或旧聊天猜测并删除所谓失败尝试；相互配套的脚本、资源、场景及 `.meta` 保持原有工作区内容。

新电脑安装和克隆步骤见[仓库入口](../../README.md)。必须选择 `HyperGraph` 分支和 `ProjectSettings/ProjectVersion.txt` 指定的 Unity **2022.3.62f3 / 96770f904ca7**。建议使用较短的工程目录（开启 Git 长路径支持）并预留资源、Git 历史和首次导入缓存所需的磁盘空间；当前源文件展开约 7.5 GiB，缓存另计。

## 已检查的可迁移性

- `Assets/`、`Packages/`、`ProjectSettings/`、`Docs/`、`Tools/`、`bin/` 中没有被 `.gitignore` 排除但仍留在本机的源文件。
- 本次新增／修改资源的 `.meta` 齐全；拟提交文件集中没有重复 GUID 或孤立的已跟踪 `.meta`。
- 本次改动的第一方代码没有发现本机盘符绝对路径依赖；简单凭据模式扫描没有发现明显密钥。这不是专业安全审计。
- 没有 Git submodule、源文件符号链接或 Git LFS 指针需要另行补取。
- Animancer 是已纳入仓库的嵌入式 Package；其他已使用的 Assets 插件随源文件提交。
- UPM Registry 依赖通过 `Packages/manifest.json` 与 `Packages/packages-lock.json` 恢复。**SerializeReference Extensions 仍是 GitHub Git 依赖**（锁定 `ee6f6ce519e64f3eeb84865e52aaa23785800133`），因此新电脑除网络外还需要能访问 GitHub 且 Unity 能调用 Git；2026-10-27 在本机复现过一次 `curl 35 Recv failure` 拉取失败。若该包无法拉取，可在有缓存的机器上把 `Library/PackageCache/com.mackysoft.serializereference-extensions@ee6f6ce519` 复制为 `Packages/` 下的本地包并改回 `file:` 引用（本轮曾实施后按要求撤回）。
- 不上传 `Library/`、`Temp/`、`Logs/`、`obj/`、IDE 生成文件、`UserSettings/` 或 `.git/` 内的本机审计副本。它们不是项目资产缺失的替代存储。

## 验证记录

验证使用从 Git 暂存树完整导出的项目副本，而不是拷贝原工程的 Library。未在原工作区自动重存场景、改写玩法参数或批量重烘焙资产。

完整源码快照的 Runtime 与 Editor 程序集通过 Unity Roslyn 编译。已提交版本的隔离 EditMode 结果为 **515 项中 490 通过、25 失败**（含 Start 对齐 14 项全部通过）；失败集中在图生命周期与初始化路径，详见[Start 对齐记录](Locomotion_Start_Alignment.md)与各功能合同文档，不代表本次同步引入。

未完成的部分：本轮曾尝试“空 Library 冷导入”端到端验证，先因 GitHub 包拉取失败、后因烘焙指纹与导入环境耦合而中断；相关改动已按要求全部撤回。因此**新电脑首次导入仍需人工确认**：等待 Package 解析、编译与资源导入结束后，打开 `Assets/Scenes/HyperGraph.unity` 检查是否有缺失脚本或控制台报错。

## 已知原有缺失与未完成验收

`Assets/Scenes/HyperGraph.unity` 有两处 Timeline `m_PlayableAsset` 引用在本地源文件中已经找不到目标：

- `700e998674eb0d64e9cba94036c3e87e`
- `ac6043455a49f7746ac3d768e31be2c0`

它们在此次同步之前的 HEAD 中同样存在，不是本次遗漏的未跟踪资源。没有擅自清空引用或伪造替代 Timeline；换电脑不会自动恢复本地本来就缺失的资产。

**动画烘焙与运行环境绑定**：`AnimationAsset` 的烘焙依赖哈希使用 Unity 的 FBX 导入依赖哈希，其中包含材质与 Shader 状态。换机器或重新导入后，`TurnToMove` 条目可能因 `Root Motion dependencies changed` 被跳过并发出警告，此时五个方向 Start 不生效。处理方式是：在目标机器的 Unity 中对相应 AnimationAsset 执行 Rebuild 后重新进入绑定会话，而不是关闭校验。

原有图生命周期测试、NodeCanvas 旧图接线、Kiana 旧动作中已失效的 `ButtonInputCheck` 条件类型，以及实际 Play Mode 手感等问题应按各自文档继续处理。提交完整现场不等于关闭这些问题，也不等于 Editor 已保存所有内存中的临时编辑。

## 两台机器交替工作

1. 离开当前机器前，显式保存代码、场景与资源，检查 `git status`，提交并推送 `HyperGraph`。
2. 另一台机器先检查自己的 `git status`。本地有未提交制作内容时先保存／提交，不要直接 reset、clean 或覆盖工程。
3. 在干净工作区执行 `git pull --ff-only origin HyperGraph`；如果提示分叉，先处理分支关系，不强制覆盖另一端提交。
4. 用指定 Editor 打开，等待导入与编译完成。Unity 授权、IDE、Git 登录、Editor 窗口布局、外部 AI 会话和未保存缓冲区不由项目仓库迁移。
