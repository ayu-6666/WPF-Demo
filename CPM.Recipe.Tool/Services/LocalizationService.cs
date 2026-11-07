using System;
using System.Collections.Generic;
using System.Globalization;

namespace CPM.Recipe.Tool.Services
{
    /// <summary>
    /// 最小可用多语言方案：键值字典 + 运行期按语言取文案。
    /// 新增文案时在 Map 中补一条（zh / en），界面代码统一用 L.T(key)（支持 string.Format 参数）。
    /// </summary>
    public static class L
    {
        public const string Zh = "zh-CN";
        public const string En = "en-US";

        private static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // 标题 / 页脚
            ["app.title"] = new[] { "CPM Recipe 迁移工具", "CPM Recipe Migration Tool" },
            ["app.windowTitle"] = new[] { "CMP Recipe 迁移工具", "CMP Recipe Migration Tool" },
            ["footer.left"] = new[] { "© 2025 CPM.Co  |  Recipe 迁移工具", "© 2025 CPM.Co  |  Recipe Migration Tool" },
            ["footer.right"] = new[] { "Version 1.0.0  |  Powered by WPF + Newtonsoft.Json", "Version 1.0.0  |  Powered by WPF + Newtonsoft.Json" },

            // 标题栏按钮
            ["tip.minimize"] = new[] { "最小化", "Minimize" },
            ["tip.maximize"] = new[] { "最大化 / 还原", "Maximize / Restore" },
            ["tip.close"] = new[] { "关闭", "Close" },
            ["tip.theme.dark"] = new[] { "切换到浅色主题", "Switch to light theme" },
            ["tip.theme.light"] = new[] { "切换到深色主题", "Switch to dark theme" },
            ["tip.help"] = new[] { "打开帮助文档 (PDF)", "Open help (PDF)" },
            ["tip.language"] = new[] { "切换界面语言", "Switch UI language" },
            ["tip.refresh"] = new[] { "刷新文件列表", "Refresh file list" },

            // 工具栏
            ["label.path"] = new[] { "📁 文件路径", "📁 File path" },
            ["btn.browse"] = new[] { "选择目录", "Select folder" },
            ["label.output"] = new[] { "输出目录", "Output folder" },
            ["btn.openOutput"] = new[] { "打开输出", "Open output" },
            ["btn.openOutputTip"] = new[] { "打开输出目录（不存在时自动创建）", "Open the output folder (created if missing)" },
            ["btn.browseOutput"] = new[] { "选择目录", "Select folder" },
            ["btn.browseOutputTip"] = new[] { "选择迁移输出目录（记住上次选择，可新建目录）", "Choose the migration output folder (remembered, can create new)" },
            ["btn.edit"] = new[] { "✏ 修改", "✏ Edit" },
            ["btn.editTip"] = new[] { "修改数据列表当前选中行（未选中时不可用）", "Edit the selected data row (disabled when nothing is selected)" },
            ["btn.saveAll"] = new[] { "💾 全部保存", "💾 Save all" },
            ["btn.saveAllTip"] = new[] { "把未保存的修改按 ID 写回当前 Recipe 类型目录的 JSON 文件", "Write pending edits back by Id to JSON files in the current recipe type folder" },
            ["btn.migrateOutput"] = new[] { "📦 迁移并导出", "📦 Migrate & export" },
            ["btn.migrateOutputTip"] = new[] { "重排 Id 预览 → 迁移到输出目录 → 打开输出目录", "Preview Id resequence → migrate to output → open output" },
            ["label.recipeType"] = new[] { "Recipe 类型", "Recipe type" },
            ["label.recipeFile"] = new[] { "Recipe 文件", "Recipe file" },
            ["combo.allTypes"] = new[] { "(全部)", "(All)" },
            ["label.preview"] = new[] { "📄 JSON 数据预览", "📄 JSON preview" },

            // Tab
            ["tab.data"] = new[] { "📋 数据列表", "📋 Data list" },
            ["tab.raw"] = new[] { "📜 原文", "📜 Raw text" },
            ["tab.files"] = new[] { "📁 文件列表", "📁 Files" },

            // 数据列表 Tab
            ["data.rows"] = new[] { "共 {0} 条数据", "{0} rows" },
            ["data.rowsWithCollection"] = new[] { "共 {0} 条数据 · 集合: {1}", "{0} rows · collection: {1}" },
            ["data.empty"] = new[] { "当前文件没有可显示的数据集合（未找到 Data 数组 / 对象数组）", "No data collection found (no Data array / object array)" },
            ["data.emptyCollection"] = new[] { "数据集合为空", "Data collection is empty" },
            ["data.doubleClickHint"] = new[] { "双击行或右键 → 修改", "Double-click a row or right-click → Edit" },
            ["data.dirty"] = new[] { "● {0} 个对象有未保存修改", "● {0} unsaved object(s)" },
            ["btn.discard"] = new[] { "放弃未保存修改", "Discard changes" },
            ["btn.discardTip"] = new[] { "从磁盘重新加载当前文件", "Reload current file from disk" },
            ["btn.save"] = new[] { "💾 保存修改（写入当前类型）", "💾 Save changes (current type)" },
            ["btn.add"] = new[] { "➕ 新增", "➕ Add" },
            ["btn.addTip"] = new[] { "新增一条数据对象（可指定插入索引）", "Add a data object (optional insert index)" },
            ["btn.delete"] = new[] { "🗑 删除", "🗑 Delete" },
            ["btn.deleteTip"] = new[] { "按 ID 从当前 Recipe 类型目录的 JSON 文件中删除该对象", "Delete objects by Id from JSON files in the current recipe type folder" },

