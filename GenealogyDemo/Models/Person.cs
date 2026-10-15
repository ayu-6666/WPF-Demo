using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenealogyDemo.Models
{
    public class Person : ObservableObject
    {
        private string _name;
        private string _gender;
        private DateTime? _birthDate;
        private Guid? _spouseId;
        private Guid? _fatherId;
        private Guid? _motherId;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Gender
        {
            get => _gender;
            set => SetProperty(ref _gender, value);
        }

        public DateTime? BirthDate
        {
            get => _birthDate;
            set => SetProperty(ref _birthDate, value);
        }

        public Guid? SpouseId
        {
            get => _spouseId;
            set => SetProperty(ref _spouseId, value);
        }

        public Guid? FatherId
        {
            get => _fatherId;
            set => SetProperty(ref _fatherId, value);
        }

        public Guid? MotherId
        {
            get => _motherId;
            set => SetProperty(ref _motherId, value);
        }

        // 仅用于 UI 展示的子女集合
        public ObservableCollection<Person> Children { get; } = new ObservableCollection<Person>();

        public override string ToString()
        {
            return $"{Name} ({Gender}){(BirthDate.HasValue ? $"，出生:{BirthDate:yyyy-MM-dd}" : "")}";
        }
    }
}
