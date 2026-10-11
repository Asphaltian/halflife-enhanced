using System;

namespace GoldSrc;

partial class Client
{
	private const int MaxUserTracerColor = 12;
	private const float ParticleLineStep = 2f;

	private static readonly int[][] BoxFaces =
	[
		[0, 4, 6, 2],
		[0, 1, 5, 4],
		[0, 2, 3, 1],
		[7, 5, 1, 3],
		[7, 3, 2, 6],
		[7, 6, 4, 5]
	];

	private readonly Vector3[] _boxCorners = new Vector3[8];

	public void BlobExplosion( Vector3 origin )
	{
		for ( var i = 0; i < 1024; i++ )
		{
			if ( NextParticle() is not { } particle )
				return;

			particle.Die = (float)(Server.RandomFloat( 1f, 1.4f ) + ClientTime);

			if ( (i & 1) == 0 )
			{
				particle.Type = ParticleType.Blob2;
				particle.Color = (short)Server.RandomLong( 150, 155 );
			}
			else
			{
				particle.Type = ParticleType.Blob;
				particle.Color = (short)Server.RandomLong( 66, 71 );
			}

			particle.PackedColor = 0;

			for ( var j = 0; j < 3; j++ )
			{
				particle.Org[j] = Server.RandomFloat( -16f, 16f ) + origin[j];
				particle.Vel[j] = Server.RandomFloat( -256f, 256f );
			}
		}
	}

	public void ParticleExplosion2( Vector3 origin, int colorStart, int colorLength )
	{
		for ( var i = 0; i < 512; i++ )
		{
			if ( NextParticle() is not { } particle )
				return;

			particle.Die = (float)(ClientTime + 0.3);
			particle.Color = (short)(colorStart + (i % colorLength));
			particle.PackedColor = 0;
			particle.Type = ParticleType.Blob;

			for ( var j = 0; j < 3; j++ )
			{
				particle.Org[j] = Server.RandomFloat( -16f, 16f ) + origin[j];
				particle.Vel[j] = Server.RandomFloat( -256f, 256f );
			}
		}
	}

	public void LavaSplash( Vector3 origin )
	{
		for ( var i = -128; i < 128; i += 8 )
		{
			for ( var j = -128; j < 128; j += 8 )
			{
				if ( NextParticle() is not { } particle )
					return;

				particle.Die = (float)(Server.RandomFloat( 2f, 2.62f ) + ClientTime);
				particle.Color = (short)Server.RandomLong( 224, 231 );
				particle.Type = ParticleType.SlowGrav;
				particle.PackedColor = 0;

				var x = Server.RandomFloat( 0f, 7f ) + j;
				var y = Server.RandomFloat( 0f, 7f ) + i;
				var direction = new Vector3( x, y, 256f );

				particle.Org.x = x + origin.x;
				particle.Org.y = y + origin.y;
				particle.Org.z = Server.RandomFloat( 0f, 63f ) + origin.z;

				MathLib.VectorNormalize( ref direction );

				particle.Vel = direction * Server.RandomFloat( 50f, 113f );
			}
		}
	}

	public void TeleportSplash( Vector3 origin )
	{
		for ( var i = -16; i < 16; i += 4 )
		{
			for ( var j = -16; j < 16; j += 4 )
			{
				for ( var k = -24; k < 32; k += 4 )
				{
					if ( NextParticle() is not { } particle )
						return;

					particle.Die = (float)(Server.RandomFloat( 0.2f, 0.34f ) + ClientTime);
					particle.Color = (short)Server.RandomLong( 7, 14 );
					particle.PackedColor = 0;
					particle.Type = ParticleType.SlowGrav;

					var direction = new Vector3( j * 8, i * 8, k * 8 );

					particle.Org.x = Server.RandomFloat( 0f, 3f ) + (i + origin.x);
					particle.Org.y = Server.RandomFloat( 0f, 3f ) + (j + origin.y);
					particle.Org.z = Server.RandomFloat( 0f, 3f ) + (k + origin.z);

					MathLib.VectorNormalize( ref direction );

					particle.Vel = direction * Server.RandomFloat( 50f, 113f );
				}
			}
		}
	}