            // 右键菜单
            ["menu.edit"] = new[] { "修改…", "Edit…" },
            ["menu.copyJson"] = new[] { "复制为 JSON", "Copy as JSON" },
            ["menu.copyId"] = new[] { "复制 ID", "Copy ID" },

            // 原文 Tab
            ["raw.hint"] = new[] { "· 只读，可选中后 Ctrl+C 复制", "· Read-only; select and Ctrl+C to copy" },
            ["raw.noFile"] = new[] { "无文件", "No file" },
            ["raw.info"] = new[] { "{0} · {1} · {2} 字符", "{0} · {1} · {2} chars" },

            // 文件列表 Tab
            ["files.count"] = new[] { "共 {0} 个文件", "{0} files" },
            ["files.hint"] = new[] { "· 仅当前 Recipe 类型目录 · 双击行加载到『数据列表 / 原文』", "· Current recipe type folder only · Double-click to load" },
            ["files.empty"] = new[] { "当前 Recipe 类型目录下没有 JSON 文件", "No JSON files in the current recipe type folder" },
            ["col.fileName"] = new[] { "文件名", "File name" },
            ["col.size"] = new[] { "大小", "Size" },
            ["col.modified"] = new[] { "修改时间", "Modified" },
            ["col.relativePath"] = new[] { "相对路径", "Relative path" },

            // 日志
            ["log.title"] = new[] { "📜 迁移变更日志", "📜 Migration log" },
            ["btn.clear"] = new[] { "清空", "Clear" },

            // 状态栏
            ["status.ready"] = new[] { "就绪", "Ready" },
            ["status.files"] = new[] { "共 {0} 个文件", "{0} files" },
            ["status.fileHint"] = new[] { "共 {0} 个 JSON", "{0} JSON files" },
            ["status.dirMissing"] = new[] { "目录不存在", "Folder not found" },
            ["status.typeNoFiles"] = new[] { "Recipe 类型 {0}：无文件", "Recipe type {0}: no files" },
            ["status.refreshed"] = new[] { "文件列表已刷新（{0} 个）", "File list refreshed ({0})" },
            ["status.loaded"] = new[] { "已加载 {0}  ({1} 条数据)", "Loaded {0} ({1} rows)" },
            ["status.modified"] = new[] { "已修改 ID={0}（仅内存），请点击『保存修改』写入文件", "Modified ID={0} (in memory); click 'Save changes' to write" },
            ["status.saved"] = new[] { "已更新 {0} 个文件（共 {1} 条）", "Updated {0} file(s), {1} object(s)" },
            ["status.inserted"] = new[] { "已插入 1 条（索引 {0}，共 {1} 条）", "Inserted 1 object (index {0}, {1} total)" },
            ["status.deleted"] = new[] { "已删除 ID={0}：{1} 个文件，共 {2} 处", "Deleted ID={0}: {1} file(s), {2} occurrence(s)" },
            ["status.copiedJson"] = new[] { "已复制选中对象 JSON 到剪贴板", "Selected object JSON copied to clipboard" },
            ["status.copiedId"] = new[] { "已复制 ID={0}", "Copied ID={0}" },
            ["status.copiedPath"] = new[] { "已复制输出目录路径", "Output folder path copied" },
            ["status.helpOpened"] = new[] { "已用系统默认查看器打开帮助文档", "Help opened with the default viewer" },
            ["status.themeLight"] = new[] { "已切换到浅色主题", "Switched to light theme" },
            ["status.themeDark"] = new[] { "已切换到深色主题", "Switched to dark theme" },
            ["status.outputOpened"] = new[] { "已打开输出目录：{0}", "Opened output folder: {0}" },
            ["status.outputChanged"] = new[] { "输出目录已切换到：{0}", "Output folder changed to: {0}" },
            ["status.resequenced"] = new[] { "Id 重排：{0} 个文件", "Id resequenced: {0} file(s)" },

