using System;

namespace GoldSrc;

partial class Client
{
	private const int MaxRingSegments = NoiseDivisions * 8;

	private readonly record struct BeamVertex( Vector3 Position, float U, float V, Color32 Color );

	private sealed class BeamMesh( DynamicMesh mesh, bool solid )
	{
		public DynamicMesh Mesh { get; } = mesh;
		public int Count;

		private BeamVertex _stripFirst;
		private BeamVertex _stripSecond;
		private bool _strip;

		public void Quad( in BeamVertex a, in BeamVertex b, in BeamVertex c, in BeamVertex d )
		{
			var vertices = Mesh.Vertices;

			if ( Count + 6 > vertices.Length )
				return;

			var first = Vertex( a, d.Color );
			var third = Vertex( c, d.Color );

			vertices[Count++] = first;
			vertices[Count++] = Vertex( b, d.Color );
			vertices[Count++] = third;
			vertices[Count++] = first;
			vertices[Count++] = third;
			vertices[Count++] = Vertex( d, d.Color );
		}

		public void BeginStrip() => _strip = false;

		public void Strip( in BeamVertex first, in BeamVertex second )
		{
			if ( _strip )
				Quad( first, _stripFirst, _stripSecond, second );

			_stripFirst = first;
			_stripSecond = second;
			_strip = true;
		}

		private Vertex Vertex( in BeamVertex vertex, Color32 provoking )
		{
			return new Vertex( vertex.Position, new Vector4( vertex.U, vertex.V, 0f, 0f ), solid ? provoking : vertex.Color );
		}
	}

	private static float BeamTextureLength( Vector3 delta ) => MathF.Max( MathLib.Length( delta ) * 0.01f, 0.5f );

	private static float BeamWrap( float value ) => (float)((double)value % 1.0);

	private void DrawSegments( Beam beam, BeamMesh mesh, float alpha )
	{
		var segments = beam.Segments;

		if ( segments < 2 )
			return;

		if ( segments > NoiseDivisions )
			segments = NoiseDivisions;

		var source = beam.Source;
		var delta = beam.Delta;
		var width = beam.Width;
		var freq = beam.Freq;
		var flags = beam.Flags;
		var sine = (flags & BeamFlags.SineNoise) == BeamFlags.SineNoise;

		var div = 1f / (segments - 1);
		var length = BeamTextureLength( delta );
		var step = div * length;
		var last = BeamWrap( freq * beam.Speed );
		var scale = length;

		if ( sine )
		{
			if ( segments < 16 )
				segments = 16;

			length = segments * 0.1f;
			scale = 100f;
		}

		scale *= beam.Amplitude;

		var screenLast = BeamScreenTransform( source );
		var point = source + (delta * div);
		var normal = BeamNormal( BeamScreenTransform( point ), screenLast );
		var last1 = source + (normal * width);
		var last2 = source - (normal * width);

		var noiseStep = (int)(div * 128f * 65536f);
		var noiseIndex = sine ? 0 : noiseStep;
		var brightness = (flags & BeamFlags.ShadeIn) == BeamFlags.ShadeIn ? 0f : 1f;

		for ( var i = 1; i < segments; i++ )
		{
			var fraction = i * div;
			var lastColor = BeamColor( beam, alpha, brightness );

			if ( (flags & BeamFlags.ShadeIn) == BeamFlags.ShadeIn )
				brightness = fraction;
			else if ( (flags & BeamFlags.ShadeOut) == BeamFlags.ShadeOut )
				brightness = 1f - fraction;

			point = source + (delta * fraction);

			if ( scale != 0f )
			{
				var factor = scale * _beamNoise[(noiseIndex >> 16) & 0x7F];

				if ( sine )
				{
					var angle = (fraction * Math.PI * length) + freq;

					point += _beamUp * (float)(Math.Sin( angle ) * factor);
					point += _beamRight * (float)(Math.Cos( angle ) * factor);
				}
				else
				{
					point += _beamUp * factor;
					point += _beamRight * (float)(Math.Cos( (fraction * Math.PI * 3.0) + freq ) * factor);
				}
			}

			var screen = BeamScreenTransform( point );

			normal = BeamNormal( screen, screenLast );

			var next1 = point + (normal * width);
			var next2 = point - (normal * width);
			var next = last + step;
			var color = BeamColor( beam, alpha, brightness );

			mesh.Quad( new BeamVertex( last1, 0f, last, lastColor ), new BeamVertex( last2, 1f, last, lastColor ), new BeamVertex( next2, 1f, next, color ), new BeamVertex( next1, 0f, next, color ) );

			last1 = next1;
			last2 = next2;
			screenLast = screen;
			last = BeamWrap( next );
			noiseIndex += noiseStep;
		}
	}

