using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class GenericCrudViewModel<T> : INotifyPropertyChanged where T : class, new()
    {
        private readonly IGenericService<T> _service;
        public ObservableCollection<T> Items { get; } = new ObservableCollection<T>();

        private T _selected;
        public T Selected { get => _selected; set { _selected = value; OnPropertyChanged(nameof(Selected)); } }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public GenericCrudViewModel(IGenericService<T> service)
        {
            _service = service;
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(_ => Edit());
            DeleteCommand = new RelayCommand(param => Delete(param));

            Load();
        }

        public void Load()
        {
            Items.Clear();
            foreach (var it in _service.GetAll()) Items.Add(it);
        }

        public virtual void Add()
        {
            // open generic popup - left for caller to override/handle
        }

        public virtual void Edit()
        {
            // open generic popup - left for caller to override/handle
        }

        public virtual void Delete(object param)
        {
            T toDelete = null;
            if (param is T t) toDelete = t;
            else if (Selected != null) toDelete = Selected;
            if (toDelete == null) return;

            var idProp = typeof(T).GetProperty("Id");
            if (idProp == null) return;
            var id = (int)idProp.GetValue(toDelete);
            _service.Delete(id);
            Load();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
