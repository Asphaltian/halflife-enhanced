namespace GoldSrc;

public enum ModelType
{
	Brush,
	Sprite,
	Alias,
	Studio
}

public abstract class Model
{
	public abstract ModelType Type { get; }
	public string Name { get; init; }
	public Vector3 Mins { get; init; }
	public Vector3 Maxs { get; init; }
	public Sandbox.Model RenderModel { get; init; }
}

public sealed class StudioModel : Model
{
	public override ModelType Type => ModelType.Studio;
	public Studio.File File { get; init; }
}
