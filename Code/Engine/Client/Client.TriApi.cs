using System;
using Sandbox.Rendering;

namespace GoldSrc;

public enum TriCullStyle
{
	Front,
	None
}

public enum TriPrimitive
{
	Triangles,
	TriangleFan,
	Quads,
	Polygon,
	Lines,
	TriangleStrip,
	QuadStrip
}

partial class Client
{
	private enum TriBlend
	{
		None,
		Alpha,
		Additive
	}

	private readonly record struct TriState( Texture Texture, TriBlend Blend, bool DepthWrite, bool Cull, bool AlphaTest, bool Lines );

	private sealed class TriBatch
	{
		public TriState State;
		public int Start;
		public int Count;
		public readonly RenderAttributes Attributes = new();
	}

	private sealed class TriPass( string name, Sandbox.Rendering.Stage stage )
	{
		public readonly CommandList Commands = new( name );
		public readonly Sandbox.Rendering.Stage Stage = stage;
		public readonly List<TriBatch> Batches = [];
		public int BatchCount;
		public CameraComponent Camera;
	}

	private sealed class TriView( int index )
	{
		public readonly TriPass Normal = new( $"TriNormal{index}", Sandbox.Rendering.Stage.AfterOpaque );
		public readonly TriPass Transparent = new( $"TriTransparent{index}", Sandbox.Rendering.Stage.AfterTransparent );
		public GpuBuffer<Vertex> Buffer;
	}

	[ConVar( "gl_cull" )]
	private static float GlCull { get; set; } = 1f;

	private const int TriInitialVertices = 4096;
	private const float TriColorScale = 255.9f;
	private const float TriAlphaScale = 255f;

	private readonly TriView[] _triViews = [new( 0 ), new( 1 )];
	private readonly Dictionary<Texture, Material> _triMaterials = [];
	private readonly List<Vertex> _triPrimitive = [];
	private Vertex[] _triVertices = new Vertex[TriInitialVertices];
	private int _triVertexCount;
	private TriPass _triPass;

	private RenderMode _triRenderMode;
	private float _triRed;
	private float _triGreen;
	private float _triBlue;
	private float _triAlpha;
	private Vector4 _triColor = Vector4.One;
	private Vector2 _triTexCoord;
	private TriPrimitive _triPrimitiveCode;
	private Texture _triTexture;
	private bool _triBlendEnabled;
	private bool _triBlendAdditive;
	private bool _triDepthWrite = true;
	private bool _triSmooth;
	private bool _triCull = true;
	private bool _triAlphaTest;

	private Vector3 _triViewOrigin;
	private Vector3 _triViewForward;
	private Vector3 _triViewRight;
	private Vector3 _triViewUp;
	private float _triTanHalfX;
	private float _triTanHalfY;

	public void TriRenderMode( RenderMode mode )
	{
		switch ( mode )
		{
			case RenderMode.Normal:
				_triBlendEnabled = false;
				_triDepthWrite = true;
				_triSmooth = false;
				break;
			case RenderMode.TransColor:
			case RenderMode.TransTexture:
				_triBlendAdditive = false;
				_triBlendEnabled = true;
				_triSmooth = true;
				break;
			case RenderMode.TransAlpha:
				_triBlendEnabled = true;
				_triBlendAdditive = false;
				_triSmooth = true;
				_triDepthWrite = false;
				break;
			case RenderMode.TransAdd:
				_triBlendAdditive = true;
				_triBlendEnabled = true;
				_triDepthWrite = false;
				_triSmooth = true;
				break;
		}

		_triRenderMode = mode;
	}

	public void TriBegin( TriPrimitive primitive )
	{
		_triPrimitiveCode = primitive;
		_triPrimitive.Clear();
	}

	public void TriColor4f( float r, float g, float b, float a )
	{
		if ( _triRenderMode == RenderMode.TransAlpha )
			_triColor = new Vector4( (byte)(int)(r * TriColorScale), (byte)(int)(g * TriColorScale), (byte)(int)(b * TriColorScale), (byte)(int)(a * TriAlphaScale) ) / 255f;
		else
			_triColor = new Vector4( r * a, g * a, b * a, 1f );

		_triRed = r;
		_triGreen = g;
		_triBlue = b;
		_triAlpha = a;
	}

	public void TriColor4ub( byte r, byte g, byte b, byte a )
	{
		_triRed = (float)(r * (1.0 / 255.0));
		_triGreen = (float)(g * (1.0 / 255.0));
		_triBlue = (float)(b * (1.0 / 255.0));
		_triAlpha = (float)(a * (1.0 / 255.0));

		_triColor = new Vector4( _triRed, _triGreen, _triBlue, 1f );
	}

	public void TriColor4fRenderMode( float r, float g, float b, float a, RenderMode mode )
	{
		if ( mode == RenderMode.TransAlpha )
		{
			_triAlpha = (float)(a * (1.0 / 255.0));
			_triColor = new Vector4( r, g, b, a );
			return;
		}

		_triColor = new Vector4( r * a, g * a, b * a, 1f );
	}

