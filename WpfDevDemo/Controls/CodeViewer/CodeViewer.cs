using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfDevDemo.Controls
{
    [TemplatePart(Name = TabControlTemplateName, Type = typeof(TabControl))]
    public class CodeViewer : HeaderedContentControl
    {
        public const string TabControlTemplateName = "PART_TabControl";
        private static readonly Type _typeofSelf = typeof(CodeViewer);
        private TabControl _tabControl;

        public ObservableCollection<SourceCodeModel> SourceCodes { get; } =
            new ObservableCollection<SourceCodeModel>();

        static CodeViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(_typeofSelf,
                new FrameworkPropertyMetadata(_typeofSelf));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _tabControl = GetTemplateChild(TabControlTemplateName) as TabControl;
            if (_tabControl == null) return;
            foreach (var item in SourceCodes)
            {
                var tabItem = CreateTabItem(item);
                if (tabItem == null) continue;
                tabItem.Height = 38;
                tabItem.Padding = new Thickness(20, 0, 20, 0);
                _tabControl.Items.Add(tabItem);
            }
        }

        private TabItem CreateTabItem(SourceCodeModel codeModel)
        {
            if (codeModel == null) return null;
            var editor = new CodeTextEditor
            {
                Code = GetCodeText(codeModel.CodeSource),
                CodeLanguage = codeModel.CodeType == CodeType.Xaml ? "Xaml" : "CSharp",
            };
            var tabItem = new TabItem { Content = editor };
            switch (codeModel.CodeType)
            {
                case CodeType.Xaml:
                    tabItem.Header = string.IsNullOrEmpty(codeModel.Haader) ? "Xaml" : codeModel.Haader;
                    break;
                case CodeType.CSharp:
                    tabItem.Header = string.IsNullOrEmpty(codeModel.Haader) ? "CSharp" : codeModel.Haader;
                    break;
            }
            return tabItem;
        }

        private static string GetCodeText(string codeSource)
        {
            if (string.IsNullOrEmpty(codeSource))
                return string.Empty;
            try
            {
                var uri = new Uri(codeSource, UriKind.RelativeOrAbsolute);
                var resourceStream = Application.GetResourceStream(uri);
                if (resourceStream != null)
                {
                    using (var streamReader = new StreamReader(resourceStream.Stream))
                        return streamReader.ReadToEnd();
                }
            }
            catch
            {
                // ignore and fall through to empty text
            }
            return string.Empty;
        }
    }
}
