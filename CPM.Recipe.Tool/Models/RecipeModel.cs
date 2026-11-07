using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPM.Recipe.Tool.Models
{
    public enum RecipeType
    {
        UnloadRecipe = 0,
        LoadRecipe = 1,
        ConditionRecipe = 2,
        DechuckRecipe = 3,
        ExsituContidionSweepRecipe = 4,
        HeadSweepRecipe = 5,
        PenA1Recipe = 6,
        PolishRecipe = 7,
        RollRecipe = 8,
        DryRecipe = 9,
    }

    public class RecipeModel : ObservableObject
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? RecipeTypeName { get; set; }
        public string? Uuid { get; set; }

        private ObservableCollection<Section>? data;
        public ObservableCollection<Section>? Data { get => data; set => SetProperty(ref data, value); }

        public class Section : ObservableObject, ITemplateKeyProvider
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Unit { get; set; }
            public string? Type { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public string Default { get; set; } = string.Empty;
            public List<string>? Options { get; set; }
            private ObservableCollection<Step> _steps = new ObservableCollection<Step>();
            public ObservableCollection<Step> Steps
            {
                get => _steps;
                set => SetProperty(ref _steps, value);
            }

            public string TemplateKey => SectionTypeCatalog.TemplateKeyFor(Type);
        }

        public class Step : ObservableObject
        {
            private int _id;
            public int Id
            {
                get => _id;
                set => SetProperty(ref _id, value);
            }

            private string _value = string.Empty;
            public string Value
            {
                get => _value;
                set => SetProperty(ref _value, value);
            }
        }
    }

    /// <summary>
    /// Section.Type 取值与 TemplateKey 映射的唯一来源：
    /// 编辑器下拉取值、TemplateKey 自动推导都从这里取，避免多处硬编码不一致。
    /// 依据：现有 UnitRecipe 数据中实际出现的 Type 为 float / list / option / int / text（另有少量空值），
    /// 其余（double / bool / string）按控件能力补充，用于兼容历史文件。
    /// </summary>
    public static class SectionTypeCatalog
    {
        public static readonly string[] Types = { "int", "float", "double", "bool", "string", "text", "option", "list" };

        /// <summary>Type → 控件模板键：数值类用 NumericUpDown，枚举类用 ComboBox，其余 TextBox。</summary>
        public static string TemplateKeyFor(string type)
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "int":
                case "float":
                case "double":
                    return "NumericUpDown";
                case "option":
                case "list":
                    return "ComboBox";
                default:
                    return "TextBox";
            }
        }
    }

    public interface ITemplateKeyProvider
    {
        string TemplateKey { get; }
    }

    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        protected void SetProperty<T>(ref T field, T value, string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        protected void Set<T>(ref T field, T value, string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }
    }
}
