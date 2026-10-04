using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_satchel" )]
public class SatchelCharge : Grenade
{
	public static void DeactivateSatchels( Player owner )
	{
		for ( var find = Util.FindEntityByClassname( null, "monster_satchel" ); find is not null; find = Util.FindEntityByClassname( find, "monster_satchel" ) )
		{
			if ( find is SatchelCharge satchel && satchel.Owner == owner )
				satchel.Deactivate();
		}
	}

	public void Deactivate()
	{
		Solid = Solid.Not;
		Remove();
	}

	private static readonly string[] BounceSounds =
	[
		"weapons/g_bounce1.wav",
		"weapons/g_bounce2.wav",
		"weapons/g_bounce3.wav"
	];

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Bounce;
		Solid = Solid.BBox;

		SetModel( "models/w_satchel.mdl" );
		SetSize( new Vector3( -4f, -4f, -4f ), new Vector3( 4f, 4f, 4f ) );
		SetOrigin( Origin );

		SetTouch( SatchelSlide );
		SetUse( DetonateUse );
		SetThink( SatchelThink );
		NextThink = (float)(Globals.Time + 0.1);

		Gravity = 0.5f;
		Friction = 0.8f;

		Damage = HalfLifeDll.SkillData.PlrDmgSatchel;
		Sequence = 1;
	}

	public void SatchelSlide( BaseEntity other )
	{
		if ( other == Owner )
			return;

		Gravity = 1f;

		var trace = TraceLine( Origin, Origin - new Vector3( 0f, 0f, 10f ), IgnoreMonsters, this );

		if ( trace.Fraction < 1.0 )
		{
			Velocity *= 0.95f;
			AVelocity *= 0.9f;
		}

		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround && MathLib.Length2D( Velocity ) > 10f )
			BounceSound();

		StudioFrameAdvance();
	}

	public void SatchelThink()
	{
		StudioFrameAdvance();
		NextThink = (float)(Globals.Time + 0.1);

		if ( !IsInWorld )
		{
			Remove();
			return;
		}

		if ( WaterLevel == 3 )
		{
			MoveType = MoveType.Fly;
			Velocity *= 0.8f;
			AVelocity *= 0.9f;
			Velocity = Velocity with { z = Velocity.z + 8f };
		}
		else if ( WaterLevel == 0 )
		{
			MoveType = MoveType.Bounce;
		}
		else
		{
			Velocity = Velocity with { z = Velocity.z - 8f };
		}
	}

	public override void Precache()
	{
		PrecacheModel( "models/grenade.mdl" );
		PrecacheSounds( BounceSounds );
	}

	public override void BounceSound()
	{
		Sound.Emit( this, SoundChannel.Voice, BounceSounds[Server.RandomLong( 0, 2 )], 1f, Server.AttenuationNormal );
	}
}

[LinkEntityToClass( "weapon_satchel" )]
public class Satchel : BasePlayerWeapon
{
	public const int WeaponId = 14;
	public const int WeaponWeight = -10;
	public const int MaxCarry = 5;
	public const int DefaultGive = 1;

	private const int AnimFidget1 = 1;
	private const int AnimDraw = 2;
	private const int AnimDrop = 3;

	private const int AnimRadioFidget1 = 1;
	private const int AnimRadioDraw = 2;
	private const int AnimRadioFire = 3;
	private const int AnimRadioHolster = 4;

	[Save( FieldType.Integer )]
	public override int ChargeReady { get; set; }

	public override int ItemSlot => 5;

	public override bool UseDecrement => true;

	public override bool AddDuplicate( BasePlayerItem original )
	{
		if ( HalfLifeDll.GameRules.IsMultiplayer() )
		{
			var satchel = (Satchel)original;

			if ( original.Player is null )
				return true;

			var inPocket = satchel.Player.Ammo[satchel.PrimaryAmmoIndex];
			var deployed = 0;
			BaseEntity live = null;

			while ( (live = Util.FindEntityInSphere( live, original.Player.Origin, 4096f )) is not null )
			{
				if ( live.ClassName == "monster_satchel" && live.Owner == original.Player )
					deployed++;
			}

			if ( satchel.ChargeReady != 0 && inPocket + deployed >= MaxCarry )
				return false;
		}

		return base.AddDuplicate( original );
	}

	public override bool AddToPlayer( Player player )
	{
		var result = ItemAddToPlayer( player );

		player.Weapons |= 1 << Id;
		ChargeReady = 0;

		return result && AddWeapon();
	}

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_satchel.mdl" );

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_satchel.mdl" );
		PrecacheModel( "models/v_satchel_radio.mdl" );
		PrecacheModel( "models/w_satchel.mdl" );
		PrecacheModel( "models/p_satchel.mdl" );
		PrecacheModel( "models/p_satchel_radio.mdl" );

