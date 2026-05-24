# UABEAC

**UABEA Cascade Edition** —— 基于 [nesrak1/UABEA](https://github.com/nesrak1/UABEA) 二次开发，新增级联导入导出功能。  
原项目作者：[nesrak1](https://github.com/nesrak1) | 许可证：[MIT License](LICENSE)

---

## 新增功能：级联导入导出（Cascade Import / Export）

- **级联导出**：将选中资源及其所有依赖资源一并导出，自动处理 PPtr 引用关系，保持资源间的关联完整性。
- **级联导入**：将级联导出的资源包重新导入，自动恢复依赖路径与类型映射。
- **批量图片导入导出**：在 Texture2D 操作中新增批量导入/导出按钮，支持批量处理贴图资源。
- **MonoBehaviour 类型自动补全**：在级联导入过程中自动识别并补全 MonoBehaviour 类型信息。

---

## 原项目

- **GitHub**：https://github.com/nesrak1/UABEA
