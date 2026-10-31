using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using CPM.Recipe.Tool.Models;

namespace CPM.Recipe.Tool.Services
{
    /// <summary>
    /// Recipe 迁移服务：按 RecipeType 枚举找模板，合并 Section 字段（Id 不变），另存到迁移目录。
    /// </summary>
    public static class RecipeMigrationService
    {
        /// <summary>RecipeType 枚举 → 模板静态实例</summary>
        private static readonly Dictionary<RecipeType, Func<ObservableCollection<RecipeModel.Section>>> TemplateMap
            = new Dictionary<RecipeType, Func<ObservableCollection<RecipeModel.Section>>>
            {
                [RecipeType.UnloadRecipe]  = () => Templates.UnloadRecipeTemplate.Template,
                [RecipeType.LoadRecipe]    = () => Templates.LoadRecipeTemplate.Template,
                [RecipeType.ConditionRecipe]       = () => Templates.ConditionRecipeTemplate.Template,
                [RecipeType.DechuckRecipe]         = () => Templates.DechuckRecipeTemplate.Template,
                [RecipeType.ExsituContidionSweepRecipe] = () => Templates.ExsituContidionSweepRecipeTemplate.Template,
                [RecipeType.HeadSweepRecipe]       = () => Templates.HeadSweepRecipeTemplate.Template,
                [RecipeType.PenA1Recipe]           = () => Templates.PenA1RecipeTemplate.Template,
                [RecipeType.PolishRecipe]          = () => Templates.PolishRecipeTemplate.Template,
                [RecipeType.RollRecipe]            = () => Templates.RollRecipeTemplate.Template,
                [RecipeType.DryRecipe]             = () => Templates.DryRecipeTemplate.Template,
            };

        // 字符串别名 → 枚举（兼容 JSON 里可能出现的 "RollerRecipe" / "PenRecipe" / "ConditionSweepRecipe" 等历史名称）
        private static readonly Dictionary<string, RecipeType> AliasMap
            = new Dictionary<string, RecipeType>(StringComparer.OrdinalIgnoreCase)
            {
                ["ConditionRecipe"] = RecipeType.ConditionRecipe,
                ["ConditionSweepRecipe"] = RecipeType.ExsituContidionSweepRecipe,
                ["ExsituContidionSweepRecipe"] = RecipeType.ExsituContidionSweepRecipe,
                ["DechuckRecipe"] = RecipeType.DechuckRecipe,
                ["DryRecipe"] = RecipeType.DryRecipe,
                ["HeadSweepRecipe"] = RecipeType.HeadSweepRecipe,
                ["LoadRecipe"] = RecipeType.LoadRecipe,
                ["PenA1Recipe"] = RecipeType.PenA1Recipe,
                ["PenRecipe"] = RecipeType.PenA1Recipe,
                ["PolishRecipe"] = RecipeType.PolishRecipe,
                ["RollRecipe"] = RecipeType.RollRecipe,
                ["RollerRecipe"] = RecipeType.RollRecipe,
                ["UnloadRecipe"] = RecipeType.UnloadRecipe,
            };

        public static List<RecipeType> SupportedTypes => TemplateMap.Keys.OrderBy(k => k.ToString()).ToList();

        /// <summary>把 JSON 里的 RecipeTypeName 字符串解析成 RecipeType 枚举（兼容别名）</summary>
        public static RecipeType? ParseRecipeType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            if (AliasMap.TryGetValue(typeName, out var t)) return t;
            if (Enum.TryParse<RecipeType>(typeName, true, out var t2)) return t2;
            return null;
        }

        public static RecipeModel Load(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<RecipeModel>(json);
        }

        public static string ToJson(RecipeModel recipe)
            => JsonConvert.SerializeObject(recipe, Formatting.Indented);

