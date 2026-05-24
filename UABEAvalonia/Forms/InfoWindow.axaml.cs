using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UABEAvalonia.Plugins;
using SixLabors.ImageSharp;
using Image = SixLabors.ImageSharp.Image;

namespace UABEAvalonia
{
    public partial class InfoWindow : Window
    {
        //todo, rework all this
        public AssetWorkspace Workspace { get; }
        public AssetsManager am { get => Workspace.am; }

        //searching
        private string searchText;
        private int searchStart;
        private bool searchDown;
        private bool searchCaseSensitive;
        private bool searching;

        private bool ignoreCloseEvent;

        private HashSet<AssetClassID> filteredOutTypeIds;

        //would prefer using a stream over byte[] but whatever, will for now
        public List<Tuple<AssetsFileInstance, byte[]>> ChangedAssetsDatas { get; set; }

        private ObservableCollection<AssetInfoDataGridItem> dataGridItems;

        private PluginManager pluginManager;

        private DataGridCollectionView dgcv;

        //for preview
        public InfoWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            KeyDown += InfoWindow_KeyDown;
            menuAdd.Click += MenuAdd_Click;
            menuSave.Click += MenuSave_Click;
            menuSaveAs.Click += MenuSaveAs_Click;
            menuCreatePackageFile.Click += MenuCreatePackageFile_Click;
            menuClose.Click += MenuClose_Click;
            menuSearchByName.Click += MenuSearchByName_Click;
            menuContinueSearch.Click += MenuContinueSearch_Click;
            menuGoToAsset.Click += MenuGoToAsset_Click;
            menuFilter.Click += MenuFilter_Click;
            menuHierarchy.Click += MenuHierarchy_Click;
            menuInfo.Click += MenuInfo_Click;
            menuTypeTree.Click += MenuTypeTree_Click;
            menuDependencies.Click += MenuDependencies_Click;
            menuScripts.Click += MenuScripts_Click;
            btnViewData.Click += BtnViewData_Click;
            btnSceneView.Click += BtnSceneView_Click;
            btnExportRaw.Click += BtnExportRaw_Click;
            btnExportDump.Click += BtnExportDump_Click;
            btnImportRaw.Click += BtnImportRaw_Click;
            btnImportDump.Click += BtnImportDump_Click;
            btnEditData.Click += BtnEditData_Click;
            btnRemove.Click += BtnRemove_Click;
            btnPlugin.Click += BtnPlugin_Click;
            dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            Closing += InfoWindow_Closing;

            btnCascadeExportDump.Click += BtnCascadeExportDump_Click;
            btnCascadeImportDump.Click += BtnCascadeImportDump_Click;

            btnBatchExportImages.Click += BtnBatchExportImages_Click;
            btnBatchImportImages.Click += BtnBatchImportImages_Click;

            ignoreCloseEvent = false;
        }

        #region 批量图片导出：自动导出所有 Texture2D 为 PNG，使用级联分隔符

