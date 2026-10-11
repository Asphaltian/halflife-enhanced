using System;
using GoldSrc;

namespace HalfLife;

[SkipHotload]
public static class HeadCrabAi
{
	public static readonly Schedule HCRangeAttack1 = new( "HCRangeAttack1",
		BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionNoAmmoLoaded,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.FaceIdeal ),
		new( TaskId.WaitRandom, 0.5f ) );

	public static readonly Schedule HCRangeAttack1Fast = new( "HCRAFast",
		BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionNoAmmoLoaded,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.SetActivity, (float)Activity.Idle ) );

	public static readonly Schedule[] List =
	[
		HCRangeAttack1,
		HCRangeAttack1Fast
	];
}

[LinkEntityToClass( "monster_headcrab" )]
public class HeadCrab : BaseMonster
{
	private const int AnimEventJumpAttack = 2;

	private static readonly string[] IdleSounds = ["headcrab/hc_idle1.wav", "headcrab/hc_idle2.wav", "headcrab/hc_idle3.wav"];
	private static readonly string[] AlertSounds = ["headcrab/hc_alert1.wav"];
	private static readonly string[] PainSounds = ["headcrab/hc_pain1.wav", "headcrab/hc_pain2.wav", "headcrab/hc_pain3.wav"];
	private static readonly string[] AttackSounds = ["headcrab/hc_attack1.wav", "headcrab/hc_attack2.wav", "headcrab/hc_attack3.wav"];
	private static readonly string[] DeathSounds = ["headcrab/hc_die1.wav", "headcrab/hc_die2.wav"];
	private static readonly string[] BiteSounds = ["headcrab/hc_headbite.wav"];

	public virtual float DamageAmount => HalfLifeDll.SkillData.HeadcrabDmgBite;

	public virtual int VoicePitch => 100;