	private void DrawTorus( Beam beam, BeamMesh mesh, float alpha )
	{
		var segments = beam.Segments;

		if ( segments < 2 )
			return;

		if ( segments > NoiseDivisions )
			segments = NoiseDivisions;

		var source = beam.Source;
		var freq = beam.Freq;
		var div = 1f / (segments - 1);
		var length = BeamTextureLength( beam.Delta );
		var step = div * length;
		var last = BeamWrap( freq * beam.Speed );
		var scale = length * beam.Amplitude;
		var noiseStep = (int)(div * 128f * 65536f);
		var noiseIndex = 0;
		var color = BeamColor( beam, alpha, 1f );
		var screenLast = Vector3.Zero;

		mesh.BeginStrip();

		for ( var i = 0; i <= segments; i++ )
		{
			var fraction = i * div;
			var angle = (double)(fraction + fraction) * Math.PI;
			var factor = scale * _beamNoise[(noiseIndex >> 16) & 0x7F];

			var point = new Vector3(
				(float)((Math.Sin( angle ) * freq * beam.Delta.z) + source.x),
				(float)((Math.Cos( angle ) * freq * beam.Delta.z) + source.y),
				source.z );

			point += _beamUp * factor;
			point += _beamRight * (float)(Math.Cos( (fraction * Math.PI * 3.0) + freq ) * factor);

			var screen = BeamScreenTransform( point );

			if ( i != 0 )
			{
				var normal = BeamNormal( screen, screenLast );

				last += step;

				mesh.Strip( new BeamVertex( point - (normal * beam.Width), 1f, last, color ), new BeamVertex( point + (normal * beam.Width), 0f, last, color ) );
			}

			last = BeamWrap( last );
			noiseIndex += noiseStep;
			screenLast = screen;
		}
	}

	private void DrawDisk( Beam beam, BeamMesh mesh, float alpha )
	{
		var segments = beam.Segments;

		if ( segments < 2 )
			return;

		if ( segments > NoiseDivisions )
			segments = NoiseDivisions;

		var source = beam.Source;
		var div = 1f / (segments - 1);
		var step = BeamTextureLength( beam.Delta ) * div;
		var last = BeamWrap( beam.Freq * beam.Speed );
		var radius = beam.Freq * beam.Delta.z;
		var color = BeamColor( beam, alpha, 1f );

		mesh.BeginStrip();

		for ( var i = 0; i < segments; i++ )
		{
			var fraction = i * div;
			var angle = (double)(fraction + fraction) * Math.PI;
			var point = new Vector3( (float)((Math.Sin( angle ) * radius) + source.x), (float)((Math.Cos( angle ) * radius) + source.y), source.z );

			mesh.Strip( new BeamVertex( source, 1f, last, color ), new BeamVertex( point, 0f, last, color ) );

			last = BeamWrap( last + step );
		}
	}

	private void DrawCylinder( Beam beam, BeamMesh mesh, float alpha )
	{
		var segments = beam.Segments;

		if ( segments < 2 )
			return;

		if ( segments > NoiseDivisions )
			segments = NoiseDivisions;

		var source = beam.Source;
		var width = beam.Width;
		var div = 1f / (segments - 1);
		var step = BeamTextureLength( beam.Delta ) * div;
		var last = BeamWrap( beam.Freq * beam.Speed );
		var dark = BeamColor( beam, alpha, 0f );
		var bright = BeamColor( beam, alpha, 1f );

		mesh.BeginStrip();

		for ( var i = 0; i < segments; i++ )
		{
			var fraction = i * div;
			var angle = (double)(fraction + fraction) * Math.PI;
			var x = Math.Sin( angle ) * beam.Freq;
			var y = Math.Cos( angle ) * beam.Freq;
			var outer = (double)(width + beam.Delta.z);

			var top = new Vector3( (float)((x * beam.Delta.z) + source.x), (float)((y * beam.Delta.z) + source.y), width + source.z );
			var bottom = new Vector3( (float)((x * outer) + source.x), (float)((y * outer) + source.y), source.z - width );

			mesh.Strip( new BeamVertex( top, 1f, last, dark ), new BeamVertex( bottom, 0f, last, bright ) );

			last = BeamWrap( last + step );
		}
	}

