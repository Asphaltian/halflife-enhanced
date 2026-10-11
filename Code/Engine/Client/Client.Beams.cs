using System;

namespace GoldSrc;

[Flags]
public enum BeamFlags
{
	StartEntity = 0x00000001,
	EndEntity = 0x00000002,
	FadeIn = 0x00000004,
	FadeOut = 0x00000008,
	SineNoise = 0x00000010,
	Solid = 0x00000020,
	ShadeIn = 0x00000040,
	ShadeOut = 0x00000080,
	StartVisible = 0x10000000,
	EndVisible = 0x20000000,
	IsActive = 0x40000000,
	Forever = unchecked((int)0x80000000)
}

public sealed class Beam
{
	public Beam Next;
	public int Type;
	public BeamFlags Flags;
	public Vector3 Source;
	public Vector3 Target;
	public Vector3 Delta;
	public float T;
	public float Freq;
	public float Die;
	public float Width;
	public float Amplitude;
	public float R;
	public float G;
	public float B;
	public float Brightness;
	public float Speed;
	public float FrameRate;
	public float Frame;
	public int Segments;
	public int StartEntity;
	public int EndEntity;
	public int ModelIndex;
	public int FrameCount;
	public Model FollowModel;
	public Particle Particles;
}

partial class Client
{
	public const int BeamPointsType = 0;
	public const int BeamEntPointType = 1;
	public const int BeamEntsType = 2;
	public const int BeamHoseType = 3;

	private const int MaxBeams = 128;
	private const int MaxBeamEntities = 64;
	private const int NoiseDivisions = 128;
	private const int MaxBeamVertices = 32768;
	private const int BeamTypeMask = 0x0F;
	private const int BeamFlagsMask = 0xF0;

	[ConVar( "egon_amplitude" )]
	private static float EgonAmplitude { get; set; } = 0f;

	private readonly Dictionary<(Texture Texture, bool Solid, int View), BeamMesh> _beamMeshes = [];
	private readonly float[] _beamNoise = new float[NoiseDivisions + 1];

	private Beam _freeBeams;
	private Beam _activeBeams;
	private Vector3 _beamViewOrigin;
	private Vector3 _beamForward;
	private Vector3 _beamRight;
	private Vector3 _beamUp;
	private float _beamTanHalfX;
	private float _beamTanHalfY;

	private void ClearBeams()
	{
		_activeBeams = null;
		_freeBeams = null;

		for ( var i = 0; i < MaxBeams; i++ )
			_freeBeams = new Beam { Next = _freeBeams };
	}

	private Beam AllocBeam()
	{
		if ( _freeBeams is not { } beam )
			return null;

		_freeBeams = beam.Next;
		beam.Next = _activeBeams;
		_activeBeams = beam;

		beam.Die = (float)ClientTime;

		return beam;
	}

	private static int BeamSegments( Vector3 delta, float amplitude )
	{
		return (int)((MathLib.Length( delta ) * (amplitude < 0.5f ? 0.075 : 0.25)) + 3.0);
	}

	private void BeamSetup( Beam beam, Vector3 start, Vector3 end, int modelIndex, float life, float width, float amplitude, float brightness, float speed )
	{
		if ( GetModelByIndex( modelIndex ) is null )
			return;

		beam.Type = TempEntity.BeamPoints;
		beam.ModelIndex = modelIndex;
		beam.Frame = 0f;
		beam.FrameRate = 0f;
		beam.FrameCount = ModelFrames( modelIndex );
		beam.Source = start;
		beam.Target = end;
		beam.Delta = end - start;
		beam.Freq = (float)(speed * ClientTime);
		beam.Die = (float)(life + ClientTime);
		beam.Width = width;
		beam.Amplitude = amplitude;
		beam.Speed = speed;
		beam.Brightness = brightness;
		beam.Segments = BeamSegments( beam.Delta, amplitude );
		beam.Flags = 0;
		beam.FollowModel = null;
	}

	private Model BeamEntityModel( int index, out bool found )
	{
		found = index >= 0;

		if ( !found )
			return null;

		return GetEntity( index & 0xFFF ) is { } entity ? GetModelByIndex( entity.ModelIndex ) : null;
	}

	private Vector3 BeamAttachmentPoint( int index )
	{
		var entity = GetEntity( index & 0xFFF );
		var attachment = (index >> 12) & 0xF;

		return attachment == 0 && entity.Index == (_playerNum + 1) ? _simOrigin : entity.GetAttachmentPoint( attachment );
	}

