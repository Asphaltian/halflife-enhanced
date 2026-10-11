using System;

namespace GoldSrc;

public enum ParticleType
{
	Static,
	Grav,
	SlowGrav,
	Fire,
	Explode,
	Explode2,
	Blob,
	Blob2,
	VoxSlowGrav,
	VoxGrav,
	ClientCustom
}

public sealed class Particle
{
	public Vector3 Org;
	public short Color;
	public short PackedColor;
	public Particle Next;
	public Vector3 Vel;
	public float Ramp;
	public float Die;
	public ParticleType Type;
	public Action<Particle> DeathFunc;
	public Action<Particle, float> Callback;
	public byte Context;
}

partial class Client
{
	private const int MaxParticles = 2048;
	private const int ParticleTextureSize = 8;

	[ConVar( "tracerspeed" )]
	public static float TracerSpeed { get; set; } = 6000f;

	[ConVar( "traceroffset" )]
	public static float TracerOffset { get; set; } = 30f;

	[ConVar( "tracerlength" )]
	public static float TracerLength { get; set; } = 0.8f;

	[ConVar( "tracerred" )]
	public static float TracerRed { get; set; } = 0.8f;

	[ConVar( "tracergreen" )]
	public static float TracerGreen { get; set; } = 0.8f;

	[ConVar( "tracerblue" )]
	public static float TracerBlue { get; set; } = 0.4f;

	[ConVar( "traceralpha" )]
	public static float TracerAlpha { get; set; } = 0.5f;

	private static readonly short[] Ramp1 = [0x6f, 0x6d, 0x6b, 0x69, 0x67, 0x65, 0x63, 0x61];
	private static readonly short[] Ramp2 = [0x6f, 0x6e, 0x6d, 0x6c, 0x6b, 0x6a, 0x68, 0x66];
	private static readonly short[] Ramp3 = [0x6d, 0x6b, 6, 5, 4, 3];
	private static readonly short[] SparkRamp = [0xfe, 0xfd, 0xfc, 0x6f, 0x6e, 0x6d, 0x6c, 0x67, 0x60];

	private static readonly byte[] DotTexture =
	[
		0, 1, 1, 0, 0, 0, 0, 0,
		1, 1, 1, 1, 0, 0, 0, 0,
		1, 1, 1, 1, 0, 0, 0, 0,
		0, 1, 1, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0
	];

	private static readonly float[] TracerSize = [1.5f, 0.5f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f];

	private readonly byte[][] _tracerColors =
	[
		[255, 255, 255],
		[255, 0, 0],
		[0, 255, 0],
		[0, 0, 255],
		[0, 0, 0],
		[255, 167, 17],
		[255, 130, 90],
		[55, 60, 144],
		[255, 130, 90],
		[255, 140, 90],
		[200, 130, 90],
		[255, 120, 70]
	];

	private readonly DynamicMesh[] _particleMeshes = new DynamicMesh[ViewCount];
	private readonly DynamicMesh[] _tracerMeshes = new DynamicMesh[ViewCount];

	private Particle _freeParticles;
	private Particle _activeParticles;
	private Particle _activeTracers;
	private byte[] _palette;
	private Vector3 _viewOrigin;
	private Texture _particleTexture;

	private void ClearParticles()
	{
		_freeParticles = null;
		_activeParticles = null;
		_activeTracers = null;

		for ( var i = 0; i < MaxParticles; i++ )
			_freeParticles = new Particle { Next = _freeParticles };
	}

	public Particle AllocParticle( Action<Particle, float> callback )
	{
		if ( _freeParticles is not { } particle )
		{
			Log.Trace( "Not enough free particles" );
			return null;
		}

		_freeParticles = particle.Next;
		particle.Next = _activeParticles;
		_activeParticles = particle;

		particle.Type = ParticleType.ClientCustom;
		particle.Die = (float)ClientTime;
		particle.Callback = callback;
		particle.Color = 0;
		particle.PackedColor = 0;
		particle.DeathFunc = null;
		particle.Org = Vector3.Zero;
		particle.Vel = Vector3.Zero;
		particle.Ramp = 0f;

		return particle;
	}

	public Particle TracerParticles( Vector3 origin, Vector3 velocity, float life )
	{
		if ( _freeParticles is not { } particle )
			return null;

		_freeParticles = particle.Next;
		particle.Next = _activeTracers;
		_activeTracers = particle;

		particle.Die = (float)(life + ClientTime);
		particle.Color = 4;
		particle.PackedColor = 255;
		particle.Type = ParticleType.Static;
		particle.Ramp = TracerLength;
		particle.Org = origin;
		particle.Vel = velocity;

		return particle;
	}

