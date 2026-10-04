using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_egon" )]
public class Egon : BasePlayerWeapon
{
	public const int WeaponId = 10;
	public const int WeaponWeight = 20;
	public const int DefaultGive = 20;

	private const int PrimaryVolume = 450;
	private const string BeamSprite = "sprites/xbeam1.spr";
	private const string FlareSprite = "sprites/XSpark1.spr";
	private const string SoundOff = "weapons/egon_off1.wav";
	private const string SoundRun = "weapons/egon_run3.wav";
	private const string SoundStartup = "weapons/egon_windup2.wav";

	private const float PulseInterval = 0.1f;
	private const float DischargeInterval = 0.1f;

	private const int AnimIdle1 = 0;
	private const int AnimFidget1 = 1;
	private const int AnimDraw = 9;
	private const int AnimHolster = 10;

	private const int FireOff = 0;
	private const int FireCharge = 1;

	private const int FireNarrow = 0;
	private const int FireWide = 1;

	[Save( FieldType.Integer )]
	public override int FireState { get; set; }

	[Save( FieldType.Integer )]
	private int _fireMode;
	[Save( FieldType.Time )]
	private float _shakeTime;
	[Save( FieldType.Time )]
	private float _ammoUseTime;

	private Beam _beam;
	private Beam _noise;
	private Sprite _sprite;
	private int _egonFire;
	private int _egonStop;

	public override int ItemSlot => 4;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_egon.mdl" );

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_egon.mdl" );
		PrecacheModel( "models/v_egon.mdl" );
		PrecacheModel( "models/p_egon.mdl" );

		PrecacheModel( "models/w_9mmclip.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );

		PrecacheSound( SoundOff );
		PrecacheSound( SoundRun );
		PrecacheSound( SoundStartup );

		PrecacheModel( BeamSprite );
		PrecacheModel( FlareSprite );

		PrecacheSound( "weapons/357_cock1.wav" );

		_egonFire = PrecacheEvent( 1, "events/egon_fire.sc" );
		_egonStop = PrecacheEvent( 1, "events/egon_stop.sc" );
	}

	public override bool Deploy()
	{
		FireState = FireOff;

		return DefaultDeploy( "models/v_egon.mdl", "models/p_egon.mdl", AnimDraw, "egon" );
	}