	public Beam BeamLightning( Vector3 start, Vector3 end, int modelIndex, float life, float width, float amplitude, float brightness, float speed )
	{
		if ( AllocBeam() is not { } beam || modelIndex < 0 )
			return null;

		BeamSetup( beam, start, end, modelIndex, life, width, amplitude, brightness, speed );

		return beam;
	}

	public Beam BeamPoints( Vector3 start, Vector3 end, int modelIndex, float life, float width, float amplitude, float brightness, float speed, int startFrame, float frameRate, float r, float g, float b )
	{
		if ( life != 0f && !BeamCull( start, end, true ) )
			return null;

		if ( BeamLightning( start, end, modelIndex, life, width, amplitude, brightness, speed ) is not { } beam )
			return null;

		if ( life == 0f )
			beam.Flags |= BeamFlags.Forever;

		beam.Frame = startFrame;
		beam.FrameRate = frameRate;
		beam.R = r;
		beam.G = g;
		beam.B = b;

		return beam;
	}

	public Beam BeamEntPoint( int startEntity, Vector3 end, int modelIndex, float life, float width, float amplitude, float brightness, float speed, int startFrame, float frameRate, float r, float g, float b )
	{
		var model = BeamEntityModel( startEntity, out var found );

		if ( !found || (life != 0f && model is null) )
			return null;

		if ( BeamLightning( Vector3.Zero, end, modelIndex, life, width, amplitude, brightness, speed ) is not { } beam )
			return null;

		beam.Type = TempEntity.BeamPoints;
		beam.Flags = BeamFlags.StartEntity;

		if ( life == 0f )
			beam.Flags |= BeamFlags.Forever;

		beam.StartEntity = startEntity;
		beam.EndEntity = 0;
		beam.Frame = startFrame;
		beam.FrameRate = frameRate;
		beam.R = r;
		beam.G = g;
		beam.B = b;

		return beam;
	}

	public Beam BeamEnts( int startEntity, int endEntity, int modelIndex, float life, float width, float amplitude, float brightness, float speed, int startFrame, float frameRate, float r, float g, float b )
	{
		var startModel = BeamEntityModel( startEntity, out var startFound );
		var endModel = BeamEntityModel( endEntity, out var endFound );

		if ( !startFound || !endFound || (life != 0f && (startModel is null || endModel is null)) )
			return null;

		if ( BeamLightning( Vector3.Zero, Vector3.Zero, modelIndex, life, width, amplitude, brightness, speed ) is not { } beam )
			return null;

		beam.Type = TempEntity.BeamPoints;
		beam.Flags = BeamFlags.StartEntity | BeamFlags.EndEntity;

		if ( life == 0f )
			beam.Flags |= BeamFlags.Forever;

		beam.StartEntity = startEntity;
		beam.EndEntity = endEntity;
		beam.Frame = startFrame;
		beam.FrameRate = frameRate;
		beam.R = r;
		beam.G = g;
		beam.B = b;

		return beam;
	}

	public Beam BeamRing( int startEntity, int endEntity, int modelIndex, float life, float width, float amplitude, float brightness, float speed, int startFrame, float frameRate, float r, float g, float b )
	{
		if ( BeamEnts( startEntity, endEntity, modelIndex, life, width, amplitude, brightness, speed, startFrame, frameRate, r, g, b ) is not { } beam )
			return null;

		beam.Type = TempEntity.BeamRing;

		return beam;
	}

	public Beam BeamCirclePoints( int type, Vector3 start, Vector3 end, int modelIndex, float life, float width, float amplitude, float brightness, float speed, int startFrame, float frameRate, float r, float g, float b )
	{
		if ( BeamLightning( start, end, modelIndex, life, width, amplitude, brightness, speed ) is not { } beam )
			return null;

		beam.Type = type;

		if ( life == 0f )
			beam.Flags |= BeamFlags.Forever;

		beam.Frame = startFrame;
		beam.FrameRate = frameRate;
		beam.R = r;
		beam.G = g;
		beam.B = b;

		return beam;
	}