	public void ParticleBurst( Vector3 origin, int size, int color, float life )
	{
		for ( var i = 0; i < 32 * 32; i++ )
		{
			if ( NextParticle() is not { } particle )
				return;

			particle.Color = (short)(Server.RandomLong( 0, 10 ) + color);
			particle.PackedColor = 0;
			particle.Type = ParticleType.Static;
			particle.Org = origin;

			var x = Server.RandomFloat( -size, size );
			var y = Server.RandomFloat( -size, size );
			var z = Server.RandomFloat( -size, size );
			var direction = new Vector3( origin.x + x - particle.Org.x, y + origin.y - particle.Org.y, z + origin.z - particle.Org.z );
			var distance = MathLib.VectorNormalize( ref direction );

			particle.Die = (float)(life + ClientTime + Server.RandomFloat( -0.5f, 0.5f ));
			particle.Vel = direction * (distance / life);
		}
	}

	private void StaticParticle( Vector3 origin, int color, float life, int heightOffset, int riseSpeed )
	{
		if ( NextParticle() is not { } particle )
			return;

		particle.Color = (short)color;
		particle.PackedColor = 0;
		particle.Type = ParticleType.Static;
		particle.Die = (float)(life + ClientTime);
		particle.Vel = new Vector3( 0f, 0f, riseSpeed );
		particle.Org = new Vector3( origin.x, origin.y, heightOffset + origin.z );
	}

	public void ParticleLine( Vector3 start, Vector3 end, int color, float life, float riseSpeed = 0f )
	{
		var direction = end - start;
		var length = MathLib.VectorNormalize( ref direction );

		for ( var distance = 0f; distance <= length; distance += ParticleLineStep )
			StaticParticle( start + (direction * distance), color, life, 0, (int)riseSpeed );
	}

	public void ParticleBox( Vector3 mins, Vector3 maxs, Vector3 origin, int color, float life )
	{
		for ( var i = 0; i < 8; i++ )
		{
			var x = (i & 1) != 0 ? mins.x : maxs.x;
			var y = (i & 2) != 0 ? mins.y : maxs.y;
			var z = (i & 4) != 0 ? mins.z : maxs.z;

			_boxCorners[i] = origin + new Vector3( x, y, z );
		}

		foreach ( var face in BoxFaces )
		{
			ParticleLine( _boxCorners[face[1]], _boxCorners[face[0]], color, life );
			ParticleLine( _boxCorners[face[0]], _boxCorners[face[3]], color, life );
			ParticleLine( _boxCorners[face[3]], _boxCorners[face[2]], color, life );
			ParticleLine( _boxCorners[face[2]], _boxCorners[face[1]], color, life );
		}
	}

	private static int BasePaletteComponent( byte value ) => (ushort)(sbyte)value;

	public int LookupColor( byte r, byte g, byte b )
	{
		_palette ??= Server.LoadMountFile( "gfx/palette.lmp" );

		var best = 999999f;
		var index = -1;

		for ( var i = 0; i < 256; i++ )
		{
			float red = r - BasePaletteComponent( _palette[i * 3] );
			float green = g - BasePaletteComponent( _palette[(i * 3) + 1] );
			float blue = b - BasePaletteComponent( _palette[(i * 3) + 2] );
			var distance = (green * 0.5f * green) + (red * 0.3f * red) + (blue * 0.2f * blue);

			if ( distance < best )
			{
				best = distance;
				index = i;
			}
		}

		return index;
	}

	public void UserTracerParticle( Vector3 origin, Vector3 velocity, float life, int color, float length, byte context, Action<Particle> deathFunc )
	{
		if ( color < 0 )
		{
			Con.Printf( "UserTracer with color < 0\n" );
			return;
		}

		if ( color > MaxUserTracerColor )
		{
			Con.Printf( $"UserTracer with color > {MaxUserTracerColor}\n" );
			return;
		}

		if ( NextTracer() is not { } tracer )
			return;

		tracer.Color = (short)color;
		tracer.PackedColor = 255;
		tracer.Type = ParticleType.Static;
		tracer.Die = (float)(life + ClientTime);
		tracer.Ramp = length;
		tracer.Org = origin;
		tracer.Vel = velocity;
		tracer.DeathFunc = deathFunc;
		tracer.Context = context;
	}
}