	private void DrawRing( Beam beam, BeamMesh mesh, float alpha )
	{
		if ( beam.Segments < 2 )
			return;

		var segments = Math.Min( (int)(beam.Segments * Math.PI), MaxRingSegments );
		var freq = beam.Freq;
		var length = MathF.Max( (float)(MathLib.Length( beam.Delta ) * 0.01 * Math.PI), 0.5f );
		var div = 1f / (segments - 1);
		var step = div * length * 0.125f;
		var last = BeamWrap( freq * beam.Speed );
		var scale = length * beam.Amplitude * 0.125f;
		var noiseStep = (int)(div * 128f * 65536f) * 8;
		var noiseIndex = 0;

		beam.Delta *= 0.5f;

		var xAxis = beam.Delta;
		var center = beam.Source + xAxis;
		var radius = MathLib.Length( xAxis );
		var extent = new Vector3( radius, radius, scale );

		if ( !BoxInViewPvs( center - extent, center + extent ) || CullBox( center - extent, center + extent ) )
			return;

		var yAxis = new Vector3( xAxis.y, -xAxis.x, 0f );

		MathLib.VectorNormalize( ref yAxis );
		yAxis *= radius;

		var resample = segments / 8;
		var remaining = resample;
		var color = BeamColor( beam, alpha, 1f );
		var screenLast = Vector3.Zero;

		mesh.BeginStrip();

		for ( var i = 0; i <= segments; i++ )
		{
			var fraction = i * div;
			var angle = (double)(fraction + fraction) * Math.PI;
			var factor = scale * _beamNoise[(noiseIndex >> 16) & 0x7F];
			var point = (yAxis * (float)Math.Cos( angle )) + (xAxis * (float)Math.Sin( angle )) + center;

			point += _beamUp * factor;
			point += _beamRight * (float)(Math.Cos( (fraction * Math.PI * 3.0 * 8.0) + freq ) * factor);

			var screen = BeamScreenTransform( point );

			if ( i != 0 )
			{
				var normal = BeamNormal( screen, screenLast );

				last += step;

				mesh.Strip( new BeamVertex( point - (normal * beam.Width), 1f, last, color ), new BeamVertex( point + (normal * beam.Width), 0f, last, color ) );
			}

			screenLast = screen;
			last = BeamWrap( last );
			noiseIndex += noiseStep;

			if ( --remaining == 0 && beam.Amplitude != 0f )
			{
				BeamNoise( 0, NoiseDivisions );
				remaining = resample;
			}
		}
	}

	private void DrawFollow( Beam beam, BeamMesh mesh, float alpha, float frameTime )
	{
		FreeDeadParticles( ref beam.Particles );

		var particles = beam.Particles;
		var distance = 0f;
		var added = false;

		if ( (beam.Flags & BeamFlags.StartEntity) == BeamFlags.StartEntity )
		{
			if ( particles is not null )
				distance = MathLib.Length( particles.Org - beam.Source );
			else if ( _freeParticles is null )
				return;

			if ( _freeParticles is { } particle && (particles is null || distance >= 32f) )
			{
				_freeParticles = particle.Next;

				particle.Org = beam.Source;
				particle.Vel = Vector3.Zero;
				particle.Die = (float)(beam.Amplitude + ClientTime);
				particle.Next = particles;

				beam.Die = (float)(beam.Amplitude + ClientTime);
				beam.Particles = particle;

				particles = particle;
				added = true;
			}
		}

		if ( particles is null )
			return;

		Vector3 start;

		if ( !added && distance != 0f )
		{
			start = beam.Source;
		}
		else
		{
			if ( particles.Next is null )
				return;

			start = particles.Org;
			particles = particles.Next;
		}

		var screenLast = BeamScreenTransform( start );
		var normal = BeamNormal( BeamScreenTransform( particles.Org ), screenLast );
		var last1 = start + (normal * beam.Width);
		var last2 = start - (normal * beam.Width);
		var scale = (double)(1f / beam.Amplitude);
		var fraction = (float)((beam.Die - ClientTime) * scale);

		for ( ; particles is not null; particles = particles.Next )
		{
			var lastColor = BeamColor( beam, alpha, fraction );
			var screen = BeamScreenTransform( particles.Org );

			normal = BeamNormal( screen, screenLast );

			var next1 = particles.Org + (normal * beam.Width);
			var next2 = particles.Org - (normal * beam.Width);

			fraction = particles.Next is null ? 0f : (float)((particles.Die - ClientTime) * scale);

			var color = BeamColor( beam, alpha, fraction );

			mesh.Quad( new BeamVertex( last1, 0f, 0f, lastColor ), new BeamVertex( last2, 1f, 0f, lastColor ), new BeamVertex( next2, 1f, 1f, color ), new BeamVertex( next1, 0f, 1f, color ) );

			last1 = next1;
			last2 = next2;
			screenLast = screen;
		}

		for ( var particle = beam.Particles; particle is not null; particle = particle.Next )
			particle.Org += particle.Vel * frameTime;
	}
}
