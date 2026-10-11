using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "hornet" )]
public class Hornet : BaseMonster
{
	public const int TypeRed = 0;
	public const int TypeOrange = 1;

	public const float RedSpeed = 600f;
	public const float OrangeSpeed = 800f;
	public const float BuzzVolume = 0.8f;

	private static readonly string[] BuzzSounds = ["hornet/ag_buzz1.wav", "hornet/ag_buzz2.wav", "hornet/ag_buzz3.wav"];
	private static readonly string[] HitSounds = ["hornet/ag_hornethit1.wav", "hornet/ag_hornethit2.wav", "hornet/ag_hornethit3.wav"];

	public static int Trail { get; private set; }

	public static int Puff { get; private set; }

	[Save( FieldType.Time )]
	private float _stopAttack;
	[Save( FieldType.Integer )]
	private int _hornetType;
	[Save( FieldType.Float )]
	private float _flySpeed;

	private bool OwnerIsClient => Owner is { } owner && (owner.EntityFlags & EntityFlags.Client) == EntityFlags.Client;

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		damageType &= ~DamageAlwaysGib;
		damageType |= DamageNeverGib;

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;
		DamageMode = DamageYes;
		EntityFlags |= EntityFlags.Monster;
		Health = 1f;

		_stopAttack = HalfLifeDll.GameRules.IsMultiplayer() ? (float)(Server.Globals.Time + 3.5) : (float)(Server.Globals.Time + 5.0);

		FieldOfView = 0.9f;

		if ( Server.RandomLong( 1, 5 ) <= 2 )
		{
			_hornetType = TypeRed;
			_flySpeed = RedSpeed;
		}
		else
		{
			_hornetType = TypeOrange;
			_flySpeed = OrangeSpeed;
		}

		SetModel( "models/hornet.mdl" );
		SetSize( new Vector3( -4f, -4f, -4f ), new Vector3( 4f, 4f, 4f ) );

		SetTouch( DieTouch );
		SetThink( StartTrack );

		Damage = OwnerIsClient ? HalfLifeDll.SkillData.PlrDmgHornet : HalfLifeDll.SkillData.MonDmgHornet;

		NextThink = (float)(Server.Globals.Time + 0.1);
		ResetSequenceInfo();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/hornet.mdl" );

		Server.PrecacheSound( "agrunt/ag_fire1.wav" );
		Server.PrecacheSound( "agrunt/ag_fire2.wav" );
		Server.PrecacheSound( "agrunt/ag_fire3.wav" );

		PrecacheSounds( BuzzSounds );
		PrecacheSounds( HitSounds );

		Puff = Server.PrecacheModel( "sprites/muz1.spr" );
		Trail = Server.PrecacheModel( "sprites/laserbeam.spr" );
	}

	public override int Relationship( BaseEntity target )
	{
		return target.ModelIndex == ModelIndex ? RelationshipNone : base.Relationship( target );
	}

	public override int Classify() => OwnerIsClient ? ClassPlayerBioWeapon : ClassAlienBioWeapon;

	public void StartTrack()
	{
		IgniteTrail();

		SetTouch( TrackTouch );
		SetThink( TrackTarget );

		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void StartDart()
	{
		IgniteTrail();

		SetTouch( DartTouch );

		SetThink( SubRemove );
		NextThink = Server.Globals.Time + 4f;
	}

	public void IgniteTrail()
	{
		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamFollow );
		Server.WriteShort( Index );
		Server.WriteShort( Trail );
		Server.WriteByte( 10 );
		Server.WriteByte( 2 );

		switch ( _hornetType )
		{
			case TypeRed:
				Server.WriteByte( 179 );
				Server.WriteByte( 39 );
				Server.WriteByte( 14 );
				break;

			case TypeOrange:
				Server.WriteByte( 255 );
				Server.WriteByte( 128 );
				Server.WriteByte( 0 );
				break;
		}

		Server.WriteByte( 128 );
		Server.MessageEnd();
	}

	private void Buzz()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( BuzzSounds ), BuzzVolume, Server.AttenuationNormal );
	}

	public void TrackTarget()
	{
		StudioFrameAdvance();

		if ( Server.Globals.Time > _stopAttack )
		{
			SetTouch( null );
			SetThink( SubRemove );
			NextThink = (float)(Server.Globals.Time + 0.1);
			return;
		}

		if ( EnemyEntity is null )
		{
			Look( 512 );
			EnemyEntity = BestVisibleEnemy();
		}

		if ( EnemyEntity is { } visible && IsVisible( visible ) )
			EnemyLastKnownPosition = visible.BodyTarget( Origin );
		else
			EnemyLastKnownPosition += Velocity * _flySpeed * 0.1f;

		var directionToEnemy = Util.Normalize( EnemyLastKnownPosition - Origin );
		var flightDirection = MathLib.Length( Velocity ) < 0.1 ? directionToEnemy : Util.Normalize( Velocity );
		var delta = Vector3.Dot( flightDirection, directionToEnemy );

		if ( delta < 0.5 )
			Buzz();

		if ( delta <= 0f && _hornetType == TypeRed )
			delta = 0.25f;

		var velocity = Util.Normalize( flightDirection + directionToEnemy );

		if ( Owner is { } owner && (owner.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster )
		{
			velocity.x += Server.RandomFloat( -0.10f, 0.10f );
			velocity.y += Server.RandomFloat( -0.10f, 0.10f );
			velocity.z += Server.RandomFloat( -0.10f, 0.10f );
		}

		switch ( _hornetType )
		{
			case TypeRed:
				velocity *= _flySpeed * delta;
				NextThink = Server.Globals.Time + Server.RandomFloat( 0.1f, 0.3f );
				break;

			case TypeOrange:
				velocity *= _flySpeed;
				NextThink = (float)(Server.Globals.Time + 0.1);
				break;
		}

		Velocity = velocity;
		Angles = MathLib.VectorAngles( Velocity );

		Solid = Solid.BBox;

		if ( EnemyEntity is null || HalfLifeDll.GameRules.IsMultiplayer() )
			return;

		if ( delta < 0.4 || MathLib.Length( Origin - EnemyLastKnownPosition ) > 300f )
			return;

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.Sprite );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteShort( Puff );
		Server.WriteByte( 2 );
		Server.WriteByte( 128 );
		Server.MessageEnd();

		Buzz();

		Velocity *= 2f;
		NextThink = (float)(Server.Globals.Time + 1.0);
		_stopAttack = Server.Globals.Time;
	}

	public void TrackTouch( BaseEntity other )
	{
		if ( other == Owner || other.ModelIndex == ModelIndex )
		{
			Solid = Solid.Not;
			return;
		}

		if ( Relationship( other ) <= RelationshipNone )
		{
			var velocity = Util.Normalize( Velocity );

			velocity.x *= -1f;
			velocity.y *= -1f;

			Origin += velocity * 4f;
			Velocity = velocity * _flySpeed;

			return;
		}

		DieTouch( other );
	}

	public void DartTouch( BaseEntity other ) => DieTouch( other );

	public void DieTouch( BaseEntity other )
	{
		ScenePhysicsDamage.HornetImpact( Server.Globals.Trace, Velocity, Origin );

		if ( other is not null && other.DamageMode != DamageNo )
		{
			Sound.Emit( this, SoundChannel.Voice, RandomSound( HitSounds ), 1f, Server.AttenuationNormal );

			other.TakeDamage( this, Owner as BaseEntity, Damage, DamageBullet );
		}

		ModelIndex = 0;
		Solid = Solid.Not;

		SetThink( SubRemove );
		NextThink = Server.Globals.Time + 1f;
	}
}