	public void TracerEffect( Vector3 start, Vector3 end )
	{
		if ( TracerSpeed <= 0f )
			TracerSpeed = 3f;

		var direction = end - start;
		var length = MathLib.Length( direction );

		direction *= 1f / length;

		start += direction * (Server.RandomLong( -10, 9 ) + TracerOffset);

		TracerParticles( start, direction * TracerSpeed, length / TracerSpeed );
	}

	public void BulletImpactParticles( Vector3 position )
	{
		var distance = MathF.Min( MathLib.Length( position - _viewOrigin ), 1000f );
		var quantity = (int)((1000f - distance) / 100f);

		if ( quantity == 0 )
			quantity = 1;

		var color = (short)(quantity * 30 / 100);

		for ( var i = 0; i < 2; i++ )
		{
			if ( _freeParticles is not { } tracer )
				break;

			_freeParticles = tracer.Next;
			tracer.Next = _activeTracers;
			_activeTracers = tracer;

			var life = Server.RandomFloat( 0.1f, 0.5f );

			tracer.Color = 5;
			tracer.PackedColor = 255;
			tracer.Type = ParticleType.Grav;
			tracer.Ramp = 0.5f;
			tracer.Die = (float)(life + ClientTime);
			tracer.Org = position;

			for ( var j = 0; j < 3; j++ )
				tracer.Vel[j] = Server.RandomFloat( -200f, 200f );
		}

		for ( var i = 0; i < quantity * 4; i++ )
		{
			if ( _freeParticles is not { } particle )
				return;

			_freeParticles = particle.Next;
			particle.Next = _activeParticles;
			_activeParticles = particle;

			particle.Org = position;

			var x = Server.RandomFloat( -1f, 1f );
			var y = Server.RandomFloat( -1f, 1f );
			var z = Server.RandomFloat( -1f, 1f );

			particle.Vel = new Vector3( x, y, z ) * Server.RandomFloat( 50f, 100f );
			particle.Type = ParticleType.Grav;
			particle.Color = color;
			particle.PackedColor = 0;
			particle.Die = (float)(ClientTime + 0.5);
		}
	}

	private void FreeDeadParticles( ref Particle list )
	{
		while ( list is { } head && head.Die < ClientTime )
		{
			head.DeathFunc?.Invoke( head );
			head.DeathFunc = null;

			list = head.Next;
			head.Next = _freeParticles;
			_freeParticles = head;
		}

		for ( var particle = list; particle is not null; particle = particle.Next )
		{
			while ( particle.Next is { } kill && kill.Die < ClientTime )
			{
				kill.DeathFunc?.Invoke( kill );
				kill.DeathFunc = null;

				particle.Next = kill.Next;
				kill.Next = _freeParticles;
				_freeParticles = kill;
			}
		}
	}

