using GoldSrc;

namespace HalfLife;

[SkipHotload]
public static class ISlaveAi
{
	public static readonly Schedule SlaveAttack1 = new( "Slave Range Attack1",
		BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionHearSound | BaseMonster.ConditionHeavyDamage,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule[] List =
	[
		SlaveAttack1
	];
}

[LinkEntityToClass( "monster_alien_slave" )]
[LinkEntityToClass( "monster_vortigaunt" )]
public class ISlave : SquadMonster
{
	private const int AnimEventClaw = 1;
	private const int AnimEventClawRake = 2;
	private const int AnimEventZapPowerUp = 3;
	private const int AnimEventZapShoot = 4;
	private const int AnimEventZapDone = 5;

	private const int MaxBeams = 8;

	private static readonly string[] AttackHitSounds = ["zombie/claw_strike1.wav", "zombie/claw_strike2.wav", "zombie/claw_strike3.wav"];
	private static readonly string[] AttackMissSounds = ["zombie/claw_miss1.wav", "zombie/claw_miss2.wav"];
	private static readonly string[] PainSounds = ["aslave/slv_pain1.wav", "aslave/slv_pain2.wav"];
	private static readonly string[] DeathSounds = ["aslave/slv_die1.wav", "aslave/slv_die2.wav"];

	[Save( FieldType.ClassPtr )]
	private readonly Beam[] _beams = new Beam[MaxBeams];
	[Save( FieldType.Integer )]
	private int _beamCount;
	[Save( FieldType.Time )]
	private float _nextAttack;
	[Save( FieldType.Integer )]
	private int _voicePitch;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, ISlaveAi.List ) ?? base.ScheduleFromName( name );

	public override int Classify() => ClassAlienMilitary;

	public override int Relationship( BaseEntity target )
	{
		if ( target.IsPlayer && (SpawnFlags & SpawnFlagWaitUntilProvoked) != 0 && !HasMemory( MemoryProvoked ) )
			return RelationshipNone;

		return base.Relationship( target );
	}

	public void CallForHelp( float distance, BaseEntity enemy, Vector3 location )
	{
		if ( string.IsNullOrEmpty( NetName ) )
			return;

		Edict entity = null;

		while ( (entity = Server.FindEntityByString( entity, "netname", NetName )) is not null )
		{
			if ( MathLib.Length( Origin - entity.Origin ) >= distance || entity is not BaseMonster monster )
				continue;

			monster.Remember( MemoryProvoked );
			monster.PushEnemy( enemy, location );
		}
	}

	public override void AlertSound()
	{
		if ( EnemyEntity is not { } enemy )
			return;

		Sentences.PlayRandom( this, "SLV_ALERT", 0.85f, Server.AttenuationNormal, 0, _voicePitch );

		CallForHelp( 512f, enemy, EnemyLastKnownPosition );
	}

	public override void IdleSound()
	{
		if ( Server.RandomLong( 0, 2 ) == 0 )
			Sentences.PlayRandom( this, "SLV_IDLE", 0.85f, Server.AttenuationNormal, 0, _voicePitch );
	}

