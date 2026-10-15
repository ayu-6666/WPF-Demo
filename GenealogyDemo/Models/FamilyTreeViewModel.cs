using GenealogyDemo.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace GenealogyDemo.Models
{
    public static class VisualTreeExtensions
    {
        /// <summary>
        /// 获取所有后代元素（包括子级、孙级等）
        /// </summary>
        public static IEnumerable<T> GetAllDescendants<T>(this DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t)
                    yield return t;

                foreach (T descendant in child.GetAllDescendants<T>())
                    yield return descendant;
            }
        }

        /// <summary>
        /// 获取所有后代元素（包括自身）
        /// </summary>
        public static IEnumerable<T> GetAllDescendantsAndSelf<T>(this DependencyObject parent) where T : DependencyObject
        {
            if (parent is T t)
                yield return t;

            foreach (T descendant in parent.GetAllDescendants<T>())
                yield return descendant;
        }

        /// <summary>
        /// 获取所有子元素（仅一级）
        /// </summary>
        public static IEnumerable<T> GetChildren<T>(this DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    yield return t;
            }
        }
    }
    public class FamilyTreeViewModel : ObservableObject
    {
        #region 数据

        public ObservableCollection<Person> AllPeople { get; } = new ObservableCollection<Person>();

        public ObservableCollection<Person> RootPeople => new ObservableCollection<Person>(
            AllPeople.Where(p => p.FatherId == null && p.MotherId == null));

        #endregion

        #region 选中项 & 命令

        private Person? _selectedPerson;
        public Person? SelectedPerson
        {
            get => _selectedPerson;
            set => SetProperty(ref _selectedPerson, value);
        }

        public ICommand AddPersonCommand { get; }
        public ICommand EditPersonCommand { get; }
        public ICommand DeletePersonCommand { get; }
        public ICommand QueryUpFiveCommand { get; }
        public ICommand QueryDownFiveCommand { get; }

        #endregion

        public FamilyTreeViewModel()
        {
            AddPersonCommand = new RelayCommand(_ => AddPerson());
            EditPersonCommand = new RelayCommand(_ => EditPerson(), _ => SelectedPerson != null);
            DeletePersonCommand = new RelayCommand(_ => DeletePerson(), _ => SelectedPerson != null);
            QueryUpFiveCommand = new RelayCommand(_ => QueryUpFive());
            QueryDownFiveCommand = new RelayCommand(_ => QueryDownFive());

            SeedDemoData();
        }

        #region 命令实现

        private void AddPerson()
        {
            var newPerson = new Person
            {
                Name = "新成员",
                Gender = "男",
                BirthDate = DateTime.Today.AddYears(-20)
            };

            if (SelectedPerson != null)
            {
                // 简单地把新人设为选中人的子女（父亲/母亲依据性别判断）
                if (SelectedPerson.Gender == "男")
                    newPerson.FatherId = SelectedPerson.Id;
                else if (SelectedPerson.Gender == "女")
                    newPerson.MotherId = SelectedPerson.Id;

                SelectedPerson.Children.Add(newPerson);
            }

            AllPeople.Add(newPerson);
            SelectedPerson = newPerson;
        }

        private void EditPerson()
        {
            if (SelectedPerson == null) return;

            var input = "";//Prompt.Show("请输入姓名（直接回车保持不变）", "编辑族人", SelectedPerson.Name);
            if (!string.IsNullOrWhiteSpace(input))
                SelectedPerson.Name = input;
        }

        private void DeletePerson()
        {
            if (SelectedPerson == null) return;

            var res = MessageBox.Show(
                $"确定删除 “{SelectedPerson.Name}” 及其后代吗？",
                "删除确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            var toDelete = GetDescendants(SelectedPerson, int.MaxValue).ToList();
            toDelete.Add(SelectedPerson);

            foreach (var p in toDelete)
            {
                if (p.FatherId.HasValue)
                {
                    var father = AllPeople.FirstOrDefault(x => x.Id == p.FatherId);
                    father?.Children.Remove(p);
                }
                if (p.MotherId.HasValue)
                {
                    var mother = AllPeople.FirstOrDefault(x => x.Id == p.MotherId);
                    mother?.Children.Remove(p);
                }

                AllPeople.Remove(p);
            }

            SelectedPerson = null;
        }

        private void QueryUpFive()
        {
            if (SelectedPerson == null) return;
            var ancestors = GetAncestors(SelectedPerson, 5);
            ShowResult("上五代祖先", ancestors);
        }

        private void QueryDownFive()
        {
            if (SelectedPerson == null) return;
            var descendants = GetDescendants(SelectedPerson, 5);
            ShowResult("下五代后代", descendants);
        }

        #endregion

        #region 辅助查询

        private IEnumerable<Person> GetAncestors(Person person, int generations)
        {
            yield return person;
            if (generations <= 0) yield break;

            var parents = new List<Person>();
            if (person.FatherId.HasValue)
                parents.Add(AllPeople.FirstOrDefault(x => x.Id == person.FatherId)!);
            if (person.MotherId.HasValue)
                parents.Add(AllPeople.FirstOrDefault(x => x.Id == person.MotherId)!);

            foreach (var p in parents.Where(p => p != null))
            {
                foreach (var anc in GetAncestors(p!, generations - 1))
                    yield return anc;
            }
        }

        private IEnumerable<Person> GetDescendants(Person person, int generations)
        {
            yield return person;
            if (generations <= 0) yield break;

            foreach (var child in person.Children)
            {
                foreach (var desc in GetDescendants(child, generations - 1))
                    yield return desc;
            }
        }

        private void ShowResult(string title, IEnumerable<Person> people)
        {
            var list = people.Distinct().Select(p => p.ToString()).ToList();
            var msg = string.Join(Environment.NewLine, list);
            MessageBox.Show($"{title}（共 {list.Count} 人）：{Environment.NewLine}{msg}",
                "查询结果", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 演示数据

        private void SeedDemoData()
        {
            var grandPa = new Person { Name = "张爷爷", Gender = "男", BirthDate = new DateTime(1940, 5, 12) };
            var grandMa = new Person { Name = "张奶奶", Gender = "女", BirthDate = new DateTime(1942, 8, 23) };
            grandPa.SpouseId = grandMa.Id;
            grandMa.SpouseId = grandPa.Id;

            var father = new Person { Name = "张爸爸", Gender = "男", BirthDate = new DateTime(1965, 1, 10), FatherId = grandPa.Id, MotherId = grandMa.Id };
            var mother = new Person { Name = "李妈妈", Gender = "女", BirthDate = new DateTime(1968, 3, 5) };
            father.SpouseId = mother.Id;
            mother.SpouseId = father.Id;

            // 父母加入爷爷奶奶的子女列表（仅用于 UI）
            grandPa.Children.Add(father);
            grandMa.Children.Add(father);
            father.Children.Add(mother); // 这里只是让树能显示配偶关系

            var me = new Person { Name = "张我", Gender = "男", BirthDate = new DateTime(1990, 7, 15), FatherId = father.Id, MotherId = mother.Id };
            father.Children.Add(me);
            mother.Children.Add(me);
            foreach (var p in new[] { grandPa, grandMa, father, mother, me })
            {
                AllPeople.Add(p);
            }
            SelectedPerson = me;
        }

        #endregion
    }
}