        private async void BtnBatchExportImages_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "选择批量图片导出目录"
            });
            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;
            string dir = selectedFolderPaths[0];

            string sep = "[%~%]";
            int successCount = 0;
            int failCount = 0;
            int skipCount = 0;
            var errors = new List<string>();

            var textureAssets = Workspace.LoadedAssets.Values
                .Where(a => a.ClassId == (int)AssetClassID.Texture2D)
                .ToList();

            foreach (AssetContainer cont in textureAssets)
            {
                try
                {
                    AssetNameUtils.GetDisplayNameFast(Workspace, cont, false, out string assetName, out string typeName);
                    assetName = PathUtils.ReplaceInvalidPathChars(assetName);
                    typeName = PathUtils.ReplaceInvalidPathChars(typeName);

                    string safeAssetName = assetName;

                    string testFileName = $"{typeName}{sep}{safeAssetName}{sep}{Path.GetFileName(cont.FileInstance.path)}{sep}{cont.PathId}.png";
                    string testPath = Path.Combine(dir, testFileName);
                    if (testPath.Length > 230)
                    {
                        string hash = Math.Abs($"{safeAssetName}_{cont.PathId}".GetHashCode()).ToString("X4");
                        safeAssetName = $"{{N:{hash}}}";
                    }

                    string fileName = $"{typeName}{sep}{safeAssetName}{sep}{Path.GetFileName(cont.FileInstance.path)}{sep}{cont.PathId}.png";
                    string filePath = Path.Combine(dir, fileName);

                    AssetTypeValueField texBaseField = TexturePluginBridge.GetByteArrayTexture(Workspace, cont);
                    if (texBaseField == null)
                    {
                        errors.Add($"[PathID {cont.PathId}] 无法反序列化 Texture2D");
                        failCount++;
                        continue;
                    }

                    TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
                    if (texFile.m_Width == 0 && texFile.m_Height == 0)
                    {
                        skipCount++;
                        continue;
                    }

                    if (!TexturePluginBridge.GetResSTexture(texFile, cont.FileInstance))
                    {
                        string resSName = Path.GetFileName(texFile.m_StreamData.path);
                        errors.Add($"[PathID {cont.PathId}] resS 未在 bundle 中找到: {resSName}");
                        failCount++;
                        continue;
                    }

                    byte[] data = TexturePluginBridge.GetRawTextureBytes(texFile, cont.FileInstance);
                    if (data == null)
                    {
                        string resSName = Path.GetFileName(texFile.m_StreamData.path);
                        errors.Add($"[PathID {cont.PathId}] resS 未在磁盘找到: {resSName}");
                        failCount++;
                        continue;
                    }

                    byte[] platformBlob = TexturePluginBridge.GetPlatformBlob(texBaseField);
                    uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

                    bool success = TexturePluginBridge.Export(
                        data, filePath,
                        texFile.m_Width, texFile.m_Height,
                        (TextureFormat)texFile.m_TextureFormat,
                        platform, platformBlob);

                    if (success)
                        successCount++;
                    else
                    {
                        errors.Add($"[PathID {cont.PathId}] 图片编码失败: {(TextureFormat)texFile.m_TextureFormat}");
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"[PathID {cont.PathId}] 异常: {ex.GetType().Name}: {ex.Message}");
                    failCount++;
                }
            }

            string summary = $"批量图片导出完成\n成功: {successCount}\n跳过(0x0): {skipCount}\n失败: {failCount}\n总计 Texture2D: {textureAssets.Count}";
            if (errors.Count > 0)
            {
                summary += "\n\n错误详情:\n" + string.Join("\n", errors.Take(30));
                if (errors.Count > 30) summary += $"\n... 还有 {errors.Count - 30} 条未显示";
            }
            await MessageBoxUtil.ShowDialog(this, "批量导出图片", summary);
        }

        #endregion

        #region 批量图片导入：按文件名 PathID 匹配现有 Texture2D 并替换图片数据

        private async void BtnBatchImportImages_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "选择批量图片导入目录（支持 .png / .tga）"
            });
            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;
            string dir = selectedFolderPaths[0];

            string sep = "[%~%]";
            int successCount = 0;
            int failCount = 0;
            int skipCount = 0;
            var errors = new List<string>();

            if (Workspace.LoadedFiles.Count == 0)
            {
                await MessageBoxUtil.ShowDialog(this, "错误", "当前没有加载任何 Assets 文件，无法执行导入。");
                return;
            }
            AssetsFileInstance targetFile = Workspace.LoadedFiles[0];

            var textureMap = Workspace.LoadedAssets.Values
                .Where(a => a.FileInstance == targetFile && a.ClassId == (int)AssetClassID.Texture2D)
                .ToDictionary(a => a.PathId, a => a);

            var imageFiles = Directory.GetFiles(dir, "*.png")
                .Concat(Directory.GetFiles(dir, "*.tga"))
                .ToList();

            foreach (string imagePath in imageFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string[] parts = fileName.Split(new[] { sep }, StringSplitOptions.None);

                if (parts.Length != 4)
                {
                    skipCount++;
                    continue;
                }

                string typeName = parts[0];
                string pathIdStr = parts[3];

                if (!string.Equals(typeName, nameof(AssetClassID.Texture2D), StringComparison.OrdinalIgnoreCase))
                {
                    skipCount++;
                    continue;
                }

                if (!long.TryParse(pathIdStr, out long pathId))
                {
                    errors.Add($"{Path.GetFileName(imagePath)}: PathID 解析失败 '{pathIdStr}'");
                    failCount++;
                    continue;
                }

                if (!textureMap.TryGetValue(pathId, out AssetContainer cont))
                {
                    skipCount++;
                    continue;
                }

                try
                {
                    AssetTypeValueField baseField = TexturePluginBridge.GetByteArrayTexture(Workspace, cont);
                    if (baseField == null)
                    {
                        errors.Add($"[PathID {pathId}] 无法获取 Texture2D 基础字段");
                        failCount++;
                        continue;
                    }

                    TextureFormat fmt = (TextureFormat)baseField["m_TextureFormat"].AsInt;
                    byte[] platformBlob = TexturePluginBridge.GetPlatformBlob(baseField);
                    uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

                    int origWidth = baseField["m_Width"].AsInt;
                    int origHeight = baseField["m_Height"].AsInt;

                    using var imgToImport = Image.Load<Rgba32>(imagePath);

                    int mips = 1;
                    if (imgToImport.Width == origWidth && imgToImport.Height == origHeight)
                    {
                        if (!baseField["m_MipCount"].IsDummy)
                            mips = baseField["m_MipCount"].AsInt;
                    }
                    else if (TexturePluginBridge.IsPo2(imgToImport.Width) && TexturePluginBridge.IsPo2(imgToImport.Height))
                    {
                        mips = TexturePluginBridge.GetMaxMipCount(imgToImport.Width, imgToImport.Height);
                    }

                    byte[] encImageBytes = TexturePluginBridge.Import(
                        imagePath, fmt,
                        out int width, out int height,
                        ref mips, platform, platformBlob);

                    if (encImageBytes == null)
                    {
                        errors.Add($"[PathID {pathId}] 图片编码失败，格式 {fmt}");
                        failCount++;
                        continue;
                    }

                    AssetTypeValueField m_StreamData = baseField["m_StreamData"];
                    m_StreamData["offset"].AsInt = 0;
                    m_StreamData["size"].AsInt = 0;
                    m_StreamData["path"].AsString = "";

                    if (!baseField["m_MipCount"].IsDummy)
                        baseField["m_MipCount"].AsInt = mips;

                    baseField["m_TextureFormat"].AsInt = (int)fmt;
                    baseField["m_CompleteImageSize"].AsInt = encImageBytes.Length;
                    baseField["m_Width"].AsInt = width;
                    baseField["m_Height"].AsInt = height;

                    AssetTypeValueField image_data = baseField["image data"];
                    image_data.Value.ValueType = AssetValueType.ByteArray;
                    image_data.TemplateField.ValueType = AssetValueType.ByteArray;
                    image_data.AsByteArray = encImageBytes;

                    byte[] savedAsset = baseField.WriteToByteArray();
                    var replacer = new AssetsReplacerFromMemory(
                        cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

                    Workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));

                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"[PathID {pathId}] 异常: {ex.GetType().Name}: {ex.Message}");
                    failCount++;
                }
            }

            string summary = $"批量图片导入完成\n成功: {successCount}\n跳过(非匹配): {skipCount}\n失败: {failCount}\n扫描图片: {imageFiles.Count}";
            if (errors.Count > 0)
            {
                summary += "\n\n错误详情:\n" + string.Join("\n", errors.Take(30));
                if (errors.Count > 30) summary += $"\n... 还有 {errors.Count - 30} 条未显示";
            }
            await MessageBoxUtil.ShowDialog(this, "批量导入图片", summary);
        }

        #endregion





        #region 级联导出：递归导出选中 Asset 及其所有 PPtr 引用（含 Windows 长路径保护）

        private async void BtnCascadeExportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "选择级联导出目录"
            });
            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;
            string dir = selectedFolderPaths[0];

            string sep = "[%~%]";

            var visited = new HashSet<(AssetsFileInstance file, long pathId)>();
            var cascadeAssets = new List<AssetContainer>();
            var externalDepDetails = new List<string>();

            foreach (AssetContainer rootCont in selection)
            {
                AssetsFileInstance rootFileInst = rootCont.FileInstance;
                if (!visited.Contains((rootCont.FileInstance, rootCont.PathId)))
                {
                    visited.Add((rootCont.FileInstance, rootCont.PathId));
                    cascadeAssets.Add(rootCont);

                    try
                    {
                        AssetTypeValueField? baseField = Workspace.GetBaseField(rootCont);
                        if (baseField != null)
                            CollectPPtrReferencesRecursive(baseField, rootCont.FileInstance, rootFileInst, visited, cascadeAssets, externalDepDetails);
                    }
                    catch (Exception ex)
                    {
                        externalDepDetails.Add($"[递归收集失败] Root {rootCont.PathId} @ {Path.GetFileName(rootCont.FileInstance.path)}: {ex.Message}");
                    }
                }
            }

            int successCount = 0;
            int failCount = 0;
            int rawFailCount = 0;
            int truncatedCount = 0;                          // 新增：被截断名称的资产数
            var failDetails = new List<string>();
            var nameRegistry = new List<(long pathId, string typeName, string shortCode, string originalName)>(); // 新增：名称映射表

            foreach (AssetContainer cont in cascadeAssets)
            {
                string? currentAssetName = null;
                string? currentTypeName = null;
                try
                {
                    AssetNameUtils.GetDisplayNameFast(Workspace, cont, false, out string assetName, out string typeName);
                    assetName = PathUtils.ReplaceInvalidPathChars(assetName);
                    typeName = PathUtils.ReplaceInvalidPathChars(typeName);

                    string originalAssetName = assetName;    // 保留原始名称用于映射表
                    string safeAssetName = assetName;

                    // ====== 关键新增：Windows 路径长度保护 ======
                    // 预估完整路径长度（目录 + 文件名）
                    string testFileName = $"{typeName}{sep}{safeAssetName}{sep}{Path.GetFileName(cont.FileInstance.path)}{sep}{cont.PathId}.txt";
                    string testPath = Path.Combine(dir, testFileName);

                    if (testPath.Length > 230) // 保守阈值，低于 Windows MAX_PATH (260)
                    {
                        // 基于原始名称+PathId 生成 4 位十六进制哈希短码，确保同一批次内唯一
                        string hashInput = $"{originalAssetName}_{cont.PathId}";
                        string hash = Math.Abs(hashInput.GetHashCode()).ToString("X4");
                        string shortCode = $"{{N:{hash}}}";

                        // 处理极端哈希冲突
                        int collisionIdx = 0;
                        while (nameRegistry.Any(r => r.shortCode == shortCode && r.pathId != cont.PathId))
                        {
                            collisionIdx++;
                            shortCode = $"{{N:{hash}_{collisionIdx}}}";
                        }

                        safeAssetName = shortCode;
                        nameRegistry.Add((cont.PathId, typeName, shortCode, originalAssetName));
                        truncatedCount++;
                    }
                    // =============================================

                    currentAssetName = safeAssetName;
                    currentTypeName = typeName;

                    // 1. 导出文本 dump（主格式）
                    string txtFileName = $"{typeName}{sep}{safeAssetName}{sep}{Path.GetFileName(cont.FileInstance.path)}{sep}{cont.PathId}.txt";
                    string txtFilePath = Path.Combine(dir, txtFileName);

                    using (FileStream fs = File.Open(txtFilePath, FileMode.Create))
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        AssetTypeValueField? baseField = Workspace.GetBaseField(cont);
                        if (baseField == null)
                        {
                            failCount++;
                            failDetails.Add($"[TXT 反序列化失败] {typeName} / {safeAssetName}  (PathID={cont.PathId}, File={Path.GetFileName(cont.FileInstance.path)}) — Workspace.GetBaseField 返回 null");
                            continue;
                        }

                        AssetImportExport dumper = new AssetImportExport();
                        dumper.DumpTextAsset(sw, baseField);
                    }

                    // 2. 同时导出 raw（辅助格式，失败不影响主流程）
                    string rawFileName = $"{typeName}{sep}{safeAssetName}{sep}{Path.GetFileName(cont.FileInstance.path)}{sep}{cont.PathId}.dat";
                    string rawFilePath = Path.Combine(dir, rawFileName);
                    try
                    {
                        using (FileStream fs = File.Open(rawFilePath, FileMode.Create))
                        {
                            AssetImportExport dumper = new AssetImportExport();
                            dumper.DumpRawAsset(fs, cont.FileReader, cont.FilePosition, cont.Size);
                        }
                    }
                    catch (Exception rawEx)
                    {
                        rawFailCount++;
                        failDetails.Add($"[RAW 导出失败] {typeName} / {safeAssetName}  (PathID={cont.PathId}) — {rawEx.Message}");
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    string displayName = currentAssetName ?? "???";
                    string displayType = currentTypeName ?? "???";
                    failDetails.Add(
                        $"[TXT 导出失败] {displayType} / {displayName}  " +
                        $"(PathID={cont.PathId}, File={Path.GetFileName(cont.FileInstance.path)}) — {ex.GetType().Name}: {ex.Message}\n" +
                        $"   堆栈: {ex.StackTrace?.Replace("\n", "\n   ")}"
                    );
                }
            }

            // ====== 关键新增：保存名称映射表 ======
            if (nameRegistry.Count > 0)
            {
                try
                {
                    string regPath = Path.Combine(dir, "_cascade_name_registry.txt");
                    using (var sw = new StreamWriter(regPath, false, System.Text.Encoding.UTF8))
                    {
                        sw.WriteLine("# 级联导出名称映射表");
                        sw.WriteLine("# 格式: PathId|TypeName|ShortCode|OriginalName");
                        sw.WriteLine("# 说明: 若资产名称过长导致 Windows 路径超限，程序自动将名称替换为 ShortCode");
                        sw.WriteLine("#       导入时无需此文件，仅用于人工查阅原始名称");
                        foreach (var entry in nameRegistry.OrderBy(r => r.pathId))
                        {
                            sw.WriteLine($"{entry.pathId}|{entry.typeName}|{entry.shortCode}|{entry.originalName}");
                        }
                    }
                }
                catch (Exception regEx)
                {
                    failDetails.Add($"[映射表保存失败] _cascade_name_registry.txt — {regEx.Message}");
                }
            }
            // =======================================

            // 组装汇总信息
            string summary = $"级联导出完成\n成功: {successCount} 个\n失败: {failCount} 个\nRaw 附赠导出失败: {rawFailCount} 个\n总计收集: {cascadeAssets.Count} 个（含自身）";

            if (truncatedCount > 0)
            {
                summary += $"\n\n名称截断替换: {truncatedCount} 个（已生成 _cascade_name_registry.txt）";
            }

            if (externalDepDetails.Count > 0)
            {
                summary += $"\n\n其中外部依赖资产: {externalDepDetails.Count} 个";
                int showCount = Math.Min(externalDepDetails.Count, 20);
                if (showCount > 0)
                {
                    summary += "\n────────────────────────────\n";
                    for (int i = 0; i < showCount; i++)
                        summary += externalDepDetails[i] + "\n";
                }
                if (externalDepDetails.Count > 20)
                    summary += $"... 还有 {externalDepDetails.Count - 20} 个外部依赖未列出";
            }

            if (failDetails.Count > 0)
            {
                summary += $"\n\n详细失败日志（共 {failDetails.Count} 条）:\n════════════════════════════\n";
                int showFailCount = Math.Min(failDetails.Count, 30);
                for (int i = 0; i < showFailCount; i++)
                    summary += failDetails[i] + "\n────────────────────────────\n";

                if (failDetails.Count > 30)
                    summary += $"... 还有 {failDetails.Count - 30} 条失败日志未显示\n";
            }

            await MessageBoxUtil.ShowDialog(this, "级联导出完成", summary);
        }

        /// <summary>
        /// 递归遍历 AssetTypeValueField，收集所有 PPtr 指向的 AssetContainer。
        /// 若引用资产所在文件与 rootFileInst（根选中资产所属文件）不同，则记录为外部依赖。
        /// </summary>
        private void CollectPPtrReferencesRecursive(
            AssetTypeValueField field,
            AssetsFileInstance fileInst,
            AssetsFileInstance rootFileInst,
            HashSet<(AssetsFileInstance file, long pathId)> visited,
            List<AssetContainer> result,
            List<string> externalDepDetails)
        {
            if (field?.Children == null)
                return;

            foreach (var child in field.Children)
            {
                if (child.Children != null && child.Children.Count == 2)
                {
                    bool hasFileId = false;
                    bool hasPathId = false;
                    foreach (var sub in child.Children)
                    {
                        if (sub.FieldName == "m_FileID") hasFileId = true;
                        if (sub.FieldName == "m_PathID") hasPathId = true;
                    }

                    if (hasFileId && hasPathId)
                    {
                        try
                        {
                            AssetContainer? refCont = Workspace.GetAssetContainer(fileInst, child, true);
                            if (refCont != null && refCont.PathId != 0 &&
                                !visited.Contains((refCont.FileInstance, refCont.PathId)))
                            {
                                // 检测并记录外部依赖（相对于根资产所在文件）
                                if (refCont.FileInstance != rootFileInst)
                                {
                                    try
                                    {
                                        AssetNameUtils.GetDisplayNameFast(Workspace, refCont, false, out string refAssetName, out string refTypeName);
                                        externalDepDetails.Add($"[{refTypeName}] {refAssetName}  (PathID={refCont.PathId}, File={Path.GetFileName(refCont.FileInstance.path)})");
                                    }
                                    catch (Exception nameEx)
                                    {
                                        externalDepDetails.Add($"[Unknown] ???  (PathID={refCont.PathId}, File={Path.GetFileName(refCont.FileInstance.path)}) — 获取名称失败: {nameEx.Message}");
                                    }
                                }

                                visited.Add((refCont.FileInstance, refCont.PathId));
                                result.Add(refCont);

                                AssetTypeValueField? refBaseField = Workspace.GetBaseField(refCont);
                                if (refBaseField != null)
                                    CollectPPtrReferencesRecursive(refBaseField, refCont.FileInstance, rootFileInst, visited, result, externalDepDetails);
                            }
                        }
                        catch (Exception ex)
                        {
                            externalDepDetails.Add($"[递归解析异常] 字段 {child.FieldName} 在 {Path.GetFileName(fileInst.path)} — {ex.Message}");
                        }
                        continue;
                    }
                }

                CollectPPtrReferencesRecursive(child, fileInst, rootFileInst, visited, result, externalDepDetails);
            }
        }

        #endregion
        /// <summary>
        /// 递归遍历 AssetTypeValueField，收集所有 PPtr 指向的 AssetContainer
        /// </summary>
        private void CollectPPtrReferencesRecursive(AssetTypeValueField field, AssetsFileInstance fileInst,
            HashSet<(AssetsFileInstance file, long pathId)> visited, List<AssetContainer> result)
        {
            if (field?.Children == null)
                return;

            foreach (var child in field.Children)
            {
                if (child.Children != null && child.Children.Count == 2)
                {
                    bool hasFileId = false;
                    bool hasPathId = false;
                    foreach (var sub in child.Children)
                    {
                        if (sub.FieldName == "m_FileID") hasFileId = true;
                        if (sub.FieldName == "m_PathID") hasPathId = true;
                    }

                    if (hasFileId && hasPathId)
                    {
                        try
                        {
                            AssetContainer? refCont = Workspace.GetAssetContainer(fileInst, child, true);
                            if (refCont != null && refCont.PathId != 0 &&
                                !visited.Contains((refCont.FileInstance, refCont.PathId)))
                            {
                                visited.Add((refCont.FileInstance, refCont.PathId));
                                result.Add(refCont);

                                AssetTypeValueField? refBaseField = Workspace.GetBaseField(refCont);
                                if (refBaseField != null)
                                    CollectPPtrReferencesRecursive(refBaseField, refCont.FileInstance, visited, result);
                            }
                        }
                        catch
                        {
                            // 忽略无法解析的跨文件引用或损坏数据
                        }
                        continue;
                    }
                }

                CollectPPtrReferencesRecursive(child, fileInst, visited, result);
            }
        }

        #region 级联导入：仅 txt，先新建资产再导入 dump 数据，保留重复 PathID 跳过，不再依赖文件名携带 MonoId

        private async void BtnCascadeImportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // ====== 新增：可选打开原Bundle以补充TypeTree ======
            AssetsFileInstance? sourceFileInst = null;
            var useSourceResult = await MessageBoxUtil.ShowDialog(this, "TypeTree 补充选项",
                "如果现Bundle缺少某些类型的TypeTree定义，可从原Bundle复制。\n\n" +
                "注意：请选择原始的 .assets 文件；若选的是 .bundle 容器，程序会尝试自动解压。\n\n是否需要打开原Bundle文件？",
                MessageBoxType.YesNo);

            if (useSourceResult == MessageBoxResult.Yes)
            {
                var sourceFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = "选择原Bundle文件",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>()
            {
                new FilePickerFileType("Unity assets / bundle") { Patterns = new List<string>() { "*.*" } }
            }
                });
                string[] sourcePaths = FileDialogUtils.GetOpenFileDialogFiles(sourceFiles);

                if (sourcePaths.Length == 0 || !File.Exists(sourcePaths[0]))
                {
                    await MessageBoxUtil.ShowDialog(this, "提示", "未选择有效文件，跳过 TypeTree 补充，继续导入。");
                }
                else if (new FileInfo(sourcePaths[0]).Length == 0)
                {
                    await MessageBoxUtil.ShowDialog(this, "提示", "所选文件为空，跳过 TypeTree 补充，继续导入。");
                }
                else
                {
                    string path = sourcePaths[0];
                    try
                    {
                        // 优先尝试作为裸 assets 文件加载
                        sourceFileInst = am.LoadAssetsFile(path, false);
                    }
                    catch (Exception exAssets)
                    {
                        // 若失败，尝试作为 Bundle 容器加载
                        try
                        {
                            var bundleInst = am.LoadBundleFile(path);
                            if (bundleInst?.file != null)
                            {
                                sourceFileInst = am.LoadAssetsFileFromBundle(bundleInst, 0, false);
                            }
                            else
                            {
                                throw new InvalidOperationException("Bundle 加载失败，file 为 null。");
                            }
                        }
                        catch (Exception exBundle)
                        {
                            await MessageBoxUtil.ShowDialog(this, "加载原Bundle失败",
                                $"无法作为 assets 文件加载：{exAssets.Message}\n" +
                                $"也无法作为 bundle 加载：{exBundle.Message}\n\n" +
                                $"路径: {path}\n\n将跳过 TypeTree 补充，继续导入。");
                            sourceFileInst = null;
                        }
                    }
                }
            }
            // =====================================================

            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "选择级联导入目录（支持 .txt 与 .dat）"
            });
            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;
            string dir = selectedFolderPaths[0];

            // 同时收集 .txt 和 .dat，按文件名去重，同名优先保留 .dat（raw 更可靠）
            var txtFiles = Directory.GetFiles(dir, "*.txt");
            var datFiles = Directory.GetFiles(dir, "*.dat");

            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in txtFiles.Concat(datFiles))
            {
                string key = Path.GetFileNameWithoutExtension(f);
                if (!fileMap.ContainsKey(key))
                {
                    fileMap.Add(key, f);
                }
                else
                {
                    string existing = fileMap[key];
                    if (Path.GetExtension(existing).Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetExtension(f).Equals(".dat", StringComparison.OrdinalIgnoreCase))
                    {
                        fileMap[key] = f;
                    }
                }
            }

            var dumpFiles = fileMap.Values.ToList();

            if (dumpFiles.Count == 0)
            {
                await MessageBoxUtil.ShowDialog(this, "未找到文件", "目录中没有 .txt 或 .dat 文件。");
                return;
            }

            if (Workspace.LoadedFiles.Count == 0)
            {
                await MessageBoxUtil.ShowDialog(this, "错误", "当前没有加载任何 Assets 文件，无法执行导入。");
                return;
            }
            AssetsFileInstance targetFile = Workspace.LoadedFiles[0];

            var allEncounteredPathIds = new HashSet<long>();
            foreach (var asset in Workspace.LoadedAssets.Values)
            {
                if (asset.FileInstance == targetFile)
                    allEncounteredPathIds.Add(asset.PathId);
            }

            int successCount = 0;
            int failCount = 0;
            int skipCount = 0;
            var duplicateDetails = new List<string>();
            var errors = new List<string>();

            string sep = "[%~%]";

            // 关键缓存：记录本次导入过程中动态注册到 ScriptTypes 的 MonoScript PathID -> monoId
            var scriptTypeMap = new Dictionary<long, ushort>();

            // 关键改动：按类型排序，MonoScript 优先，其他中间，MonoBehaviour 最后
            var orderedFiles = dumpFiles.OrderBy(f =>
            {
                string name = Path.GetFileNameWithoutExtension(f);
                string[] parts = name.Split(new[] { sep }, StringSplitOptions.None);
                if (parts.Length > 0 && parts[0] == nameof(AssetClassID.MonoScript))
                    return 0;
                if (parts.Length > 0 && parts[0] == nameof(AssetClassID.MonoBehaviour))
                    return 2;
                return 1;
            }).ToList();

            foreach (string filePath in orderedFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                bool isRaw = Path.GetExtension(filePath).Equals(".dat", StringComparison.OrdinalIgnoreCase);

                string[] parts = fileName.Split(new[] { sep }, StringSplitOptions.None);
                if (parts.Length != 4)
                {
                    errors.Add($"{Path.GetFileName(filePath)}: 文件名格式错误（分隔符段数={parts.Length}，预期=4）");
                    skipCount++;
                    continue;
                }

                string typeName = parts[0];
                string pathIdStr = parts[3];

                if (!long.TryParse(pathIdStr, out long pathId))
                {
                    errors.Add($"{Path.GetFileName(filePath)}: PathID 解析失败 '{pathIdStr}'");
                    skipCount++;
                    continue;
                }

                if (allEncounteredPathIds.Contains(pathId))
                {
                    duplicateDetails.Add($"PathID {pathId}  <--  文件: {Path.GetFileName(filePath)}");
                    failCount++;
                    continue;
                }

                int classId = -1;
                if (Enum.TryParse<AssetClassID>(typeName, true, out AssetClassID classEnum))
                {
                    classId = (int)classEnum;
                }
                else if (int.TryParse(typeName, out int parsedClassId))
                {
                    classId = parsedClassId;
                }
                else
                {
                    errors.Add($"{Path.GetFileName(filePath)}: 无法识别的类型名称 '{typeName}'");
                    failCount++;
                    continue;
                }

                try
                {
                    byte[]? bytes = null;
                    string? exceptionMessage = null;
                    string? dumpText = null;

                    // ========== 根据扩展名选择读取方式 ==========
                    if (isRaw)
                    {
                        bytes = File.ReadAllBytes(filePath);
                    }
                    else
                    {
                        dumpText = File.ReadAllText(filePath);
                        using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(dumpText)))
                        using (StreamReader sr = new StreamReader(ms))
                        {
                            AssetImportExport importer = new AssetImportExport();
                            bytes = importer.ImportTextAsset(sr, out exceptionMessage);
                        }
                    }

                    if (bytes == null)
                    {
                        errors.Add($"{Path.GetFileName(filePath)}: 数据读取/解析失败 ({exceptionMessage ?? "未知错误"})");
                        failCount++;
                        continue;
                    }

                    long scriptPathId = 0;
                    ushort monoId = 0xFFFF;
                    if (classId == (int)AssetClassID.MonoBehaviour)
                    {
                        string? resolveText = dumpText;

                        // Raw 模式下尝试读取同名 .txt 来辅助推导 monoId
                        if (isRaw && string.IsNullOrEmpty(resolveText))
                        {
                            string txtPath = Path.Combine(dir, fileName + ".txt");
                            if (File.Exists(txtPath))
                            {
                                resolveText = File.ReadAllText(txtPath);
                            }
                            else
                            {
                                errors.Add($"{Path.GetFileName(filePath)}: Raw MonoBehaviour 缺少同名 .txt 元数据，无法推导 MonoScript 索引");
                                failCount++;
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(resolveText))
                        {
                            scriptPathId = ExtractScriptPathId(resolveText);
                            monoId = ResolveMonoId(targetFile, resolveText, dir, sep, scriptTypeMap);
                        }

                        if (monoId == 0xFFFF)
                        {
                            errors.Add($"{Path.GetFileName(filePath)}: 无法推导 MonoBehaviour 的 MonoScript 索引");
                            failCount++;
                            continue;
                        }
                    }

                    // ====== 关键新增：如果指定了原Bundle，确保TypeTree已注册 ======
                    if (sourceFileInst != null)
                    {
                        EnsureTypeTreeRegistered(targetFile, sourceFileInst, classId, monoId, scriptPathId);
                    }
                    // ==============================================================

                    var replacer = new AssetsReplacerFromMemory(pathId, classId, monoId, bytes);
                    Workspace.AddReplacer(targetFile, replacer, new MemoryStream(bytes));

                    if (classId == (int)AssetClassID.MonoBehaviour && monoId != 0xFFFF)
                    {
                        var newCont = Workspace.LoadedAssets.Values.FirstOrDefault(a =>
                            a.FileInstance == targetFile && a.PathId == pathId);
                        if (newCont != null)
                            SetMonoId(newCont, monoId);
                    }

                    // ====== 关键新增：MonoScript 导入后动态注册到 ScriptTypes ======
                    if (classId == (int)AssetClassID.MonoScript)
                    {
                        var meta = targetFile.file.Metadata;
                        if (meta?.ScriptTypes != null)
                        {
                            bool alreadyInScriptTypes = false;
                            for (ushort i = 0; i < meta.ScriptTypes.Count; i++)
                            {
                                if (meta.ScriptTypes[i].PathId == pathId)
                                {
                                    scriptTypeMap[pathId] = i;
                                    alreadyInScriptTypes = true;
                                    break;
                                }
                            }

                            if (!alreadyInScriptTypes)
                            {
                                try
                                {
                                    var stType = meta.ScriptTypes.GetType().GetGenericArguments().FirstOrDefault()
                                                 ?? meta.ScriptTypes[0]?.GetType();
                                    if (stType != null)
                                    {
                                        var newSt = Activator.CreateInstance(stType);
                                        var pathIdProp = stType.GetProperty("PathId");
                                        var pathIdField = stType.GetField("PathId");
                                        var fileIdProp = stType.GetProperty("FileId");
                                        var fileIdField = stType.GetField("FileId");

                                        if (pathIdProp != null)
                                            pathIdProp.SetValue(newSt, pathId);
                                        else if (pathIdField != null)
                                            pathIdField.SetValue(newSt, pathId);

                                        if (fileIdProp != null)
                                            fileIdProp.SetValue(newSt, 0);
                                        else if (fileIdField != null)
                                            fileIdField.SetValue(newSt, 0);

                                        meta.ScriptTypes.Add((AssetPPtr)newSt);
                                        ushort newIndex = (ushort)(meta.ScriptTypes.Count - 1);
                                        scriptTypeMap[pathId] = newIndex;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errors.Add($"MonoScript PathID {pathId}: 已导入但无法注册到 ScriptTypes ({ex.Message})");
                                }
                            }
                        }
                    }
                    // ==============================================================

                    allEncounteredPathIds.Add(pathId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    failCount++;
                }
            }

            string summary = $"级联导入完成\n成功: {successCount}\n失败: {failCount}\n跳过: {skipCount}";

            if (duplicateDetails.Count > 0)
            {
                summary += $"\n\n检测到重复的 PathID（共 {duplicateDetails.Count} 个）:\n"
                         + string.Join("\n", duplicateDetails);
            }

            if (errors.Count > 0)
            {
                summary += "\n\n错误/跳过详情:\n" + string.Join("\n", errors.Take(30));
                if (errors.Count > 30) summary += $"\n... 还有 {errors.Count - 30} 条";
            }

            await MessageBoxUtil.ShowDialog(this, "级联导入结果", summary);
        }

        /// <summary>
        /// 从 MonoBehaviour 的 dump 文本中提取 m_Script 的 m_PathID
        /// </summary>
        private long ExtractScriptPathId(string dumpText)
        {
            string[] lines = dumpText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("PPtr<MonoScript>") && lines[i].Contains("m_Script"))
                {
                    for (int j = i + 1; j < Math.Min(i + 5, lines.Length); j++)
                    {
                        if (lines[j].Contains("m_PathID"))
                        {
                            int eq = lines[j].IndexOf('=');
                            if (eq >= 0 && long.TryParse(lines[j].Substring(eq + 1).Trim(), out long pid))
                                return pid;
                        }
                    }
                    break;
                }
            }
            return 0;
        }

        /// <summary>
        /// 检查目标文件的TypeTree中是否已包含指定类型（classId/monoId），
        /// 若不存在且源文件存在该类型定义，则复制到目标文件。
        /// 对于 MonoBehaviour，会根据 scriptPathId 精确匹配源Bundle中的类型定义，
        /// 并将复制后的 ScriptTypeIndex 修正为现Bundle的 monoId。
        /// </summary>
        private void EnsureTypeTreeRegistered(AssetsFileInstance targetFile, AssetsFileInstance sourceFile, int classId, ushort monoId, long scriptPathId = 0)
        {
            try
            {
                var targetMeta = targetFile.file.Metadata;
                var sourceMeta = sourceFile.file.Metadata;
                if (targetMeta == null || sourceMeta == null) return;

                if (classId == (int)AssetClassID.MonoBehaviour && monoId != 0xFFFF)
                {
                    // 目标文件若已有 (TypeId=114, ScriptTypeIndex=monoId)，无需处理
                    if (targetMeta.FindTypeTreeTypeByID(classId, monoId) != null)
                        return;

                    // 在源文件 ScriptTypes 中定位该 MonoScript 的原始索引
                    ushort sourceScriptIdx = 0xFFFF;
                    if (scriptPathId != 0 && sourceMeta.ScriptTypes != null)
                    {
                        for (ushort i = 0; i < sourceMeta.ScriptTypes.Count; i++)
                        {
                            if (sourceMeta.ScriptTypes[i].PathId == scriptPathId)
                            {
                                sourceScriptIdx = i;
                                break;
                            }
                        }
                    }

                    // 只有源文件确实包含该 MonoScript 时，才尝试复制其 TypeTreeType
                    if (sourceScriptIdx != 0xFFFF)
                    {
                        // 优先按 (TypeId, ScriptTypeIndex) 精确匹配；失败则退化为仅按 ScriptTypeIndex 匹配
                        var sourceType = sourceMeta.FindTypeTreeTypeByID(classId, sourceScriptIdx)
                                      ?? sourceMeta.FindTypeTreeTypeByScriptIndex(sourceScriptIdx);

                        if (sourceType != null)
                        {
                            var cloned = ShallowCloneTypeTreeType(sourceType);
                            if (cloned != null)
                            {
                                cloned.ScriptTypeIndex = monoId;
                                targetMeta.TypeTreeTypes.Add(cloned);
                            }
                        }
                    }
                }
                else if (classId != (int)AssetClassID.MonoBehaviour)
                {
                    // 非 MonoBehaviour：仅按 TypeId 匹配
                    if (targetMeta.FindTypeTreeTypeByID(classId) != null)
                        return;

                    var sourceType = sourceMeta.FindTypeTreeTypeByID(classId);
                    if (sourceType != null)
                    {
                        var cloned = ShallowCloneTypeTreeType(sourceType);
                        if (cloned != null)
                        {
                            targetMeta.TypeTreeTypes.Add(cloned);
                        }
                    }
                }
            }
            catch
            {
                // TypeTree复制失败不应阻塞主导入流程
            }
        }

        /// <summary>
        /// 对 TypeTreeType 执行浅拷贝（MemberwiseClone）。
        /// 导入流程不会修改 TypeTree 节点结构，浅拷贝足以避免引用污染。
        /// </summary>
        private TypeTreeType? ShallowCloneTypeTreeType(TypeTreeType source)
        {
            try
            {
                var method = typeof(TypeTreeType).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) return null;
                return (TypeTreeType)method.Invoke(source, null)!;
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// 尝试从类型中获取指定候选名称的属性
        /// </summary>
        private string? GetPropertyName(Type type, string[] candidates)
        {
            foreach (var name in candidates)
            {
                if (type.GetProperty(name) != null) return name;
            }
            return null;
        }

        /// <summary>
        /// 深拷贝对象（优先ICloneable，否则MemberwiseClone）
        /// </summary>
        private object CloneObject(object obj)
        {
            if (obj is System.ICloneable cloneable)
                return cloneable.Clone();

            var method = obj.GetType().GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
                return method.Invoke(obj, null);

            return obj;
        }

        /// <summary>
        /// 推导 MonoBehaviour 的 monoId。
        /// </summary>
        private ushort ResolveMonoId(AssetsFileInstance targetFile, string dumpText, string dir, string sep,
            Dictionary<long, ushort> scriptTypeMap)
        {
            long scriptPathId = 0;
            string[] lines = dumpText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("PPtr<MonoScript>") && lines[i].Contains("m_Script"))
                {
                    for (int j = i + 1; j < Math.Min(i + 5, lines.Length); j++)
                    {
                        if (lines[j].Contains("m_PathID"))
                        {
                            int eq = lines[j].IndexOf('=');
                            if (eq >= 0 && long.TryParse(lines[j].Substring(eq + 1).Trim(), out long pid))
                            {
                                scriptPathId = pid;
                                break;
                            }
                        }
                    }
                    break;
                }
            }

            if (scriptPathId == 0)
                return 0xFFFF;

            if (scriptTypeMap.TryGetValue(scriptPathId, out ushort cachedMonoId))
                return cachedMonoId;

            foreach (var asset in Workspace.LoadedAssets.Values)
            {
                if (asset.ClassId != (int)AssetClassID.MonoBehaviour)
                    continue;

                if (MonoBehaviourReferencesScript(asset, scriptPathId))
                {
                    ushort? id = GetMonoId(asset);
                    if (id.HasValue)
                        return id.Value;
                }
            }

            var meta = targetFile.file.Metadata;
            if (meta?.ScriptTypes != null)
            {
                for (ushort i = 0; i < meta.ScriptTypes.Count; i++)
                {
                    if (meta.ScriptTypes[i].PathId == scriptPathId)
                        return i;
                }
            }

            try
            {
                string scriptDumpFile = Directory.GetFiles(dir, "*.txt")
                    .FirstOrDefault(f =>
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        string[] parts = name.Split(new[] { sep }, StringSplitOptions.None);
                        return parts.Length == 4
                               && parts[0] == nameof(AssetClassID.MonoScript)
                               && parts[3] == scriptPathId.ToString();
                    });

                if (scriptDumpFile != null)
                {
                    string scriptDump = File.ReadAllText(scriptDumpFile);
                    string scriptClassName = ExtractFieldFromDump(scriptDump, "m_ClassName");
                    string scriptAssemblyName = ExtractFieldFromDump(scriptDump, "m_AssemblyName");

                    if (!string.IsNullOrEmpty(scriptClassName) && meta?.ScriptTypes != null)
                    {
                        for (ushort i = 0; i < meta.ScriptTypes.Count; i++)
                        {
                            long stPathId = meta.ScriptTypes[i].PathId;
                            var monoScriptCont = Workspace.LoadedAssets.Values.FirstOrDefault(a =>
                                a.FileInstance == targetFile
                                && a.PathId == stPathId
                                && a.ClassId == (int)AssetClassID.MonoScript);

                            if (monoScriptCont != null)
                            {
                                var bf = Workspace.GetBaseField(monoScriptCont);
                                if (bf != null)
                                {
                                    string stClassName = bf["m_ClassName"]?.AsString ?? "";
                                    string stAssemblyName = bf["m_AssemblyName"]?.AsString ?? "";
                                    if (stClassName == scriptClassName && stAssemblyName == scriptAssemblyName)
                                        return i;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return 0xFFFF;
        }

        /// <summary>
        /// 从 txt dump 中按行提取 fieldName = "value" 或 fieldName = value 的字符串值
        /// </summary>
        private string ExtractFieldFromDump(string dump, string fieldName)
        {
            var lines = dump.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains(fieldName) && line.Contains('='))
                {
                    int eq = line.IndexOf('=');
                    if (eq >= 0)
                    {
                        string value = line.Substring(eq + 1).Trim();
                        if (value.StartsWith("\"") && value.EndsWith("\""))
                            value = value.Substring(1, value.Length - 2);
                        return value;
                    }
                }
            }
            return null;
        }

        private bool MonoBehaviourReferencesScript(AssetContainer asset, long scriptPathId)
        {
            try
            {
                AssetTypeValueField? bf = asset.HasValueField ? asset.BaseValueField : Workspace.GetBaseField(asset);
                if (bf == null)
                    return false;

                var scriptField = bf["m_Script"];
                if (scriptField == null)
                    return false;

                long assetScriptPathId = scriptField["m_PathID"].AsLong;
                return assetScriptPathId == scriptPathId;
            }
            catch
            {
                return false;
            }
        }

        private ushort? GetMonoId(AssetContainer cont)
        {
            try
            {
                FieldInfo? fi = cont.GetType().GetField("monoId", BindingFlags.Instance | BindingFlags.NonPublic)
                             ?? cont.GetType().GetField("_monoId", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    object? val = fi.GetValue(cont);
                    if (val is ushort u)
                        return u;
                }
            }
            catch { }
            return null;
        }

        private void SetMonoId(AssetContainer cont, ushort monoId)
        {
            try
            {
                FieldInfo? fi = cont.GetType().GetField("monoId", BindingFlags.Instance | BindingFlags.NonPublic)
                             ?? cont.GetType().GetField("_monoId", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                    fi.SetValue(cont, monoId);
            }
            catch { }
        }

        #endregion
        private void InfoWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            {
                NextNameSearch();
            }
        }

        public InfoWindow(AssetsManager assetsManager, List<AssetsFileInstance> assetsFiles, bool fromBundle) : this()
        {
            Workspace = new AssetWorkspace(assetsManager, fromBundle);
            Workspace.ItemUpdated += Workspace_ItemUpdated;
            Workspace.MonoTemplateLoadFailed += Workspace_MonoTemplateLoadFailed;

            LoadAllAssetsWithDeps(assetsFiles);
            SetupContainers();
            MakeDataGridItems();
            dataGrid.ItemsSource = dataGridItems;

            dgcv = GetDataGridCollectionView(dataGrid);

            pluginManager = new PluginManager();
            pluginManager.LoadPluginsInDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"));

            searchText = "";
            searchStart = 0;
            searchDown = false;
            searchCaseSensitive = true;
            searching = false;

            filteredOutTypeIds = new HashSet<AssetClassID>();

            ChangedAssetsDatas = new List<Tuple<AssetsFileInstance, byte[]>>();
        }

        private async void MenuAdd_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AddAssetWindow win = new AddAssetWindow(Workspace);
            await win.ShowDialog(this);
        }

        private async void MenuSave_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveFile(false);
            ClearModified();
            Workspace.Modified = false;
        }

        private async void MenuSaveAs_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveFile(true);
            ClearModified();
            Workspace.Modified = false;
        }

        private async void MenuCreatePackageFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ModMakerDialog dialog = new ModMakerDialog(Workspace);
            await dialog.ShowDialog(this);
        }

        private async void MenuClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await AskForSaveAndClose();
        }

        private async void MenuSearchByName_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SearchDialog dialog = new SearchDialog();
            SearchDialogResult res = await dialog.ShowDialog<SearchDialogResult>(this);
            if (res != null && res.ok)
            {
                int selectedIndex = dataGrid.SelectedIndex;

                searchText = res.text;
                searchStart = selectedIndex != -1 ? selectedIndex : 0;
                searchDown = res.isDown;
                searchCaseSensitive = res.caseSensitive;
                searching = true;
                NextNameSearch();
            }
        }

        private void MenuContinueSearch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NextNameSearch();
        }

        private async void MenuGoToAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            GoToAssetDialog dialog = new GoToAssetDialog(Workspace);
            AssetPPtr res = await dialog.ShowDialog<AssetPPtr>(this);
            if (res != null)
            {
                AssetsFileInstance targetFile = Workspace.LoadedFiles[res.FileId];
                long targetPathId = res.PathId;

                IdSearch(targetFile, targetPathId);
            }
        }

        private async void MenuFilter_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            HashSet<AssetClassID> usedIds = Workspace.LoadedAssets.Select(a => a.Value.ClassId).Distinct().Cast<AssetClassID>().ToHashSet();
            FilterAssetTypeDialog dialog = new FilterAssetTypeDialog(filteredOutTypeIds, usedIds);
            filteredOutTypeIds = await dialog.ShowDialog<HashSet<AssetClassID>>(this);

            var filter = new Func<object, bool>(item => !filteredOutTypeIds.Contains(((AssetInfoDataGridItem)item).TypeClass));
            dgcv.Filter = null; // avalonia bug? idk, doesn't update the filter without doing this
            dgcv.Filter = filter;
        }

        private void MenuHierarchy_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            GameObjectViewWindow dialog = new GameObjectViewWindow(this, Workspace);
            dialog.Show(this);
        }

        private void MenuInfo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenAssetsFileInfoWindow(AssetsFileInfoWindowStartTab.General);
        }

        private void MenuTypeTree_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenAssetsFileInfoWindow(AssetsFileInfoWindowStartTab.TypeTree);
        }

        private void MenuDependencies_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenAssetsFileInfoWindow(AssetsFileInfoWindowStartTab.Dependencies);
        }

        private void MenuScripts_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenAssetsFileInfoWindow(AssetsFileInfoWindowStartTab.Script);
        }

        private async void BtnViewData_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            if (!await WarnIfAssetSizeLarge(gridItem))
                return;

            List<AssetContainer> selectedConts = GetSelectedAssetsReplaced();
            if (selectedConts.Count > 0)
            {
                DataWindow data = new DataWindow(this, Workspace, selectedConts[0]);
                data.Show();
            }
        }

        private async void BtnSceneView_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            AssetContainer container = gridItem.assetContainer;
            if (gridItem.TypeClass == AssetClassID.GameObject)
            {
                GameObjectViewWindow dialog = new GameObjectViewWindow(this, Workspace, container);
                dialog.Show(this);
            }
            else
            {
                bool hasGameObjectParent = false;
                if (container.HasValueField)
                {
                    hasGameObjectParent = container.BaseValueField!.Children.Any(c => c.FieldName == "m_GameObject");
                }
                else
                {
                    // fast method in case asset hasn't loaded yet
                    AssetTypeTemplateField template = Workspace.GetTemplateField(container, false, true);
                    hasGameObjectParent = template.Children.Any(c => c.Name == "m_GameObject");
                }

                if (!hasGameObjectParent)
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "Warning", "The asset you selected is not a scene asset.");

                    return;
                }

                AssetTypeValueField componentBf;
                if (container.HasValueField)
                {
                    componentBf = container.BaseValueField;
                }
                else
                {
                    try
                    {
                        componentBf = Workspace.GetBaseField(container);
                    }
                    catch
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "Error", "Asset failed to deserialize.");

                        return;
                    }
                }

                if (componentBf == null)
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "Error", "Asset failed to deserialize.");

                    return;
                }

                AssetContainer goContainer = Workspace.GetAssetContainer(
                    container.FileInstance, componentBf["m_GameObject"], true);
                GameObjectViewWindow dialog = new GameObjectViewWindow(this, Workspace, goContainer);
                dialog.Show(this);
            }
        }

        private async void BtnExportRaw_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchExportRaw(selection);
            else
                await SingleExportRaw(selection);
        }

        private async void BtnExportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchExportDump(selection);
            else
                await SingleExportDump(selection);
        }

        private async void BtnImportRaw_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchImportRaw(selection);
            else
                await SingleImportRaw(selection);
        }

        private async void BtnImportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> selection = GetSelectedAssetsReplaced();

            if (selection.Count > 1)
                await BatchImportDump(selection);
            else
                await SingleImportDump(selection);
        }

        private async void BtnEditData_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            if (!await WarnIfAssetSizeLarge(gridItem))
                return;

            AssetContainer? selection = GetSelectedAssetsReplaced()[0];
            if (selection != null && !selection.HasValueField)
            {
                selection = Workspace.GetAssetContainer(selection.FileInstance, 0, selection.PathId, false);
            }
            if (selection == null)
            {
                await MessageBoxUtil.ShowDialog(this,
                    "Error", "Asset failed to deserialize.");
                return;
            }

            await ShowEditAssetWindow(selection);
        }

        private async void BtnRemove_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            MessageBoxResult choice = await MessageBoxUtil.ShowDialog(this,
                "Removing assets", "Removing an asset referenced by other assets can cause crashes!\nAre you sure?",
                MessageBoxType.YesNo);
            if (choice == MessageBoxResult.Yes)
            {
                List<AssetContainer> selection = GetSelectedAssetsReplaced();
                foreach (AssetContainer cont in selection)
                {
                    Workspace.AddReplacer(cont.FileInstance, new AssetsRemover(cont.PathId));
                }
            }
        }

        private async void BtnPlugin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetContainer> conts = GetSelectedAssetsReplaced();
            PluginWindow plug = new PluginWindow(this, Workspace, conts, pluginManager);
            await plug.ShowDialog(this);
        }

        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var gridItem = (AssetInfoDataGridItem)dataGrid.SelectedItem;
            if (gridItem == null)
            {
                boxName.Text = string.Empty;
                boxPathId.Text = string.Empty;
                boxFileId.Text = string.Empty;
                boxType.Text = string.Empty;
            }
            else
            {
                boxName.Text = gridItem.Name;
                boxPathId.Text = gridItem.PathID.ToString();
                boxFileId.Text = gridItem.FileID.ToString();
                boxType.Text = $"0x{gridItem.TypeID:X8} ({gridItem.Type})";
            }
        }

        private async void InfoWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (Workspace == null)
                return;

            if (!Workspace.Modified || ignoreCloseEvent)
            {
                e.Cancel = false;
                ignoreCloseEvent = false;
            }
            else
            {
                e.Cancel = true;
                ignoreCloseEvent = true;

                await AskForSave();
                CloseFile();
            }
        }

        public async Task AskForSaveAndClose()
        {
            if (Workspace.Modified)
            {
                await AskForSave();
            }
            ignoreCloseEvent = true;
            CloseFile();
        }

        private async Task AskForSave()
        {
            MessageBoxResult choice = await MessageBoxUtil.ShowDialog(this,
                "Changes made", "You've modified this file. Would you like to save?",
                MessageBoxType.YesNo);
            if (choice == MessageBoxResult.Yes)
            {
                await SaveFile(false);
            }
        }

        private async Task SaveFile(bool saveAs)
        {
            var fileToReplacer = new Dictionary<AssetsFileInstance, List<AssetsReplacer>>();
            var changedFiles = Workspace.GetChangedFiles();

            foreach (var newAsset in Workspace.NewAssets)
            {
                AssetID assetId = newAsset.Key;
                AssetsReplacer replacer = newAsset.Value;
                string fileName = assetId.fileName;

                if (Workspace.LoadedFileLookup.TryGetValue(fileName.ToLower(), out AssetsFileInstance? file))
                {
                    if (!fileToReplacer.ContainsKey(file))
                        fileToReplacer[file] = new List<AssetsReplacer>();

                    fileToReplacer[file].Add(replacer);
                }
            }

            if (Workspace.fromBundle)
            {
                ChangedAssetsDatas.Clear();
                foreach (var file in changedFiles)
                {
                    List<AssetsReplacer> replacers;
                    if (fileToReplacer.ContainsKey(file))
                        replacers = fileToReplacer[file];
                    else
                        replacers = new List<AssetsReplacer>(0);

                    try
                    {
                        using (MemoryStream ms = new MemoryStream())
                        using (AssetsFileWriter w = new AssetsFileWriter(ms))
                        {
                            file.file.Write(w, 0, replacers);
                            ChangedAssetsDatas.Add(new Tuple<AssetsFileInstance, byte[]>(file, ms.ToArray()));
                        }
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "Write exception", "There was a problem while writing the file:\n" + ex.ToString());
                    }
                }

                await MessageBoxUtil.ShowDialog(this, "Success", "File saved. To complete changes, exit this window and File->Save in bundle window.");
            }
            else
            {
                List<int> changedFileIds = new List<int>();

                foreach (var file in changedFiles)
                {
                    List<AssetsReplacer> replacers;
                    if (fileToReplacer.ContainsKey(file))
                        replacers = fileToReplacer[file];
                    else
                        replacers = new List<AssetsReplacer>(0);

                    string? filePath;

                    if (saveAs)
                    {
                        while (true)
                        {
                            var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
                            {
                                Title = "Save as...",
                                SuggestedFileName = file.name
                            });

                            filePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);

                            if (filePath == null)
                                return;

                            if (Path.GetFullPath(filePath) == Path.GetFullPath(file.path))
                            {
                                await MessageBoxUtil.ShowDialog(this,
                                    "File in use", "You already have this file open. To overwrite, use Save instead of Save as.");

                                continue;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        string newName = "~" + file.name;
                        string dir = Path.GetDirectoryName(file.path)!;
                        filePath = Path.Combine(dir, newName);
                    }

                    try
                    {
                        using (FileStream fs = File.Open(filePath, FileMode.Create))
                        using (AssetsFileWriter w = new AssetsFileWriter(fs))
                        {
                            file.file.Write(w, 0, replacers);
                        }

                        if (!saveAs)
                        {
                            string origFilePath = file.path;

                            // "overwrite" the original
                            file.file.Reader.Close();
                            File.Delete(file.path);
                            File.Move(filePath, origFilePath);
                            file.file = new AssetsFile();
                            file.file.Read(new AssetsFileReader(File.OpenRead(origFilePath)));
                            file.file.GenerateQuickLookup();
                        }

                        changedFileIds.Add(Workspace.LoadedFiles.IndexOf(file));
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "Write exception", "There was a problem while writing the file:\n" + ex.ToString());
                    }
                }

                if (!saveAs)
                {
                    foreach (AssetInfoDataGridItem item in dataGridItems)
                    {
                        int fileId = item.FileID;
                        if (changedFileIds.Contains(fileId))
                        {
                            item.assetContainer.SetNewFile(Workspace.LoadedFiles[fileId]);
                        }
                    }
                }
            }
        }

        private void CloseFile()
        {
            am.UnloadAllAssetsFiles(true);
            Close();
        }

        private async Task<bool> WarnIfAssetSizeLarge(AssetInfoDataGridItem gridItem)
        {
            if (gridItem.Size > 500000)
            {
                var result = await MessageBoxUtil.ShowDialogCustom(this,
                    "Warning", "The asset you are about to open is very big and may take a lot of time and memory.",
                    "Continue anyway", "Cancel");

                if (result == "Cancel")
                    return false;
            }

            return true;
        }

        private async void OpenAssetsFileInfoWindow(AssetsFileInfoWindowStartTab startTab)
        {
            AssetsFileInfoWindow dialog = new AssetsFileInfoWindow(Workspace, startTab);
            var changedFiles = await dialog.ShowDialog<Dictionary<AssetsFileInstance, AssetsFileChangeTypes>>(this);
            if (changedFiles != null && changedFiles.Count > 0)
            {
                Workspace.Modified = true;
                foreach ((AssetsFileInstance changedFile, AssetsFileChangeTypes newFlags) in changedFiles)
                {
                    Workspace.SetOtherAssetChangeFlag(changedFile, newFlags);
                }
            }
        }

        private async Task BatchExportRaw(List<AssetContainer> selection)
        {
            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Select export directory"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;

            string dir = selectedFolderPaths[0];

            foreach (AssetContainer selectedCont in selection)
            {
                AssetsFileInstance selectedInst = selectedCont.FileInstance;

                AssetNameUtils.GetDisplayNameFast(Workspace, selectedCont, false, out string assetName, out string _);
                assetName = PathUtils.ReplaceInvalidPathChars(assetName);
                string file = Path.Combine(dir, $"{assetName}-{Path.GetFileName(selectedInst.path)}-{selectedCont.PathId}.dat");

                using (FileStream fs = File.Open(file, FileMode.Create))
                {
                    AssetImportExport dumper = new AssetImportExport();
                    dumper.DumpRawAsset(fs, selectedCont.FileReader, selectedCont.FilePosition, selectedCont.Size);
                }
            }
        }

        private async Task SingleExportRaw(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            AssetNameUtils.GetDisplayNameFast(Workspace, selectedCont, false, out string assetName, out string _);
            assetName = PathUtils.ReplaceInvalidPathChars(assetName);

            var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save as...",
                FileTypeChoices = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("Raw Unity Asset (*.dat)") { Patterns = new List<string>() { "*.dat" } },
                    new FilePickerFileType("All types (*.*)") { Patterns = new List<string>() { "*" } }
                },
                DefaultExtension = "dat",
                SuggestedFileName = $"{assetName}-{Path.GetFileName(selectedInst.path)}-{selectedCont.PathId}"
            });

            string? selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
            if (selectedFilePath == null)
                return;

            using (FileStream fs = File.Open(selectedFilePath, FileMode.Create))
            {
                AssetImportExport dumper = new AssetImportExport();
                dumper.DumpRawAsset(fs, selectedCont.FileReader, selectedCont.FilePosition, selectedCont.Size);
            }
        }

        private async Task BatchExportDump(List<AssetContainer> selection)
        {
            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Select export directory"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;

            string dir = selectedFolderPaths[0];

            SelectDumpWindow selectDumpWindow = new SelectDumpWindow(true);
            string? extension = await selectDumpWindow.ShowDialog<string?>(this);

            if (extension == null)
                return;

            foreach (AssetContainer selectedCont in selection)
            {
                AssetNameUtils.GetDisplayNameFast(Workspace, selectedCont, false, out string assetName, out string _);
                assetName = PathUtils.ReplaceInvalidPathChars(assetName);
                string file = Path.Combine(dir, $"{assetName}-{Path.GetFileName(selectedCont.FileInstance.path)}-{selectedCont.PathId}.{extension}");

                using (FileStream fs = File.Open(file, FileMode.Create))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    AssetTypeValueField? baseField = Workspace.GetBaseField(selectedCont);

                    if (baseField == null)
                    {
                        sw.WriteLine("Asset failed to deserialize.");
                        continue;
                    }

                    AssetImportExport dumper = new AssetImportExport();
                    if (extension == "json")
                        dumper.DumpJsonAsset(sw, baseField);
                    else //if (extension == "txt")
                        dumper.DumpTextAsset(sw, baseField);
                }
            }
        }

        private async Task SingleExportDump(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            AssetNameUtils.GetDisplayNameFast(Workspace, selectedCont, false, out string assetName, out string _);
            assetName = PathUtils.ReplaceInvalidPathChars(assetName);

            var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save as...",
                FileTypeChoices = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("UABE text dump (*.txt)") { Patterns = new List<string>() { "*.txt" } },
                    new FilePickerFileType("UABEA json dump (*.json)") { Patterns = new List<string>() { "*.json" } }
                },
                DefaultExtension = "txt",
                SuggestedFileName = $"{assetName}-{Path.GetFileName(selectedInst.path)}-{selectedCont.PathId}"
            });

            string? selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
            if (selectedFilePath == null)
                return;

            using (FileStream fs = File.Open(selectedFilePath, FileMode.Create))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                AssetTypeValueField? baseField = Workspace.GetBaseField(selectedCont);

                if (baseField == null)
                {
                    sw.WriteLine("Asset failed to deserialize.");
                    return;
                }

                AssetImportExport dumper = new AssetImportExport();

                if (selectedFilePath.EndsWith(".json"))
                    dumper.DumpJsonAsset(sw, baseField);
                else //if (extension == "txt")
                    dumper.DumpTextAsset(sw, baseField);
            }
        }

        private async Task BatchImportRaw(List<AssetContainer> selection)
        {
            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Select import directory"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;

            string dir = selectedFolderPaths[0];

            List<string> extensions = new List<string>() { "dat" };

            ImportBatch dialog = new ImportBatch(Workspace, selection, dir, extensions);
            List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(this);
            if (batchInfos != null)
            {
                foreach (ImportBatchInfo batchInfo in batchInfos)
                {
                    string selectedFilePath = batchInfo.importFile;
                    AssetContainer selectedCont = batchInfo.cont;
                    AssetsFileInstance selectedInst = selectedCont.FileInstance;

                    using (FileStream fs = File.OpenRead(selectedFilePath))
                    {
                        AssetImportExport importer = new AssetImportExport();
                        byte[] bytes = importer.ImportRawAsset(fs);

                        AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                        Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
                    }
                }
            }
        }

        private async Task SingleImportRaw(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open",
                FileTypeFilter = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("Raw Unity Asset") { Patterns = new List<string>() { "*.dat" } }
                }
            });

            string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
            if (selectedFilePaths.Length == 0)
                return;

            string file = selectedFilePaths[0];

            using (FileStream fs = File.OpenRead(file))
            {
                AssetImportExport importer = new AssetImportExport();
                byte[] bytes = importer.ImportRawAsset(fs);

                AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
            }
        }

        private async Task BatchImportDump(List<AssetContainer> selection)
        {
            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Select import directory"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;

            string dir = selectedFolderPaths[0];

            SelectDumpWindow selectDumpWindow = new SelectDumpWindow(false);
            string? extension = await selectDumpWindow.ShowDialog<string?>(this);

            if (extension == null)
                return;

            List<string> extensions;
            if (extension == "any")
                extensions = SelectDumpWindow.ALL_EXTENSIONS;
            else
                extensions = new List<string>() { extension };

            ImportBatch dialog = new ImportBatch(Workspace, selection, dir, extensions);
            List<ImportBatchInfo> batchInfos = await dialog.ShowDialog<List<ImportBatchInfo>>(this);
            if (batchInfos != null)
            {
                foreach (ImportBatchInfo batchInfo in batchInfos)
                {
                    string selectedFilePath = batchInfo.importFile;
                    AssetContainer selectedCont = batchInfo.cont;
                    AssetsFileInstance selectedInst = selectedCont.FileInstance;

                    using (FileStream fs = File.OpenRead(selectedFilePath))
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        AssetImportExport importer = new AssetImportExport();

                        byte[]? bytes;
                        string? exceptionMessage;

                        if (selectedFilePath.EndsWith(".json"))
                        {
                            AssetTypeTemplateField tempField = Workspace.GetTemplateField(selectedCont);
                            bytes = importer.ImportJsonAsset(tempField, sr, out exceptionMessage);
                        }
                        else
                        {
                            bytes = importer.ImportTextAsset(sr, out exceptionMessage);
                        }

                        if (bytes == null)
                        {
                            await MessageBoxUtil.ShowDialog(this, "Parse error", "Something went wrong when reading the dump file:\n" + exceptionMessage);
                            return;
                        }

                        AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                        Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
                    }
                }
            }
        }

        private async Task SingleImportDump(List<AssetContainer> selection)
        {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open",
                FileTypeFilter = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("UABE text dump") { Patterns = new List<string>() { "*.txt" } },
                    new FilePickerFileType("UABEA json dump") { Patterns = new List<string>() { "*.json" } }
                }
            });

            string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
            if (selectedFilePaths.Length == 0)
                return;

            string file = selectedFilePaths[0];

            using (FileStream fs = File.OpenRead(file))
            using (StreamReader sr = new StreamReader(fs))
            {
                AssetImportExport importer = new AssetImportExport();

                byte[]? bytes = null;
                string? exceptionMessage = null;
                if (file.EndsWith(".json"))
                {
                    AssetTypeTemplateField tempField = Workspace.GetTemplateField(selectedCont);
                    bytes = importer.ImportJsonAsset(tempField, sr, out exceptionMessage);
                }
                else
                {
                    bytes = importer.ImportTextAsset(sr, out exceptionMessage);
                }

                if (bytes == null)
                {
                    await MessageBoxUtil.ShowDialog(this, "Parse error", "Something went wrong when reading the dump file:\n" + exceptionMessage);
                    return;
                }

                AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
            }
        }

        public async Task<bool> ShowEditAssetWindow(AssetContainer cont)
        {
            AssetTypeValueField baseField = cont.BaseValueField;
            if (baseField == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "Something went wrong deserializing this asset.");
                return false;
            }

            EditDataWindow editWin = new EditDataWindow(baseField);
            byte[]? data = await editWin.ShowDialog<byte[]?>(this);
            if (data == null)
            {
                return false;
            }

            AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(cont, data);
            Workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(data));
            return true;
        }

        private async void NextNameSearch()
        {
            bool foundResult = false;
            if (searching)
            {
                List<AssetInfoDataGridItem> itemList = GetDataGridItemsSorted(dgcv);
                if (searchDown)
                {
                    for (int i = searchStart; i < itemList.Count; i++)
                    {
                        string name = itemList[i].Name;
                        if (SearchUtils.WildcardMatches(name, searchText, searchCaseSensitive))
                        {
                            dataGrid.SelectedIndex = i;
                            dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
                            searchStart = i + 1;
                            foundResult = true;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = searchStart; i >= 0; i--)
                    {
                        string name = itemList[i].Name;
                        if (SearchUtils.WildcardMatches(name, searchText, searchCaseSensitive))
                        {
                            dataGrid.SelectedIndex = i;
                            dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
                            searchStart = i - 1;
                            foundResult = true;
                            break;
                        }
                    }
                }
            }

            if (!foundResult)
            {
                await MessageBoxUtil.ShowDialog(this, "Search end", "Can't find any assets that match.");

                searchText = "";
                searchStart = 0;
                searchDown = false;
                searching = false;
                return;
            }
        }

        private async void IdSearch(AssetsFileInstance targetFile, long targetPathId)
        {
            if (!SelectAsset(targetFile, targetPathId))
            {
                await MessageBoxUtil.ShowDialog(this, "Search end", "Can't find any assets that match.");
                return;
            }
        }

        public bool SelectAsset(AssetsFileInstance targetFile, long targetPathId)
        {
            bool foundResult = false;

            List<AssetInfoDataGridItem> itemList = GetDataGridItemsSorted(dgcv);
            for (int i = 0; i < itemList.Count; i++)
            {
                AssetContainer cont = itemList[i].assetContainer;
                if (cont.FileInstance == targetFile && cont.PathId == targetPathId)
                {
                    dataGrid.SelectedIndex = i;
                    dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
                    foundResult = true;
                    break;
                }
            }

            return foundResult;
        }

        private void SetupContainers()
        {
            if (Workspace.LoadedFiles.Count == 0)
            {
                return;
            }

            UnityContainer ucont = new UnityContainer();
            foreach (AssetsFileInstance file in Workspace.LoadedFiles)
            {
                AssetsFileInstance? actualFile;
                AssetTypeValueField? ucontBaseField;
                if (UnityContainer.TryGetBundleContainerBaseField(Workspace, file, out actualFile, out ucontBaseField))
                {
                    ucont.FromAssetBundle(am, actualFile, ucontBaseField);
                }
                else if (UnityContainer.TryGetRsrcManContainerBaseField(Workspace, file, out actualFile, out ucontBaseField))
                {
                    ucont.FromResourceManager(am, actualFile, ucontBaseField);
                }
            }

            foreach (var asset in Workspace.LoadedAssets)
            {
                AssetPPtr pptr = new AssetPPtr(asset.Key.fileName, 0, asset.Key.pathID);
                string? path = ucont.GetContainerPath(pptr);
                if (path != null)
                {
                    asset.Value.Container = path;
                }
            }
        }

        private ObservableCollection<AssetInfoDataGridItem> MakeDataGridItems()
        {
            dataGridItems = new ObservableCollection<AssetInfoDataGridItem>();

            Workspace.GenerateAssetsFileLookup();

            foreach (AssetContainer cont in Workspace.LoadedAssets.Values)
            {
                AddDataGridItem(cont);
            }
            return dataGridItems;
        }

        private AssetInfoDataGridItem AddDataGridItem(AssetContainer cont, bool isNewAsset = false)
        {
            AssetsFileInstance thisFileInst = cont.FileInstance;

            string name;
            string container;
            string type;
            int fileId;
            long pathId;
            int size;
            string modified;

            container = cont.Container;
            fileId = Workspace.LoadedFiles.IndexOf(thisFileInst);
            pathId = cont.PathId;
            size = (int)cont.Size;
            modified = "";

            AssetNameUtils.GetDisplayNameFast(Workspace, cont, true, out name, out type);

            if (name.Length > 100)
                name = name[..100];
            if (type.Length > 100)
                type = type[..100];

            var item = new AssetInfoDataGridItem
            {
                TypeClass = (AssetClassID)cont.ClassId,
                Name = name,
                Container = container,
                Type = type,
                TypeID = cont.ClassId,
                FileID = fileId,
                PathID = pathId,
                Size = size,
                Modified = modified,
                assetContainer = cont
            };

            if (!isNewAsset)
                dataGridItems.Add(item);
            else
                dataGridItems.Insert(0, item);
            return item;
        }

        private void LoadAllAssetsWithDeps(List<AssetsFileInstance> files)
        {
            foreach (AssetsFileInstance file in files)
            {
                Workspace.LoadAssetsFile(file, true);
            }
        }

        private async Task<bool> FailIfNothingSelected()
        {
            if (dataGrid.SelectedItem == null)
            {
                await MessageBoxUtil.ShowDialog(this, "Note", "No item selected.");
                return true;
            }
            return false;
        }

        private AssetInfoDataGridItem GetSelectedGridItem()
        {
            return (AssetInfoDataGridItem)dataGrid.SelectedItem;
        }

        private List<AssetInfoDataGridItem> GetSelectedGridItems()
        {
            return dataGrid.SelectedItems.Cast<AssetInfoDataGridItem>().ToList();
        }

        private List<AssetContainer> GetSelectedAssetsReplaced()
        {
            List<AssetInfoDataGridItem> gridItems = GetSelectedGridItems();
            List<AssetContainer> exts = new List<AssetContainer>();
            foreach (var gridItem in gridItems)
            {
                exts.Add(gridItem.assetContainer);
            }
            return exts;
        }

        private void SetFieldModified(AssetInfoDataGridItem gridItem)
        {
            gridItem.Modified = "*";
            gridItem.Update();
        }

        private void ClearModified()
        {
            foreach (AssetInfoDataGridItem gridItem in dataGrid.ItemsSource)
            {
                if (gridItem.Modified != "")
                {
                    gridItem.Modified = "";
                    gridItem.Update();
                }
            }
        }

        private void Workspace_ItemUpdated(AssetsFileInstance file, AssetID assetId)
        {
            int fileId = Workspace.LoadedFiles.IndexOf(file);
            long pathId = assetId.pathID;

            var gridItem = dataGridItems.FirstOrDefault(i => i.FileID == fileId && i.PathID == pathId);

            if (Workspace.LoadedAssets.ContainsKey(assetId))
            {
                //added/modified entry
                if (file != null)
                {
                    AssetContainer? cont = Workspace.GetAssetContainer(file, 0, assetId.pathID);
                    if (cont != null)
                    {
                        if (gridItem != null)
                        {
                            gridItem.assetContainer = cont;
                            SetFieldModified(gridItem);
                        }
                        else
                        {
                            gridItem = AddDataGridItem(cont, true);
                            gridItem.Modified = "*";
                        }
                    }
                }
            }
            else
            {
                //removed entry
                if (gridItem != null)
                {
                    dataGridItems.Remove(gridItem);
                }
            }
        }

        private async void Workspace_MonoTemplateLoadFailed(string path)
        {
            await MessageBoxUtil.ShowDialog(
                this, "Error",
                "MonoBehaviour template info failed to load.\n" +
                "MonoBehaviour assets will not be fully deserialized.\n" +
                $"Searched in {path}");
        }

        // TEMPORARY DATAGRID HACKS
        private DataGridCollectionView GetDataGridCollectionView(DataGrid dg)
        {
            object dgdc = typeof(DataGrid)
                .GetProperty("DataConnection", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(dg);

            object dgds = dgdc.GetType()
                .GetProperty("DataSource", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(dgdc);

            if (dgds is DataGridCollectionView dgcv)
            {
                return dgcv;
            }
            return null;
        }

        private List<AssetInfoDataGridItem> GetDataGridItemsSorted(DataGridCollectionView dgcv)
        {
            int itemCount = dgcv.ItemCount;
            List<AssetInfoDataGridItem> items = new List<AssetInfoDataGridItem>();
            for (int i = 0; i < itemCount; i++)
            {
                items.Add(dgcv.GetItemAt(i) as AssetInfoDataGridItem);
            }
            return items;
        }
        // END TEMPORARY DATAGRID HACKS
    }

    public class AssetInfoDataGridItem : INotifyPropertyChanged
    {
        public AssetClassID TypeClass { get; set; }
        public string Name { get; set; }
        public string Container { get; set; }
        public string Type { get; set; }
        public int TypeID { get; set; }
        public int FileID { get; set; }
        public long PathID { get; set; }
        public int Size { get; set; }
        public string Modified { get; set; }

        public AssetContainer assetContainer;

        public event PropertyChangedEventHandler? PropertyChanged;

        //ultimate lazy
        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