	public void TriBrightness( float brightness )
	{
		_triColor = new Vector4( brightness * _triRed * _triAlpha, brightness * _triGreen * _triAlpha, brightness * _triBlue * _triAlpha, 1f );
	}

	public void TriTexCoord2f( float u, float v )
	{
		_triTexCoord = new Vector2( u, v );
	}

	public void TriVertex3f( float x, float y, float z ) => TriVertex3fv( new Vector3( x, y, z ) );

	public void TriVertex3fv( Vector3 position )
	{
		var color = new Color32( TriChannel( _triColor.x ), TriChannel( _triColor.y ), TriChannel( _triColor.z ), TriChannel( _triColor.w ) );

		_triPrimitive.Add( new Vertex( position, new Vector4( _triTexCoord.x, _triTexCoord.y, 0f, 0f ), color ) );
	}

	private static byte TriChannel( float value )
	{
		return (byte)((Math.Clamp( value, 0f, 1f ) * 255f) + 0.5f);
	}

	public void TriCullFace( TriCullStyle style )
	{
		if ( style == TriCullStyle.Front )
			_triCull = true;
		else if ( style == TriCullStyle.None )
			_triCull = false;
	}

	public SpriteModel LoadMapSprite( string fileName ) => SpriteModel.LoadMapSprite( fileName );

	public bool TriSpriteTexture( SpriteModel sprite, int frame )
	{
		if ( sprite is null )
		{
			Log.Info( "Sprite:  no pSprite!!!\n" );
			return false;
		}

		if ( sprite.Frames.Length == 0 )
		{
			Log.Info( "Sprite:  pSprite has no frames!!!\n" );
			return false;
		}

		if ( frame >= sprite.Frames.Length || frame < 0 )
		{
			Log.Trace( $"Sprite: no such frame {frame}\n" );
			frame = 0;
		}

		if ( sprite.Frames[frame] is null )
			return false;

		_triTexture = sprite.Frames[frame].Texture ?? Texture.Load( $"{Server.GetMountPath( sprite.Name )}/{frame}.vtex" );

		return true;
	}

	public bool TriWorldToScreen( Vector3 world, out Vector3 screen )
	{
		var delta = world - _triViewOrigin;
		var x = Vector3.Dot( delta, _triViewRight ) / _triTanHalfX;
		var y = Vector3.Dot( delta, _triViewUp ) / _triTanHalfY;
		var w = Vector3.Dot( delta, _triViewForward );

		if ( w != 0f )
		{
			w = (float)(1.0 / w);
			x *= w;
			y *= w;
		}

		screen = new Vector3( x, y, 0f );

		return w <= 0f;
	}

	public void TriEnd()
	{
		var vertices = _triPrimitive;
		var count = vertices.Count;

		if ( _triPass is null || count == 0 || _triTexture is null )
			return;

		var lines = _triPrimitiveCode == TriPrimitive.Lines;
		var state = new TriState( _triTexture, _triBlendEnabled ? (_triBlendAdditive ? TriBlend.Additive : TriBlend.Alpha) : TriBlend.None, _triDepthWrite, _triCull, _triAlphaTest, lines );
		var start = _triVertexCount;

		switch ( _triPrimitiveCode )
		{
			case TriPrimitive.Triangles:
				for ( var i = 0; i + 2 < count; i += 3 )
					TriTriangle( vertices[i], vertices[i + 1], vertices[i + 2], vertices[i + 2] );

				break;
			case TriPrimitive.TriangleFan:
				for ( var i = 1; i + 1 < count; i++ )
					TriTriangle( vertices[0], vertices[i], vertices[i + 1], vertices[i + 1] );

				break;
			case TriPrimitive.Polygon:
				for ( var i = 1; i + 1 < count; i++ )
					TriTriangle( vertices[0], vertices[i], vertices[i + 1], vertices[0] );

				break;
			case TriPrimitive.Quads:
				for ( var i = 0; i + 3 < count; i += 4 )
				{
					TriTriangle( vertices[i], vertices[i + 1], vertices[i + 2], vertices[i + 3] );
					TriTriangle( vertices[i], vertices[i + 2], vertices[i + 3], vertices[i + 3] );
				}

				break;
			case TriPrimitive.TriangleStrip:
				for ( var i = 0; i + 2 < count; i++ )
				{
					if ( (i & 1) == 0 )
						TriTriangle( vertices[i], vertices[i + 1], vertices[i + 2], vertices[i + 2] );
					else
						TriTriangle( vertices[i + 1], vertices[i], vertices[i + 2], vertices[i + 2] );
				}

				break;
			case TriPrimitive.QuadStrip:
				for ( var i = 0; i + 3 < count; i += 2 )
				{
					TriTriangle( vertices[i], vertices[i + 1], vertices[i + 3], vertices[i + 3] );
					TriTriangle( vertices[i], vertices[i + 3], vertices[i + 2], vertices[i + 3] );
				}

				break;
			case TriPrimitive.Lines:
				for ( var i = 0; i + 1 < count; i += 2 )
				{
					TriEmit( TriShade( vertices[i], vertices[i + 1] ) );
					TriEmit( vertices[i + 1] );
				}

				break;
		}

		if ( _triVertexCount == start )
			return;

		var pass = _triPass;

		if ( pass.BatchCount > 0 && pass.Batches[pass.BatchCount - 1] is { } last && last.State == state && last.Start + last.Count == start )
		{
			last.Count = _triVertexCount - last.Start;
			return;
		}

		if ( pass.BatchCount == pass.Batches.Count )
			pass.Batches.Add( new TriBatch() );

		var batch = pass.Batches[pass.BatchCount++];

		batch.State = state;
		batch.Start = start;
		batch.Count = _triVertexCount - start;
	}