	public Beam BeamFollow( int startEntity, int modelIndex, float life, float width, float r, float g, float b, float brightness )
	{
		if ( BeamLightning( Vector3.Zero, Vector3.Zero, modelIndex, life, width, life, brightness, 1f ) is not { } beam )
			return null;

		beam.Type = TempEntity.BeamFollow;
		beam.Flags = BeamFlags.StartEntity;
		beam.StartEntity = startEntity;
		beam.Frame = 0f;
		beam.FrameRate = 1f;
		beam.R = r;
		beam.G = g;
		beam.B = b;

		return beam;
	}

	private void KillDeadBeams( int deadEntity )
	{
		Beam kept = null;

		for ( var beam = _activeBeams; beam is not null; )
		{
			var next = beam.Next;

			if ( beam.StartEntity == deadEntity )
				beam.Flags &= ~(BeamFlags.StartEntity | BeamFlags.EndEntity);

			if ( beam.StartEntity == deadEntity && beam.Type != TempEntity.BeamFollow )
			{
				for ( var particle = beam.Particles; particle is not null; particle = particle.Next )
					particle.Die = (float)(ClientTime - 0.1);

				FreeDeadParticles( ref beam.Particles );

				_freeBeams = new Beam { Next = _freeBeams };
			}
			else
			{
				beam.Next = kept;
				kept = beam;
			}

			beam = next;
		}

		_activeBeams = kept;
	}

	public void BeamKill( int deadEntity )
	{
		for ( var beam = _activeBeams; beam is not null; beam = beam.Next )
		{
			if ( (beam.Flags & BeamFlags.StartEntity) == BeamFlags.StartEntity && beam.StartEntity == deadEntity )
			{
				beam.Flags &= ~(BeamFlags.StartEntity | BeamFlags.Forever);

				if ( beam.Type != TempEntity.BeamFollow )
					beam.Die = (float)ClientTime;
			}

			if ( (beam.Flags & BeamFlags.EndEntity) == BeamFlags.EndEntity && beam.EndEntity == deadEntity )
			{
				beam.Flags &= ~(BeamFlags.EndEntity | BeamFlags.Forever);
				beam.Die = (float)ClientTime;
			}
		}
	}

	private bool BeamCull( Vector3 start, Vector3 end, bool pvsOnly )
	{
		var mins = Vector3.Min( start, end );
		var maxs = Vector3.Max( start, end );

		for ( var i = 0; i < 3; i++ )
		{
			if ( mins[i] == maxs[i] )
				maxs[i] += 1f;
		}

		if ( !BoxInViewPvs( mins, maxs ) )
			return false;

		return pvsOnly || !CullBox( mins, maxs );
	}

	private void BeamNoise( int offset, int divisions )
	{
		while ( divisions > 1 )
		{
			var half = divisions >> 1;

			_beamNoise[offset + half] = ((_beamNoise[offset + divisions] + _beamNoise[offset]) * 0.5f) + (Server.RandomFloat( -0.125f, 0.125f ) * divisions);

			if ( half < 2 )
				return;

			BeamNoise( offset + half, half );

			divisions = half;
		}
	}

	private Vector3 BeamScreenTransform( Vector3 point )
	{
		var relative = point - _beamViewOrigin;
		var w = Vector3.Dot( relative, _beamForward );
		var screen = new Vector3( Vector3.Dot( relative, _beamRight ) / _beamTanHalfX, Vector3.Dot( relative, _beamUp ) / _beamTanHalfY, 0f );

		return w != 0f ? screen * (1f / w) : screen;
	}

	private Vector3 BeamNormal( Vector3 screen, Vector3 screenLast )
	{
		var direction = screen - screenLast;

		direction.z = 0f;
		MathLib.VectorNormalize( ref direction );

		return (_beamUp * direction.x) - (_beamRight * direction.y);
	}

	private static Color32 BeamColor( Beam beam, float alpha, float brightness )
	{
		static byte Channel( float value ) => (byte)MathF.Round( Math.Clamp( value, 0f, 1f ) * 255f );

		return new Color32( Channel( brightness * beam.R * alpha ), Channel( brightness * beam.G * alpha ), Channel( brightness * beam.B * alpha ), 255 );
	}