	public override bool AddToPlayer( Player player )
	{
		if ( !base.AddToPlayer( player ) )
			return false;

		SendWeaponPickup( player );

		return true;
	}

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);
		SendWeaponAnim( AnimHolster );

		EndAttack();
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "uranium",
			MaxAmmo1 = Gauss.MaxCarryUranium,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 3,
			Position = 2,
			Id = WeaponId,
			Flags = 0,
			Weight = WeaponWeight
		};

		return true;
	}

	private bool HasAmmo => Player.AmmoUranium > 0;

	public override bool CanHolster() => ClientSide || !MultiplayBusters.IsBustingGame();

	private void UseAmmo( int count )
	{
		if ( !ClientSide && MultiplayBusters.IsBustingGame() )
			return;

		if ( Player.AmmoSlot( PrimaryAmmoType ) >= count )
			Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - count );
		else
			Player.SetAmmoSlot( PrimaryAmmoType, 0 );
	}

	public void Attack()
	{
		if ( Player.WaterLevel == 3 )
		{
			if ( FireState != FireOff || _beam is not null )
				EndAttack();
			else
				PlayEmptySound();

			return;
		}

		MakeVectors( Player.ViewAngle + Player.PunchAngle );

		var aiming = Globals.Forward;
		var source = Player.GetGunPosition();

		switch ( FireState )
		{
			case FireOff:
				if ( !HasAmmo )
				{
					NextPrimaryAttack = NextSecondaryAttack = (float)(WeaponTimeBase + 0.25);
					PlayEmptySound();
					return;
				}

				_ammoUseTime = Globals.Time;

				PlaybackEvent( EventFlags.NotHost, Player, _egonFire, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, FireState, _fireMode, 1, 0 );

				_shakeTime = 0f;

				Player.WeaponVolume = PrimaryVolume;
				TimeWeaponIdle = (float)(WeaponTimeBase + 0.1);
				FUser1 = WeaponTimeBase + 2f;

				DamageTime = Globals.Time + PulseInterval;
				FireState = FireCharge;
				break;

			case FireCharge:
				Fire( source, aiming );
				Player.WeaponVolume = PrimaryVolume;

				if ( FUser1 <= WeaponTimeBase )
				{
					PlaybackEvent( EventFlags.NotHost, Player, _egonFire, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, FireState, _fireMode, 0, 0 );
					FUser1 = 1000f;
				}

				if ( !HasAmmo )
				{
					EndAttack();
					NextPrimaryAttack = NextSecondaryAttack = (float)(WeaponTimeBase + 1.0);
				}

				break;
		}
	}

	public override void PrimaryAttack()
	{
		_fireMode = FireWide;
		Attack();
	}

	public void Fire( Vector3 originalSource, Vector3 direction )
	{
		var destination = originalSource + (direction * 2048f);

		var trace = TraceLine( originalSource, destination, DontIgnoreMonsters, Player );

		if ( trace.AllSolid )
			return;

		var entity = trace.Ent as BaseEntity;

		if ( !ClientSide )
		{
			if ( entity is null )
				return;

			if ( HalfLifeDll.GameRules.IsMultiplayer() && _sprite is not null )
			{
				if ( entity.DamageMode != DamageNo )
					_sprite.Effects &= ~Effects.NoDraw;
				else
					_sprite.Effects |= Effects.NoDraw;
			}
		}

		var timeDistance = 0f;

		switch ( _fireMode )
		{
			case FireNarrow:
				if ( !ClientSide && DamageTime < Globals.Time )
				{
					Combat.ClearMultiDamage();

					ScenePhysicsDamage.MeleeImpact( trace, HalfLifeDll.SkillData.PlrDmgEgonNarrow, direction );

					if ( entity.DamageMode != DamageNo )
						entity.TraceAttack( Player, HalfLifeDll.SkillData.PlrDmgEgonNarrow, direction, trace, DamageEnergyBeam );

					Combat.ApplyMultiDamage( Player, Player );

					if ( Globals.Time >= _ammoUseTime )
					{
						UseAmmo( 1 );
						_ammoUseTime = (float)(Globals.Time + (HalfLifeDll.GameRules.IsMultiplayer() ? 0.1 : 0.166));
					}

					DamageTime = Globals.Time + PulseInterval;
				}

				timeDistance = (DamageTime - Globals.Time) / PulseInterval;
				break;

			case FireWide:
				if ( !ClientSide && DamageTime < Globals.Time )
				{
					Combat.ClearMultiDamage();

					ScenePhysicsDamage.MeleeImpact( trace, HalfLifeDll.SkillData.PlrDmgEgonWide, direction );

					if ( entity.DamageMode != DamageNo )
						entity.TraceAttack( Player, HalfLifeDll.SkillData.PlrDmgEgonWide, direction, trace, DamageEnergyBeam | DamageAlwaysGib );

					Combat.ApplyMultiDamage( Player, Player );

					if ( HalfLifeDll.GameRules.IsMultiplayer() )
						Combat.RadiusDamage( trace.EndPos, this, Player, HalfLifeDll.SkillData.PlrDmgEgonWide / 4, 128f, ClassNone, DamageEnergyBeam | DamageBlast | DamageAlwaysGib );

					if ( !Player.IsAlive )
						return;

					if ( Globals.Time >= _ammoUseTime )
					{
						UseAmmo( 1 );
						_ammoUseTime = (float)(Globals.Time + (HalfLifeDll.GameRules.IsMultiplayer() ? 0.2 : 0.1));
					}

					DamageTime = Globals.Time + DischargeInterval;

					if ( _shakeTime < Globals.Time )
					{
						Util.ScreenShake( trace.EndPos, 5f, 150f, 0.75f, 250f );
						_shakeTime = (float)(Globals.Time + 1.5);
					}
				}

				timeDistance = (DamageTime - Globals.Time) / DischargeInterval;
				break;
		}

		if ( timeDistance < 0f )
			timeDistance = 0f;
		else if ( timeDistance > 1f )
			timeDistance = 1f;

		timeDistance = 1f - timeDistance;

		UpdateEffect( trace.EndPos, timeDistance );
	}

	public void UpdateEffect( Vector3 endPoint, float timeBlend )
	{
		if ( ClientSide )
			return;

		if ( _beam is null )
			CreateEffect();

		_beam.SetStartPos( endPoint );
		_beam.SetBrightness( (int)(255 - (timeBlend * 180)) );
		_beam.SetWidth( (int)(40 - (timeBlend * 20)) );

		var blue = (int)(64 + (80 * Math.Abs( Math.Sin( Globals.Time * 10 ) )));

		if ( _fireMode == FireWide )
			_beam.SetColor( (int)(30 + (25 * timeBlend)), (int)(30 + (30 * timeBlend)), blue );
		else
			_beam.SetColor( (int)(60 + (25 * timeBlend)), (int)(120 + (30 * timeBlend)), blue );

		Server.SetOrigin( _sprite, endPoint );
		_sprite.Frame += 8 * Globals.FrameTime;

		if ( _sprite.Frame > _sprite.Frames )
			_sprite.Frame = 0f;

		_noise.SetStartPos( endPoint );
	}

	public void CreateEffect()
	{
		if ( ClientSide )
			return;

		DestroyEffect();

		_beam = Beam.BeamCreate( Server, BeamSprite, 40 );
		_beam.PointEntInit( Origin, Player.Index );
		_beam.SetBeamFlags( (int)BeamFlags.SineNoise );
		_beam.SetEndAttachment( 1 );
		_beam.SpawnFlags |= Beam.SpawnFlagTemporary;
		_beam.EntityFlags |= EntityFlags.SkipLocalHost;
		_beam.Owner = Player;

		_noise = Beam.BeamCreate( Server, BeamSprite, 55 );
		_noise.PointEntInit( Origin, Player.Index );
		_noise.SetScrollRate( 25 );
		_noise.SetBrightness( 100 );
		_noise.SetEndAttachment( 1 );
		_noise.SpawnFlags |= Beam.SpawnFlagTemporary;
		_noise.EntityFlags |= EntityFlags.SkipLocalHost;
		_noise.Owner = Player;

		_sprite = Sprite.SpriteCreate( Server, FlareSprite, Origin, false );
		_sprite.Scale = 1f;
		_sprite.SetTransparency( RenderMode.Glow, 255, 255, 255, 255, RenderFx.NoDissipation );
		_sprite.SpawnFlags |= Sprite.SpawnFlagTemporary;
		_sprite.Owner = Player;

		if ( _fireMode == FireWide )
		{
			_beam.SetScrollRate( 50 );
			_beam.SetNoise( 20 );
			_noise.SetColor( 50, 50, 255 );
			_noise.SetNoise( 8 );
		}
		else
		{
			_beam.SetScrollRate( 110 );
			_beam.SetNoise( 5 );
			_noise.SetColor( 80, 120, 255 );
			_noise.SetNoise( 2 );
		}
	}

	public void DestroyEffect()
	{
		if ( ClientSide )
			return;

		if ( _beam is not null )
		{
			_beam.Remove();
			_beam = null;
		}

		if ( _noise is not null )
		{
			_noise.Remove();
			_noise = null;
		}

		if ( _sprite is not null )
		{
			if ( _fireMode == FireWide )
				_sprite.Expand( 10f, 500f );
			else
				_sprite.Remove();

			_sprite = null;
		}
	}

	public override void WeaponIdle()
	{
		ResetEmptySound();

		if ( TimeWeaponIdle > Globals.Time )
			return;

		if ( FireState != FireOff )
			EndAttack();

		int animation;
		var random = Server.RandomFloat( 0f, 1f );

		if ( random <= 0.5 )
		{
			animation = AnimIdle1;
			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
		}
		else
		{
			animation = AnimFidget1;
			TimeWeaponIdle = WeaponTimeBase + 3f;
		}

		SendWeaponAnim( animation );
	}

	public void EndAttack()
	{
		var makeNoise = FireState != FireOff;

		PlaybackEvent( EventFlags.Global | EventFlags.Reliable, Player, _egonStop, 0f, Player.Origin, Player.Angles, 0f, 0f, makeNoise ? 1 : 0, 0, 0, 0 );

		TimeWeaponIdle = (float)(WeaponTimeBase + 2.0);
		NextPrimaryAttack = NextSecondaryAttack = (float)(WeaponTimeBase + 0.5);

		FireState = FireOff;

		DestroyEffect();
	}
}

[LinkEntityToClass( "ammo_egonclip" )]
public class EgonAmmo : BasePlayerAmmo
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_chainammo.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_chainammo.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( Gauss.UraniumBoxGive, "uranium", Gauss.MaxCarryUranium ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
