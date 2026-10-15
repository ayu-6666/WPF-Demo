namespace WpfDevDemo.Controls
{
    public enum CodeType
    {
        Xaml,
        CSharp,
    }

    public class SourceCodeModel
    {
        public CodeType CodeType { get; set; }
        public string Haader { get; set; }
        public string CodeSource { get; set; }
    }
}
