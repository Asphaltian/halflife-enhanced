namespace GoldSrc;

public enum ResourceKind
{
	Sound,
	Skin,
	Model,
	Decal,
	Generic,
	EventScript,
	World
}

public sealed class Resource
{
	public const int MaxResources = 1280;
	public const int MaxNameLength = 63;
	public const int Md5Size = 16;
	public const int ReservedSize = 32;

	public const int FatalIfMissing = 1 << 0;
	public const int WasMissing = 1 << 1;
	public const int Custom = 1 << 2;
	public const int Requested = 1 << 3;
	public const int Precached = 1 << 4;

	public string Name = "";
	public ResourceKind Type;
	public int Index;
	public int DownloadSize;
	public int Flags;
	public byte[] Md5 = new byte[Md5Size];
	public byte[] Reserved = new byte[ReservedSize];
}
