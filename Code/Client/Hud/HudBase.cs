using GoldSrc;

namespace HalfLife;

public abstract class HudBase( Hud hud )
{
	public const int Active = 1;
	public const int Intermission = 2;

	protected Hud Hud { get; } = hud;

	protected Client Engine => Hud.Engine;

	public int Flags { get; protected set; }

	public abstract void Init();

	public virtual void VidInit()
	{
	}

	public virtual void Draw( float time )
	{
	}

	public virtual void Think()
	{
	}

	public virtual void Reset()
	{
	}

	public virtual void InitHudData()
	{
	}
}
