using System;
using GoldSrc;

namespace HalfLife;

public static class ScenePhysicsDamage
{
	private const float KgPerPound = 1f / 2.2f;
	private const float BulletImpulseExaggeration = 3f;
	private const float PushScale = 1f;
	private const float ForceTargetMass = 75f;
	private const float ForceSpeedPerDamage = 4f;
	private const float ExplosiveClampSpeed = 400f;
	private const float ClubDamage = 50f;

	private static float ImpulseScale( float targetMass, float desiredSpeed ) => targetMass * desiredSpeed;

	private static float BulletImpulse( float grains, float feetPerSecond )
	{
		return feetPerSecond * 12f * (0.002285f * grains / 16f * KgPerPound) * BulletImpulseExaggeration;
	}

	private static float DamageForce( Bullet bulletType )
	{
		return bulletType switch
		{
			Bullet.Player357 => BulletImpulse( 650f, 6000f ),
			Bullet.PlayerBuckshot => BulletImpulse( 200f, 1200f ),
			Bullet.Monster12mm => BulletImpulse( 300f, 1200f ),
			_ => BulletImpulse( 500f, 1325f )
		};
	}

	public static void BulletImpact( in Trace trace, Bullet bulletType, Vector3 direction )
	{
		if ( bulletType == Bullet.None )
		{
			MeleeImpact( trace, ClubDamage, direction );
			return;
		}

		Impact( trace, trace.EndPos, Util.Normalize( direction ) * (DamageForce( bulletType ) * PushScale) );
	}

	public static void HornetImpact( in Trace trace, Vector3 velocity, Vector3 origin )
	{
		Impact( trace, origin, Util.Normalize( velocity ) * (BulletImpulse( 100f, 1200f ) * PushScale) );
	}

	public static void MeleeImpact( in Trace trace, float damage, Vector3 direction )
	{
		Impact( trace, trace.EndPos, MeleeForce( damage, direction ) );
	}

	public static Vector3 MeleeForce( float damage, Vector3 direction )
	{
		return Util.Normalize( direction ) * (damage * ImpulseScale( ForceTargetMass, ForceSpeedPerDamage ) * PushScale);
	}

	public static Vector3 ExplosiveForce( float damage, Vector3 direction )
	{
		var forceScale = MathF.Min( damage * ImpulseScale( ForceTargetMass, ForceSpeedPerDamage ), ImpulseScale( ForceTargetMass, ExplosiveClampSpeed ) );

		forceScale *= Game.Random.Float( 0.85f, 1.15f );

		return Util.Normalize( direction ) * (forceScale * PushScale);
	}

	private static void Impact( in Trace trace, Vector3 position, Vector3 force )
	{
		Combat.DamageForce = force;

		if ( trace.Body is { MotionEnabled: true } body )
			body.ApplyImpulseAt( position, force );
	}

	public static void Explosion( Vector3 source, BaseEntity inflictor, float damage, float radius, float falloff )
	{
		foreach ( var body in Server.Current.FindSceneBodies( source, radius ) )
		{
			var spot = body.WorldPosition;
			var trace = Util.TraceLine( source, spot, BaseEntity.DontIgnoreMonsters, inflictor );

			if ( trace.Fraction != 1f && trace.Body != body )
				continue;

			if ( damage - (MathLib.Length( source - trace.EndPos ) * falloff) <= 0f )
				continue;

			body.ApplyImpulseAt( source, ExplosiveForce( damage, spot - source ) );
		}
	}
}
