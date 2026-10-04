using System;

namespace GoldSrc;

public static class MathLib
{
	public static void AngleVectors( Vector3 angles, out Vector3 forward, out Vector3 right, out Vector3 up )
	{
		var (sy, cy) = SinCos( angles.y );
		var (sp, cp) = SinCos( angles.x );
		var (sr, cr) = SinCos( angles.z );

		forward = new Vector3( cp * cy, cp * sy, -sp );
		right = new Vector3( (-1f * sr * sp * cy) + (-1f * cr * -sy), (-1f * sr * sp * sy) + (-1f * cr * cy), -1f * sr * cp );
		up = new Vector3( (cr * sp * cy) + (-sr * -sy), (cr * sp * sy) + (-sr * cy), cr * cp );
	}

	private const double DegreesToRadians = 0.017453292519943295;

	private static (float Sin, float Cos) SinCos( float degrees )
	{
		var (sin, cos) = Math.SinCos( (float)(degrees * DegreesToRadians) );

		return ((float)sin, (float)cos);
	}

	public static float AngleMod( float a )
	{
		return (float)(360.0 / 65536 * ((int)(a * (65536 / 360.0)) & 65535));
	}

	public static float VecToYaw( Vector3 vector )
	{
		if ( vector.y == 0f && vector.x == 0f )
			return 0f;

		var yaw = (float)(int)(Math.Atan2( vector.y, vector.x ) * 180.0 / Math.PI);

		if ( yaw < 0f )
			yaw += 360f;

		return yaw;
	}

	public static Vector3 VectorAngles( Vector3 forward )
	{
		float yaw;
		float pitch;

		if ( forward.y == 0f && forward.x == 0f )
		{
			yaw = 0f;
			pitch = forward.z > 0f ? 90f : 270f;
		}
		else
		{
			yaw = (float)(Math.Atan2( forward.y, forward.x ) * 180.0 / Math.PI);
			if ( yaw < 0f )
				yaw += 360f;

			var length = MathF.Sqrt( (forward.x * forward.x) + (forward.y * forward.y) );

			pitch = (float)(Math.Atan2( forward.z, length ) * 180.0 / Math.PI);
			if ( pitch < 0f )
				pitch += 360f;
		}

		return new Vector3( pitch, yaw, 0f );
	}

	public static float Length( Vector3 v )
	{
		return MathF.Sqrt( (v.x * v.x) + (v.y * v.y) + (v.z * v.z) );
	}

	public static float Length2D( Vector3 v ) => MathF.Sqrt( (v.x * v.x) + (v.y * v.y) );

	public static Vector2 Normalize2D( Vector3 v )
	{
		var length = Length2D( v );

		if ( length == 0f )
			return Vector2.Zero;

		length = 1f / length;

		return new Vector2( v.x * length, v.y * length );
	}

	public static float VectorNormalize( ref Vector3 v )
	{
		var length = MathF.Sqrt( (v.x * v.x) + (v.y * v.y) + (v.z * v.z) );

		if ( length != 0f )
		{
			var inverse = 1f / length;
			v.x *= inverse;
			v.y *= inverse;
			v.z *= inverse;
		}

		return length;
	}

	public static Vector3 CrossProduct( Vector3 a, Vector3 b )
	{
		return new Vector3( (a.y * b.z) - (a.z * b.y), (a.z * b.x) - (a.x * b.z), (a.x * b.y) - (a.y * b.x) );
	}

	public static void AngleVectorsTranspose( Vector3 angles, out Vector3 forward, out Vector3 right, out Vector3 up )
	{
		var (sy, cy) = SinCos( angles.y );
		var (sp, cp) = SinCos( angles.x );
		var (sr, cr) = SinCos( angles.z );

		forward = new Vector3( cp * cy, (sr * sp * cy) + (cr * -sy), (cr * sp * cy) + (-sr * -sy) );
		right = new Vector3( cp * sy, (sr * sp * sy) + (cr * cy), (cr * sp * sy) + (-sr * cy) );
		up = new Vector3( -sp, sr * cp, cr * cp );
	}

	public static void VectorMatrix( Vector3 forward, out Vector3 right, out Vector3 up )
	{
		if ( forward.x == 0f && forward.y == 0f )
		{
			right = new Vector3( 1f, 0f, 0f );
			up = new Vector3( -forward.z, 0f, 0f );
			return;
		}

		right = CrossProduct( forward, new Vector3( 0f, 0f, 1f ) );
		VectorNormalize( ref right );

		up = CrossProduct( right, forward );
		VectorNormalize( ref up );
	}

	public static void AngleMatrix( Vector3 angles, float[,] matrix )
	{
		var angle = angles.y * (MathF.PI * 2 / 360);
		var sy = MathF.Sin( angle );
		var cy = MathF.Cos( angle );

		angle = angles.x * (MathF.PI * 2 / 360);

		var sp = MathF.Sin( angle );
		var cp = MathF.Cos( angle );

		angle = angles.z * (MathF.PI * 2 / 360);

		var sr = MathF.Sin( angle );
		var cr = MathF.Cos( angle );

		matrix[0, 0] = cp * cy;
		matrix[1, 0] = cp * sy;
		matrix[2, 0] = -sp;
		matrix[0, 1] = (sr * sp * cy) + (cr * -sy);
		matrix[1, 1] = (sr * sp * sy) + (cr * cy);
		matrix[2, 1] = sr * cp;
		matrix[0, 2] = (cr * sp * cy) + (-sr * -sy);
		matrix[1, 2] = (cr * sp * sy) + (-sr * cy);
		matrix[2, 2] = cr * cp;
		matrix[0, 3] = 0f;
		matrix[1, 3] = 0f;
		matrix[2, 3] = 0f;
	}

	public static Vector3 VectorTransform( Vector3 v, float[,] matrix )
	{
		return new Vector3(
			(v.x * matrix[0, 0]) + (v.y * matrix[0, 1]) + (v.z * matrix[0, 2]) + matrix[0, 3],
			(v.x * matrix[1, 0]) + (v.y * matrix[1, 1]) + (v.z * matrix[1, 2]) + matrix[1, 3],
			(v.x * matrix[2, 0]) + (v.y * matrix[2, 1]) + (v.z * matrix[2, 2]) + matrix[2, 3] );
	}

	public static bool VectorCompare( Vector3 a, Vector3 b ) => a.x == b.x && a.y == b.y && a.z == b.z;

	public static Vector3 InterpolateAngles( ref Vector3 start, ref Vector3 end, float fraction )
	{
		start = NormalizeAngles( start );
		end = NormalizeAngles( end );

		var output = new Vector3();

		for ( var i = 0; i < 3; i++ )
		{
			var d = end[i] - start[i];

			if ( d > 180f )
				d -= 360f;
			else if ( d < -180f )
				d += 360f;

			output[i] = (d * fraction) + start[i];
		}

		return NormalizeAngles( output );
	}

	public static Vector3 NormalizeAngles( Vector3 angles )
	{
		for ( var i = 0; i < 3; i++ )
		{
			if ( angles[i] > 180f )
				angles[i] -= 360f;
			else if ( angles[i] < -180f )
				angles[i] += 360f;
		}

		return angles;
	}
}
