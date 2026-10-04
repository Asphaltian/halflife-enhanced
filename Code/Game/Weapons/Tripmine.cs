using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_tripmine" )]
public class Tripmine : BasePlayerWeapon
{
	public const int WeaponId = 13;
	public const int WeaponWeight = -10;
	public const int MaxCarry = 5;
	public const int DefaultGive = 1;

	private const int AnimIdle1 = 0;
	private const int AnimIdle2 = 1;
	private const int AnimFidget = 4;
	private const int AnimHolster = 5;
	private const int AnimDraw = 6;
	private const int AnimGround = 8;

	private int _tripFire;

	public override int ItemSlot => 5;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/v_tripmine.mdl" );
		Frame = 0f;
		Body = 3;
		Sequence = AnimGround;
		FrameRate = 0f;

		FallInit();

		DefaultAmmo = DefaultGive;

		if ( ClientSide ? !ClientWeapons.IsMultiplayer : !HalfLifeDll.GameRules.IsDeathmatch() )
			SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 28f ) );
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_tripmine.mdl" );
		PrecacheModel( "models/p_tripmine.mdl" );
		if ( !ClientSide )
			Util.PrecacheOther( "monster_tripmine" );

		_tripFire = PrecacheEvent( 1, "events/tripfire.sc" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "Trip Mine",
			MaxAmmo1 = MaxCarry,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 4,
			Position = 2,
			Id = WeaponId,
			Weight = WeaponWeight,
			Flags = ItemFlags.LimitInWorld | ItemFlags.Exhaustible
		};

		return true;
	}

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -16f, -16f, -5f );
		AbsMax = Origin + new Vector3( 16f, 16f, 28f );
	}

	public override bool Deploy()
	{
		Body = 0;

		return DefaultDeploy( "models/v_tripmine.mdl", "models/p_tripmine.mdl", AnimDraw, "trip" );
	}

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);

		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
		{
			Player.Weapons &= ~(1 << WeaponId);
			SetThink( DestroyItem );
			NextThink = (float)(Globals.Time + 0.1);
		}

		SendWeaponAnim( AnimHolster );
		Sound.Emit( Player, SoundChannel.Weapon, "common/null.wav", 1f, Server.AttenuationNormal );
	}

	public override void PrimaryAttack()
	{
		if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			return;

		MakeVectors( Player.ViewAngle + Player.PunchAngle );

		var source = Player.GetGunPosition();
		var aiming = Globals.Forward;

		var trace = TraceLine( source, source + (aiming * 128f), DontIgnoreMonsters, Player );

		PlaybackEvent( EventFlags.NotHost, Player, _tripFire, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, 0, 0, 0, 0 );

		if ( trace.Fraction < 1.0 && trace.Ent is BaseEntity entity && (entity.EntityFlags & EntityFlags.Conveyor) != EntityFlags.Conveyor )
		{
			var angles = MathLib.VectorAngles( trace.PlaneNormal );

			Create( "monster_tripmine", trace.EndPos + (trace.PlaneNormal * 8f), angles, Player );

			Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );

			Player.SetAnimation( PlayerAnim.Attack1 );

			if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			{
				RetireWeapon();
				return;
			}
		}

		NextPrimaryAttack = GetNextAttackDelay( 0.3f );
		TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void WeaponIdle()
	{
		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		if ( Player.AmmoSlot( PrimaryAmmoType ) > 0 )
		{
			SendWeaponAnim( AnimDraw );
		}
		else
		{
			RetireWeapon();
			return;
		}

		int animation;
		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.25 )
		{
			animation = AnimIdle1;
			TimeWeaponIdle = (float)(WeaponTimeBase + (90.0 / 30.0));
		}
		else if ( random <= 0.75 )
		{
			animation = AnimIdle2;
			TimeWeaponIdle = (float)(WeaponTimeBase + (60.0 / 30.0));
		}
		else
		{
			animation = AnimFidget;
			TimeWeaponIdle = (float)(WeaponTimeBase + (100.0 / 30.0));
		}

		SendWeaponAnim( animation );
	}
}
