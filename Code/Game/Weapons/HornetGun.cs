using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_hornetgun" )]
public class HornetGun : BasePlayerWeapon
{
	public const int WeaponId = 11;
	public const int WeaponWeight = 15;
	public const int MaxCarryHornet = 8;
	public const int DefaultGive = 8;

	private const int AnimIdle1 = 0;
	private const int AnimFidgetSway = 1;
	private const int AnimFidgetShake = 2;
	private const int AnimDown = 3;
	private const int AnimUp = 4;

	private const int FireModeTrack = 0;
	private const int FireModeFast = 1;

	private float _rechargeTime;
	private int _firePhase;
	private int _hornetFire;

	public override int ItemSlot => 4;

	public override bool UseDecrement => true;

	private float RechargeTime => MaxClients > 1 ? 0.3f : 0.5f;

	public override bool IsUseable() => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_hgun.mdl" );

		DefaultAmmo = DefaultGive;
		_firePhase = 0;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_hgun.mdl" );
		PrecacheModel( "models/w_hgun.mdl" );
		PrecacheModel( "models/p_hgun.mdl" );

		_hornetFire = PrecacheEvent( 1, "events/firehornet.sc" );

		if ( !ClientSide )
			Util.PrecacheOther( "hornet" );
	}

	public override bool AddToPlayer( Player player )
	{
		if ( !base.AddToPlayer( player ) )
			return false;

		if ( HalfLifeDll.GameRules.IsMultiplayer() )
			player.Ammo[PrimaryAmmoIndex] = MaxCarryHornet;

		SendWeaponPickup( player );

		return true;
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "Hornets",
			MaxAmmo1 = MaxCarryHornet,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 3,
			Position = 3,
			Id = WeaponId,
			Flags = ItemFlags.NoAutoSwitchEmpty | ItemFlags.NoAutoReload,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool Deploy()
	{
		return DefaultDeploy( "models/v_hgun.mdl", "models/p_hgun.mdl", AnimUp, "hive" );
	}

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);
		SendWeaponAnim( AnimDown );

		if ( Player.AmmoSlot( PrimaryAmmoIndex ) == 0 )
			Player.SetAmmoSlot( PrimaryAmmoIndex, 1 );
	}

	public override void PrimaryAttack()
	{
		Reload();

		if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			return;

		if ( !ClientSide )
		{
			MakeVectors( Player.ViewAngle );

			var source = Player.GetGunPosition() + (Globals.Forward * 16f) + (Globals.Right * 8f) + (Globals.Up * -12f);
			var hornet = Create( "hornet", source, Player.ViewAngle, Player );

			hornet.Velocity = Globals.Forward * 300f;

			_rechargeTime = Globals.Time + RechargeTime;
		}

		Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );

		Player.WeaponVolume = QuietGunVolume;
		Player.WeaponFlash = DimGunFlash;

		PlaybackEvent( EventFlags.NotHost, Player, _hornetFire, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, FireModeTrack, 0, 0, 0 );

		Player.SetAnimation( PlayerAnim.Attack1 );

		NextPrimaryAttack = GetNextAttackDelay( 0.25f );

		if ( NextPrimaryAttack < WeaponTimeBase )
			NextPrimaryAttack = (float)(WeaponTimeBase + 0.25);

		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
			NextPrimaryAttack += RechargeTime;

		TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void SecondaryAttack()
	{
		Reload();

		if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			return;

		if ( !ClientSide )
		{
			MakeVectors( Player.ViewAngle );

			var forward = Globals.Forward;
			var right = Globals.Right;
			var up = Globals.Up;
			var source = Player.GetGunPosition() + (forward * 16f) + (right * 8f) + (up * -12f);

			_firePhase++;

			switch ( _firePhase )
			{
				case 1:
					source += up * 8f;
					break;
				case 2:
					source += up * 8f;
					source += right * 8f;
					break;
				case 3:
					source += right * 8f;
					break;
				case 4:
					source += up * -8f;
					source += right * 8f;
					break;
				case 5:
					source += up * -8f;
					break;
				case 6:
					source += up * -8f;
					source += right * -8f;
					break;
				case 7:
					source += right * -8f;
					break;
				case 8:
					source += up * 8f;
					source += right * -8f;
					_firePhase = 0;
					break;
			}

			var hornet = (Hornet)Create( "hornet", source, Player.ViewAngle, Player );

			hornet.Velocity = forward * 1200f;
			hornet.Angles = MathLib.VectorAngles( hornet.Velocity );

			hornet.SetThink( hornet.StartDart );

			_rechargeTime = Globals.Time + RechargeTime;
		}

		PlaybackEvent( EventFlags.NotHost, Player, _hornetFire, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, FireModeFast, 0, 0, 0 );

		Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );
		Player.WeaponVolume = NormalGunVolume;
		Player.WeaponFlash = DimGunFlash;

		Player.SetAnimation( PlayerAnim.Attack1 );

		NextPrimaryAttack = NextSecondaryAttack = (float)(WeaponTimeBase + 0.1);

		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
		{
			_rechargeTime = (float)(Globals.Time + 0.5);
			NextSecondaryAttack += 0.5f;
			NextPrimaryAttack += 0.5f;
		}

		TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void Reload()
	{
		if ( Player.AmmoSlot( PrimaryAmmoType ) >= MaxCarryHornet )
			return;

		while ( Player.AmmoSlot( PrimaryAmmoType ) < MaxCarryHornet && _rechargeTime < Globals.Time )
		{
			Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) + 1 );
			_rechargeTime += RechargeTime;
		}
	}

	public override void WeaponIdle()
	{
		Reload();

		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		int animation;
		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.75 )
		{
			animation = AnimIdle1;
			TimeWeaponIdle = (float)(WeaponTimeBase + (30.0 / 16 * 2));
		}
		else if ( random <= 0.875 )
		{
			animation = AnimFidgetSway;
			TimeWeaponIdle = (float)(WeaponTimeBase + (40.0 / 16.0));
		}
		else
		{
			animation = AnimFidgetShake;
			TimeWeaponIdle = (float)(WeaponTimeBase + (35.0 / 16.0));
		}

		SendWeaponAnim( animation );
	}
}
