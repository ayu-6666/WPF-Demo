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

            public string TemplateKey => Type?.ToLower() switch
            {
                "option" or "list" => "ComboBox",
                "int" or "float" => "NumericUpDown",
                _ => "TextBox"
            };
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