            // 输出目录校验
            ["dlg.outputFolder"] = new[] { "选择迁移输出目录", "Select migration output folder" },
            ["msg.title.confirmCreate"] = new[] { "创建输出目录", "Create output folder" },
            ["msg.outputEmpty"] = new[] { "输出目录不能为空。", "Output folder cannot be empty." },
            ["msg.outputInvalid"] = new[] { "输出目录路径无效：{0}", "Invalid output folder path: {0}" },
            ["msg.outputIsFile"] = new[] { "该路径指向一个文件，不能作为输出目录：\n{0}", "The path points to a file and cannot be used as the output folder:\n{0}" },
            ["msg.outputMissing"] = new[] { "输出目录不存在：\n{0}", "Output folder does not exist:\n{0}" },
            ["msg.outputCreateConfirm"] = new[] { "输出目录不存在，是否创建后使用？\n{0}", "The output folder does not exist. Create it now?\n{0}" },
            ["msg.outputCreateFailed"] = new[] { "创建输出目录失败：\n{0}\n{1}", "Failed to create output folder:\n{0}\n{1}" },
            ["msg.outputNoAccess"] = new[] { "没有写入权限，无法作为输出目录：\n{0}\n{1}", "No write permission for the output folder:\n{0}\n{1}" },

            // 通用提示
            ["msg.title.info"] = new[] { "提示", "Notice" },
            ["msg.title.warning"] = new[] { "提示", "Warning" },
            ["msg.title.error"] = new[] { "错误", "Error" },
            ["msg.title.confirm"] = new[] { "确认", "Confirm" },
            ["btn.ok"] = new[] { "确定", "OK" },
            ["btn.yes"] = new[] { "是", "Yes" },
            ["btn.no"] = new[] { "否", "No" },
            ["btn.cancel"] = new[] { "取消", "Cancel" },

