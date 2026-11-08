using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DollIrisGenerator.Models
{
    public class HistoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Title { get; set; } = "";
        public string Details { get; set; } = "";
        public IrisParameters Snapshot { get; set; }

        /// <summary>Consecutive entries with the same non-null key are kept as one (the newest state).</summary>
        public string? MergeKey { get; init; }

        public HistoryItem(IrisParameters parameters, string actionDescription = "変更")
        {
            Snapshot = parameters.Clone();
            Timestamp = DateTime.Now;
            Title = $"{parameters.Name} ({parameters.SizeMm:F1}mm)";
            Details = $"{actionDescription} - シード:{parameters.Seed} / ベース色:{parameters.BaseColor}";
        }
    }

    public class FavoriteItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _name = "お気に入り虹彩";
        private string _note = "";
        private DateTime _createdAt = DateTime.Now;
        private IrisParameters _parameters;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Note
        {
            get => _note;
            set { _note = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        public IrisParameters Parameters
        {
            get => _parameters;
            set { _parameters = value; OnPropertyChanged(); }
        }

        public FavoriteItem()
        {
            _parameters = new IrisParameters();
        }

        public FavoriteItem(IrisParameters parameters, string? note = null)
        {
            _name = parameters.Name;
            _parameters = parameters.Clone();
            _note = note ?? $"サイズ: {parameters.SizeMm:F1}mm / シード: {parameters.Seed}";
            _createdAt = DateTime.Now;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