	private BeamMesh GetBeamMesh( SpriteModel sprite, int frame, bool solid )
	{
		if ( frame < 0 || frame >= sprite.Frames.Length )
			frame = 0;

		var texture = Texture.Load( $"{Server.GetMountPath( sprite.Name )}/{frame}.vtex" );

		if ( !_beamMeshes.TryGetValue( (texture, solid, _drawView), out var mesh ) )
		{
			mesh = new BeamMesh( new DynamicMesh( "beams", MaxBeamVertices, texture, !solid, _drawView ), solid );
			_beamMeshes.Add( (texture, solid, _drawView), mesh );
		}

		return mesh;
	}

	private bool BeamUpdateEntities( Beam beam )
	{
		if ( (beam.Flags & BeamFlags.StartEntity) == BeamFlags.StartEntity )
		{
			var model = BeamEntityModel( beam.StartEntity, out var found );

			if ( !found )
				return false;

			if ( model is null || (beam.FollowModel is not null && beam.FollowModel != model) )
			{
				if ( (beam.Flags & BeamFlags.Forever) != BeamFlags.Forever )
					beam.Flags &= ~BeamFlags.StartEntity;
			}
			else
			{
				beam.Source = BeamAttachmentPoint( beam.StartEntity );
				beam.Flags |= BeamFlags.StartVisible;
				beam.FollowModel ??= model;
			}
		}

		if ( (beam.Flags & BeamFlags.EndEntity) == BeamFlags.EndEntity )
		{
			var model = BeamEntityModel( beam.EndEntity, out var found );

			if ( !found )
				return false;

			if ( model is null )
			{
				if ( (beam.Flags & BeamFlags.Forever) != BeamFlags.Forever )
				{
					beam.Flags &= ~BeamFlags.EndEntity;
					beam.Die = (float)ClientTime;
				}

				return false;
			}

			beam.Target = BeamAttachmentPoint( beam.EndEntity );
			beam.Flags |= BeamFlags.EndVisible;
		}

		if ( (beam.Flags & (BeamFlags.StartVisible | BeamFlags.StartEntity)) == BeamFlags.StartEntity )
			return false;

		var delta = beam.Target - beam.Source;

		if ( MathLib.Length( delta ) > 0.0000001 )
			beam.Delta = delta;

		beam.Segments = BeamSegments( beam.Delta, beam.Amplitude );

		return true;
	}

	private void BeamDraw( Beam beam, float frameTime )
	{
		if ( beam.ModelIndex < 0 )
		{
			beam.Die = (float)ClientTime;
			return;
		}

		if ( GetModelByIndex( beam.ModelIndex ) is not { } model )
			return;

		beam.Freq += frameTime;

		_beamNoise[0] = 0f;
		_beamNoise[NoiseDivisions] = 0f;

		if ( beam.Amplitude != 0f )
		{
			if ( (beam.Flags & BeamFlags.SineNoise) == BeamFlags.SineNoise )
			{
				var angle = 0f;

				for ( var i = 0; i < NoiseDivisions; i++ )
				{
					_beamNoise[i] = (float)Math.Sin( angle );
					angle += 0.024543693f;
				}
			}
			else
			{
				BeamNoise( 0, NoiseDivisions );
			}
		}

		if ( (beam.Flags & (BeamFlags.StartEntity | BeamFlags.EndEntity)) != 0 && !BeamUpdateEntities( beam ) )
			return;

		if ( beam.Type == TempEntity.BeamPoints && !BeamCull( beam.Source, beam.Target, false ) )
			return;

		if ( model is not SpriteModel sprite )
			return;

		var frame = (int)((beam.FrameRate * ClientTime) + beam.Frame) % beam.FrameCount;
		var mesh = GetBeamMesh( sprite, frame, (beam.Flags & BeamFlags.Solid) == BeamFlags.Solid );

		if ( (beam.Flags & BeamFlags.SineNoise) == BeamFlags.SineNoise && EgonAmplitude > 0f )
		{
			var radius = beam.Amplitude * EgonAmplitude;
			var spot = beam.Target + (_beamUp * (float)(Math.Sin( beam.Freq * 10f ) * radius));

			spot += _beamRight * (float)(Math.Cos( beam.Freq * 10f ) * radius);

			var direction = beam.Source - spot;
			var distance = MathLib.Length( direction );

			if ( distance != 0f )
				direction *= 1000f / distance;

			if ( TracerParticles( spot, direction, distance * 0.001f ) is { } tracer )
				tracer.Color = 7;
		}

		beam.T = (float)(beam.Die - ClientTime + beam.Freq);

		if ( beam.T != 0f )
			beam.T = 1f - (beam.Freq / beam.T);

		var alpha = beam.Brightness;

		if ( (beam.Flags & BeamFlags.FadeIn) == BeamFlags.FadeIn )
			alpha *= beam.T;
		else if ( (beam.Flags & BeamFlags.FadeOut) == BeamFlags.FadeOut )
			alpha *= 1f - beam.T;

		switch ( beam.Type )
		{
			case TempEntity.BeamPoints:
				DrawSegments( beam, mesh, alpha );
				break;
			case TempEntity.BeamTorus:
				DrawTorus( beam, mesh, alpha );
				break;
			case TempEntity.BeamDisk:
				DrawDisk( beam, mesh, alpha );
				break;
			case TempEntity.BeamCylinder:
				DrawCylinder( beam, mesh, alpha );
				break;
			case TempEntity.BeamFollow:
				DrawFollow( beam, mesh, alpha, frameTime );
				break;
			case TempEntity.BeamRing:
				DrawRing( beam, mesh, alpha );
				break;
		}
	}

