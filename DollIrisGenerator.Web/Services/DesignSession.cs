using System.Text.Json;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.Web.Services;

/// <summary>
/// The design being edited, with undo / redo (up to 40 steps, like the desktop app).
///
/// A slider drag is one edit: the first change of a gesture takes the "before" snapshot (<see cref="BeginEdit"/>),
/// later changes only redraw, and releasing the control (<see cref="CommitEdit"/>) records one undo step.
/// Whole-design operations (preset, random, restore...) go through <see cref="Apply"/> / <see cref="Replace"/>.
/// </summary>
public sealed class DesignSession
{
    public const int MaxUndo = 40;

    private readonly List<IrisParameters> _undo = new();
    private readonly List<IrisParameters> _redo = new();
    private IrisParameters? _gestureStart;

    public IrisParameters Current { get; private set; } = new();

    /// <summary>Goes up on every change of <see cref="Current"/>; the preview redraws when it moves.</summary>
    public int Version { get; private set; }

    public bool CanUndo => _undo.Count > 0 || _gestureStart != null;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Short message about the last operation, for the status line.</summary>
    public string Status { get; set; } = "";

    public event Action? Changed;

    /// <summary>Call before changing <see cref="Current"/> directly (first input of a gesture).</summary>
    public void BeginEdit()
    {
        _gestureStart ??= Current.Clone();
    }

    /// <summary>Call after changing <see cref="Current"/> directly: redraws.</summary>
    public void Touch()
    {
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Ends the current gesture: one undo step if anything actually changed.</summary>
    public void CommitEdit()
    {
        if (_gestureStart == null) return;
        var before = _gestureStart;
        _gestureStart = null;
        if (SameDesign(before, Current)) return;
        PushUndo(before);
        _redo.Clear();
        Changed?.Invoke();
    }

    /// <summary>Changes the design in one undoable step.</summary>
    public void Apply(string status, Action<IrisParameters> change)
    {
        CommitEdit();
        var before = Current.Clone();
        change(Current);
        PushUndo(before);
        _redo.Clear();
        Status = status;
        Touch();
    }

    /// <summary>Replaces the design in one undoable step (the new one is used as given, not copied).</summary>
    public void Replace(IrisParameters design, string status)
    {
        CommitEdit();
        PushUndo(Current);
        _redo.Clear();
        Current = design;
        Status = status;
        Touch();
    }

    public void Undo()
    {
        CommitEdit();
        if (_undo.Count == 0) return;
        _redo.Add(Current);
        Current = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Status = "元に戻しました。";
        Touch();
    }

    public void Redo()
    {
        CommitEdit();
        if (_redo.Count == 0) return;
        _undo.Add(Current);
        Current = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Status = "やり直しました。";
        Touch();
    }

    private void PushUndo(IrisParameters snapshot)
    {
        _undo.Add(snapshot);
        if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
    }

    private static bool SameDesign(IrisParameters a, IrisParameters b) =>
        JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
}