	public override void PainSound()
	{
		if ( Server.RandomLong( 0, 2 ) == 0 )
			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( PainSounds ), 1f, Server.AttenuationNormal, 0, _voicePitch );
	}

	public override void DeathSound()
	{
		Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( DeathSounds ), 1f, Server.AttenuationNormal, 0, _voicePitch );
	}

	public override int SoundMask() => WorldSound.World | WorldSound.Combat | WorldSound.Danger | WorldSound.Player;

	public override void Killed( BaseEntity attacker, int gib )
	{
		ClearBeams();

		base.Killed( attacker, gib );
	}

	public override void SetYawSpeed()
	{
		YawSpeed = Activity switch
		{
			Activity.Walk => 50f,
			Activity.Run => 70f,
			Activity.Idle => 50f,
			_ => 90f
		};
	}

	private void Claw( float damage )
	{
		if ( CheckTraceHullAttack( 70f, (int)damage, DamageSlash ) is { } hurt )
		{
			if ( (hurt.EntityFlags & (EntityFlags.Monster | EntityFlags.Client)) != 0 )
				hurt.PunchAngle = hurt.PunchAngle with { z = -18f, x = 5f };

			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackHitSounds ), 1f, Server.AttenuationNormal, 0, _voicePitch );
		}
		else
		{
			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackMissSounds ), 1f, Server.AttenuationNormal, 0, _voicePitch );
		}
	}

	private void ZapPowerUp()
	{
		if ( HalfLifeDll.SkillLevel == SkillData.Hard )
			FrameRate = 1.5f;

		Util.MakeAimVectors( Angles, Server.Globals );

		if ( _beamCount == 0 )
		{
			var source = Origin + (Server.Globals.Forward * 2f);

			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, source );
			Server.WriteByte( TempEntity.DLight );
			Server.WriteCoord( source.x );
			Server.WriteCoord( source.y );
			Server.WriteCoord( source.z );
			Server.WriteByte( 12 );
			Server.WriteByte( 255 );
			Server.WriteByte( 180 );
			Server.WriteByte( 96 );
			Server.WriteByte( (int)(20 / FrameRate) );
			Server.WriteByte( 0 );
			Server.MessageEnd();
		}

		ArmBeam( -1 );
		ArmBeam( 1 );
		BeamGlow();

		Sound.EmitDyn( this, SoundChannel.Weapon, "debris/zap4.wav", 1f, Server.AttenuationNormal, 0, 100 + (_beamCount * 10) );
		Skin = _beamCount / 2;
	}

	private void ZapShoot()
	{
		ClearBeams();

		Combat.ClearMultiDamage();

		Util.MakeAimVectors( Angles, Server.Globals );

		ZapBeam( -1 );
		ZapBeam( 1 );

		Sound.EmitDyn( this, SoundChannel.Weapon, "hassault/hw_shoot1.wav", 1f, Server.AttenuationNormal, 0, Server.RandomLong( 130, 160 ) );

		Combat.ApplyMultiDamage( this, this );

		_nextAttack = Server.Globals.Time + Server.RandomFloat( 0.5f, 4f );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventClaw:
				Claw( HalfLifeDll.SkillData.SlaveDmgClaw );
				break;

			case AnimEventClawRake:
				Claw( HalfLifeDll.SkillData.SlaveDmgClawrake );
				break;

			case AnimEventZapPowerUp:
				ZapPowerUp();
				break;

			case AnimEventZapShoot:
				ZapShoot();
				break;

			case AnimEventZapDone:
				ClearBeams();
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		return _nextAttack <= Server.Globals.Time && base.CheckRangeAttack1( dot, distance );
	}

	public override bool CheckRangeAttack2( float dot, float distance ) => false;

	public override void StartTask( Task task )
	{
		ClearBeams();

		base.StartTask( task );
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/islave.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorGreen;
		Effects = 0;
		Health = HalfLifeDll.SkillData.SlaveHealth;
		ViewOffset = new Vector3( 0f, 0f, 64f );
		FieldOfView = ViewFieldWide;
		MonsterState = MonsterState.None;
		Capability = CapabilityHear | CapabilityTurnHead | CapabilityRangeAttack2 | CapabilityDoorsGroup;

		_voicePitch = Server.RandomLong( 85, 110 );

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/islave.mdl" );
		Server.PrecacheModel( "sprites/lgtning.spr" );
		Server.PrecacheSound( "debris/zap1.wav" );
		Server.PrecacheSound( "debris/zap4.wav" );
		Server.PrecacheSound( "weapons/electro4.wav" );
		Server.PrecacheSound( "hassault/hw_shoot1.wav" );
		Server.PrecacheSound( "zombie/zo_pain2.wav" );
		Server.PrecacheSound( "headcrab/hc_headbite.wav" );
		Server.PrecacheSound( "weapons/cbar_miss1.wav" );

		PrecacheSounds( AttackHitSounds );
		PrecacheSounds( AttackMissSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( DeathSounds );

		Util.PrecacheOther( "test_effect" );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( (damageType & DamageSlash) != 0 && attacker is not null && Relationship( attacker ) < RelationshipDislike )
			return false;

		Remember( MemoryProvoked );

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( (damageType & DamageShock) != 0 )
			return;

		base.TraceAttack( attacker, damage, direction, trace, damageType );
	}

	public override Schedule GetSchedule()
	{
		ClearBeams();

		if ( HasConditions( ConditionHearSound ) )
		{
			var sound = BestSound();

			if ( sound is not null && (sound.Type & WorldSound.Danger) != 0 )
				return GetScheduleOfType( ScheduleType.TakeCoverFromBestSound );

			if ( (sound.Type & WorldSound.Combat) != 0 )
				Remember( MemoryProvoked );
		}

		if ( MonsterState == MonsterState.Combat )
		{
			if ( HasConditions( ConditionEnemyDead ) )
				return GetBaseSchedule();

			if ( Health < 20f && !HasConditions( ConditionCanMeleeAttack1 ) )
			{
				FailSchedule = ScheduleType.ChaseEnemy;

				if ( HasConditions( ConditionLightDamage | ConditionHeavyDamage ) )
					return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );

				if ( HasConditions( ConditionSeeEnemy ) && HasConditions( ConditionEnemyFacingMe ) )
					return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );
			}
		}

		return base.GetSchedule();
	}

	public override Schedule GetScheduleOfType( int type )
	{
		switch ( type )
		{
			case ScheduleType.Fail when HasConditions( ConditionCanMeleeAttack1 ):
				return base.GetScheduleOfType( ScheduleType.MeleeAttack1 );

			case ScheduleType.RangeAttack1:
			case ScheduleType.RangeAttack2:
				return ISlaveAi.SlaveAttack1;
		}

		return base.GetScheduleOfType( type );
	}

	private Beam CreateBeam( Vector3 start, int side, int width )
	{
		var beam = Beam.BeamCreate( Server, "sprites/lgtning.spr", width );

		_beams[_beamCount] = beam;

		if ( beam is null )
			return null;

		beam.PointEntInit( start, Index );
		beam.SetEndAttachment( side < 0 ? 2 : 1 );

		return beam;
	}

	public void ArmBeam( int side )
	{
		var trace = default( Trace );
		var distance = 1f;

		if ( _beamCount >= MaxBeams )
			return;

		var globals = Server.Globals;

		Util.MakeAimVectors( Angles, globals );

		var source = Origin + (globals.Up * 36f) + (globals.Right * side * 16f) + (globals.Forward * 32f);

		for ( var i = 0; i < 3; i++ )
		{
			var aim = (globals.Right * side * Server.RandomFloat( 0f, 1f )) + (globals.Up * Server.RandomFloat( -1f, 1f ));
			var candidate = Util.TraceLine( source, source + (aim * 512f), DontIgnoreMonsters, this );

			if ( distance > candidate.Fraction )
			{
				trace = candidate;
				distance = trace.Fraction;
			}
		}

		if ( distance == 1.0 )
			return;

		Combat.DecalGunshot( trace, Bullet.PlayerCrowbar );

		if ( CreateBeam( trace.EndPos, side, 30 ) is not { } beam )
			return;

		beam.SetColor( 96, 128, 16 );
		beam.SetBrightness( 64 );
		beam.SetNoise( 80 );
		_beamCount++;
	}

	public void BeamGlow()
	{
		var brightness = _beamCount * 32;

		if ( brightness > 255 )
			brightness = 255;

		for ( var i = 0; i < _beamCount; i++ )
		{
			if ( _beams[i].GetBrightness() != 255 )
				_beams[i].SetBrightness( brightness );
		}
	}

	public void ZapBeam( int side )
	{
		if ( _beamCount >= MaxBeams )
			return;

		var globals = Server.Globals;
		var source = Origin + (globals.Up * 36f);
		var aim = ShootAtEnemy( source );
		var deflection = 0.01f;

		aim = aim + (side * globals.Right * Server.RandomFloat( 0f, deflection )) + (globals.Up * Server.RandomFloat( -deflection, deflection ));

		var trace = Util.TraceLine( source, source + (aim * 1024f), DontIgnoreMonsters, this );

		if ( CreateBeam( trace.EndPos, side, 50 ) is not { } beam )
			return;

		beam.SetColor( 180, 255, 96 );
		beam.SetBrightness( 255 );
		beam.SetNoise( 20 );
		_beamCount++;

		if ( trace.Ent is BaseEntity entity && entity.DamageMode != DamageNo )
			entity.TraceAttack( this, HalfLifeDll.SkillData.SlaveDmgZap, aim, trace, DamageShock );

		Sound.EmitAmbient( this, trace.EndPos, "weapons/electro4.wav", 0.5f, Server.AttenuationNormal, 0, Server.RandomLong( 140, 160 ) );
	}

	public void ClearBeams()
	{
		for ( var i = 0; i < MaxBeams; i++ )
		{
			if ( _beams[i] is null )
				continue;

			_beams[i].Remove();
			_beams[i] = null;
		}

		_beamCount = 0;
		Skin = 0;

		Server.StopSound( this, SoundChannel.Weapon, "debris/zap4.wav" );
	}
}
