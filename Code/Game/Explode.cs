using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "spark_shower" )]
public class Shower : BaseEntity
{
	public override ObjectCaps ObjectCaps => ObjectCaps.DontSave;

	public override void Spawn()
	{
		var velocity = Server.RandomFloat( 200f, 300f ) * Angles;

		velocity.x += Server.RandomFloat( -100f, 100f );
		velocity.y += Server.RandomFloat( -100f, 100f );

		if ( velocity.z >= 0f )
			velocity.z += 200f;
		else
			velocity.z -= 200f;

		Velocity = velocity;
		MoveType = MoveType.Bounce;
		Gravity = 0.5f;
		NextThink = (float)(Server.Globals.Time + 0.1);
		Solid = Solid.Not;

		SetModel( "models/grenade.mdl" );
		SetSize( Vector3.Zero, Vector3.Zero );

		Effects |= Effects.NoDraw;
		Speed = Server.RandomFloat( 0.5f, 1.5f );

		Angles = Vector3.Zero;
	}

	public override void Think()
	{
		Util.Sparks( Origin );

		Speed -= 0.1f;

		if ( Speed > 0f )
			NextThink = (float)(Server.Globals.Time + 0.1);
		else
			Remove();

		EntityFlags &= ~EntityFlags.OnGround;
	}

	public override void Touch( BaseEntity other )
	{
		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
			Velocity *= 0.1f;
		else
			Velocity *= 0.6f;

		if ( (Velocity.x * Velocity.x) + (Velocity.y * Velocity.y) < 10.0 )
			Speed = 0f;
	}
}

[LinkEntityToClass( "env_explosion" )]
public class EnvExplosion : BaseMonster
{
	public const int SpawnFlagNoDamage = 1 << 0;
	public const int SpawnFlagRepeatable = 1 << 1;
	public const int SpawnFlagNoFireball = 1 << 2;
	public const int SpawnFlagNoSmoke = 1 << 3;
	public const int SpawnFlagNoDecal = 1 << 4;
	public const int SpawnFlagNoSparks = 1 << 5;

	[Save( FieldType.Integer )]
	private int _magnitude;
	[Save( FieldType.Integer )]
	private int _spriteScale;

	public override bool KeyValue( string key, string value )
	{
		if ( key != "iMagnitude" )
			return base.KeyValue( key, value );

		_magnitude = Util.Atoi( value );

		return true;
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		Effects = Effects.NoDraw;

		MoveType = MoveType.None;

		var spriteScale = (float)((_magnitude - 50) * 0.6);

		if ( spriteScale < 10f )
			spriteScale = 10f;

		_spriteScale = (int)spriteScale;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		ModelName = null;
		Solid = Solid.Not;

		var spot = Origin + new Vector3( 0f, 0f, 8f );
		var trace = Util.TraceLine( spot, spot + new Vector3( 0f, 0f, -40f ), IgnoreMonsters, this );

		if ( trace.Fraction != 1f )
			Origin = trace.EndPos + (trace.PlaneNormal * (float)((_magnitude - 24) * 0.6));

		if ( (SpawnFlags & SpawnFlagNoDecal) == 0 )
			Util.DecalTrace( trace, Server.RandomFloat( 0f, 1f ) < 0.5 ? Decals.Scorch1 : Decals.Scorch2 );

		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.Explosion );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteShort( GameModels.Fireball );
		Server.WriteByte( (SpawnFlags & SpawnFlagNoFireball) == 0 ? (byte)_spriteScale : 0 );
		Server.WriteByte( 15 );
		Server.WriteByte( TempEntity.ExplosionNone );
		Server.MessageEnd();

		if ( (SpawnFlags & SpawnFlagNoDamage) == 0 )
			RadiusDamage( this, this, _magnitude, ClassNone, DamageBlast );

		SetThink( Smoke );
		NextThink = (float)(Server.Globals.Time + 0.3);

		if ( (SpawnFlags & SpawnFlagNoSparks) != 0 )
			return;

		var sparkCount = Server.RandomLong( 0, 3 );

		for ( var i = 0; i < sparkCount; i++ )
			Create( "spark_shower", Origin, trace.PlaneNormal, null );
	}

	private void Smoke()
	{
		if ( (SpawnFlags & SpawnFlagNoSmoke) == 0 )
		{
			Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, Origin );
			Server.WriteByte( TempEntity.Smoke );
			Server.WriteCoord( Origin.x );
			Server.WriteCoord( Origin.y );
			Server.WriteCoord( Origin.z );
			Server.WriteShort( GameModels.Smoke );
			Server.WriteByte( (byte)_spriteScale );
			Server.WriteByte( 12 );
			Server.MessageEnd();
		}

		if ( (SpawnFlags & SpawnFlagRepeatable) == 0 )
			Remove();
	}
}

partial class BaseEntity
{
	protected void ExplosionCreate( Vector3 center, Vector3 angles, Edict owner, int magnitude, bool doDamage )
	{
		var explosion = Create( "env_explosion", center, angles, owner );

		explosion.KeyValue( "iMagnitude", $"{magnitude,3}" );

		if ( !doDamage )
			explosion.SpawnFlags |= EnvExplosion.SpawnFlagNoDamage;

		explosion.Spawn();
		explosion.Use( null, null, UseType.Toggle, 0f );
	}
}