            // 作用域（新增 / 修改 / 删除仅作用于当前 Recipe 类型目录）
            ["scope.label"] = new[] { "当前类型", "current type" },
            ["scope.root"] = new[] { "(根目录)", "(root folder)" },
            ["scope.line"] = new[] { "作用范围（仅当前类型）：{0}（{1} 个文件）", "Scope (current type only): {0} ({1} file(s))" },
            ["scope.short"] = new[] { "{0}（{1} 个文件）", "{0} ({1} file(s))" },
            ["status.savedScope"] = new[] { "✅ 已更新 {0} 个文件，共替换 {1} 条对象（仅当前类型：{2}）", "✅ Updated {0} file(s), {1} object(s) replaced (current type only: {2})" },
            ["msg.saveNoScope"] = new[] { "当前 Recipe 类型目录下没有可写回的 JSON 文件：{0}", "No JSON files to write back in the current recipe type folder: {0}" },
            ["msg.scopeTypeRequired"] = new[] { "当前没有选中『文件列表』中的 Recipe 类型文件。\n请先在文件列表中选择一个具体类型下的文件；新增/删除/修改仅允许操作文件列表中的条目。", "No recipe-type file from the file list is selected.\nSelect a file under a specific recipe type first; add/delete/edit only work on entries in the file list." },
            ["msg.scopeEmpty"] = new[] { "当前 Recipe 类型目录（{0}）下没有 JSON 文件，无法执行该操作。", "The current recipe type folder ({0}) has no JSON files; this operation is unavailable." },
            ["msg.title.unsaved"] = new[] { "未保存的修改", "Unsaved changes" },
            ["msg.title.loadFailed"] = new[] { "加载失败", "Load failed" },
            ["msg.title.saveFailed"] = new[] { "保存失败", "Save failed" },
            ["msg.title.added"] = new[] { "插入完成", "Inserted" },
            ["msg.title.deleted"] = new[] { "删除完成", "Deleted" },
            ["msg.title.helpMissing"] = new[] { "帮助文档缺失", "Help file missing" },
            ["msg.title.copyFailed"] = new[] { "复制失败", "Copy failed" },
            ["msg.noFile"] = new[] { "请先选择一个 JSON 文件", "Please select a JSON file first" },
            ["msg.noDataArray"] = new[] { "当前文件没有可操作的数据集合", "Current file has no editable data collection" },
            ["msg.selectRow"] = new[] { "请先在『数据列表』中选中一行（可双击或右键）。", "Select a row in the Data list first (double-click or right-click)." },
            ["msg.noId"] = new[] { "该对象没有可用的整数 Id，无法按 ID 跨文件替换。", "This object has no integer Id; cross-file replacement is unavailable." },
            ["msg.pendingUnsaved"] = new[]
            {
                "当前文件有 {0} 个对象尚未保存。\n\n是：先保存到所有相关文件，再继续\n否：放弃这些修改，再继续\n取消：留在当前文件",
                "{0} object(s) in the current file are not saved.\n\nYes: save to all related files, then continue\nNo: discard these changes, then continue\nCancel: stay in the current file"
            },
            ["msg.closingUnsaved"] = new[] { "还有 {0} 个对象未保存，是否保存后退出？", "{0} object(s) are unsaved. Save before exit?" },
            ["msg.migratePending"] = new[] { "当前有 {0} 个对象未保存，请先点击『保存修改』或『放弃未保存修改』，再执行迁移操作。", "{0} unsaved object(s). Please save or discard before migrating." },
            ["msg.discardNone"] = new[] { "当前没有未保存的修改。", "No unsaved changes." },
            ["msg.discardConfirm"] = new[] { "放弃当前所有未保存的修改，并从磁盘重新加载当前文件？", "Discard all unsaved changes and reload the current file from disk?" },
            ["msg.saveNone"] = new[] { "没有待保存的修改。", "Nothing to save." },
            ["msg.saveConfirm"] = new[]
            {
                "将按 ID 把以下对象写回当前 Recipe 类型目录下的 JSON 文件：\nID: {0}\n\n作用范围（仅当前类型）: {1}\n数据集合: {2}\n写回前自动备份到 {3} 目录，并用临时文件原子替换。\n\n是否继续？",
                "The following objects will be written back by Id to JSON files in the current recipe type folder:\nId: {0}\n\nScope (current type only): {1}\nCollection: {2}\nBackups go to '{3}' and files are replaced atomically.\n\nContinue?"
            },
            ["msg.saveHead"] = new[] { "已更新 {0} 个文件，共替换 {1} 条对象。", "Updated {0} file(s), replaced {1} object(s)." },
            ["msg.savePartial"] = new[] { "已更新 {0} 个文件，{1} 个文件写回失败：\n{2}\n\n未保存的修改已保留，可修正后重试。", "Updated {0} file(s); {1} failed:\n{2}\n\nUnsaved changes were kept; fix and retry." },
            ["msg.addInvalidIndex"] = new[] { "插入索引超出范围：当前共 {0} 条，合法范围 -1 或 0..{0}。", "Insert index out of range: {0} rows; valid range is -1 or 0..{0}." },
            ["msg.addDuplicate"] = new[]
            {
                "ID={0} 已存在于当前文件。\n继续插入会产生重复 ID（后续按 ID 修改/删除会同时影响这些对象）。\n\n仍要插入吗？",
                "Id={0} already exists in the current file.\nInserting creates a duplicate Id (future edit/delete by Id will affect all of them).\n\nInsert anyway?"
            },
            ["msg.addDone"] = new[] { "已在索引 {0} 插入 1 条数据（共 {1} 条）。\n备份: {2}", "Inserted 1 object at index {0} ({1} total).\nBackup: {2}" },
            ["msg.addNoId"] = new[] { "新增对象必须填写整数 ID。", "A new object must have an integer Id." },
            ["msg.deleteConfirm"] = new[]
            {
                "将扫描并删除当前 Recipe 类型目录（{2}）下的 {0} 个 JSON 文件中 ID={1} 的对象（含子目录与重复出现），写回前自动备份。\n\n是否继续？",
                "Delete objects with Id={1} from {0} JSON file(s) in the current recipe type folder ({2}), including sub-folders and duplicates. Backups are created automatically.\n\nContinue?"
            },
            ["msg.deleteDone"] = new[] { "已删除 ID={0}：{1} 个文件，共 {2} 处对象。", "Deleted Id={0}: {1} file(s), {2} occurrence(s)." },
            ["msg.deleteNotFound"] = new[] { "未找到 ID={0}：当前 Recipe 类型目录（{1}）下没有任何 JSON 文件包含该对象。", "Id={0} not found in any JSON file in the current recipe type folder ({1})." },
            ["msg.deletePartial"] = new[] { "已删除 {0} 处，{1} 个文件写入失败：\n{2}\n\n详见日志。", "Deleted {0} occurrence(s); {1} file(s) failed:\n{2}\n\nSee the log for details." },
            ["msg.deleteInvalidId"] = new[] { "请输入有效的整数 ID。", "Please enter a valid integer Id." },
            ["msg.helpMissing"] = new[] { "未找到帮助文档：\n{0}\n\n请确认 Help 目录随程序一起发布。", "Help file not found:\n{0}\n\nMake sure the Help folder is deployed with the application." },
            ["msg.openHelpFailed"] = new[] { "打开帮助文档失败：\n{0}", "Failed to open the help file:\n{0}" },
            ["msg.dirMissing"] = new[] { "目录不存在:\n{0}", "Folder not found:\n{0}" },
            ["msg.copyFailed"] = new[] { "复制失败：{0}", "Copy failed: {0}" },
            ["msg.loadFailed"] = new[] { "加载失败：\n{0}", "Load failed:\n{0}" },
            ["msg.title.saveDone"] = new[] { "保存完成", "Saved" },
            ["msg.title.confirmWrite"] = new[] { "确认写回", "Confirm write-back" },
            ["msg.title.partial"] = new[] { "部分文件写入失败", "Some files failed" },
            ["msg.title.noContent"] = new[] { "没有可保存的内容", "Nothing to save" },
            ["msg.title.migrateFailed"] = new[] { "迁移失败", "Migration failed" },
            ["msg.title.batch"] = new[] { "批量迁移", "Batch migration" },
            ["msg.title.batchDone"] = new[] { "批量迁移完成", "Batch migration completed" },
            ["msg.title.batchFailed"] = new[] { "批量迁移异常", "Batch migration error" },
            ["msg.title.discard"] = new[] { "放弃修改", "Discard changes" },
            ["msg.noContent"] = new[] { "没有可保存的内容", "Nothing to save" },
            ["msg.migratedDone"] = new[] { "已迁移 (新增 {0} 个 Section)", "Migrated (+{0} sections)" },
            ["msg.saveDone"] = new[] { "已保存到:\n{0}", "Saved to:\n{0}" },
            ["msg.migratedSaved"] = new[] { "保存成功", "Saved" },
            ["msg.batchPartial"] = new[] { "成功 {0} 个，失败 {1} 个。详见日志。", "{0} succeeded, {1} failed. See the log." },
            ["msg.batchAll"] = new[] { "全部成功！共 {0} 个。\n输出目录:\n{1}", "All succeeded ({0}).\nOutput folder:\n{1}" },
            ["msg.saveDupNote"] = new[] { "注意：以下文件中同一 ID 出现多次，已全部替换：", "Note: these files contain duplicate Ids; all were replaced:" },
            ["status.batchDone"] = new[] { "批量迁移完成 {0}/{1}", "Batch migration {0}/{1}" },
            ["status.migrated"] = new[] { "已迁移（新增 {0} 个 Section）", "Migrated (+{0} sections)" },
            ["dlg.openFilter"] = new[] { "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*", "JSON files (*.json)|*.json|All files (*.*)|*.*" },
            ["dlg.migrateRoot"] = new[] { "选择要迁移的根目录（UnitRecipe）", "Select the root folder to migrate (UnitRecipe)" },
            ["data.rootArray"] = new[] { "(根数组)", "(root array)" },
            ["data.arrayItem"] = new[] { "{0} 项", "{0} items" },
            ["data.object"] = new[] { "对象", "Object" },

