namespace ScreenAnnotation.Core.Annotations;

public interface IAnnotationCommand
{
    void Execute(AnnotationStore store);
    void Undo(AnnotationStore store);
}

/// <summary>펜·형광펜·도형·텍스트 확정.</summary>
public sealed class AddCommand(Annotation annotation) : IAnnotationCommand
{
    public void Execute(AnnotationStore store) => store.Add(annotation);
    public void Undo(AnnotationStore store) => store.Remove(annotation.Id);
}

/// <summary>지우개, Delete.</summary>
public sealed class RemoveCommand(Guid id) : IAnnotationCommand
{
    private Annotation? _removed;

    public void Execute(AnnotationStore store) => _removed = store.Remove(id) ?? _removed;
    public void Undo(AnnotationStore store) { if (_removed is not null) store.Add(_removed); }
}

/// <summary>도형 보정, 텍스트 재편집, 요소 이동·크기 조절.</summary>
public sealed class ReplaceCommand(Guid oldId, Annotation replacement) : IAnnotationCommand
{
    private Annotation? _original;

    public void Execute(AnnotationStore store) => _original = store.Replace(oldId, replacement) ?? _original;
    public void Undo(AnnotationStore store) { if (_original is not null) store.Replace(replacement.Id, _original); }
}

/// <summary>요소를 처음 움직일 때: 요소 + 구멍 메움 패치를 한 번에.</summary>
public sealed class LiftElementCommand(ElementAnnotation element, PatchAnnotation patch) : IAnnotationCommand
{
    public void Execute(AnnotationStore store) { store.Add(patch); store.Add(element); }
    public void Undo(AnnotationStore store) { store.Remove(element.Id); store.Remove(patch.Id); }
}

/// <summary>전체 지우기, 지우개 드래그 한 번. Undo는 역순.</summary>
public sealed class CompositeCommand(IReadOnlyList<IAnnotationCommand> commands) : IAnnotationCommand
{
    public void Execute(AnnotationStore store) { foreach (var c in commands) c.Execute(store); }
    public void Undo(AnnotationStore store) { for (var i = commands.Count - 1; i >= 0; i--) commands[i].Undo(store); }
}
