using System;
using GoldSrc;

namespace HalfLife;

partial class Player
{
	public const int SuitPlayListSize = 4;
	public const int SuitNoRepeatSize = 32;

	public const int SuitRepeatOk = 0;
	public const int SuitNextIn30Sec = 30;
	public const int SuitNextIn1Min = 60;
	public const int SuitNextIn5Min = 300;
	public const int SuitNextIn10Min = 600;
	public const int SuitNextIn30Min = 1800;
	public const int SuitNextIn1Hour = 3600;

	private const int TimeBasedParalyze = 0;
	private const int TimeBasedNerveGas = 1;
	private const int TimeBasedPoison = 2;
	private const int TimeBasedRadiation = 3;
	private const int TimeBasedDrownRecover = 4;
	private const int TimeBasedAcid = 5;
	private const int TimeBasedSlowBurn = 6;
	private const int TimeBasedSlowFreeze = 7;

	private const byte ParalyzeDuration = 2;
	private const byte NerveGasDuration = 2;
	private const byte PoisonDuration = 5;
	private const float PoisonDamage = 2f;
	private const byte RadiationDuration = 2;
	private const byte AcidDuration = 2;
	private const byte SlowBurnDuration = 2;
	private const byte SlowFreezeDuration = 2;

	private const float ArmorRatio = 0.2f;
	private const float ArmorBonus = 0.5f;

	private const double GeigerDelay = 0.25;
	private const double SuitUpdateTime = 3.5;
	private const double SuitFirstUpdateTime = 0.1;

	private const double FlashDrainTime = 1.2;
	private const double FlashChargeTime = 0.2;

	private const int DirectorMessageEvent = 2;