            // 编辑对话框
            ["dlg.edit.title"] = new[] { "修改数据对象", "Edit data object" },
            ["dlg.edit.titleWithId"] = new[] { "修改数据对象  (ID = {0})", "Edit data object  (ID = {0})" },
            ["dlg.edit.newTitle"] = new[] { "新增数据对象", "New data object" },
            ["dlg.edit.typeTip"] = new[] { "Type 决定 TemplateKey：int/float/double → NumericUpDown，option/list → ComboBox，其余 → TextBox", "Type drives TemplateKey: int/float/double → NumericUpDown, option/list → ComboBox, others → TextBox" },
            ["dlg.edit.hint"] = new[] { "ID 只读（跨文件按 ID 整对象替换），TemplateKey 由 Type 自动维护（不显示）；确定后写回前自动备份。", "Id is read-only (objects are replaced by Id across files); TemplateKey is derived from Type (hidden). Backups are written before saving." },
            ["dlg.edit.modifyHint"] = new[] { "勾选要修改的属性；未勾选的属性保持文件原值，不写回。", "Check the properties to modify; unchecked properties keep their original values." },
            ["dlg.edit.enableTip"] = new[] { "勾选后才写回该属性（默认不勾选）", "Check to write this property back (unchecked by default)" },
            ["dlg.edit.noFieldSelected"] = new[] { "请至少勾选一个要修改的属性。", "Check at least one property to modify." },
            ["dlg.edit.source"] = new[] { "来源: {0}　·　", "Source: {0}　·　" },
            ["dlg.edit.insertHint"] = new[] { "插入索引 -1 表示末尾；确定后写入当前文件（自动备份），随后自动重排 Id 并导出。", "Insert index -1 means append; on OK the file is written (backup first), then Ids are resequenced and exported." },
            ["dlg.edit.indexLabel"] = new[] { "插入索引", "Insert index" },
            ["dlg.edit.indexTip"] = new[] { "-1 = 末尾", "-1 = append" },
            ["dlg.edit.indexInvalid"] = new[] { "插入索引必须是 -1 或 0..{0} 之间的整数", "Insert index must be an integer: -1 or 0..{0}" },
            ["dlg.edit.idHint"] = new[] { "ID 为对象主键，不可修改", "Id is the object key and cannot be changed" },
            ["dlg.edit.idRequired"] = new[] { "ID 必须是整数且不能为空", "Id must be a non-empty integer" },
            ["dlg.edit.required"] = new[] { "必填项不能为空", "Required field cannot be empty" },
            ["dlg.edit.idLocked"] = new[] { "ID 不可修改", "Id cannot be changed" },
            ["dlg.edit.errors"] = new[] { "存在 {0} 处校验错误，请修正后再确定。", "{0} validation error(s); please fix before OK." },
            ["dlg.edit.cancel"] = new[] { "取消", "Cancel" },
            ["dlg.edit.ok"] = new[] { "确定", "OK" },
            ["dlg.edit.boolHint"] = new[] { "（true / false）", "(true / false)" },
            ["dlg.edit.gridTip"] = new[] { "子表：可直接双击单元格编辑；Id 列只读", "Sub-table: double-click cells to edit; Id column is read-only" },
            ["dlg.edit.nestedTip"] = new[] { "嵌套对象暂不支持直接编辑，仅以只读 JSON 展示", "Nested objects are read-only JSON for now" },
            ["dlg.edit.int"] = new[] { "必须是整数（当前值 \"{0}\"）", "Must be an integer (current \"{0}\")" },
            ["dlg.edit.number"] = new[] { "必须是数字（当前值 \"{0}\"）", "Must be a number (current \"{0}\")" },
            ["dlg.edit.bool"] = new[] { "必须是 true 或 false", "Must be true or false" },
            ["dlg.edit.arrayInt"] = new[] { "数组元素必须是整数（\"{0}\"）", "Array item must be an integer (\"{0}\")" },
            ["dlg.edit.arrayNumber"] = new[] { "数组元素必须是数字（\"{0}\"）", "Array item must be a number (\"{0}\")" },
            ["dlg.edit.gridJson"] = new[] { "子表列 {0} 不是合法 JSON", "Sub-table column {0} is not valid JSON" },
            ["dlg.edit.gridInt"] = new[] { "子表列 {0} 必须是整数（当前值 \"{1}\"）", "Sub-table column {0} must be an integer (current \"{1}\")" },
            ["dlg.edit.gridNumber"] = new[] { "子表列 {0} 必须是数字（当前值 \"{1}\"）", "Sub-table column {0} must be a number (current \"{1}\")" },
            ["dlg.edit.defaultNumber"] = new[] { "Default 必须是数字（当前值 \"{0}\"）", "Default must be a number (current \"{0}\")" },
            ["dlg.edit.defaultRange"] = new[] { "Default 需在 [{0}, {1}] 范围内（当前 {2}）", "Default must be within [{0}, {1}] (current {2})" },
            ["dlg.edit.defaultOptions"] = new[] { "Default 必须是 Options 中的一项（可选: {0}）", "Default must be one of Options (choices: {0})" },

