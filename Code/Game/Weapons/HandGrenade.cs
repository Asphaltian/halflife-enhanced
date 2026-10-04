using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_handgrenade" )]
public class HandGrenade : BasePlayerWeapon
{
	public const int WeaponId = 12;
	public const int WeaponWeight = 5;
	public const int MaxCarry = 10;
	public const int DefaultGive = 5;

	private const int AnimIdle = 0;
	private const int AnimFidget = 1;
	private const int AnimPinPull = 2;
	private const int AnimThrow1 = 3;
	private const int AnimThrow2 = 4;
	private const int AnimThrow3 = 5;
	private const int AnimHolster = 6;
	private const int AnimDraw = 7;

	private const float Multiplier = 6.5f;

	public override int ItemSlot => 5;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_grenade.mdl" );

		if ( !ClientSide )
			Damage = HalfLifeDll.SkillData.PlrDmgHandGrenade;

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_grenade.mdl" );
		PrecacheModel( "models/v_grenade.mdl" );
		PrecacheModel( "models/p_grenade.mdl" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "Hand Grenade",
			MaxAmmo1 = MaxCarry,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 4,
			Position = 0,
			Id = WeaponId,
			Weight = WeaponWeight,
			Flags = ItemFlags.LimitInWorld | ItemFlags.Exhaustible
		};

		return true;
	}

	public override bool Deploy()
	{
		ReleaseThrow = -1f;

		return DefaultDeploy( "models/v_grenade.mdl", "models/p_grenade.mdl", AnimDraw, "crowbar" );
	}

	public override bool CanHolster() => StartThrow == 0f;

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);

		if ( Player.AmmoSlot( PrimaryAmmoType ) != 0 )
		{
			SendWeaponAnim( AnimHolster );
		}
		else
		{
			Player.Weapons &= ~(1 << WeaponId);
			SetThink( DestroyItem );
			NextThink = (float)(Globals.Time + 0.1);
		}

		Sound.Emit( Player, SoundChannel.Weapon, "common/null.wav", 1f, Server.AttenuationNormal );
	}

	public override void PrimaryAttack()
	{
		if ( StartThrow != 0f || Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			return;

		StartThrow = Globals.Time;
		ReleaseThrow = 0f;

		SendWeaponAnim( AnimPinPull );
		TimeWeaponIdle = (float)(WeaponTimeBase + 0.5);
	}

	public override void WeaponIdle()
	{
		if ( ReleaseThrow == 0f && StartThrow != 0f )
			ReleaseThrow = Globals.Time;

		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		if ( StartThrow != 0f )
		{
			var throwAngle = Player.ViewAngle + Player.PunchAngle;

			if ( throwAngle.x < 0f )
				throwAngle.x = (float)(-10 + (throwAngle.x * ((90 - 10) / 90.0)));
			else
				throwAngle.x = (float)(-10 + (throwAngle.x * ((90 + 10) / 90.0)));

			var speed = (90f - throwAngle.x) * Multiplier;

			if ( speed > 1000f )
				speed = 1000f;

			MakeVectors( throwAngle );

			var source = Player.Origin + Player.ViewOffset + (Globals.Forward * 16f);
			var velocity = (Globals.Forward * speed) + Player.Velocity;

			var time = (float)(StartThrow - Globals.Time + 3.0);

			if ( time < 0f )
				time = 0f;

			Grenade.ShootTimed( Player, source, velocity, time );

			if ( speed < 500f )
				SendWeaponAnim( AnimThrow1 );
			else if ( speed < 1000f )
				SendWeaponAnim( AnimThrow2 );
			else
				SendWeaponAnim( AnimThrow3 );

			Player.SetAnimation( PlayerAnim.Attack1 );

			ReleaseThrow = 0f;
			StartThrow = 0f;
			NextPrimaryAttack = GetNextAttackDelay( 0.5f );
			TimeWeaponIdle = (float)(WeaponTimeBase + 0.5);

			Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );

			if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
				TimeWeaponIdle = NextSecondaryAttack = NextPrimaryAttack = GetNextAttackDelay( 0.5f );

			return;
		}

		if ( ReleaseThrow > 0f )
		{
			StartThrow = 0f;

			if ( Player.AmmoSlot( PrimaryAmmoType ) != 0 )
			{
				SendWeaponAnim( AnimDraw );
			}
			else
			{
				RetireWeapon();
				return;
			}

			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
			ReleaseThrow = -1f;

			return;
		}

		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
			return;

		int animation;
		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.75 )
		{
			animation = AnimIdle;
			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
		}
		else
		{
			animation = AnimFidget;
			TimeWeaponIdle = (float)(WeaponTimeBase + (75.0 / 30.0));
		}

		SendWeaponAnim( animation );
	}
}