		if ( !ClientSide )
			Util.PrecacheOther( "monster_satchel" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "Satchel Charge",
			MaxAmmo1 = MaxCarry,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 4,
			Position = 1,
			Flags = ItemFlags.SelectOnEmpty | ItemFlags.LimitInWorld | ItemFlags.Exhaustible,
			Id = WeaponId,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool IsUseable() => Player.AmmoSlot( PrimaryAmmoIndex ) > 0 || ChargeReady != 0;

	public override bool CanDeploy() => Player.AmmoSlot( PrimaryAmmoIndex ) > 0 || ChargeReady != 0;

	public override bool Deploy()
	{
		Player.NextAttack = (float)(WeaponTimeBase + 1.0);
		TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );

		return ChargeReady != 0
			? DefaultDeploy( "models/v_satchel_radio.mdl", "models/p_satchel_radio.mdl", AnimRadioDraw, "hive" )
			: DefaultDeploy( "models/v_satchel.mdl", "models/p_satchel.mdl", AnimDraw, "trip" );
	}

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);

		SendWeaponAnim( ChargeReady != 0 ? AnimRadioHolster : AnimDrop );

		Sound.Emit( Player, SoundChannel.Weapon, "common/null.wav", 1f, Server.AttenuationNormal );

		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 && ChargeReady == 0 )
		{
			Player.Weapons &= ~(1 << WeaponId);
			SetThink( DestroyItem );
			NextThink = (float)(Globals.Time + 0.1);
		}
	}

	public override void PrimaryAttack()
	{
		if ( ChargeReady != 2 )
			Throw();
	}

	public override void SecondaryAttack()
	{
		if ( ChargeReady != 1 )
			return;

		SendWeaponAnim( AnimRadioFire );

		BaseEntity satchel = null;

		while ( !ClientSide && (satchel = Util.FindEntityInSphere( satchel, Player.Origin, 4096f )) is not null )
		{
			if ( satchel.ClassName == "monster_satchel" && satchel.Owner == Player )
			{
				satchel.Use( Player, Player, UseType.On, 0f );
				ChargeReady = 2;
			}
		}

		ChargeReady = 2;
		NextPrimaryAttack = GetNextAttackDelay( 0.5f );
		NextSecondaryAttack = (float)(WeaponTimeBase + 0.5);
		TimeWeaponIdle = (float)(WeaponTimeBase + 0.5);
	}

	public void Throw()
	{
		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
			return;

		var source = Player.Origin;
		var velocity = (Globals.Forward * 274f) + Player.Velocity;

		if ( ClientSide )
		{
			ClientWeapons.LoadViewModel( "models/v_satchel_radio.mdl", Player );
		}
		else
		{
			var satchel = Create( "monster_satchel", source, Vector3.Zero, Player );

			satchel.Velocity = velocity;
			satchel.AVelocity = satchel.AVelocity with { y = 400f };

			Player.ViewModel = "models/v_satchel_radio.mdl";
			Player.WeaponModel = "models/p_satchel_radio.mdl";
		}

		SendWeaponAnim( AnimRadioDraw );

		Player.SetAnimation( PlayerAnim.Attack1 );

		ChargeReady = 1;

		Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );

		NextPrimaryAttack = GetNextAttackDelay( 1f );
		NextSecondaryAttack = (float)(WeaponTimeBase + 0.5);
	}

	public override void WeaponIdle()
	{
		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		switch ( ChargeReady )
		{
			case 0:
				SendWeaponAnim( AnimFidget1 );
				Player.AnimExtension = "trip";
				break;
			case 1:
				SendWeaponAnim( AnimRadioFidget1 );
				Player.AnimExtension = "hive";
				break;
			case 2:
				if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
				{
					ChargeReady = 0;
					RetireWeapon();
					return;
				}

				if ( ClientSide )
				{
					ClientWeapons.LoadViewModel( "models/v_satchel.mdl", Player );
				}
				else
				{
					Player.ViewModel = "models/v_satchel.mdl";
					Player.WeaponModel = "models/p_satchel.mdl";
				}

				SendWeaponAnim( AnimDraw );

				Player.AnimExtension = "trip";

				NextPrimaryAttack = GetNextAttackDelay( 0.5f );
				NextSecondaryAttack = (float)(WeaponTimeBase + 0.5);
				ChargeReady = 0;
				break;
		}

		TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}
}
