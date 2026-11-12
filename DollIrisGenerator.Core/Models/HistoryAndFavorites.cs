using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Renames the favorite: the list entry and the design it holds, so restoring it gives the design the new
        /// name too. Surrounding spaces are dropped.
        /// </summary>
        /// <returns>False (nothing changes) when the new name is empty or the same as now.</returns>
        public bool Rename(string? newName)
        {
            string name = (newName ?? "").Trim();
            if (name.Length == 0 || (name == Name && name == Parameters.Name)) return false;
            Name = name;
            Parameters.Name = name;
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    /// <summary>Naming rules for favorites, shared by the desktop and browser apps.</summary>
    public static class FavoriteNames
    {
        /// <summary>
        /// <paramref name="name"/> when no favorite uses it yet, otherwise 「〇〇 (2)」, 「〇〇 (3)」…, so favorites
        /// added from a design that keeps the same name (such as the default 「新規虹彩デザイン」) can be told apart.
        /// </summary>
        public static string MakeUnique(string name, IEnumerable<FavoriteItem> favorites)
        {
            var used = favorites.Select(f => f.Name).ToHashSet();
            if (!used.Contains(name)) return name;
            for (int n = 2; ; n++)
            {
                string candidate = $"{name} ({n})";
                if (!used.Contains(candidate)) return candidate;
            }
        }

        /// <summary>「〇〇 のコピー」, then 「〇〇 のコピー (2)」…</summary>
        public static string MakeCopyName(string name, IEnumerable<FavoriteItem> favorites) =>
            MakeUnique($"{name} のコピー", favorites);

        /// <summary>
        /// A new favorite of <paramref name="design"/>, named so it differs from the existing favorites. The design
        /// in the favorite gets the same name; <paramref name="design"/> itself is not changed.
        /// </summary>
        public static FavoriteItem CreateFavorite(IrisParameters design, IEnumerable<FavoriteItem> favorites)
        {
            var favorite = new FavoriteItem(design);
            string name = MakeUnique(design.Name, favorites);
            if (name != design.Name) favorite.Rename(name);
            return favorite;
        }
    }
}