	private void DrawParticles( Vector3 viewOrigin, Vector3 forward, Vector3 right, Vector3 up, float tanHalfX, float tanHalfY )
	{
		_viewOrigin = viewOrigin;
		_palette ??= Server.LoadMountFile( "gfx/palette.lmp" );
		_particleTexture ??= CreateParticleTexture();

		var particleMesh = _particleMeshes[_drawView] ??= new DynamicMesh( "particles", MaxParticles * 3, _particleTexture, false, _drawView );

		var particleUp = up * 1.5f;
		var particleRight = right * 1.5f;

		var frameTime = (float)(ClientTime - _oldTime);
		var gravity = frameTime * _moveVars.Gravity * 0.05f;
		var deltaVelocity = frameTime * 4f;

		FreeDeadParticles( ref _activeParticles );

		var vertices = particleMesh.Vertices;
		var count = 0;

		for ( var particle = _activeParticles; particle is not null; particle = particle.Next )
		{
			if ( particle.Type != ParticleType.Blob )
			{
				var scale = Vector3.Dot( particle.Org - viewOrigin, forward );

				scale = scale < 20f ? 1f : 1f + (scale * 0.004f);

				var index = particle.Color * 3;
				var color = _palette is null ? Color32.White : new Color32( _palette[index], _palette[index + 1], _palette[index + 2], 255 );

				vertices[count++] = new Vertex( particle.Org, new Vector4( 0f, 0f, 0f, 0f ), color );
				vertices[count++] = new Vertex( particle.Org + (particleUp * scale), new Vector4( 1f, 0f, 0f, 0f ), color );
				vertices[count++] = new Vertex( particle.Org + (particleRight * scale), new Vector4( 0f, 1f, 0f, 0f ), color );
			}

			if ( particle.Type != ParticleType.ClientCustom )
				particle.Org += particle.Vel * frameTime;

			switch ( particle.Type )
			{
				case ParticleType.Grav:
					particle.Vel.z -= gravity * 20f;
					break;

				case ParticleType.SlowGrav:
					particle.Vel.z = gravity;
					break;

				case ParticleType.Fire:
					particle.Ramp += frameTime * 5f;

					if ( particle.Ramp >= 6f )
						particle.Die = -1f;
					else
						particle.Color = Ramp3[(int)particle.Ramp];

					particle.Vel.z += gravity;
					break;

				case ParticleType.Explode:
					particle.Ramp += frameTime * 10f;

					if ( particle.Ramp >= 8f )
						particle.Die = -1f;
					else
						particle.Color = Ramp1[(int)particle.Ramp];

					particle.Vel += particle.Vel * deltaVelocity;
					particle.Vel.z -= gravity;
					break;

				case ParticleType.Explode2:
					particle.Ramp += frameTime * 15f;

					if ( particle.Ramp >= 8f )
						particle.Die = -1f;
					else
						particle.Color = Ramp2[(int)particle.Ramp];

					particle.Vel -= particle.Vel * frameTime;
					particle.Vel.z -= gravity;
					break;

				case ParticleType.Blob:
				case ParticleType.Blob2:
					particle.Ramp += frameTime * 10f;

					if ( particle.Ramp >= 9f )
						particle.Ramp = 0f;

					particle.Color = SparkRamp[(int)particle.Ramp];
					particle.Vel.x -= particle.Vel.x * 0.5f * frameTime;
					particle.Vel.y -= particle.Vel.y * 0.5f * frameTime;
					particle.Vel.z -= gravity * 5f;
					particle.Type = Server.RandomLong( 0, 3 ) != 0 ? ParticleType.Blob : ParticleType.Blob2;
					break;

				case ParticleType.VoxSlowGrav:
					particle.Vel.z -= gravity * 4f;
					break;

				case ParticleType.VoxGrav:
					particle.Vel.z -= gravity * 8f;
					break;

				case ParticleType.ClientCustom:
					particle.Callback?.Invoke( particle, frameTime );
					break;
			}
		}

		particleMesh.Update( count );

		DrawTracers( viewOrigin, forward, right, up, tanHalfX, tanHalfY, frameTime );
	}

	private void DrawTracers( Vector3 viewOrigin, Vector3 forward, Vector3 right, Vector3 up, float tanHalfX, float tanHalfY, float frameTime )
	{
		var tracerMesh = _tracerMeshes[_drawView] ??= new DynamicMesh( "tracers", MaxParticles * 6, Texture.Load( Server.GetMountPath( "sprites/dot.spr/0.vtex" ) ), true, _drawView );

		if ( _activeTracers is null )
		{
			tracerMesh.Update( 0 );
			return;
		}

		_tracerColors[4][0] = (byte)(int)(TracerAlpha * TracerRed * 255f);
		_tracerColors[4][1] = (byte)(int)(TracerGreen * TracerAlpha * 255f);
		_tracerColors[4][2] = (byte)(int)(TracerBlue * TracerAlpha * 255f);

		FreeDeadParticles( ref _activeTracers );

		var gravity = frameTime * _moveVars.Gravity;
		var viewDistance = Vector3.Dot( viewOrigin, forward );
		var damp = MathF.Max( 1f - (frameTime * 0.9f), 0f );

		var vertices = tracerMesh.Vertices;
		var count = 0;

		Vector3 Screen( Vector3 point, out float w )
		{
			var relative = point - viewOrigin;

			w = Vector3.Dot( relative, forward );

			return new Vector3( Vector3.Dot( relative, right ) / tanHalfX, Vector3.Dot( relative, up ) / tanHalfY, 0f );
		}

		for ( var tracer = _activeTracers; tracer is not null; tracer = tracer.Next )
		{
			var scale = tracer.Die - 0.1 <= ClientTime ? (float)((tracer.Die - ClientTime) * tracer.Ramp) : (float)(tracer.Ramp * 0.1);

			var start = tracer.Org;
			var delta = tracer.Vel * scale;
			var end = start + delta;
			var deltaX = delta.x;
			var draw = true;

			Screen( start, out var startW );

			if ( startW != 0f )
				startW = 1f / startW;

			var clip = startW <= 0f;

			if ( !clip )
			{
				Screen( end, out var endW );

				if ( endW != 0f )
					endW = 1f / endW;

				clip = endW <= 0f;
			}

			if ( clip )
			{
				var startDistance = Vector3.Dot( start, forward ) - viewDistance;
				var endDistance = Vector3.Dot( end, forward ) - viewDistance;
				var range = endDistance - startDistance;

				if ( (startDistance > 0f || endDistance > 0f) && range >= 0.01f )
				{
					var fraction = startDistance / range;
					var clippedX = (end.x - start.x) * fraction;

					if ( startDistance > 0f )
					{
						end.y = ((end.y - start.y) * fraction) + start.y;
						end.z = ((end.z - start.z) * fraction) + start.z;
						deltaX = clippedX;
					}
					else
					{
						start = new Vector3( clippedX + start.x, start.y + ((end.y - start.y) * fraction), ((end.z - start.z) * fraction) + start.z );
					}
				}
				else
				{
					draw = false;
				}
			}

			if ( draw )
			{
				end.x = tracer.Org.x + deltaX;

				var screenStart = Screen( start, out var w );

				if ( w != 0f )
					screenStart *= 1f / w;

				var screenEnd = Screen( end, out w );

				if ( w != 0f )
					screenEnd *= 1f / w;

				var normal = screenEnd - screenStart;

				MathLib.VectorNormalize( ref normal );

				var size = TracerSize[(int)tracer.Type];
				var offset = (up * (normal.x * size)) + (right * (-normal.y * size));

				var rgb = _tracerColors[tracer.Color];
				var alpha = (byte)tracer.PackedColor * 0.003921569f;
				var bright = new Color32( (byte)(rgb[0] * alpha), (byte)(rgb[1] * alpha), (byte)(rgb[2] * alpha), 255 );
				var dark = new Color32( 0, 0, 0, 255 );

				var a = new Vertex( start + offset, new Vector4( 0f, 0f, 0f, 0f ), dark );
				var b = new Vertex( end + offset, new Vector4( 0f, 1f, 0f, 0f ), bright );
				var c = new Vertex( end - offset, new Vector4( 1f, 1f, 0f, 0f ), bright );
				var d = new Vertex( start - offset, new Vector4( 1f, 0f, 0f, 0f ), dark );

				vertices[count++] = a;
				vertices[count++] = b;
				vertices[count++] = c;
				vertices[count++] = a;
				vertices[count++] = c;
				vertices[count++] = d;
			}

			tracer.Org += tracer.Vel * frameTime;

			if ( tracer.Type == ParticleType.Grav )
			{
				tracer.Vel.z -= gravity;
				tracer.Vel.x *= damp;
				tracer.Vel.y *= damp;

				tracer.PackedColor = (short)(int)(((tracer.Die - ClientTime) + (tracer.Die - ClientTime)) * 255.0);

				if ( tracer.PackedColor > 255 )
					tracer.PackedColor = 255;
			}
			else if ( tracer.Type == ParticleType.SlowGrav )
			{
				tracer.Vel.z = gravity * 0.05f;
			}
		}

		tracerMesh.Update( count );
	}

