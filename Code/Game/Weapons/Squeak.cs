using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_snark" )]
public class SqueakGrenade : Grenade
{
	private const int SequenceRun = 3;

	private const float DetonateDelay = 15f;

	private static float _nextBounceSoundTime;

	[Save( FieldType.Time )]
	private float _die;
	[Save( FieldType.Vector )]
	private Vector3 _target;
	[Save( FieldType.Time )]
	private float _nextHunt;
	[Save( FieldType.Time )]
	private float _nextHit;
	[Save( FieldType.PositionVector )]
	private Vector3 _previousPosition;
	[Save( FieldType.EHandle )]
	private BaseEntity _owner;

	private int _class;

	public override int BloodColor => BloodColorYellow;

	public override int Classify()
	{
		if ( _class != 0 )
			return _class;

		if ( EnemyEntity is { } enemy )
		{
			_class = ClassInsect;

			switch ( enemy.Classify() )
			{
				case ClassPlayer:
				case ClassHumanPassive:
				case ClassHumanMilitary:
					_class = 0;
					return ClassAlienMilitary;
			}

			_class = 0;
		}

		return ClassAlienBioWeapon;
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Bounce;
		Solid = Solid.BBox;

		SetModel( "models/w_squeak.mdl" );
		SetSize( new Vector3( -4f, -4f, 0f ), new Vector3( 4f, 4f, 8f ) );
		SetOrigin( Origin );

		SetTouch( SuperBounceTouch );
		SetThink( HuntThink );
		NextThink = (float)(Globals.Time + 0.1);
		_nextHunt = (float)(Globals.Time + 1E6);

		EntityFlags |= EntityFlags.Monster;
		DamageMode = DamageAim;
		Health = HalfLifeDll.SkillData.SnarkHealth;
		Gravity = 0.5f;
		Friction = 0.5f;

		Damage = HalfLifeDll.SkillData.SnarkDmgPop;

		_die = Globals.Time + DetonateDelay;

		FieldOfView = 0f;

		if ( Owner is BaseEntity owner )
			_owner = owner;

		_nextBounceSoundTime = Globals.Time;

		Sequence = SequenceRun;
		ResetSequenceInfo();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_squeak.mdl" );
		PrecacheSound( "squeek/sqk_blast1.wav" );
		PrecacheSound( "common/bodysplat.wav" );
		PrecacheSound( "squeek/sqk_die1.wav" );
		PrecacheSound( "squeek/sqk_hunt1.wav" );
		PrecacheSound( "squeek/sqk_hunt2.wav" );
		PrecacheSound( "squeek/sqk_hunt3.wav" );
		PrecacheSound( "squeek/sqk_deploy1.wav" );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		ModelName = null;
		SetThink( SubRemove );
		SetTouch( null );
		NextThink = (float)(Globals.Time + 0.1);

		DamageMode = DamageNo;

		Sound.EmitDyn( this, SoundChannel.Item, "squeek/sqk_blast1.wav", 1f, 0.5f, 0, Server.PitchNormal );

		SoundEnt.InsertSound( WorldSound.Combat, Origin, BasePlayerWeapon.SmallExplosionVolume, 3f );

		Util.BloodDrips( Origin, Vector3.Zero, BloodColor, 80 );

		var owner = _owner.IsValid() ? _owner : null;

		RadiusDamage( this, owner ?? this, Damage, ClassNone, DamageBlast );

		if ( owner is not null )
			Owner = owner;

		MonsterKilled( attacker, GibAlways );
	}