            // Id 重排预览 / 导出
            ["reseq.title"] = new[] { "Id 重排预览", "Id resequence preview" },
            ["reseq.hint"] = new[] { "确认后：先迁移到输出目录，再对输出副本按数组顺序重排 Id（从 1 开始）；源文件默认保持原 Id。", "On confirm: migrate to the output folder first, then resequence Ids (1..N) on the exported copies; source files keep their Ids by default." },
            ["reseq.applySource"] = new[] { "同时重排源文件（默认关闭，会改写 UnitRecipe 下的原始 JSON）", "Also resequence source files (off by default; rewrites the original JSON under UnitRecipe)" },
            ["reseq.body"] = new[] { "共 {0} 个文件，其中 {1} 个文件 Id 需要变化。", "{0} file(s); {1} need Id changes." },
            ["reseq.conflict"] = new[] { "⚠ 语义告警：{0} 个 Id 在不同文件中对应不同名称，重排后这些对象的 Id 会改变，" +
                "按 Id 跨文件定位的语义将不再保持。", "⚠ Warning: {0} Id(s) map to different names across files; after resequencing these Ids change and cross-file Id semantics will not hold." },
            ["reseq.ok"] = new[] { "确认重排并导出", "Resequence & export" },
            ["reseq.cancel"] = new[] { "取消", "Cancel" },
            ["msg.reseqNone"] = new[] { "当前目录没有可重排的 JSON 数据列表。", "No resequenceable JSON data array under the current folder." },
            ["msg.reseqDone"] = new[] { "已导出 {0} 个文件；输出副本重排 {1} 个文件，源文件重排 {2} 个文件。\n输出目录：\n{3}", "Exported {0} file(s); resequenced {1} output file(s) and {2} source file(s).\nOutput:\n{3}" },
            ["msg.reseqFailed"] = new[] { "导出完成 {0}/{1}，{2} 个文件失败：\n{3}", "Exported {0}/{1}; {2} failed:\n{3}" },
            ["msg.title.export"] = new[] { "迁移并导出", "Migrate & export" },

            // 删除对话框
            ["dlg.delete.title"] = new[] { "按 ID 删除数据对象", "Delete objects by Id" },
            ["dlg.delete.label"] = new[] { "要删除的 ID", "Id to delete" },
            ["dlg.delete.hint"] = new[] { "将删除当前 Recipe 类型目录下 JSON 文件中该 ID 的对象（同一文件多处出现会全部删除）。", "Deletes this Id from JSON files in the current recipe type folder (all occurrences)." },
            ["dlg.delete.ok"] = new[] { "删除", "Delete" },
            ["dlg.delete.cancel"] = new[] { "取消", "Cancel" },
            ["dlg.delete.suggest"] = new[] { "选择或输入 ID", "Select or type an Id" },