	private static Texture CreateParticleTexture()
	{
		var data = new byte[ParticleTextureSize * ParticleTextureSize * 4];

		for ( var x = 0; x < ParticleTextureSize; x++ )
		{
			for ( var y = 0; y < ParticleTextureSize; y++ )
			{
				var texel = ((y * ParticleTextureSize) + x) * 4;

				data[texel] = 255;
				data[texel + 1] = 255;
				data[texel + 2] = 255;
				data[texel + 3] = (byte)(DotTexture[(x * ParticleTextureSize) + y] * 255);
			}
		}

		return Texture.Create( ParticleTextureSize, ParticleTextureSize ).WithData( data ).Finish();
	}

	private sealed class DynamicMesh
	{
		public Vertex[] Vertices { get; }

		private readonly Mesh _mesh;
		private readonly ModelRenderer _renderer;

		public DynamicMesh( string name, int capacity, Texture texture, bool additive, int view )
		{
			Vertices = new Vertex[capacity];

			var material = Material.Create( name, "goldsrc_particle" );

			material.Set( "Color", texture );

			_mesh = new Mesh( material );
			_mesh.CreateVertexBuffer<Vertex>( capacity, Vertices.AsSpan() );
			_mesh.Bounds = new BBox( new Vector3( -65536f ), new Vector3( 65536f ) );

			_renderer = new GameObject( true, name ).AddComponent<ModelRenderer>();
			_renderer.GameObject.NetworkMode = NetworkMode.Never;
			_renderer.GameObject.Tags.Add( ViewTag( view ) );
			_renderer.Model = Sandbox.Model.Builder.AddMesh( _mesh ).Create();
			_renderer.Attributes.SetCombo( "D_ADDITIVE", additive ? 1 : 0 );
		}

		public void Update( int count )
		{
			_renderer.Enabled = count > 0;

			if ( count == 0 )
				return;

			_mesh.SetVertexBufferData( Vertices.AsSpan( 0, count ) );
			_mesh.SetVertexRange( 0, count );

			if ( _renderer.SceneObject is not { } sceneObject )
				return;

			sceneObject.Flags.IsTranslucent = true;
			sceneObject.Flags.IsOpaque = false;
			sceneObject.Flags.WantsPrePass = false;
		}
	}
}
