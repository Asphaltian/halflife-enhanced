using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "gibshooter" )]
public class GibShooter : BaseDelay
{
	private const int SpawnFlagRepeatable = 1;

	[Save( FieldType.Integer )]
	private int _gibs;
	[Save( FieldType.Integer )]
	private int _gibCapacity;
	[Save( FieldType.Integer )]
	protected int GibMaterial;
	[Save( FieldType.Integer )]
	protected int GibModelIndex;
	[Save( FieldType.Float )]
	private float _gibVelocity;
	[Save( FieldType.Float )]
	private float _variance;
	[Save( FieldType.Float )]
	private float _gibLife;

	public override void Precache()
	{
		GibModelIndex = Server.PrecacheModel( HalfLifeDll.Language == HalfLifeDll.LanguageGerman ? "models/germanygibs.mdl" : "models/hgibs.mdl" );
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "m_iGibs":
				_gibs = _gibCapacity = Util.Atoi( value );
				return true;
			case "m_flVelocity":
				_gibVelocity = Util.Atof( value );
				return true;
			case "m_flVariance":
				_variance = Util.Atof( value );
				return true;
			case "m_flGibLife":
				_gibLife = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		SetThink( ShootThink );
		NextThink = Server.Globals.Time;
	}

	public override void Spawn()
	{
		Precache();

		Solid = Solid.Not;
		Effects = Effects.NoDraw;

		if ( Delay == 0f )
			Delay = 0.1f;

		if ( _gibLife == 0f )
			_gibLife = 25f;

		SetMoveDir();

		Body = Server.ModelFrames( GibModelIndex );
	}

	public virtual Gib CreateGib()
	{
		if ( Cvar.GetFloat( "violence_hgibs" ) == 0f )
			return null;

		var gib = Server.CreateEntity<Gib>();

		gib.Spawn( "models/hgibs.mdl" );
		gib.GibBloodColor = BloodColorRed;

		if ( Body <= 1 )
			Util.Alert( AlertType.AiConsole, "GibShooter Body is <= 1!" );

		gib.Body = Server.RandomLong( 1, Body - 1 );

		return gib;
	}

	public void ShootThink()
	{
		var globals = Server.Globals;

		NextThink = globals.Time + Delay;

		var shootDirection = MoveDir;

		shootDirection += globals.Right * Server.RandomFloat( -1f, 1f ) * _variance;
		shootDirection += globals.Forward * Server.RandomFloat( -1f, 1f ) * _variance;
		shootDirection += globals.Up * Server.RandomFloat( -1f, 1f ) * _variance;

		shootDirection = Util.Normalize( shootDirection );

		if ( CreateGib() is { } gib )
		{
			gib.Origin = Origin;
			gib.Velocity = shootDirection * _gibVelocity;

			gib.AVelocity = gib.AVelocity with { x = Server.RandomFloat( 100f, 200f ), y = Server.RandomFloat( 100f, 300f ) };

			var thinkTime = gib.NextThink - globals.Time;

			gib.LifeTime = _gibLife * Server.RandomFloat( 0.95f, 1.05f );

			if ( gib.LifeTime < thinkTime )
			{
				gib.NextThink = globals.Time + gib.LifeTime;
				gib.LifeTime = 0f;
			}
		}

		if ( --_gibs > 0 )
			return;

		if ( (SpawnFlags & SpawnFlagRepeatable) != 0 )
		{
			_gibs = _gibCapacity;
			SetThink( null );
		}
		else
		{
			SetThink( SubRemove );
		}

		NextThink = globals.Time;
	}
}

[LinkEntityToClass( "env_shooter" )]
public class EnvShooter : GibShooter
{
	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "shootmodel":
				ModelName = Server.AllocString( value );
				return true;

			case "shootsounds":
				GibMaterial = (int)(Util.Atoi( value ) switch
				{
					0 => Materials.Glass,
					1 => Materials.Wood,
					2 => Materials.Metal,
					3 => Materials.Flesh,
					4 => Materials.Rocks,
					_ => Materials.None
				});

				return true;

			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Precache()
	{
		GibModelIndex = Server.PrecacheModel( ModelName );

		Breakable.MaterialSoundPrecache( Server, (Materials)GibMaterial );
	}

	public override Gib CreateGib()
	{
		var gib = Server.CreateEntity<Gib>();

		gib.Spawn( ModelName );

		gib.Body = Body > 1 ? Server.RandomLong( 0, Body - 1 ) : 0;
		gib.GibBloodColor = DontBleed;
		gib.Material = (Materials)GibMaterial;

		gib.RenderMode = RenderMode;
		gib.RenderAmt = RenderAmt;
		gib.RenderColor = RenderColor;
		gib.RenderFx = RenderFx;
		gib.Scale = Scale;
		gib.Skin = Skin;

		return gib;
	}
}
