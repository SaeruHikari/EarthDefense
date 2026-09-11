# 本地产物目录

此目录存放测试截图、性能报告和临时实验输出，不进入版本控制。
少量 `csharp-*-golden.json`、`csharp-natural-gd.json` 和 `godot-legacy-canonical.json` 是已有 GDScript 行为的冻结测试输入，作为兼容性基准保留。

个人游戏进度、通关备份与本机启动配置位于被忽略的 `.runtime/`、`.runtime-tests/` 和 `saves/`。`LaunchDebug.bat` 优先保留本机开发进度；新检出环境可用 `LaunchNewCampaign.bat` 开始新局。本机的旧存档原生迁移验收需要原始受保护备份，普通代码和数值测试不依赖它。
