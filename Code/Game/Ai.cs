using System;
using GoldSrc;

namespace HalfLife;

public static class Ai
{
	public static bool BoxVisible( BaseEntity looker, BaseEntity target, ref Vector3 targetOrigin, float size )
	{
		if ( (looker.WaterLevel != 3 && target.WaterLevel == 3) || (looker.WaterLevel == 3 && target.WaterLevel == 0) )
			return false;

		var lookerOrigin = looker.Origin + looker.ViewOffset;

		for ( var i = 0; i < 5; i++ )
		{
			var spot = target.Origin;

			spot.x += Server.RandomFloat( target.Mins.x + size, target.Maxs.x - size );
			spot.y += Server.RandomFloat( target.Mins.y + size, target.Maxs.y - size );
			spot.z += Server.RandomFloat( target.Mins.z + size, target.Maxs.z - size );

			var trace = Util.TraceLine( lookerOrigin, spot, BaseEntity.IgnoreMonsters | BaseEntity.IgnoreGlass, looker );

			if ( trace.Fraction == 1.0 )
			{
				targetOrigin = spot;
				return true;
			}
		}

		return false;
	}

	public static Vector3 VecCheckToss( BaseEntity entity, Vector3 spot1, Vector3 spot2, float gravityAdjust = 1f )
	{
		var globals = entity.Server.Globals;
		var gravity = Cvar.GetFloat( "sv_gravity" ) * gravityAdjust;

		if ( spot2.z - spot1.z > 500f )
			return Vector3.Zero;

		globals.MakeVectors( entity.Angles );

		spot2 += globals.Right * (Server.RandomFloat( -8f, 8f ) + Server.RandomFloat( -16f, 16f ));
		spot2 += globals.Forward * (Server.RandomFloat( -8f, 8f ) + Server.RandomFloat( -16f, 16f ));

		var midPoint = spot1 + ((spot2 - spot1) * 0.5f);
		var trace = Util.TraceLine( midPoint, midPoint + new Vector3( 0f, 0f, 500f ), BaseEntity.IgnoreMonsters, entity );

		midPoint = trace.EndPos;
		midPoint.z -= 15f;

		if ( midPoint.z < spot1.z || midPoint.z < spot2.z )
			return Vector3.Zero;

		var distance1 = midPoint.z - spot1.z;
		var distance2 = midPoint.z - spot2.z;

		var time1 = (float)Math.Sqrt( distance1 / (0.5 * gravity) );
		var time2 = (float)Math.Sqrt( distance2 / (0.5 * gravity) );

		if ( time1 < 0.1 )
			return Vector3.Zero;

		var grenadeVelocity = (spot2 - spot1) / (time1 + time2);

		grenadeVelocity.z = gravity * time1;

		var apex = spot1 + (grenadeVelocity * time1);

		apex.z = midPoint.z;

		trace = Util.TraceLine( spot1, apex, BaseEntity.DontIgnoreMonsters, entity );

		if ( trace.Fraction != 1.0 )
			return Vector3.Zero;

		trace = Util.TraceLine( spot2, apex, BaseEntity.IgnoreMonsters, entity );

		if ( trace.Fraction != 1.0 )
			return Vector3.Zero;

		return grenadeVelocity;
	}

	public static Vector3 VecCheckThrow( BaseEntity entity, Vector3 spot1, Vector3 spot2, float speed, float gravityAdjust = 1f )
	{
		var gravity = Cvar.GetFloat( "sv_gravity" ) * gravityAdjust;
		var grenadeVelocity = spot2 - spot1;
		var time = MathLib.Length( grenadeVelocity ) / speed;

		grenadeVelocity *= (float)(1.0 / time);
		grenadeVelocity.z = (float)(grenadeVelocity.z + (gravity * time * 0.5));

		var apex = spot1 + ((spot2 - spot1) * 0.5f);

		apex.z = (float)(apex.z + (0.5 * gravity * (time * 0.5) * (time * 0.5)));

		var trace = Util.TraceLine( spot1, apex, BaseEntity.DontIgnoreMonsters, entity );

		if ( trace.Fraction != 1.0 )
			return Vector3.Zero;

		trace = Util.TraceLine( spot2, apex, BaseEntity.IgnoreMonsters, entity );

		if ( trace.Fraction != 1.0 )
			return Vector3.Zero;

		return grenadeVelocity;
	}
}
