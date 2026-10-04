using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

[LinkEntityToClass( "laser_spot" )]
public class LaserSpot : BaseEntity
{
	public override ObjectCaps ObjectCaps => ObjectCaps.DontSave;

	public static LaserSpot CreateSpot( Server server )
	{
		var spot = server.CreateEntity<LaserSpot>();

		spot.Spawn();

		spot.ClassName = "laser_spot";

		return spot;
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.None;
		Solid = Solid.Not;

		RenderMode = RenderMode.Glow;
		RenderFx = RenderFx.NoDissipation;
		RenderAmt = 255f;

		SetModel( "sprites/laserdot.spr" );
		SetOrigin( Origin );
	}

	public void Suspend( float suspendTime )
	{
		Effects |= Effects.NoDraw;

		SetThink( Revive );
		NextThink = Globals.Time + suspendTime;
	}

	public void Revive()
	{
		Effects &= ~Effects.NoDraw;

		SetThink( null );
	}

	public override void Precache()
	{
		PrecacheModel( "sprites/laserdot.spr" );
	}
}

[LinkEntityToClass( "rpg_rocket" )]
public class RpgRocket : Grenade
{
	private int _trail;

	[Save( FieldType.Time )]
	private float _igniteTime;
	[Save( FieldType.EHandle )]
	private BaseEntity _launcher;

	public static RpgRocket CreateRpgRocket( Vector3 origin, Vector3 angles, BaseEntity owner, Rpg launcher )
	{
		var rocket = owner.Server.CreateEntity<RpgRocket>();

		rocket.SetOrigin( origin );
		rocket.Angles = angles;
		rocket.Spawn();
		rocket.SetTouch( rocket.RocketTouch );

		launcher.ActiveRockets++;
		rocket._launcher = launcher;
		rocket.Owner = owner;

		return rocket;
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Bounce;
		Solid = Solid.BBox;

		SetModel( "models/rpgrocket.mdl" );
		SetSize( Vector3.Zero, Vector3.Zero );
		SetOrigin( Origin );

		ClassName = "rpg_rocket";

		SetThink( IgniteThink );
		SetTouch( ExplodeTouch );

		Angles = Angles with { x = Angles.x - 30f };
		MakeVectors( Angles );
		Angles = Angles with { x = -(Angles.x + 30f) };

		Velocity = Globals.Forward * 250f;
		Gravity = 0.5f;

		NextThink = (float)(Globals.Time + 0.4);

		Damage = HalfLifeDll.SkillData.PlrDmgRPG;
	}

	public void RocketTouch( BaseEntity other )
	{
		ReleaseLauncher();

		Sound.Stop( this, SoundChannel.Voice, "weapons/rocket1.wav" );
		ExplodeTouch( other );
	}

	public override void Explode( in Trace trace, int damageType )
	{
		Sound.Stop( this, SoundChannel.Voice, "weapons/rocket1.wav" );

		ReleaseLauncher();

		base.Explode( trace, damageType );
	}

	public override void Precache()
	{
		PrecacheModel( "models/rpgrocket.mdl" );
		_trail = PrecacheModel( "sprites/smoke.spr" );
		PrecacheSound( "weapons/rocket1.wav" );
	}

	public void IgniteThink()
	{
		MoveType = MoveType.Fly;
		Effects |= Effects.Light;

		Sound.Emit( this, SoundChannel.Voice, "weapons/rocket1.wav", 1f, 0.5f );

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamFollow );
		Server.WriteShort( Index );
		Server.WriteShort( _trail );
		Server.WriteByte( 40 );
		Server.WriteByte( 5 );
		Server.WriteByte( 224 );
		Server.WriteByte( 224 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.MessageEnd();

		_igniteTime = Globals.Time;

		SetThink( FollowThink );
		NextThink = (float)(Globals.Time + 0.1);
	}

	public Rpg GetLauncher() => _launcher.IsValid() ? (Rpg)_launcher : null;

	private void ReleaseLauncher()
	{
		if ( GetLauncher() is not { } launcher )
			return;

		launcher.ActiveRockets--;
		_launcher = null;
	}