	private void DrawBeamEntity( ClientEntity entity, float frameTime )
	{
		ref readonly var state = ref entity.CurState;

		var beam = new Beam();
		var renderMode = state.RenderMode;

		BeamSetup( beam, state.Origin, state.Angles, state.ModelIndex, 0f, state.Scale, (float)(state.Body * 0.01), (float)(entity.FxBlend() * (1.0 / 255)), state.AnimTime );

		beam.R = (float)(state.RenderColorR * (1.0 / 255));
		beam.G = (float)(state.RenderColorG * (1.0 / 255));
		beam.B = (float)(state.RenderColorB * (1.0 / 255));
		beam.FrameRate = 0f;
		beam.Frame = (int)state.Frame & 0xFF;

		switch ( renderMode & BeamTypeMask )
		{
			case BeamEntPointType:
				beam.Type = TempEntity.BeamPoints;
				beam.Flags = BeamFlags.EndEntity;
				beam.StartEntity = 0;
				beam.EndEntity = state.Skin;
				break;

			case BeamEntsType:
				beam.Type = TempEntity.BeamPoints;
				beam.Flags = BeamFlags.StartEntity | BeamFlags.EndEntity;
				beam.StartEntity = state.Sequence;
				beam.EndEntity = state.Skin;
				break;
		}

		beam.Flags |= (BeamFlags)(renderMode & BeamFlagsMask);
		beam.Particles = null;

		BeamDraw( beam, frameTime );
	}

	private void DrawBeams( Vector3 viewOrigin, Vector3 forward, Vector3 right, Vector3 up, float tanHalfX, float tanHalfY )
	{
		_beamViewOrigin = viewOrigin;
		_beamForward = forward;
		_beamRight = right;
		_beamUp = up;
		_beamTanHalfX = tanHalfX;
		_beamTanHalfY = tanHalfY;

		foreach ( var (key, mesh) in _beamMeshes )
		{
			if ( key.View == _drawView )
				mesh.Count = 0;
		}

		var frameTime = (float)(ClientTime - _oldTime);
		Beam previous = null;

		for ( var beam = _activeBeams; beam is not null; )
		{
			var next = beam.Next;
			var dead = previous is null ? beam.Die < ClientTime : beam.Die <= ClientTime;

			if ( dead && (beam.Flags & BeamFlags.Forever) != BeamFlags.Forever )
			{
				if ( previous is null )
					_activeBeams = next;
				else
					previous.Next = next;

				beam.Next = _freeBeams;
				_freeBeams = beam;
			}
			else
			{
				BeamDraw( beam, frameTime );
				previous = beam;
			}

			beam = next;
		}

		var count = 0;

		foreach ( var entity in _beamEntities )
		{
			if ( count++ >= MaxBeamEntities )
				break;

			DrawBeamEntity( entity, frameTime );
		}

		foreach ( var (key, mesh) in _beamMeshes )
		{
			if ( key.View == _drawView )
				mesh.Mesh.Update( mesh.Count );
		}
	}

	private void HideViewEffects( int view )
	{
		_particleMeshes[view]?.Update( 0 );
		_tracerMeshes[view]?.Update( 0 );

		foreach ( var (key, mesh) in _beamMeshes )
		{
			if ( key.View == view )
				mesh.Mesh.Update( 0 );
		}
	}
}
