using System.Collections.Generic;
using System.ComponentModel;

namespace DollIrisGenerator.Models
{
    /// <summary>
    /// An item of a design's list that sits at a position in the iris (decorative symbol, highlight), so the editor
    /// can scatter several at once (並べて追加), copy one (複製) and change the ticked ones together (まとめて変更).
    /// </summary>
    public interface IArrangeableItem : INotifyPropertyChanged
    {
        string Id { get; }
        double PositionX { get; set; }
        double PositionY { get; set; }
        double Rotation { get; set; }
        double Size { get; set; }

        /// <summary>
        /// Ticked for まとめて変更. Screen-only state: not saved, not copied by Clone, and not an edit of the design
        /// (<see cref="IrisParameters"/> does not pass it on as a change).
        /// </summary>
        bool IsSelected { get; set; }
    }

    public interface IArrangeableItem<T> : IArrangeableItem where T : IArrangeableItem<T>
    {
        /// <summary>
        /// The settings まとめて変更 sets on every ticked item: everything saved except the Id and the position
        /// (one position for all would stack them on top of each other).
        /// </summary>
        static abstract IReadOnlyList<string> BulkSettingNames { get; }

        /// <summary>A copy with a new Id (not ticked).</summary>
        T Clone();

        /// <summary>Sets the target's setting <paramref name="propertyName"/> to this item's value; false for any other name.</summary>
        bool CopySettingTo(T target, string propertyName);
    }
}
