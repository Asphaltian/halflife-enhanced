using System;
using GoldSrc;

namespace HalfLife;

public sealed class Interpolation
{
	private Vector3 _startPoint;
	private Vector3 _endPoint;
	private Vector3 _startAngle;
	private Vector3 _endAngle;
	private Vector3 _center;
	private float _startFov;
	private float _endFov;
	private bool _smoothStart;
	private bool _smoothEnd;

	private static float Determinant( Vector3 a, Vector3 b, Vector3 c )
	{
		return (a.z * ((b.x * c.y) - (b.y * c.x))) + (a.y * ((b.z * c.x) - (b.x * c.z))) + (a.x * ((b.y * c.z) - (b.z * c.y)));
	}

	private static float Length( Vector3 v ) => (float)Math.Sqrt( (v.x * v.x) + (v.y * v.y) + (v.z * v.z) );

	private static Vector3 Normalize( Vector3 v )
	{
		var length = Length( v );

		if ( length == 0 )
			return new Vector3( 0, 0, 1 );

		length = 1 / length;

		return new Vector3( v.x * length, v.y * length, v.z * length );
	}

	private static Vector3 Subtract( Vector3 a, Vector3 b ) => new( a.x - b.x, a.y - b.y, a.z - b.z );

	private static Vector3 MultiplyAdd( Vector3 a, float scale, Vector3 b ) => new( a.x + (b.x * scale), a.y + (b.y * scale), a.z + (b.z * scale) );

	private static bool GetPointBetweenLines( ref Vector3 p, Vector3 a1, Vector3 m1, Vector3 a2, Vector3 m2 )
	{
		var t1 = MathLib.CrossProduct( m1, m2 );
		var t2 = Subtract( a2, a1 );

		var d = Determinant( m1, t1, m2 );

		if ( d == 0.0f )
			return false;

		var x = Determinant( t2, t1, m2 ) / d;
		var z = Determinant( m1, t1, t2 ) / d;

		t1 = MultiplyAdd( a1, x, m1 );
		t2 = MultiplyAdd( a2, -z, m2 );

		p = new Vector3( (t1.x + t2.x) / 2.0f, (t1.y + t2.y) / 2.0f, (t1.z + t2.z) / 2.0f );

		return true;
	}

	public void SetViewAngles( Vector3 start, Vector3 end )
	{
		_startAngle = MathLib.NormalizeAngles( start );
		_endAngle = MathLib.NormalizeAngles( end );
	}

	public void SetFovs( float start, float end )
	{
		_startFov = start;
		_endFov = end;
	}

	public void SetWaypoints( Vector3? prev, Vector3 start, Vector3 end, Vector3? next )
	{
		_startPoint = start;
		_endPoint = end;

		if ( prev is null && next is null )
		{
			_center = new Vector3( (_startPoint.x + _endPoint.x) / 2.0f, (_startPoint.y + _endPoint.y) / 2.0f, (_startPoint.z + _endPoint.z) / 2.0f );
		}
		else if ( prev is null )
		{
			var a = Subtract( start, end );
			var dist = Length( a ) / 2.0f;

			a = Normalize( a );

			var b = Normalize( Subtract( next.Value, end ) );
			var c = Normalize( Subtract( a, b ) );

			_center = MultiplyAdd( end, dist, c );
		}
		else if ( next is null )
		{
			var a = Normalize( Subtract( prev.Value, start ) );
			var b = Subtract( end, start );
			var dist = Length( b ) / 2.0f;

			b = Normalize( b );

			var c = Normalize( Subtract( b, a ) );

			_center = MultiplyAdd( start, dist, c );
		}
		else
		{
			var a = Normalize( Subtract( prev.Value, start ) );
			var b = Normalize( Subtract( end, start ) );
			var c = Subtract( b, a );

			a = Normalize( Subtract( start, end ) );
			b = Normalize( Subtract( next.Value, end ) );

			var d = Subtract( a, b );

			GetPointBetweenLines( ref _center, start, c, end, d );
		}
	}

	public void SetSmoothing( bool start, bool end )
	{
		_smoothStart = start;
		_smoothEnd = end;
	}

	public void Interpolate( float t, out Vector3 point, out Vector3 angle, out float fov )
	{
		if ( _smoothStart && _smoothEnd )
		{
			t = ((1.0f - t) * (t * t)) + (t * (1.0f - ((t - 1.0f) * (t - 1.0f))));
		}
		else if ( _smoothStart )
		{
			t = t * t;
		}
		else if ( _smoothEnd )
		{
			t = t - 1.0f;
			t = -(t * t) + 1;
		}

		point = BezierInterpolatePoint( t );
		angle = InterpolateAngle( t );
		fov = _startFov + (t * (_endFov - _startFov));
	}

	private Vector3 BezierInterpolatePoint( float t )
	{
		var b0 = (1.0f - t) * (1.0f - t);
		var b1 = 2.0f * t * (1.0f - t);
		var b2 = t * t;

		var point = new Vector3( _startPoint.x * b0, _startPoint.y * b0, _startPoint.z * b0 );

		point = new Vector3( point.x + (_center.x * b1), point.y + (_center.y * b1), point.z + (_center.z * b1) );

		return new Vector3( point.x + (_endPoint.x * b2), point.y + (_endPoint.y * b2), point.z + (_endPoint.z * b2) );
	}

	private Vector3 InterpolateAngle( float t )
	{
		var angle = new Vector3();

		for ( var i = 0; i < 3; i++ )
		{
			var ang1 = _startAngle[i];
			var ang2 = _endAngle[i];

			var d = ang2 - ang1;

			if ( d > 180 )
				d -= 360;
			else if ( d < -180 )
				d += 360;

			angle[i] = ang1 + (d * t);
		}

		return MathLib.NormalizeAngles( angle );
	}
}
