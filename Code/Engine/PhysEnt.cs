using System;

namespace GoldSrc;

public sealed class PhysEnt
{
	public string Name { get; set; }
	public int Player { get; set; }
	public Vector3 Origin { get; set; }
	public BrushModel Model { get; set; }
	public Studio.File StudioModel { get; set; }
	public Vector3 Mins { get; set; }
	public Vector3 Maxs { get; set; }
	public int Info { get; set; }
	public Vector3 Angles { get; set; }
	public Solid Solid { get; set; }
	public int Skin { get; set; }
	public RenderMode RenderMode { get; set; }
	public float Frame { get; set; }
	public int Sequence { get; set; }
	public byte[] Controller { get; } = new byte[4];
	public byte[] Blending { get; } = new byte[2];
	public MoveType MoveType { get; set; }
	public int TakeDamage { get; set; }
	public int ClassNumber { get; set; }

	public void Clear()
	{
		Name = null;
		Player = 0;
		Origin = default;
		Model = null;
		StudioModel = null;
		Mins = default;
		Maxs = default;
		Info = 0;
		Angles = default;
		Solid = default;
		Skin = 0;
		RenderMode = default;
		Frame = 0f;
		Sequence = 0;
		Controller.AsSpan().Clear();
		Blending.AsSpan().Clear();
		MoveType = default;
		TakeDamage = 0;
		ClassNumber = 0;
	}
}

public sealed class PhysEntPool
{
	private readonly List<PhysEnt> _items = [];
	private int _used;

	public void Reset() => _used = 0;

	public PhysEnt Alloc()
	{
		if ( _used == _items.Count )
			_items.Add( new PhysEnt() );

		var physEnt = _items[_used++];

		physEnt.Clear();

		return physEnt;
	}
}