	public void FollowThink()
	{
		BaseEntity other = null;

		Util.MakeAimVectors( Angles, Globals );

		var target = Globals.Forward;
		var max = 4096f;

		while ( (other = Util.FindEntityByClassname( other, "laser_spot" )) is not null )
		{
			var trace = TraceLine( Origin, other.Origin, DontIgnoreMonsters, this );

			if ( trace.Fraction < 0.90 )
				continue;

			var direction = other.Origin - Origin;
			var distance = MathLib.Length( direction );

			direction = Util.Normalize( direction );

			var dot = Vector3.Dot( Globals.Forward, direction );

			if ( dot > 0f && distance * (1f - dot) < max )
			{
				max = distance * (1f - dot);
				target = direction;
			}
		}

		Angles = MathLib.VectorAngles( target );

		var speed = MathLib.Length( Velocity );

		if ( Globals.Time - _igniteTime < 1.0 )
		{
			Velocity = (Velocity * 0.2f) + (target * (float)((speed * 0.8) + 400));

			if ( WaterLevel == 3 )
			{
				if ( MathLib.Length( Velocity ) > 300f )
					Velocity = Util.Normalize( Velocity ) * 300f;

				Util.BubbleTrail( Origin - (Velocity * 0.1f), Origin, 4 );
			}
			else if ( MathLib.Length( Velocity ) > 2000f )
			{
				Velocity = Util.Normalize( Velocity ) * 2000f;
			}
		}
		else
		{
			if ( (Effects & Effects.Light) == Effects.Light )
			{
				Effects = 0;
				Sound.Stop( this, SoundChannel.Voice, "weapons/rocket1.wav" );
			}

			Velocity = (Velocity * 0.2f) + (target * speed * 0.798f);

			if ( WaterLevel == 0 && MathLib.Length( Velocity ) < 1500f )
				Detonate();
		}

		if ( GetLauncher() is { } launcher )
		{
			var distance = MathLib.Length( Origin - launcher.Origin );

			if ( distance > 8192f || Globals.Time - _igniteTime > 6f )
			{
				launcher.ActiveRockets--;
				_launcher = null;
			}
		}

		if ( Util.PointContents( Origin ) == (int)Contents.Sky )
			Detonate();

		NextThink = (float)(Globals.Time + 0.1);
	}
}

[LinkEntityToClass( "weapon_rpg" )]
public class Rpg : BasePlayerWeapon
{
	public const int WeaponId = 8;
	public const int WeaponWeight = 20;
	public const int MaxClipSize = 1;
	public const int DefaultGive = 1;
	public const int MaxCarryRocket = 5;

	private const int AnimIdle = 0;
	private const int AnimFidget = 1;
	private const int AnimReload = 2;
	private const int AnimHolster1 = 4;
	private const int AnimDraw1 = 5;
	private const int AnimDrawUnloaded = 7;
	private const int AnimIdleUnloaded = 8;
	private const int AnimFidgetUnloaded = 9;

	[Save( FieldType.Integer )]
	public int ActiveRockets;

	[Save( FieldType.Integer )]
	public int SpotActive;

	private LaserSpot _spot;
	private int _rpg;

	public override int ItemSlot => 4;

	public override bool UseDecrement => true;

	public override bool ShouldWeaponIdle => true;

