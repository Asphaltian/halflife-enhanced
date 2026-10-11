using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

public enum SatchelCode
{
	Detonate,
	Release
}

[LinkEntityToClass( "grenade" )]
public class Grenade : BaseMonster
{
	private const int SpawnFlagDetonate = 0x0001;
	private const float ContactBlastRadius = 100f;

	public bool RegisteredSound;

	[Save( FieldType.Boolean )]
	private bool _contact;

	public override int BloodColor => DontBleed;

	public void Explode( Vector3 source, Vector3 aim )
	{
		var trace = Util.TraceLine( Origin, Origin + new Vector3( 0f, 0f, -32f ), IgnoreMonsters, this );

		Explode( trace, DamageBlast );
	}

	public virtual void Explode( in Trace trace, int damageType )
	{
		ModelName = null;
		Solid = Solid.Not;
		DamageMode = DamageNo;

		var blastOrigin = !_contact && trace.Fraction != 1.0 ? trace.EndPos + (trace.PlaneNormal * 0.6f) : Origin;

		if ( trace.Fraction != 1.0 )
			Origin = trace.EndPos + (trace.PlaneNormal * (Damage - 24f) * 0.6f);

		var contents = Util.PointContents( Origin );

		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.Explosion );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteShort( contents != (int)Contents.Water ? GameModels.Fireball : GameModels.WaterExplosion );
		Server.WriteByte( (int)((Damage - 50f) * .60) );
		Server.WriteByte( 15 );
		Server.WriteByte( TempEntity.ExplosionNone );
		Server.MessageEnd();

		Combat.RagdollExplosion( blastOrigin, (int)(_contact ? ContactBlastRadius : Damage * 2.5f), (int)Damage );

		SoundEnt.InsertSound( WorldSound.Combat, Origin, BasePlayerWeapon.NormalExplosionVolume, 3f );

		var owner = Owner as BaseEntity;

		Owner = null;

		Combat.RadiusDamage( Origin, this, owner, Damage, (float)(Damage * 2.5), ClassNone, damageType, blastOrigin );

		Util.DecalTrace( trace, Server.RandomFloat( 0f, 1f ) < 0.5 ? Decals.Scorch1 : Decals.Scorch2 );

		Server.RandomFloat( 0f, 1f );

