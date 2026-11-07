# CombatSample｜素材与第三方说明

## 制作范围

本作品提交的是一个完整 Unity 战斗演示工程。项目侧的玩法组织、数据配置、动作／命中／状态逻辑、AI 任务适配、镜头协调、HUD、结算重开与训练场集成，主要位于 Assets/Scripts、Assets/Data 及最终场景中。

模型、原始动画、部分音效／特效和第三方插件并非全部由项目自行从零制作。修改、配置或集成第三方资源，不改变原资源的著作权归属。工程内还保留未用于最终场景的制作资料与资源，不应把目录中全部内容都视为本次展示成果。

## 工程内主要依赖与素材目录

| 内容 | 工程位置／名称 | 使用及归属说明 |
| --- | --- | --- |
| Unity 与 URP、Input System、Cinemachine、UGUI 等 | Packages/manifest.json、packages-lock.json | Unity 生态依赖，通过包清单恢复，遵循各包原有许可 |
| Animancer | Packages/com.kybernetik.animancer | 动画播放及编辑能力；包内 License.txt 明确主产品遵循 Unity Asset Store EULA，示例资源另有各自许可 |
| Kinematic Character Controller | Assets/Plugins/KinematicCharacterController | 角色运动底层；遵循原产品许可，不表述为自制插件 |
| NodeCanvas／ParadoxNotion | Assets/Plugins/ParadoxNotion | AI 行为图工具；项目任务适配与具体图配置见 Scripts/Graph、Data/Graph |
| DOTween | Assets/Plugins/Demigiant | 随工程保留的第三方组件，遵循原产品许可 |
| TagTree | Assets/Plugins/Deive_Ex/TagTree | 标签基础能力，保留原有 README 和包信息 |
| SerializeReference Extensions | Packages 清单中的 GitHub 依赖 | 用于序列化引用类型的编辑，依赖锁定信息随工程提交 |
| 角色与模型 | Assets/Art/Models 下各资源目录 | 第三方角色／武器及项目环境资源，保留原文件与引用，不统一宣称原创 |
| 动画 | KatanaAnimationPack、GreatSwordAnimations、Gient_Golem_AnimSet、FighterAnimations 等 | 原始动作素材及其他保留资源；项目将其配置为具体动作、位移、判定和反馈 |
| 特效与音频 | Assets/Art/Effects、Assets/Audio | 第三方素材与项目配置混合，按各自来源许可使用 |
| 结算字体 | Assets/Resources/UI/BattleDeathFont.otf | Noto Sans SC 的改名子集，SIL Open Font License 1.1；原版权、来源版本、SHA-256 和完整许可证见同目录 BattleDeathFont-License.txt |

文档 PDF 使用 Noto Sans SC 排版并嵌入所需字形，其原字体版权归 Adobe，遵循 SIL Open Font License 1.1。文档排版字体不作为额外的 Unity Package 或系统字体安装依赖。

## 许可与源码交付边界

- 本说明是目录与归属索引，不是对第三方资源的重新授权，也不是全部购买／下载凭证。
- 可在游戏中使用某资源，不自动等于可以向任意对象公开转交其原始工程文件。提交者应确认本次评审／工程交付范围符合实际取得的许可，尤其是付费 Asset Store 插件与原始素材。
- 保留工程中的原有许可证、版权声明与素材来源文件；不因精简或压缩而删除这些说明。
- 工作区未包含所有素材的完整来源凭证，本次没有猜测作者、购买状态或补写不存在的授权结论。若提交方要求逐项来源，请由素材取得者补充实际链接及凭证。
- 本工程用于本次作品评审与复现；不因附有源码而声明全部内容可作为开源资源重新分发。