	public override void Reload()
	{
		if ( Clip == 1 )
			return;

		if ( Player.AmmoRockets <= 0 )
			return;

		NextPrimaryAttack = GetNextAttackDelay( 0.5f );

		if ( ActiveRockets != 0 && SpotActive != 0 )
			return;

		if ( !ClientSide )
		{
			if ( _spot is not null && SpotActive != 0 )
			{
				_spot.Suspend( 2.1f );
				NextSecondaryAttack = (float)(WeaponTimeBase + 2.1);
			}
		}

		if ( DefaultReload( MaxClipSize, AnimReload, 2f ) )
			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;

		SetModel( "models/w_rpg.mdl" );
		SpotActive = 1;

		DefaultAmmo = IsMultiplayer ? DefaultGive * 2 : DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_rpg.mdl" );
		PrecacheModel( "models/v_rpg.mdl" );
		PrecacheModel( "models/p_rpg.mdl" );

		PrecacheSound( "items/9mmclip1.wav" );

		if ( !ClientSide )
		{
			Util.PrecacheOther( "laser_spot" );
			Util.PrecacheOther( "rpg_rocket" );
		}

		PrecacheSound( "weapons/rocketfire1.wav" );
		PrecacheSound( "weapons/glauncher.wav" );

		_rpg = PrecacheEvent( 1, "events/rpg.sc" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "rockets",
			MaxAmmo1 = MaxCarryRocket,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = MaxClipSize,
			Slot = 3,
			Position = 0,
			Id = WeaponId,
			Flags = ItemFlags.NoAutoSwitchTo,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool AddToPlayer( Player player )
	{
		if ( !base.AddToPlayer( player ) )
			return false;

		SendWeaponPickup( player );

		return true;
	}

	public override bool Deploy()
	{
		return DefaultDeploy( "models/v_rpg.mdl", "models/p_rpg.mdl", Clip == 0 ? AnimDrawUnloaded : AnimDraw1, "rpg" );
	}

	public override bool CanHolster() => SpotActive == 0 || ActiveRockets == 0;

	public override void Holster( int skipLocal = 0 )
	{
		InReload = false;

		Player.NextAttack = (float)(WeaponTimeBase + 0.5);

		SendWeaponAnim( AnimHolster1 );

		if ( !ClientSide && _spot is not null )
		{
			_spot.Killed( null, GibNever );
			_spot = null;
		}
	}

	public override void PrimaryAttack()
	{
		if ( Clip != 0 )
		{
			Player.WeaponVolume = LoudGunVolume;
			Player.WeaponFlash = BrightGunFlash;

			if ( !ClientSide )
			{
				Player.SetAnimation( PlayerAnim.Attack1 );

				MakeVectors( Player.ViewAngle );

				var source = Player.GetGunPosition() + (Globals.Forward * 16f) + (Globals.Right * 8f) + (Globals.Up * -8f);

				var rocket = RpgRocket.CreateRpgRocket( source, Player.ViewAngle, Player, this );

				MakeVectors( Player.ViewAngle );
				rocket.Velocity += Globals.Forward * Vector3.Dot( Player.Velocity, Globals.Forward );
			}

			PlaybackEvent( EventFlags.NotHost, Player, _rpg, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, 0, 0, 0, 0 );

			Clip--;

			NextPrimaryAttack = GetNextAttackDelay( 1.5f );
			TimeWeaponIdle = (float)(WeaponTimeBase + 1.5);

			ResetEmptySound();
		}
		else
		{
			PlayEmptySound();
		}

		UpdateSpot();
	}

	public override void SecondaryAttack()
	{
		SpotActive = SpotActive == 0 ? 1 : 0;

		if ( !ClientSide && SpotActive == 0 && _spot is not null )
		{
			_spot.Killed( null, GibNormal );
			_spot = null;
		}

		NextSecondaryAttack = (float)(WeaponTimeBase + 0.2);
	}

	public override void WeaponIdle()
	{
		UpdateSpot();

		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		if ( Player.AmmoSlot( PrimaryAmmoType ) == 0 )
		{
			TimeWeaponIdle = WeaponTimeBase + 1f;
			return;
		}

		int animation;
		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.75 || SpotActive != 0 )
		{
			animation = Clip == 0 ? AnimIdleUnloaded : AnimIdle;
			TimeWeaponIdle = (float)(WeaponTimeBase + (90.0 / 15.0));
		}
		else
		{
			animation = Clip == 0 ? AnimFidgetUnloaded : AnimFidget;
			TimeWeaponIdle = (float)(WeaponTimeBase + 3.0);
		}

		ResetEmptySound();
		SendWeaponAnim( animation );
	}

	public void UpdateSpot()
	{
		if ( ClientSide || SpotActive == 0 )
			return;

		_spot ??= LaserSpot.CreateSpot( Server );

		MakeVectors( Player.ViewAngle );

		var source = Player.GetGunPosition();
		var aiming = Globals.Forward;

		var trace = TraceLine( source, source + (aiming * 8192f), DontIgnoreMonsters, Player );

		Server.SetOrigin( _spot, trace.EndPos );
	}
}

[LinkEntityToClass( "ammo_rpgclip" )]
public class RpgAmmo : BasePlayerAmmo
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_rpgammo.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_rpgammo.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		var give = HalfLifeDll.GameRules.IsMultiplayer() ? Rpg.MaxClipSize * 2 : Rpg.MaxClipSize;

		if ( other.GiveAmmo( give, "rockets", Rpg.MaxCarryRocket ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
