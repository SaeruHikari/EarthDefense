namespace SkrGui.Gallery;
public static class GallerySourceTypeRegistry
{
 public static IReadOnlyDictionary<Type,Guid> Types { get; } = new Dictionary<Type,Guid>
 {
  [typeof(BatchCmdBackdropGlass)] = new("72303764-e38b-4a2d-b61d-11c37b07e19c"),
  [typeof(VisualGlass)] = new("a91e2bf6-b190-4d8f-9200-0f63fd5b6959"),
 };
}