            // EC Configuration 页签
            ["ec.label.root"] = new[] { "📁 Chamber 目录", "📁 Chamber folder" },
            ["ec.label.type"] = new[] { "文件类型", "File type" },
            ["ec.label.file"] = new[] { "文件", "File" },
            ["ec.label.sheet"] = new[] { "SheetName", "SheetName" },
            ["ec.btn.add"] = new[] { "➕ 批量新增", "➕ Batch add" },
            ["ec.btn.addTip"] = new[] { "在当前类别下每个文件的当前 SheetName 各新增 N 行（可设置数量）", "Add N rows to the current SheetName of every file in the current category" },
            ["ec.btn.batchEdit"] = new[] { "✏ 批量修改", "✏ Batch edit" },
            ["ec.btn.batchEditTip"] = new[] { "按当前类别 + 当前 SheetName 批量修改；仅勾选的属性写入，未勾选保持原值", "Batch edit the current category + SheetName; only checked properties are written" },
            ["ec.btn.delete"] = new[] { "🗑 批量删除", "🗑 Batch delete" },
            ["ec.btn.deleteTip"] = new[] { "删除当前类别下当前 SheetName 的全部行（写盘前自动备份）", "Delete all rows of the current SheetName under the current category (backup first)" },
            ["ec.menu.add"] = new[] { "批量新增", "Batch add" },
            ["ec.menu.edit"] = new[] { "批量修改…", "Batch modify…" },
            ["ec.menu.deleteSelected"] = new[] { "批量删除", "Batch delete" },
            ["ec.grid.empty"] = new[] { "当前 SheetName 没有可显示的数据", "No data under the selected SheetName" },
            ["ec.rows"] = new[] { "共 {0} 条数据 · 已选 {1} 条 · 批量作用域：类别 {2} · {3} 个文件 / 匹配 SheetName+Class+Name 共 {4} 行", "{0} row(s) · {1} selected · Batch scope: category {2} · {3} file(s) / {4} row(s) matching SheetName+Class+Name" },
            ["ec.sheetHint"] = new[] { "{0} 个分类", "{0} group(s)" },
            ["ec.rootHint"] = new[] { "共 {0} 个配置文件", "{0} config file(s)" },

            ["ec.dlg.root"] = new[] { "选择 ChamberConfig 目录", "Select the ChamberConfig folder" },
            ["ec.dlg.newTitle"] = new[] { "批量新增 EC 记录 · 类别 {0}", "Batch add EC entries · {0}" },
            ["ec.dlg.editTitle"] = new[] { "批量修改 EC 记录 · 类别 {0}", "Batch edit EC entries · {0}" },
            ["ec.dlg.batchTitle"] = new[] { "批量修改 {0} 行 · 类别 {1}", "Batch edit {0} rows · {1}" },
            ["ec.dlg.newHint"] = new[] { "将在类别 {1} 的 {2} 个文件、SheetName「{0}」下新增；TemplateType / Words 由 TemplateKey 自动带出（Enum → 1/1，Text → 3/2）。", "Creates under SheetName \"{0}\" in {2} file(s) of category {1}; TemplateType / Words follow TemplateKey (Enum -> 1/1, Text -> 3/2)." },
            ["ec.dlg.batchHint"] = new[] { "类别 {0} · {1} 个文件 · SheetName「{2}」+ Class + Name 精确匹配共 {3} 行：勾选要修改的属性，未勾选的属性保持原值。", "Category {0} · {1} file(s) · SheetName \"{2}\" + Class + Name exact match: {3} row(s). Check the properties to modify; unchecked properties keep their original values." },
            ["ec.dlg.newFooter"] = new[] { "新增会写入类别 {0} 下的全部 {1} 个文件；ECID 按各文件最大值 + 2 顺延。", "Rows are added to all {1} file(s) in category {0}; ECID continues from each file's maximum + 2." },
            ["ec.dlg.editFooter"] = new[] { "修改只写入 SheetName + Class + Name 三者与模板完全一致的行；其它行原样保留。", "Only rows whose SheetName + Class + Name exactly match the template are written; all other rows stay untouched." },
            ["ec.dlg.batchFooter"] = new[] { "仅勾选的属性写入匹配行；未勾选的属性与其它行保持原值。", "Only checked properties are written to matching rows; unchecked properties and other rows keep their original values." },
            ["ec.dlg.optionHint"] = new[] { "从当前文件已有选项中选择，或输入自定义值（多个用 | 分隔）", "Pick an existing option set from the current file, or type custom values separated by |" },
            ["ec.dlg.ecidHint"] = new[] { "ECID 由系统维护：新增按各文件最大值 + 2 顺延，编辑时只读。", "ECID is system-managed: new rows continue from each file's maximum + 2; read-only when editing." },
            ["ec.dlg.count"] = new[] { "每个文件新增数量", "Count per file" },
            ["ec.dlg.countHint"] = new[] { "当前类别下每个文件的该 SheetName 各新增 N 行", "Add N rows to this SheetName in every file of the current category" },
            ["ec.dlg.fieldChkTip"] = new[] { "勾选表示本次批量修改会写入该属性（默认不勾选）", "Check to write this property in this batch (unchecked by default)" },
            ["ec.dlg.noFieldSelected"] = new[] { "请至少勾选一个要修改的属性。", "Check at least one property to modify." },
            ["ec.dlg.nameRequired"] = new[] { "Name 不能为空。", "Name is required." },
            ["ec.dlg.countInvalid"] = new[] { "新增数量必须是 1~500 的整数。", "Count must be an integer between 1 and 500." },
            ["ec.dlg.templateTypeRequired"] = new[] { "请选择 TemplateType。", "Please select TemplateType." },
            ["ec.dlg.noChange"] = new[] { "没有任何字段被修改。", "No field was changed." },