	public override void GibMonster()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, "common/bodysplat.wav", 0.75f, Server.AttenuationNormal, 0, 200 );
	}

	public void HuntThink()
	{
		if ( !IsInWorld )
		{
			SetTouch( null );
			Remove();
			return;
		}

		StudioFrameAdvance();
		NextThink = (float)(Globals.Time + 0.1);

		if ( Globals.Time >= _die )
		{
			AttackDir = Util.Normalize( Velocity );
			Health = -1f;
			Killed( this, 0 );
			return;
		}

		if ( WaterLevel != 0 )
		{
			if ( MoveType == MoveType.Bounce )
				MoveType = MoveType.Fly;

			Velocity *= 0.9f;
			Velocity = Velocity with { z = Velocity.z + 8f };
		}
		else
		{
			MoveType = MoveType.Bounce;
		}

		if ( _nextHunt > Globals.Time )
			return;

		_nextHunt = (float)(Globals.Time + 2.0);

		MakeVectors( Angles );

		if ( EnemyEntity is null || !EnemyEntity.IsAlive )
		{
			Look( 512 );
			EnemyEntity = BestVisibleEnemy();
		}

		if ( _die - Globals.Time <= 0.5 && _die - Globals.Time >= 0.3 )
		{
			Sound.EmitDyn( this, SoundChannel.Voice, "squeek/sqk_die1.wav", 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( 0, 0x3F ) );
			SoundEnt.InsertSound( WorldSound.Combat, Origin, 256, 0.25f );
		}

		if ( EnemyEntity is { } enemy )
		{
			if ( IsVisible( enemy ) )
				_target = Util.Normalize( enemy.EyePosition - Origin );

			var speed = MathLib.Length( Velocity );
			var adjust = (float)(50.0 / (speed + 10.0));

			if ( adjust > 1.2 )
				adjust = 1.2f;

			Velocity = (Velocity * adjust) + (_target * 300f);
		}

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			AVelocity = Vector3.Zero;
		}
		else if ( MathLib.VectorCompare( AVelocity, Vector3.Zero ) )
		{
			AVelocity = AVelocity with { x = Server.RandomFloat( -100f, 100f ), z = Server.RandomFloat( -100f, 100f ) };
		}

		if ( MathLib.Length( Origin - _previousPosition ) < 1.0 )
			Velocity = Velocity with { x = Server.RandomFloat( -100f, 100f ), y = Server.RandomFloat( -100f, 100f ) };

		_previousPosition = Origin;

		Angles = MathLib.VectorAngles( Velocity ) with { x = 0f, z = 0f };
	}

	public void SuperBounceTouch( BaseEntity other )
	{
		var trace = Globals.Trace;

		if ( Owner is not null && other == Owner )
			return;

		Owner = null;

		Angles = Angles with { x = 0f, z = 0f };

		if ( _nextHit > Globals.Time )
			return;

		var pitch = (float)(155.0 - (60.0 * ((_die - Globals.Time) / DetonateDelay)));

		if ( other.DamageMode != DamageNo && NextAttack < Globals.Time && (other.EntityFlags & EntityFlags.WorldBrush) != EntityFlags.WorldBrush )
		{
			if ( trace.Ent == other && trace.Ent.ModelIndex != ModelIndex )
			{
				var owner = _owner.IsValid() ? _owner : null;

				Combat.ClearMultiDamage();
				other.TraceAttack( this, HalfLifeDll.SkillData.SnarkDmgBite, Globals.Forward, trace, DamageSlash );
				Combat.ApplyMultiDamage( this, owner ?? this );

				Damage += HalfLifeDll.SkillData.SnarkDmgPop;

				Sound.EmitDyn( this, SoundChannel.Weapon, "squeek/sqk_deploy1.wav", 1f, Server.AttenuationNormal, 0, (int)pitch );
				NextAttack = (float)(Globals.Time + 0.5);
			}
		}

		_nextHit = (float)(Globals.Time + 0.1);
		_nextHunt = Globals.Time;

		if ( HalfLifeDll.GameRules.IsMultiplayer() && Globals.Time < _nextBounceSoundTime )
			return;

		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
		{
			var random = Server.RandomFloat( 0f, 1f );
			var sample = random <= 0.33 ? "squeek/sqk_hunt1.wav" : random <= 0.66 ? "squeek/sqk_hunt2.wav" : "squeek/sqk_hunt3.wav";

			Sound.EmitDyn( this, SoundChannel.Voice, sample, 1f, Server.AttenuationNormal, 0, (int)pitch );
			SoundEnt.InsertSound( WorldSound.Combat, Origin, 256, 0.25f );
		}
		else
		{
			SoundEnt.InsertSound( WorldSound.Combat, Origin, 100, 0.1f );
		}

		_nextBounceSoundTime = (float)(Globals.Time + 0.5);
	}
}

[LinkEntityToClass( "weapon_snark" )]
public class Squeak : BasePlayerWeapon
{
	public const int WeaponId = 15;
	public const int WeaponWeight = 5;
	public const int MaxCarry = 15;
	public const int DefaultGive = 5;

	private const int AnimIdle1 = 0;
	private const int AnimFidgetFit = 1;
	private const int AnimFidgetNip = 2;
	private const int AnimDown = 3;
	private const int AnimUp = 4;

	private static readonly Vector3 DuckOffset = new Vector3( -16f, -16f, -36f ) - new Vector3( -16f, -16f, -18f );

