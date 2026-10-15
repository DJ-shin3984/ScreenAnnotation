using System.Windows.Media.Imaging;

namespace ScreenAnnotation.Rendering;

/// <summary>떼어낸 요소의 잘라낸 비트맵. 렌더러·내보내기가 ElementAnnotation.Id로 찾는다. 보드를 끌 때 비운다.</summary>
public static class ElementImageCache
{
    private static readonly Dictionary<Guid, BitmapSource> _images = [];

    public static void Set(Guid id, BitmapSource image) => _images[id] = image;
    public static BitmapSource? Get(Guid id) => _images.GetValueOrDefault(id);
    public static void Clear() => _images.Clear();
}