            ["ec.msg.loadFailed"] = new[] { "加载 EC 文件失败：{0}\n{1}", "Failed to load EC file: {0}\n{1}" },
            ["ec.msg.batchLoadFailed"] = new[] { "读取类别 {0} 的配置文件失败，未执行任何写入：\n{1}", "Failed to read config files of category {0}; nothing was written:\n{1}" },
            ["ec.msg.sheetRequired"] = new[] { "请先选择一个 SheetName 分类。", "Select a SheetName group first." },
            ["ec.msg.noSheetRows"] = new[] { "类别 {0} 下没有与 SheetName「{1}」匹配的数据。", "Category {0} has no data for SheetName \"{1}\"." },
            ["ec.msg.pickRowFirst"] = new[] { "请先在右侧 DataGrid 选择一行或让首行成为模板（用于匹配 SheetName + Class + Name）。",
                "Select a row in the DataGrid first; the template matches SheetName + Class + Name across all files in this category." },
            ["ec.msg.noMatchingRows"] = new[] { "类别 {0} 下没有匹配 SheetName「{1}」+ Class「{2}」+ Name「{3}」的行。",
                "No rows match SheetName \"{1}\" + Class \"{2}\" + Name \"{3}\" in category {0}." },
            ["ec.msg.batchEditConfirm"] = new[] { "将把勾选的属性写入类别 {0} 的 {1} 个文件，SheetName「{2}」+ Class「{4}」+ Name「{5}」匹配共 {3} 行（其它行不改）。\n写盘前自动备份，确认继续？", "Apply the checked properties to {3} row(s) matching SheetName \"{2}\" + Class \"{4}\" + Name \"{5}\" in {1} file(s) of category {0}; other rows are untouched.\nA backup is created first. Continue?" },
            ["ec.msg.batchEditDone"] = new[] { "已更新类别 {0}（{1} 个文件 / SheetName「{2}」）共 {3} 行。", "Updated {3} row(s) in category {0} ({1} file(s), SheetName \"{2}\")." },
            ["ec.msg.batchAddConfirm"] = new[] { "将在类别 {0} 的 {1} 个文件、SheetName「{2}」下各新增 {3} 行（共 {4} 行）。\n写盘前自动备份，确认继续？", "Add {3} row(s) to SheetName \"{2}\" in each of the {1} file(s) of category {0} ({4} rows total).\nA backup is created first. Continue?" },
            ["ec.msg.batchAddConfirmWithKey"] = new[] { "将在类别 {0} 的 {1} 个文件、SheetName「{2}」+ Class「{3}」下各新增 {4} 条「{5}」（共 {6} 行）。\n写盘前自动备份，确认继续？", "Add \"{5}\" to SheetName \"{2}\" + Class \"{3}\" in each of the {1} file(s) of category {0} ({4} row(s)/file, {6} total).\nA backup is created first. Continue?" },
            ["ec.msg.batchAddDone"] = new[] { "已在类别 {0} 的 {1} 个文件新增 {2} 行（共 {3} 行），SheetName「{4}」。", "Added {2} row(s) to each of the {1} file(s) in category {0} ({3} rows total), SheetName \"{4}\"." },
            ["ec.msg.batchDeleteConfirm"] = new[] { "将删除类别 {0} 的 {1} 个文件，SheetName「{2}」+ Class「{4}」+ Name「{5}」匹配的 {3} 行（其它行不改）。\n写盘前自动备份，确认继续？", "Delete {3} row(s) matching SheetName \"{2}\" + Class \"{4}\" + Name \"{5}\" in {1} file(s) of category {0}; other rows are untouched.\nA backup is created first. Continue?" },
            ["ec.msg.batchDeleteDone"] = new[] { "已删除类别 {0}（{1} 个文件 / SheetName「{2}」）共 {3} 行。", "Deleted {3} row(s) in category {0} ({1} file(s), SheetName \"{2}\")." },
            ["ec.msg.batchPartial"] = new[] { "已写入 {0} 行，{1} 个文件失败：\n{2}", "Wrote {0} row(s); {1} file(s) failed:\n{2}" },
            ["ec.msg.saveFailed"] = new[] { "写盘失败：{0}", "Write failed: {0}" }
        };

        public static string Language { get; private set; } = Zh;

        public static bool IsEnglish => string.Equals(Language, En, StringComparison.OrdinalIgnoreCase);

        public static void SetLanguage(string lang)
        {
            Language = string.Equals(lang, En, StringComparison.OrdinalIgnoreCase) ? En : Zh;
            CultureInfo.CurrentUICulture = new CultureInfo(Language);
        }

        public static string T(string key)
        {
            var pair = Map.TryGetValue(key, out var v) ? v : null;
            if (pair == null) return key;
            return IsEnglish ? pair[1] : pair[0];
        }

        public static string T(string key, params object[] args)
        {
            var text = T(key);
            try { return string.Format(CultureInfo.InvariantCulture, text, args); }
            catch { return text; }
        }
    }
}