		switch ( Server.RandomLong( 0, 2 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Voice, "weapons/debris1.wav", 0.55f, Server.AttenuationNormal );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Voice, "weapons/debris2.wav", 0.55f, Server.AttenuationNormal );
				break;
			case 2:
				Sound.Emit( this, SoundChannel.Voice, "weapons/debris3.wav", 0.55f, Server.AttenuationNormal );
				break;
		}

		Effects |= Effects.NoDraw;
		SetThink( Smoke );
		Velocity = Vector3.Zero;
		NextThink = (float)(Server.Globals.Time + 0.3);

		if ( contents == (int)Contents.Water )
			return;

		var sparkCount = Server.RandomLong( 0, 3 );

		for ( var i = 0; i < sparkCount; i++ )
			Create( "spark_shower", Origin, trace.PlaneNormal, null );
	}

	public void Smoke()
	{
		if ( Util.PointContents( Origin ) == (int)Contents.Water )
		{
			Util.Bubbles( Origin - new Vector3( 64f, 64f, 64f ), Origin + new Vector3( 64f, 64f, 64f ), 100 );
		}
		else
		{
			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
			Server.WriteByte( TempEntity.Smoke );
			Server.WriteCoord( Origin.x );
			Server.WriteCoord( Origin.y );
			Server.WriteCoord( Origin.z );
			Server.WriteShort( GameModels.Smoke );
			Server.WriteByte( (int)((Damage - 50f) * 0.80) );
			Server.WriteByte( 12 );
			Server.MessageEnd();
		}

		Remove();
	}

	protected void MonsterKilled( BaseEntity attacker, int gib ) => base.Killed( attacker, gib );

	public override void Killed( BaseEntity attacker, int gib ) => Detonate();

	public void DetonateUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		SetThink( Detonate );
		NextThink = Server.Globals.Time;
	}

	public void PreDetonate()
	{
		SoundEnt.InsertSound( WorldSound.Danger, Origin, 400, 0.3f );

		SetThink( Detonate );
		NextThink = Server.Globals.Time + 1f;
	}

	public void Detonate()
	{
		var spot = Origin + new Vector3( 0f, 0f, 8f );
		var trace = Util.TraceLine( spot, spot + new Vector3( 0f, 0f, -40f ), IgnoreMonsters, this );

		Explode( trace, DamageBlast );
	}

	public void ExplodeTouch( BaseEntity other )
	{
		Enemy = other;

		var spot = Origin - (Util.Normalize( Velocity ) * 32f);
		var trace = Util.TraceLine( spot, spot + (Util.Normalize( Velocity ) * 64f), IgnoreMonsters, this );

		Explode( trace, DamageBlast );
	}

	public void DangerSoundThink()
	{
		if ( !IsInWorld )
		{
			Remove();
			return;
		}

		SoundEnt.InsertSound( WorldSound.Danger, Origin + (Velocity * 0.5f), (int)MathLib.Length( Velocity ), 0.2f );
		NextThink = (float)(Server.Globals.Time + 0.2);

		if ( WaterLevel != 0 )
			Velocity *= 0.5f;
	}

	public void BounceTouch( BaseEntity other )
	{
		if ( other == Owner )
			return;

		if ( NextAttack < Server.Globals.Time && MathLib.Length( Velocity ) > 100f )
		{
			if ( Owner is BaseEntity owner )
			{
				var trace = Server.Globals.Trace;

				Combat.ClearMultiDamage();
				ScenePhysicsDamage.MeleeImpact( trace, 1f, Velocity );

				other.TraceAttack( owner, 1f, Server.Globals.Forward, trace, DamageClub );
				Combat.ApplyMultiDamage( this, owner );
			}

			NextAttack = (float)(Server.Globals.Time + 1.0);
		}

		var testVelocity = Velocity;

		testVelocity.z *= 0.45f;

		if ( !RegisteredSound && MathLib.Length( testVelocity ) <= 60f )
		{
			SoundEnt.InsertSound( WorldSound.Danger, Origin, (int)(Damage / 0.4), 0.3f );
			RegisteredSound = true;
		}

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			Velocity *= 0.8f;
			Sequence = Server.RandomLong( 1, 1 );
		}
		else
		{
			BounceSound();
		}

		FrameRate = (float)(MathLib.Length( Velocity ) / 200.0);

		if ( FrameRate > 1.0 )
			FrameRate = 1f;
		else if ( FrameRate < 0.5 )
			FrameRate = 0f;
	}

	public void SlideTouch( BaseEntity other )
	{
		if ( other == Owner )
			return;

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
			Velocity *= 0.95f;
		else
			BounceSound();
	}

	public virtual void BounceSound()
	{
		switch ( Server.RandomLong( 0, 2 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Voice, "weapons/grenade_hit1.wav", 0.25f, Server.AttenuationNormal );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Voice, "weapons/grenade_hit2.wav", 0.25f, Server.AttenuationNormal );
				break;
			case 2:
				Sound.Emit( this, SoundChannel.Voice, "weapons/grenade_hit3.wav", 0.25f, Server.AttenuationNormal );
				break;
		}
	}

	public void TumbleThink()
	{
		if ( !IsInWorld )
		{
			Remove();
			return;
		}

		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( DamageTime - 1f < Server.Globals.Time )
			SoundEnt.InsertSound( WorldSound.Danger, Origin + (Velocity * (DamageTime - Server.Globals.Time)), 400, 0.1f );

		if ( DamageTime <= Server.Globals.Time )
			SetThink( Detonate );

		if ( WaterLevel != 0 )
		{
			Velocity *= 0.5f;
			FrameRate = 0.2f;
		}
	}

	public override void Spawn()
	{
		MoveType = MoveType.Bounce;
		ClassName = "grenade";

		Solid = Solid.BBox;

		SetModel( "models/grenade.mdl" );
		SetSize( Vector3.Zero, Vector3.Zero );

		Damage = 100f;
		RegisteredSound = false;
	}

	public static Grenade ShootContact( BaseEntity owner, Vector3 start, Vector3 velocity )
	{
		if ( owner.ClientSide )
			return null;

		var server = owner.Server;
		var grenade = server.CreateEntity<Grenade>();

		grenade.Spawn();
		grenade.Gravity = 0.5f;
		grenade.SetOrigin( start );
		grenade.Velocity = velocity;
		grenade.Angles = MathLib.VectorAngles( grenade.Velocity );
		grenade.Owner = owner;

		grenade.SetThink( grenade.DangerSoundThink );
		grenade.NextThink = server.Globals.Time;

		grenade.AVelocity = grenade.AVelocity with { x = Server.RandomFloat( -100f, -500f ) };

		grenade.SetTouch( grenade.ExplodeTouch );

		grenade._contact = true;
		grenade.Damage = HalfLifeDll.SkillData.PlrDmgM203Grenade;

		return grenade;
	}

	public static Grenade ShootTimed( BaseEntity owner, Vector3 start, Vector3 velocity, float time )
	{
		if ( owner.ClientSide )
			return null;

		var server = owner.Server;
		var grenade = server.CreateEntity<Grenade>();

		grenade.Spawn();
		grenade.SetOrigin( start );
		grenade.Velocity = velocity;
		grenade.Angles = MathLib.VectorAngles( grenade.Velocity );
		grenade.Owner = owner;

		grenade.SetTouch( grenade.BounceTouch );

		grenade.DamageTime = server.Globals.Time + time;
		grenade.SetThink( grenade.TumbleThink );
		grenade.NextThink = (float)(server.Globals.Time + 0.1);

		if ( time < 0.1 )
		{
			grenade.NextThink = server.Globals.Time;
			grenade.Velocity = Vector3.Zero;
		}

		grenade.Sequence = Server.RandomLong( 3, 6 );
		grenade.FrameRate = 1f;

		grenade.Gravity = 0.5f;
		grenade.Friction = 0.8f;

		grenade.SetModel( "models/w_grenade.mdl" );
		grenade.Damage = 100f;

		return grenade;
	}

	public static Grenade ShootSatchelCharge( BaseEntity owner, Vector3 start, Vector3 velocity )
	{
		var grenade = owner.Server.CreateEntity<Grenade>();

		grenade.MoveType = MoveType.Bounce;
		grenade.ClassName = "grenade";

		grenade.Solid = Solid.BBox;

		grenade.SetModel( "models/grenade.mdl" );
		grenade.SetSize( Vector3.Zero, Vector3.Zero );

		grenade.Damage = 200f;
		grenade.SetOrigin( start );
		grenade.Velocity = velocity;
		grenade.Angles = Vector3.Zero;
		grenade.Owner = owner;

		grenade.SetThink( grenade.SubDoNothing );
		grenade.SetUse( grenade.DetonateUse );
		grenade.SetTouch( grenade.SlideTouch );
		grenade.SpawnFlags = SpawnFlagDetonate;

		grenade.Friction = 0.9f;

		return grenade;
	}

	public static void UseSatchelCharges( BaseEntity owner, SatchelCode code )
	{
		if ( owner is null )
			return;

		BaseEntity entity = null;

		while ( (entity = Util.FindEntityByClassname( entity, "grenade" )) is not null )
		{
			if ( (entity.SpawnFlags & SpawnFlagDetonate) == 0 || entity.Owner != owner )
				continue;

			if ( code == SatchelCode.Detonate )
				entity.Use( owner, owner, UseType.On, 0f );
			else
				entity.Owner = null;
		}
	}
}