	private bool _justThrown;
	private int _snarkFire;

	public override int ItemSlot => 5;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_sqknest.mdl" );

		FallInit();

		DefaultAmmo = DefaultGive;

		Sequence = 1;
		AnimTime = Globals.Time;
		FrameRate = 1f;
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_sqknest.mdl" );
		PrecacheModel( "models/v_squeak.mdl" );
		PrecacheModel( "models/p_squeak.mdl" );
		PrecacheSound( "squeek/sqk_hunt2.wav" );
		PrecacheSound( "squeek/sqk_hunt3.wav" );
		if ( !ClientSide )
			Util.PrecacheOther( "monster_snark" );

		_snarkFire = PrecacheEvent( 1, "events/snarkfire.sc" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "Snarks",
			MaxAmmo1 = MaxCarry,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 4,
			Position = 3,
			Id = WeaponId,
			Weight = WeaponWeight,
			Flags = ItemFlags.LimitInWorld | ItemFlags.Exhaustible
		};

		return true;
	}

	public override bool Deploy()
	{
		EmitHunt( 100 );

		Player.WeaponVolume = QuietGunVolume;

		return DefaultDeploy( "models/v_squeak.mdl", "models/p_squeak.mdl", AnimUp, "squeak" );
	}

	private void EmitHunt( int pitch )
	{
		var sample = Server.RandomFloat( 0f, 1f ) <= 0.5 ? "squeek/sqk_hunt2.wav" : "squeek/sqk_hunt3.wav";

		Sound.EmitDyn( this, SoundChannel.Voice, sample, 1f, Server.AttenuationNormal, 0, pitch );
	}

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);

		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
		{
			Player.Weapons &= ~(1 << WeaponId);
			SetThink( DestroyItem );
			NextThink = (float)(Globals.Time + 0.1);
			return;
		}

		SendWeaponAnim( AnimDown );
		Sound.Emit( Player, SoundChannel.Weapon, "common/null.wav", 1f, Server.AttenuationNormal );
	}

	public override void PrimaryAttack()
	{
		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
			return;

		MathLib.AngleVectors( Player.ViewAngle with { x = 0f }, out var entityForward, out _, out _ );
		MathLib.AngleVectors( Player.ViewAngle, out var forward, out _, out _ );

		var aimDownFraction = Player.ViewAngle.x > 0f ? Player.ViewAngle.x / 90f : 0f;
		var traceOrigin = Player.Origin;

		if ( (Player.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking )
			traceOrigin -= (aimDownFraction + 1f) * DuckOffset;

		var traceForward = (aimDownFraction * entityForward) + ((1f - aimDownFraction) * forward);
		var trace = TraceLine( traceOrigin + (traceForward * 24f), traceOrigin + (forward * 60f), DontIgnoreMonsters, null );

		PlaybackEvent( EventFlags.NotHost, Player, _snarkFire, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, 0, 0, 0, 0 );

		if ( trace.AllSolid || trace.StartSolid || trace.Fraction <= 0f )
			return;

		Player.SetAnimation( PlayerAnim.Attack1 );

		if ( !ClientSide )
		{
			var squeak = Create( "monster_snark", trace.EndPos, Player.ViewAngle, Player );

			squeak.Velocity = (traceForward * 200f) + Player.Velocity;
		}

		EmitHunt( 105 );

		Player.WeaponVolume = QuietGunVolume;

		Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );

		_justThrown = true;

		NextPrimaryAttack = GetNextAttackDelay( 0.3f );
		TimeWeaponIdle = (float)(WeaponTimeBase + 1.0);
	}

	public override void SecondaryAttack()
	{
	}

	public override void WeaponIdle()
	{
		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		if ( _justThrown )
		{
			_justThrown = false;

			if ( Player.AmmoSlot( PrimaryAmmoIndex ) == 0 )
			{
				RetireWeapon();
				return;
			}

			SendWeaponAnim( AnimUp );
			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
			return;
		}

		int animation;
		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.75 )
		{
			animation = AnimIdle1;
			TimeWeaponIdle = (float)(WeaponTimeBase + (30.0 / 16 * 2));
		}
		else if ( random <= 0.875 )
		{
			animation = AnimFidgetFit;
			TimeWeaponIdle = (float)(WeaponTimeBase + (70.0 / 16.0));
		}
		else
		{
			animation = AnimFidgetNip;
			TimeWeaponIdle = (float)(WeaponTimeBase + (80.0 / 16.0));
		}

		SendWeaponAnim( animation );
	}
}
