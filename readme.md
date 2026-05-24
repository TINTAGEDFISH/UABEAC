# UABEAC

本项目基于 [nesrak1/UABEA](https://github.com/nesrak1/UABEA)（MIT 许可证）进行二次开发。  
原项目作者：[nesrak1](https://github.com/nesrak1)。  
UABEAC 中的 **C** 代表 **Cascade（级联）**。

---

## 新增功能

### 级联导入导出（Cascade Import / Export）
- **级联导出**：支持将选中资源及其所有依赖资源一并导出，自动处理 PPtr 引用关系，保持资源间的关联完整性。
- **级联导入**：支持将级联导出的资源包重新导入，自动恢复依赖路径与类型映射。
- **批量图片导入导出**：在 Texture2D 操作中新增批量导入/导出按钮，支持批量处理贴图资源。
- **MonoBehaviour 类型自动补全**：在级联导入过程中自动识别并补全 MonoBehaviour 类型信息。

&gt; 新增文件：`UABEAvalonia/TexturePluginBridge.cs`  
&gt; 修改文件：`UABEAvalonia/Forms/InfoWindow.axaml`、`UABEAvalonia/Forms/InfoWindow.axaml.cs`

---

## 原项目

- **GitHub**: https://github.com/nesrak1/UABEA
- **许可证**: [MIT License](LICENSE)
- **Issues / 反馈**: 请前往原项目提交