        public static RecipeModel Migrate(RecipeModel recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            RecipeType? rt = null;
            if (!string.IsNullOrWhiteSpace(recipe.RecipeTypeName))
                rt = ParseRecipeType(recipe.RecipeTypeName);
            if (rt == null) return recipe;
            if (!TemplateMap.TryGetValue(rt.Value, out var templateFactory)) return recipe;

            var template = templateFactory();
            if (template == null) return recipe;

            recipe.Data = recipe.Data ?? new ObservableCollection<RecipeModel.Section>();

            foreach (var tplSec in template)
            {
                var exist = recipe.Data.FirstOrDefault(s => s.Id == tplSec.Id);
                if (exist == null)
                {
                    recipe.Data.Add(new RecipeModel.Section
                    {
                        Id = tplSec.Id,
                        Name = tplSec.Name,
                        Unit = tplSec.Unit,
                        Type = tplSec.Type,
                        Min = tplSec.Min,
                        Max = tplSec.Max,
                        Default = tplSec.Default ?? "",
                        Options = tplSec.Options != null ? new List<string>(tplSec.Options) : null,
                        Steps = new ObservableCollection<RecipeModel.Step>()
                    });
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(exist.Name)) exist.Name = tplSec.Name;
                    if (string.IsNullOrWhiteSpace(exist.Unit)) exist.Unit = tplSec.Unit;
                    if (string.IsNullOrWhiteSpace(exist.Type)) exist.Type = tplSec.Type;
                    if (exist.Min == 0 && tplSec.Min != 0) exist.Min = tplSec.Min;
                    if (exist.Max == 0 && tplSec.Max != 0) exist.Max = tplSec.Max;
                    if (string.IsNullOrWhiteSpace(exist.Default)) exist.Default = tplSec.Default ?? "";
                    if ((exist.Options == null || exist.Options.Count == 0) && tplSec.Options != null && tplSec.Options.Count > 0)
                        exist.Options = new List<string>(tplSec.Options);
                }
            }

            return recipe;
        }

        public static string SaveMigrated(RecipeModel recipe, string originalPath, string sourceRoot, string targetRoot)
        {
            var rel = GetRelativePath(sourceRoot, originalPath);
            var destPath = Path.Combine(targetRoot, rel);
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir!);

            var json = JsonConvert.SerializeObject(recipe, Formatting.Indented);
            File.WriteAllText(destPath, json);
            return destPath;
        }

        public static IEnumerable<MigrationResult> MigrateDirectory(string sourceRoot, string migratedRoot, Action<string> log = null)
        {
            var results = new List<MigrationResult>();
            if (!Directory.Exists(sourceRoot))
            {
                results.Add(new MigrationResult { Success = false, Message = $"源目录不存在: {sourceRoot}" });
                return results;
            }

            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    log?.Invoke($"读取 {file}");
                    var recipe = Load(file);
                    var before = recipe.Data?.Count ?? 0;
                    Migrate(recipe);
                    var after = recipe.Data?.Count ?? 0;
                    var dest = SaveMigrated(recipe, file, sourceRoot, migratedRoot);
                    results.Add(new MigrationResult
                    {
                        Success = true,
                        Source = file,
                        Target = dest,
                        Added = after - before,
                        RecipeTypeName = recipe.RecipeTypeName ?? "(unknown)"
                    });
                    log?.Invoke($"  OK → {dest}  [Section: {before} → {after}]");
                }
                catch (Exception ex)
                {
                    results.Add(new MigrationResult { Success = false, Source = file, Message = ex.Message });
                    log?.Invoke($"  FAIL → {ex.Message}");
                }
            }
            return results;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            basePath = Path.GetFullPath(basePath).TrimEnd('\\', '/');
            fullPath = Path.GetFullPath(fullPath);
            var baseUri = new Uri(basePath + Path.DirectorySeparatorChar);
            var fullUri = new Uri(fullPath);
            var rel = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
            return rel;
        }
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";
        public int Added { get; set; }
        public string RecipeTypeName { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
