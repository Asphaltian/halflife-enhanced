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

		if ( trace.Body is not { MotionEnabled: true } body )
			return;

		body.ApplyImpulseAt( trace.EndPos, Util.Normalize( direction ) * (DamageForce( bulletType ) * PushScale) );
	}

	public static void HornetImpact( in Trace trace, Vector3 velocity, Vector3 origin )
	{
		if ( trace.Body is not { MotionEnabled: true } body )
			return;

		body.ApplyImpulseAt( origin, Util.Normalize( velocity ) * (BulletImpulse( 100f, 1200f ) * PushScale) );
	}

	public static void MeleeImpact( in Trace trace, float damage, Vector3 direction )
	{
		if ( trace.Body is not { MotionEnabled: true } body )
			return;

		body.ApplyImpulseAt( trace.EndPos, Util.Normalize( direction ) * (damage * ImpulseScale( ForceTargetMass, ForceSpeedPerDamage ) * PushScale) );
	}

	public static void Explosion( Vector3 source, BaseEntity inflictor, float damage, float radius, float falloff )
	{
		foreach ( var body in Server.Current.FindSceneBodies( source, radius ) )
		{
			var spot = body.WorldPosition;
			var trace = Util.TraceLine( source, spot, BaseEntity.DontIgnoreMonsters, inflictor );

			if ( trace.Fraction != 1f && trace.Body != body )
				continue;

			var adjustedDamage = damage - (MathLib.Length( source - trace.EndPos ) * falloff);

			if ( adjustedDamage <= 0f )
				continue;

			var forceScale = MathF.Min( adjustedDamage * ImpulseScale( ForceTargetMass, ForceSpeedPerDamage ), ImpulseScale( ForceTargetMass, ExplosiveClampSpeed ) );

			forceScale *= Game.Random.Float( 0.85f, 1.15f );

			body.ApplyImpulseAt( source, Util.Normalize( spot - source ) * (forceScale * PushScale) );
		}
	}
}