	private Vertex TriShade( Vertex vertex, Vertex provoking )
	{
		if ( !_triSmooth )
			vertex.Color = provoking.Color;

		return vertex;
	}

	private void TriTriangle( Vertex a, Vertex b, Vertex c, Vertex provoking )
	{
		TriEmit( TriShade( a, provoking ) );
		TriEmit( TriShade( c, provoking ) );
		TriEmit( TriShade( b, provoking ) );
	}

	private void TriEmit( Vertex vertex )
	{
		if ( _triVertexCount == _triVertices.Length )
			Array.Resize( ref _triVertices, _triVertices.Length * 2 );

		_triVertices[_triVertexCount++] = vertex;
	}

	private Material TriMaterial( Texture texture )
	{
		if ( _triMaterials.TryGetValue( texture, out var material ) )
			return material;

		material = Material.Create( "tri", "shaders/triangle_api.shader" );
		material.Set( "Color", texture );

		_triMaterials.Add( texture, material );

		return material;
	}

	private void TriSubmit( TriView view, TriPass pass, CameraComponent camera )
	{
		if ( pass.Camera != camera )
		{
			pass.Camera?.RemoveCommandList( pass.Commands );
			camera.AddCommandList( pass.Commands, pass.Stage );
			pass.Camera = camera;
		}

		pass.Commands.Reset();

		for ( var i = 0; i < pass.BatchCount; i++ )
		{
			var batch = pass.Batches[i];
			var state = batch.State;

			batch.Attributes.SetCombo( "D_TRI_BLEND", (int)state.Blend );
			batch.Attributes.SetCombo( "D_TRI_DEPTH_WRITE", state.DepthWrite ? 1 : 0 );
			batch.Attributes.SetCombo( "D_TRI_CULL", state.Cull ? 1 : 0 );
			batch.Attributes.SetCombo( "D_TRI_ALPHA_TEST", state.AlphaTest ? 1 : 0 );

			pass.Commands.Draw( view.Buffer, TriMaterial( state.Texture ), batch.Start, batch.Count, batch.Attributes, state.Lines ? Graphics.PrimitiveType.Lines : Graphics.PrimitiveType.Triangles );
		}
	}

	private void ClearTriangles( int viewIndex )
	{
		var view = _triViews[viewIndex];

		view.Normal.BatchCount = 0;
		view.Transparent.BatchCount = 0;
		view.Normal.Commands.Reset();
		view.Transparent.Commands.Reset();
	}

	private void DrawTriangles( int viewIndex, CameraComponent camera, Vector3 viewOrigin, Vector3 forward, Vector3 right, Vector3 up, float tanHalfX, float tanHalfY )
	{
		var view = _triViews[viewIndex];

		_triViewOrigin = viewOrigin;
		_triViewForward = forward;
		_triViewRight = right;
		_triViewUp = up;
		_triTanHalfX = tanHalfX;
		_triTanHalfY = tanHalfY;

		_triVertexCount = 0;
		view.Normal.BatchCount = 0;
		view.Transparent.BatchCount = 0;

		_triBlendEnabled = false;
		_triDepthWrite = true;
		_triCull = GlCull != 0f;
		_triAlphaTest = false;
		_triPass = view.Normal;

		_clientDll.DrawNormalTriangles();
		TriRenderMode( RenderMode.Normal );

		if ( ViewEntity.DrawEntities != 0f )
		{
			_triAlphaTest = true;
			_triPass = view.Transparent;

			_clientDll.DrawTransparentTriangles();
			TriRenderMode( RenderMode.Normal );
		}

		_triPass = null;

		if ( _triVertexCount > 0 )
		{
			if ( view.Buffer is null || view.Buffer.ElementCount < _triVertexCount )
				view.Buffer = new GpuBuffer<Vertex>( _triVertices.Length, GpuBuffer.UsageFlags.Vertex, "TriApi" );

			view.Buffer.SetData( _triVertices.AsSpan( 0, _triVertexCount ) );
		}

		TriSubmit( view, view.Normal, camera );
		TriSubmit( view, view.Transparent, camera );
	}
}
