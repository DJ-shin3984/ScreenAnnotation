namespace ScreenAnnotation.Core.Annotations;

/// <summary>보드당 전역 단일 Undo 스택. capacity 초과 시 가장 오래된 것부터 버린다.</summary>
public sealed class HistoryManager(AnnotationStore store, int capacity = 200)
{
    private readonly LinkedList<IAnnotationCommand> _undo = new();
    private readonly Stack<IAnnotationCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;

    public void Execute(IAnnotationCommand command)
    {
        command.Execute(store);
        _undo.AddLast(command);
        if (_undo.Count > capacity) _undo.RemoveFirst();
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Last is not { } node) return false;
        _undo.RemoveLast();
        node.Value.Undo(store);
        _redo.Push(node.Value);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var c)) return false;
        c.Execute(store);
        _undo.AddLast(c);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
