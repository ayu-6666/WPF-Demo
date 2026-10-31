using System;
using System.Windows;
using System.Windows.Controls;
using MindMapWpf48.Models;

namespace MindMapWpf48
{
    /// <summary>个人信息编辑弹框</summary>
    public partial class PersonEditWindow : Window
    {
        private readonly Person _person;
        private readonly bool _isNew;

        /// <param name="title">窗口标题（如"添加儿子"）</param>
        /// <param name="lockedGender">非空时锁定性别（妻子=女、儿子=男、女儿=女）</param>
        /// <param name="person">要编辑的成员</param>
        /// <param name="isNew">是否新建（确定时才提交到树）</param>
        public PersonEditWindow(string title, string lockedGender, Person person, bool isNew)
        {
            InitializeComponent();
            _person = person;
            _isNew = isNew;
            HeaderTxt.Text = title;
            Title = title;

            NameBox.Text = isNew ? "" : person.Name;
            BirthBox.Text = isNew ? "" : person.Birth;
            DeathBox.Text = isNew ? "" : person.Death;
            CourtesyBox.Text = isNew ? "" : person.Courtesy;
            NoteBox.Text = isNew ? "" : person.Note;
            EndedCheck.IsChecked = isNew ? false : person.LineEnded;
            AdoptedCheck.IsChecked = isNew ? false : person.Adopted;

            string g = isNew ? (lockedGender ?? "男") : person.Gender;
            GenderCombo.SelectedIndex = g == "女" ? 1 : 0;
            if (!string.IsNullOrEmpty(lockedGender))
                GenderCombo.IsEnabled = false; // 添加妻子/儿子/女儿时性别自动确定
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text == null ? "" : NameBox.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("请填写姓名。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }
            if (name.Length > 8)
            {
                MessageBox.Show("姓名过长（最多 8 字）。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var item = GenderCombo.SelectedItem as ComboBoxItem;
            _person.Name = name;
            _person.Gender = item == null ? "男" : (string)item.Content;
            _person.Birth = BirthBox.Text == null ? "" : BirthBox.Text.Trim();
            _person.Death = DeathBox.Text == null ? "" : DeathBox.Text.Trim();
            _person.Courtesy = CourtesyBox.Text == null ? "" : CourtesyBox.Text.Trim();
            _person.Note = NoteBox.Text == null ? "" : NoteBox.Text.Trim();
            _person.LineEnded = EndedCheck.IsChecked == true;
            _person.Adopted = AdoptedCheck.IsChecked == true;
            DialogResult = true;
        }
    }
}