	public override void DeathSound()
	{
		switch ( Server.RandomLong( 1, 5 ) )
		{
			case 1:
				Sound.Emit( this, SoundChannel.Voice, "player/pl_pain5.wav", 1f, Server.AttenuationNormal );
				break;
			case 2:
				Sound.Emit( this, SoundChannel.Voice, "player/pl_pain6.wav", 1f, Server.AttenuationNormal );
				break;
			case 3:
				Sound.Emit( this, SoundChannel.Voice, "player/pl_pain7.wav", 1f, Server.AttenuationNormal );
				break;
		}

		Sound.EmitGroupSuit( this, "HEV_DEAD" );
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( DamageMode == DamageNo )
			return;

		LastHitGroup = trace.HitGroup;

		switch ( trace.HitGroup )
		{
			case HitGroupHead:
				damage *= HalfLifeDll.SkillData.PlrHead;
				break;
			case HitGroupChest:
				damage *= HalfLifeDll.SkillData.PlrChest;
				break;
			case HitGroupStomach:
				damage *= HalfLifeDll.SkillData.PlrStomach;
				break;
			case HitGroupLeftArm:
			case HitGroupRightArm:
				damage *= HalfLifeDll.SkillData.PlrArm;
				break;
			case HitGroupLeftLeg:
			case HitGroupRightLeg:
				damage *= HalfLifeDll.SkillData.PlrLeg;
				break;
		}

		Combat.SpawnBlood( trace.EndPos, BloodColor, damage );
		TraceBleed( damage, direction, trace, damageType );
		Combat.AddMultiDamage( attacker, this, damage, damageType );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		var damageBits = damageType;
		var found = true;
		var healthPrev = Health;
		var bonus = ArmorBonus;

		if ( (damageType & DamageBlast) != 0 && HalfLifeDll.GameRules.IsMultiplayer() )
			bonus *= 2f;

		if ( !IsAlive )
			return false;

		if ( !HalfLifeDll.GameRules.PlayerCanTakeDamage( this, attacker ) )
			return false;

		_lastDamageAmount = (int)damage;

		if ( ArmorValue != 0f && (damageType & (DamageFall | DamageDrown)) == 0 )
		{
			var newDamage = damage * ArmorRatio;
			var armor = (damage - newDamage) * bonus;

			if ( armor > ArmorValue )
			{
				armor = ArmorValue;
				armor *= 1f / bonus;
				newDamage = damage - armor;
				ArmorValue = 0f;
			}
			else
			{
				ArmorValue -= armor;
			}

			damage = newDamage;
		}

		var tookDamage = base.TakeDamage( inflictor, attacker, (int)damage, damageType );

		for ( var i = 0; i < TimeBasedDamageCount; i++ )
		{
			if ( (damageType & (DamageParalyze << i)) != 0 )
				TimeBasedDamage[i] = 0;
		}

		Server.MessageBegin( MessageDest.Spectator, Server.DirectorMessage );
		Server.WriteByte( 9 );
		Server.WriteByte( DirectorMessageEvent );
		Server.WriteShort( Index );
		Server.WriteShort( inflictor?.Index ?? 0 );
		Server.WriteLong( 5 );
		Server.MessageEnd();

		var trivial = Health > 75f || _lastDamageAmount < 5;
		var major = _lastDamageAmount > 25;
		var critical = Health < 30f;

		DamageTypeBits |= damageBits;
		_hudDamageBits = -1;

		while ( tookDamage && (!trivial || (damageBits & DamageTimeBased) != 0) && found && damageBits != 0 )
		{
			found = false;

			if ( (damageBits & DamageClub) != 0 )
			{
				if ( major )
					SetSuitUpdate( "!HEV_DMG4", false, SuitNextIn30Sec );

				damageBits &= ~DamageClub;
				found = true;
			}

			if ( (damageBits & (DamageFall | DamageCrush)) != 0 )
			{
				SetSuitUpdate( major ? "!HEV_DMG5" : "!HEV_DMG4", false, SuitNextIn30Sec );

				damageBits &= ~(DamageFall | DamageCrush);
				found = true;
			}

			if ( (damageBits & DamageBullet) != 0 )
			{
				if ( _lastDamageAmount > 5 )
					SetSuitUpdate( "!HEV_DMG6", false, SuitNextIn30Sec );

				damageBits &= ~DamageBullet;
				found = true;
			}

			if ( (damageBits & DamageSlash) != 0 )
			{
				SetSuitUpdate( major ? "!HEV_DMG1" : "!HEV_DMG0", false, SuitNextIn30Sec );

				damageBits &= ~DamageSlash;
				found = true;
			}

			if ( (damageBits & DamageSonic) != 0 )
			{
				if ( major )
					SetSuitUpdate( "!HEV_DMG2", false, SuitNextIn1Min );

				damageBits &= ~DamageSonic;
				found = true;
			}

			if ( (damageBits & (DamagePoison | DamageParalyze)) != 0 )
			{
				SetSuitUpdate( "!HEV_DMG3", false, SuitNextIn1Min );

				damageBits &= ~(DamagePoison | DamageParalyze);
				found = true;
			}

			if ( (damageBits & DamageAcid) != 0 )
			{
				SetSuitUpdate( "!HEV_DET1", false, SuitNextIn1Min );

				damageBits &= ~DamageAcid;
				found = true;
			}

			if ( (damageBits & DamageNerveGas) != 0 )
			{
				SetSuitUpdate( "!HEV_DET0", false, SuitNextIn1Min );

				damageBits &= ~DamageNerveGas;
				found = true;
			}

			if ( (damageBits & DamageRadiation) != 0 )
			{
				SetSuitUpdate( "!HEV_DET2", false, SuitNextIn1Min );

				damageBits &= ~DamageRadiation;
				found = true;
			}

			if ( (damageBits & DamageShock) != 0 )
			{
				damageBits &= ~DamageShock;
				found = true;
			}
		}

		PunchAngle = new Vector3( -2f, PunchAngle.y, PunchAngle.z );

		if ( tookDamage && !trivial && major && healthPrev >= 75f )
		{
			SetSuitUpdate( "!HEV_MED1", false, SuitNextIn30Min );
			SetSuitUpdate( "!HEV_HEAL7", false, SuitNextIn30Min );
		}

		if ( tookDamage && !trivial && critical && healthPrev < 75f )
		{
			if ( Health < 6f )
				SetSuitUpdate( "!HEV_HLTH3", false, SuitNextIn10Min );
			else if ( Health < 20f )
				SetSuitUpdate( "!HEV_HLTH2", false, SuitNextIn10Min );

			if ( Server.RandomLong( 0, 3 ) == 0 && healthPrev < 50f )
				SetSuitUpdate( "!HEV_DMG7", false, SuitNextIn5Min );
		}

		if ( tookDamage && (damageType & DamageTimeBased) != 0 && healthPrev < 75f )
		{
			if ( healthPrev < 50f )
			{
				if ( Server.RandomLong( 0, 3 ) == 0 )
					SetSuitUpdate( "!HEV_DMG7", false, SuitNextIn5Min );
			}
			else
			{
				SetSuitUpdate( "!HEV_HLTH1", false, SuitNextIn10Min );
			}
		}

		return tookDamage;
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		ActiveItem?.Holster();

		if ( ClientSide )
		{
			ClientWeapons.RunningGaussPrediction = false;
			return;
		}

		HalfLifeDll.GameRules.PlayerKilled( this, attacker, HalfLifeDll.LastInflictor );

		if ( Tank is { } tank )
		{
			tank.Use( this, this, UseType.Off, 0f );
			Tank = null;
		}

		SoundEnt.SoundPointerForIndex( SoundEnt.ClientSoundIndex( this ) )?.Reset();

		SetAnimation( PlayerAnim.Die );

		_respawnTimer = 0f;

		ModelIndex = HalfLifeDll.ModelIndexPlayer;

		DeadFlag = DeadDying;
		MoveType = MoveType.Toss;
		EntityFlags &= ~EntityFlags.OnGround;

		if ( Velocity.z < 10f )
			Velocity += new Vector3( 0f, 0f, Server.RandomFloat( 0f, 300f ) );

		SetSuitUpdate( null, false, 0 );

		_clientHealth = 0;

		Server.MessageBegin( MessageDest.One, UserMessages.Health, default, this );
		Server.WriteByte( _clientHealth );
		Server.MessageEnd();

		Server.MessageBegin( MessageDest.One, UserMessages.CurWeapon, default, this );
		Server.WriteByte( 0 );
		Server.WriteByte( 0xFF );
		Server.WriteByte( 0xFF );
		Server.MessageEnd();

		Fov = FieldOfViewSetting = ClientFieldOfView = 0;

		Server.MessageBegin( MessageDest.One, UserMessages.SetFov, default, this );
		Server.WriteByte( 0 );
		Server.MessageEnd();

		if ( HalfLifeDll.GameRules.IsMultiplayer() )
			Solid = Solid.Not;

		if ( (Health < -40f && gib != GibNever) || gib == GibAlways )
		{
			Solid = Solid.Not;
			GibMonster();
			Effects |= Effects.NoDraw;
			return;
		}

		DeathSound();

		Angles = new Vector3( 0f, Angles.y, 0f );

		SetThink( PlayerDeathThink );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void SetAnimation( PlayerAnim playerAnim )
	{
		if ( ClientSide )
			return;

		int animDesired;

		var speed = (float)Math.Sqrt( (Velocity.x * Velocity.x) + (Velocity.y * Velocity.y) );

		if ( (EntityFlags & EntityFlags.Frozen) == EntityFlags.Frozen )
		{
			speed = 0f;
			playerAnim = PlayerAnim.Idle;
		}

		switch ( playerAnim )
		{
			case PlayerAnim.Jump:
				IdealActivity = Activity.Hop;
				break;

			case PlayerAnim.SuperJump:
				IdealActivity = Activity.Leap;
				break;

			case PlayerAnim.Die:
				IdealActivity = Activity.DieSimple;
				IdealActivity = GetDeathActivity();
				break;

			case PlayerAnim.Attack1:
				IdealActivity = Activity is Activity.Hover or Activity.Swim or Activity.Hop or Activity.Leap or Activity.DieSimple
					? Activity
					: Activity.RangeAttack1;
				break;

			case PlayerAnim.Idle:
			case PlayerAnim.Walk:
				if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround && Activity is Activity.Hop or Activity.Leap )
					IdealActivity = Activity;
				else if ( WaterLevel > 1 )
					IdealActivity = speed == 0f ? Activity.Hover : Activity.Swim;
				else
					IdealActivity = Activity.Walk;

				break;
		}

		switch ( IdealActivity )
		{
			default:
				if ( Activity == IdealActivity )
					return;

				Activity = IdealActivity;

				animDesired = LookupActivity( (int)Activity );

				if ( Sequence == animDesired )
					return;

				GaitSequence = 0;
				Sequence = animDesired;
				Frame = 0f;
				ResetSequenceInfo();
				return;

			case Activity.RangeAttack1:
				animDesired = LookupSequence( ((EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking ? "crouch_shoot_" : "ref_shoot_") + AnimExtension );

				if ( animDesired == -1 )
					animDesired = 0;

				if ( Sequence != animDesired || !SequenceLoops )
					Frame = 0f;

				if ( !SequenceLoops )
					Effects |= Effects.NoInterp;

				Activity = IdealActivity;

				Sequence = animDesired;
				ResetSequenceInfo();
				break;

			case Activity.Walk:
				if ( Activity != Activity.RangeAttack1 || SequenceFinished )
				{
					animDesired = LookupSequence( ((EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking ? "crouch_aim_" : "ref_aim_") + AnimExtension );

					if ( animDesired == -1 )
						animDesired = 0;

					Activity = Activity.Walk;
				}
				else
				{
					animDesired = Sequence;
				}

				break;
		}

		if ( (EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking )
			GaitSequence = LookupActivity( (int)(speed == 0f ? Activity.CrouchIdle : Activity.Crouch) );
		else if ( speed > 220f )
			GaitSequence = LookupActivity( (int)Activity.Run );
		else if ( speed > 0f )
			GaitSequence = LookupActivity( (int)Activity.Walk );
		else
			GaitSequence = LookupSequence( "deep_idle" );

		if ( Sequence == animDesired )
			return;

		Sequence = animDesired;
		Frame = 0f;
		ResetSequenceInfo();
	}

	public void CheckTimeBasedDamage()
	{
		if ( (DamageTypeBits & DamageTimeBased) == 0 )
			return;

		if ( MathF.Abs( Server.Globals.Time - _timeBasedDamagePrev ) < 2.0 )
			return;

		_timeBasedDamagePrev = Server.Globals.Time;

		for ( var i = 0; i < TimeBasedDamageCount; i++ )
		{
			if ( (DamageTypeBits & (DamageParalyze << i)) == 0 )
				continue;

			byte duration;

			switch ( i )
			{
				case TimeBasedParalyze:
					duration = ParalyzeDuration;
					break;

				case TimeBasedNerveGas:
					duration = NerveGasDuration;
					break;

				case TimeBasedPoison:
					TakeDamage( this, this, PoisonDamage, DamageGeneric );
					duration = PoisonDuration;
					break;

				case TimeBasedRadiation:
					duration = RadiationDuration;
					break;

				case TimeBasedDrownRecover:
					if ( _drownDamage > _drownRestored )
					{
						var difference = Math.Min( _drownDamage - _drownRestored, 10 );

						TakeHealth( difference, DamageGeneric );
						_drownRestored += difference;
					}

					duration = 4;
					break;

				case TimeBasedAcid:
					duration = AcidDuration;
					break;

				case TimeBasedSlowBurn:
					duration = SlowBurnDuration;
					break;

				case TimeBasedSlowFreeze:
					duration = SlowFreezeDuration;
					break;

				default:
					duration = 0;
					break;
			}

			if ( TimeBasedDamage[i] == 0 )
			{
				TimeBasedDamage[i] = duration;
				continue;
			}

			if ( ((i == TimeBasedNerveGas && TimeBasedDamage[i] < NerveGasDuration) || (i == TimeBasedPoison && TimeBasedDamage[i] < PoisonDuration))
				&& Items[Item.Antidote] != 0 )
			{
				TimeBasedDamage[i] = 0;
				Items[Item.Antidote]--;
				SetSuitUpdate( "!HEV_HEAL4", false, SuitRepeatOk );
			}

			if ( TimeBasedDamage[i] == 0 || --TimeBasedDamage[i] == 0 )
			{
				TimeBasedDamage[i] = 0;
				DamageTypeBits &= ~(DamageParalyze << i);
			}
		}
	}

	public void UpdateGeigerCounter()
	{
		if ( Server.Globals.Time < _geigerDelay )
			return;

		_geigerDelay = (float)(Server.Globals.Time + GeigerDelay);

		var range = (byte)(GeigerRange / 4f);

		if ( range != _geigerRangePrev )
		{
			_geigerRangePrev = range;

			Server.MessageBegin( MessageDest.One, UserMessages.GeigerRange, default, this );
			Server.WriteByte( range );
			Server.MessageEnd();
		}

		if ( Server.RandomLong( 0, 3 ) == 0 )
			GeigerRange = 1000f;
	}

	public void CheckSuitUpdate()
	{
		if ( (Weapons & (1 << WeaponSuit)) == 0 )
			return;

		UpdateGeigerCounter();

		if ( HalfLifeDll.GameRules.IsMultiplayer() )
			return;

		if ( Server.Globals.Time < _suitUpdate || _suitUpdate <= 0f )
			return;

		var sentence = 0;
		var search = _suitPlayNext;

		for ( var i = 0; i < SuitPlayListSize; i++ )
		{
			sentence = _suitPlayList[search];

			if ( sentence != 0 )
				break;

			if ( ++search == SuitPlayListSize )
				search = 0;
		}

		if ( sentence == 0 )
		{
			_suitUpdate = 0f;
			return;
		}

		_suitPlayList[search] = 0;

		if ( sentence > 0 )
			Sound.EmitSuit( this, $"!{Sentences.Name( sentence )}" );
		else
			Sound.EmitGroupSuit( this, -sentence );

		_suitUpdate = (float)(Server.Globals.Time + SuitUpdateTime);
	}

	public void SetSuitUpdate( string name, bool group, int noRepeatTime )
	{
		if ( ClientSide )
			return;

		if ( (Weapons & (1 << WeaponSuit)) == 0 )
			return;

		if ( HalfLifeDll.GameRules.IsMultiplayer() )
			return;

		if ( name is null )
		{
			Array.Clear( _suitPlayList );
			return;
		}

		int sentence;

		if ( !group )
		{
			sentence = Sentences.Lookup( name, out _ );

			if ( sentence < 0 )
				return;
		}
		else
		{
			sentence = -Sentences.GetIndex( name );
		}

		var empty = -1;

		for ( var i = 0; i < SuitNoRepeatSize; i++ )
		{
			if ( sentence == _suitNoRepeat[i] )
			{
				if ( _suitNoRepeatTime[i] >= Server.Globals.Time )
					return;

				_suitNoRepeat[i] = 0;
				_suitNoRepeatTime[i] = 0f;
				empty = i;
				break;
			}

			if ( _suitNoRepeat[i] == 0 )
				empty = i;
		}

		if ( noRepeatTime != 0 )
		{
			if ( empty < 0 )
				empty = Server.RandomLong( 0, SuitNoRepeatSize - 1 );

			_suitNoRepeat[empty] = sentence;
			_suitNoRepeatTime[empty] = noRepeatTime + Server.Globals.Time;
		}

		_suitPlayList[_suitPlayNext++] = sentence;

		if ( _suitPlayNext == SuitPlayListSize )
			_suitPlayNext = 0;

		if ( _suitUpdate <= Server.Globals.Time )
			_suitUpdate = (float)(Server.Globals.Time + (_suitUpdate == 0f ? SuitFirstUpdateTime : SuitUpdateTime));
	}

	public void UpdatePlayerSound()
	{
		var sound = SoundEnt.SoundPointerForIndex( SoundEnt.ClientSoundIndex( this ) );

		if ( sound is null )
		{
			Util.Alert( AlertType.Console, "Client lost reserved sound!\n" );
			return;
		}

		sound.Type = WorldSound.None;

		var bodyVolume = 0;

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			bodyVolume = (int)MathLib.Length( Velocity );

			if ( bodyVolume > 512 )
				bodyVolume = 512;
		}

		if ( (Button & (int)InButtons.Jump) != 0 )
			bodyVolume += 100;

		if ( WeaponVolume > bodyVolume )
		{
			TargetVolume = WeaponVolume;
			sound.Type |= WorldSound.Combat;
		}
		else
		{
			TargetVolume = bodyVolume;
		}

		WeaponVolume = (int)(WeaponVolume - (250 * Server.Globals.FrameTime));

		var volume = sound.Volume;

		if ( TargetVolume > volume )
		{
			volume = TargetVolume;
		}
		else if ( volume > TargetVolume )
		{
			volume = (int)(volume - (250 * Server.Globals.FrameTime));

			if ( volume < TargetVolume )
				volume = 0;
		}

		if ( _noPlayerSound )
			volume = 0;

		if ( Server.Globals.Time > StopExtraSoundTime )
			ExtraSoundTypes = 0;

		sound.Origin = Origin;
		sound.Type |= WorldSound.Player | ExtraSoundTypes;
		sound.Volume = volume;

		WeaponFlash = (int)(WeaponFlash - (256 * Server.Globals.FrameTime));

		if ( WeaponFlash < 0 )
			WeaponFlash = 0;
	}

	public bool FlashlightIsOn => (Effects & Effects.DimLight) == Effects.DimLight;

	public void FlashlightTurnOn()
	{
		if ( !HalfLifeDll.GameRules.AllowFlashlight() )
			return;

		if ( (Weapons & (1 << WeaponSuit)) == 0 )
			return;

		Sound.EmitDyn( this, SoundChannel.Weapon, SoundFlashlightOn, 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

		Effects |= Effects.DimLight;

		Server.MessageBegin( MessageDest.One, UserMessages.Flashlight, default, this );
		Server.WriteByte( 1 );
		Server.WriteByte( _flashBattery );
		Server.MessageEnd();

		_flashLightTime = (float)(FlashDrainTime + Server.Globals.Time);
	}

	public void FlashlightTurnOff()
	{
		Sound.EmitDyn( this, SoundChannel.Weapon, SoundFlashlightOff, 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

		Effects &= ~Effects.DimLight;

		Server.MessageBegin( MessageDest.One, UserMessages.Flashlight, default, this );
		Server.WriteByte( 0 );
		Server.WriteByte( _flashBattery );
		Server.MessageEnd();

		_flashLightTime = (float)(FlashChargeTime + Server.Globals.Time);
	}
}
