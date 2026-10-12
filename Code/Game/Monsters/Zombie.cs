using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_zombie" )]
public class Zombie : BaseMonster
{
	private const int AnimEventAttackRight = 1;
	private const int AnimEventAttackLeft = 2;
	private const int AnimEventAttackBoth = 3;

	private const float FlinchDelay = 2f;

	private static readonly string[] AttackHitSounds = ["zombie/claw_strike1.wav", "zombie/claw_strike2.wav", "zombie/claw_strike3.wav"];
	private static readonly string[] AttackMissSounds = ["zombie/claw_miss1.wav", "zombie/claw_miss2.wav"];
	private static readonly string[] AttackSounds = ["zombie/zo_attack1.wav", "zombie/zo_attack2.wav"];
	private static readonly string[] IdleSounds = ["zombie/zo_idle1.wav", "zombie/zo_idle2.wav", "zombie/zo_idle3.wav", "zombie/zo_idle4.wav"];
	private static readonly string[] AlertSounds = ["zombie/zo_alert10.wav", "zombie/zo_alert20.wav", "zombie/zo_alert30.wav"];
	private static readonly string[] PainSounds = ["zombie/zo_pain1.wav", "zombie/zo_pain2.wav"];

	private float _nextFlinch;

	public override int Classify() => ClassAlienMonster;

	public override void SetYawSpeed() => YawSpeed = 120f;

	public override bool CheckRangeAttack1( float dot, float distance ) => false;

	public override bool CheckRangeAttack2( float dot, float distance ) => false;

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( damageType == DamageBullet )
		{
			var direction = Util.Normalize( Origin - ((inflictor.AbsMin + inflictor.AbsMax) * 0.5f) );

			Velocity += direction * DamageForce( damage );
			damage *= 0.3f;
		}

		if ( IsAlive )
			PainSound();

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void PainSound()
	{
		var pitch = 95 + Server.RandomLong( 0, 9 );

		if ( Server.RandomLong( 0, 5 ) < 2 )
			Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( PainSounds ), 1f, Server.AttenuationNormal, 0, pitch );
	}

	public override void AlertSound()
	{
		var pitch = 95 + Server.RandomLong( 0, 9 );

		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( AlertSounds ), 1f, Server.AttenuationNormal, 0, pitch );
	}

	public override void IdleSound()
	{
		Server.RandomLong( 0, 9 );

		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( IdleSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );
	}

	public void AttackSound()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( AttackSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );
	}

	private void Slash( float damage, float? punchRoll, Vector3 push )
	{
		if ( CheckTraceHullAttack( 70f, (int)damage, DamageSlash ) is { } hurt )
		{
			if ( (hurt.EntityFlags & (EntityFlags.Monster | EntityFlags.Client)) != 0 )
			{
				hurt.PunchAngle = hurt.PunchAngle with { x = 5f, z = punchRoll ?? hurt.PunchAngle.z };
				hurt.Velocity += push;
			}

			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackHitSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );
		}
		else
		{
			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackMissSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );
		}

		if ( Server.RandomLong( 0, 1 ) != 0 )
			AttackSound();
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		var skill = HalfLifeDll.SkillData;

		switch ( monsterEvent.Event )
		{
			case AnimEventAttackRight:
				Slash( skill.ZombieDmgOneSlash, -18f, Server.Globals.Right * -100f );
				break;

			case AnimEventAttackLeft:
				Slash( skill.ZombieDmgOneSlash, 18f, Server.Globals.Right * 100f );
				break;

			case AnimEventAttackBoth:
				Slash( skill.ZombieDmgBothSlash, null, Server.Globals.Forward * -100f );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/zombie.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorGreen;
		Health = HalfLifeDll.SkillData.ZombieHealth;
		ViewOffset = ViewVector;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;
		Capability = CapabilityDoorsGroup;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/zombie.mdl" );

		PrecacheSounds( AttackHitSounds );
		PrecacheSounds( AttackMissSounds );
		PrecacheSounds( AttackSounds );
		PrecacheSounds( IdleSounds );
		PrecacheSounds( AlertSounds );
		PrecacheSounds( PainSounds );
	}

	public override int IgnoreConditions()
	{
		var ignore = base.IgnoreConditions();

		if ( Activity == Activity.MeleeAttack1 && _nextFlinch >= Server.Globals.Time )
			ignore |= ConditionLightDamage | ConditionHeavyDamage;

		if ( Activity is Activity.SmallFlinch or Activity.BigFlinch && _nextFlinch < Server.Globals.Time )
			_nextFlinch = Server.Globals.Time + FlinchDelay;

		return ignore;
	}
}