	public virtual float VoiceVolume => 1f;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, HeadCrabAi.List ) ?? base.ScheduleFromName( name );

	public override int Classify() => ClassAlienPrey;

	public override bool CanBecomeRagdoll() => true;

	public override Vector3 Center => new( Origin.x, Origin.y, Origin.z + 6f );

	public override Vector3 BodyTarget( Vector3 source ) => Center;

	public override void SetYawSpeed()
	{
		YawSpeed = Activity switch
		{
			Activity.Run or Activity.Walk => 20f,
			Activity.TurnLeft or Activity.TurnRight => 60f,
			_ => 30f
		};
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		if ( monsterEvent.Event != AnimEventJumpAttack )
		{
			base.HandleAnimEvent( monsterEvent );
			return;
		}

		EntityFlags &= ~EntityFlags.OnGround;

		SetOrigin( Origin + new Vector3( 0f, 0f, 1f ) );
		Server.Globals.MakeVectors( Angles );

		Vector3 jumpDirection;

		if ( EnemyEntity is { } enemy )
		{
			var gravity = Cvar.GetFloat( "sv_gravity" );

			if ( gravity <= 1f )
				gravity = 1f;

			var height = enemy.Origin.z + enemy.ViewOffset.z - Origin.z;

			if ( height < 16f )
				height = 16f;

			var speed = (float)Math.Sqrt( 2 * gravity * height );
			var time = speed / gravity;

			jumpDirection = enemy.Origin + enemy.ViewOffset - Origin;
			jumpDirection *= (float)(1.0 / time);
			jumpDirection.z = speed;

			var distance = MathLib.Length( jumpDirection );

			if ( distance > 650f )
				jumpDirection *= (float)(650.0 / distance);
		}
		else
		{
			jumpDirection = new Vector3( Server.Globals.Forward.x, Server.Globals.Forward.y, Server.Globals.Up.z ) * 350f;
		}

		var sound = Server.RandomLong( 0, 2 );

		if ( sound != 0 )
			Sound.EmitDyn( this, SoundChannel.Voice, AttackSounds[sound], VoiceVolume, Server.AttenuationIdle, 0, VoicePitch );

		Velocity = jumpDirection;
		NextAttack = (float)(Server.Globals.Time + 2.0);
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/headcrab.mdl" );
		SetSize( new Vector3( -12f, -12f, 0f ), new Vector3( 12f, 12f, 24f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorGreen;
		Effects = 0;
		Health = HalfLifeDll.SkillData.HeadcrabHealth;
		ViewOffset = new Vector3( 0f, 0f, 20f );
		YawSpeed = 5f;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;

		MonsterInit();
	}

	public override void Precache()
	{
		PrecacheSounds( IdleSounds );
		PrecacheSounds( AlertSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( AttackSounds );
		PrecacheSounds( DeathSounds );
		PrecacheSounds( BiteSounds );

		Server.PrecacheModel( "models/headcrab.mdl" );
	}

	public override void RunTask( Task task )
	{
		if ( task.Id is not (TaskId.RangeAttack1 or TaskId.RangeAttack2) )
		{
			base.RunTask( task );
			return;
		}

		if ( !SequenceFinished )
			return;

		TaskComplete();
		SetTouch( null );
		IdealActivity = Activity.Idle;
	}

	public void LeapTouch( BaseEntity other )
	{
		if ( other.DamageMode == DamageNo || other.Classify() == Classify() )
			return;

		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
		{
			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( BiteSounds ), VoiceVolume, Server.AttenuationIdle, 0, VoicePitch );

			other.TakeDamage( this, this, DamageAmount, DamageSlash );
		}

		SetTouch( null );
	}

	public override void PrescheduleThink()
	{
		if ( MonsterState == MonsterState.Combat && Server.RandomFloat( 0f, 5f ) < 0.1 )
			IdleSound();
	}

	public override void StartTask( Task task )
	{
		TaskStatus = TaskState.Running;

		if ( task.Id != TaskId.RangeAttack1 )
		{
			base.StartTask( task );
			return;
		}

		Sound.EmitDyn( this, SoundChannel.Weapon, AttackSounds[0], VoiceVolume, Server.AttenuationIdle, 0, VoicePitch );

		IdealActivity = Activity.RangeAttack1;
		SetTouch( LeapTouch );
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		return (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && distance <= 256f && dot >= 0.65;
	}

	public override bool CheckRangeAttack2( float dot, float distance ) => false;

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( (damageType & DamageAcid) != 0 )
			damage = 0f;

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void IdleSound()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( IdleSounds ), VoiceVolume, Server.AttenuationIdle, 0, VoicePitch );
	}

	public override void AlertSound()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( AlertSounds ), VoiceVolume, Server.AttenuationIdle, 0, VoicePitch );
	}

	public override void PainSound()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( PainSounds ), VoiceVolume, Server.AttenuationIdle, 0, VoicePitch );
	}

	public override void DeathSound()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( DeathSounds ), VoiceVolume, Server.AttenuationIdle, 0, VoicePitch );
	}

	public override Schedule GetScheduleOfType( int type )
	{
		return type == ScheduleType.RangeAttack1 ? HeadCrabAi.HCRangeAttack1 : base.GetScheduleOfType( type );
	}
}

[LinkEntityToClass( "monster_babycrab" )]
public class BabyCrab : HeadCrab
{
	public override float DamageAmount => (float)(HalfLifeDll.SkillData.HeadcrabDmgBite * 0.3);

	public override int VoicePitch => Server.PitchNormal + Server.RandomLong( 40, 50 );

	public override float VoiceVolume => 0.8f;

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/baby_headcrab.mdl" );

		RenderMode = RenderMode.TransTexture;
		RenderAmt = 192f;

		SetSize( new Vector3( -12f, -12f, 0f ), new Vector3( 12f, 12f, 24f ) );

		Health = (float)(HalfLifeDll.SkillData.HeadcrabHealth * 0.25);
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/baby_headcrab.mdl" );

		base.Precache();
	}

	public override void SetYawSpeed() => YawSpeed = 120f;

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			return false;

		if ( GroundEntity is { } ground && (ground.EntityFlags & (EntityFlags.Client | EntityFlags.Monster)) != 0 )
			return true;

		return distance <= 180f && dot >= 0.55;
	}

	public override Schedule GetScheduleOfType( int type )
	{
		switch ( type )
		{
			case ScheduleType.Fail when EnemyEntity is not null:
			case ScheduleType.RangeAttack1:
				return HeadCrabAi.HCRangeAttack1Fast;
		}

		return base.GetScheduleOfType( type );
	}
}
