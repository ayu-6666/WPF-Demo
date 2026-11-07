using System;
using System.ComponentModel;

namespace MediaStudioWpf48.Models
{
    public class ToolItem : INotifyPropertyChanged
    {
        private string _group;
        private string _name;
        private string _help;

        public string GroupKey { get; set; }
        public string NameKey { get; set; }
        public string HelpKey { get; set; }
        public string Key { get; set; }

        public string Group
        {
            get => _group;
            set { _group = value; Raise(nameof(Group)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; Raise(nameof(Name)); }
        }

        public string Help
        {
            get => _help;
            set { _help = value; Raise(nameof(Help)); }
        }

        public void Localize(Func<string, string> translate)
        {
            Group = translate(GroupKey);
            Name = translate(NameKey);
            Help = translate(HelpKey);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
