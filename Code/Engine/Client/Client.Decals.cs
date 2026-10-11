using System;
using System.Runtime.InteropServices;
using GoldSrc.Bsp;

namespace GoldSrc;

[Flags]
public enum DecalFlags
{
	Permanent = 0x01,
	Reference = 0x02,
	Custom = 0x04,
	HFlip = 0x08,
	VFlip = 0x10,
	ClipTest = 0x20,
	NoClip = 0x40,
	UseAxis = 0x80
}

public readonly record struct DecalEntry( string Name, short EntityIndex, byte Depth, byte Flags, Vector3 Position );

public sealed class Decal
{
	public int Surface = -1;
	public float Dx;
	public float Dy;
	public float Scale;
	public int Texture;
	public DecalFlags Flags;
	public int EntityIndex;
}

partial class Client
{
	public const int MaxDecals = 4096;

	private const float DecalDistance = 4f;

	[ConVar( "r_decals" )]
	public static float DecalLimit { get; set; } = MaxDecals;

	[ConVar( "mp_decals", ConVarFlags.Saved )]
	public static float MultiplayerDecalLimit { get; set; } = 300f;

	[StructLayout( LayoutKind.Sequential )]
	private struct DecalVertex
	{
		[VertexLayout.Position]
		public Vector3 Position;

		[VertexLayout.Normal]
		public Vector3 Normal;

		[VertexLayout.TexCoord( 0 )]
		public Vector2 TexCoord;

		[VertexLayout.TexCoord( 1 )]
		public Vector2 LightmapCoord;

		[VertexLayout.Color]
		public Color32 LightmapBlock;
	}

	private sealed class DecalBatch
	{
		public Mesh Mesh;
		public Sandbox.Model Model;
		public int VertexCapacity;
		public int IndexCapacity;
		public List<DecalVertex[]> Parts;
		public BBox Bounds;
	}

	private sealed class DecalSceneObject( SceneWorld world ) : SceneCustomObject( world )
	{
		public readonly Dictionary<(int Texture, int Ordinal), DecalBatch> Batches = [];
		public List<Sandbox.Model> Models = [];
		public bool Visible;
		public bool Opaque;

		private readonly Transform[] _transform = new Transform[1];

		public override void RenderSceneObject()
		{
			_transform[0] = Transform;

			foreach ( var model in Models )
				Graphics.DrawModelInstanced( model, _transform, 0, Attributes );
		}
	}

	private readonly Decal[] _decalPool = new Decal[MaxDecals];
	private readonly Dictionary<int, List<Decal>> _surfaceDecals = [];
	private readonly HashSet<int> _dirtyDecalEntities = [];
	private readonly Dictionary<int, DecalSceneObject> _decalObjects = [];
	private readonly Sandbox.Rendering.CommandList _decalCommands = new( "Decals" );
	private readonly Dictionary<int, (bool Visible, RenderMode RenderMode, float Blend, Transform Transform)> _decalStates = [];
	private readonly Dictionary<int, Material> _decalMaterials = [];
	private readonly Dictionary<Decal, DecalVertex[]> _decalVertices = [];

	private CameraComponent _decalCamera;
	private CameraComponent _decalNextCamera;
	private int _decalAllocIndex;
	private Vector3 _decalPosition;
	private int _decalTexture;
	private int _decalSize;
	private DecalFlags _decalFlags;
	private int _decalEntity;

	public void DecalShoot( int textureIndex, int entityIndex, int modelIndex, Vector3 position, DecalFlags flags )
	{
		var entity = GetEntity( entityIndex );
		var model = GetModelByIndex( entity?.ModelIndex ?? 0 );

		if ( model is null && modelIndex != 0 )
			model = GetModelByIndex( modelIndex );

		if ( model is null && Server.Get( Scene ) is { Active: true } server )
			model = server.GetModel( server.GetEdict( entityIndex )?.ModelIndex ?? 0 );

		if ( model is not BrushModel brush || textureIndex >= DecalWad.Textures.Length )
		{
			Con.DPrintf( "Decals must hit mod_brush!\n" );
			return;
		}

		_decalPosition = position;

		var node = WorldFile.Models[0].HeadNode[0];

		if ( entityIndex != 0 )
		{
			var origin = entity?.Origin ?? default;
			var angles = entity?.Angles ?? default;
			var subModel = WorldFile.Models[brush.SubModel];

			if ( subModel.FirstFace != 0 )
			{
				_decalPosition = position - brush.Hulls[0].ClipMins - origin;
				node = subModel.HeadNode[0];
			}

			if ( angles.x != 0f || angles.y != 0f || angles.z != 0f )
			{
				MathLib.AngleVectors( angles, out var forward, out var right, out var up );

				_decalPosition = new Vector3( Vector3.Dot( _decalPosition, forward ), -Vector3.Dot( _decalPosition, right ), Vector3.Dot( _decalPosition, up ) );
			}
		}

		_decalTexture = textureIndex;
		_decalFlags = (flags & DecalFlags.Custom) == DecalFlags.Custom ? flags : flags | DecalFlags.ClipTest;
		_decalEntity = entityIndex;

		var (_, width, height) = DecalTexture( textureIndex );

		_decalSize = Math.Max( width >> 1, height >> 1 );

		DecalNode( node );
	}

	private void DecalNode( int nodeIndex )
	{
		while ( nodeIndex >= 0 )
		{
			var node = WorldFile.Nodes[nodeIndex];
			var plane = WorldFile.Planes[node.PlaneNum];

			var distance = (_decalPosition.y * plane.Normal.y) + (_decalPosition.x * plane.Normal.x) + (_decalPosition.z * plane.Normal.z) - plane.Dist;

			if ( distance > _decalSize )
			{
				nodeIndex = node.Child0;
				continue;
			}

			if ( distance >= -_decalSize )
			{
				if ( distance < DecalDistance && distance > -DecalDistance )
					DecalNodeSurfaces( node );

				DecalNode( node.Child0 );
			}

			nodeIndex = node.Child1;
		}
	}

	private void DecalNodeSurfaces( in Node node )
	{
		var (_, decalWidth, decalHeight) = DecalTexture( _decalTexture );

		for ( var i = 0; i < node.NumFaces; i++ )
		{
			var surface = node.FirstFace + i;

			if ( (WorldFile.SurfaceFlags[surface] & (Bsp.File.SurfaceDrawTurb | Bsp.File.SurfaceDrawTiled)) != 0 )
				continue;

			var face = WorldFile.Faces[surface];
			var texInfo = WorldFile.TexInfos[face.TexInfo];
			var extents = WorldFile.SurfaceExtents[surface];
			var scale = MathLib.Length( texInfo.S );

			if ( scale == 0f )
				continue;

			var w = (int)(decalWidth * scale);
			var h = (int)(decalHeight * scale);

			var s = (_decalPosition.y * texInfo.S.y) + (_decalPosition.x * texInfo.S.x) + (_decalPosition.z * texInfo.S.z) + texInfo.OffsetS - extents.MinS - (w * 0.5f);
			var t = (_decalPosition.x * texInfo.T.x) + (_decalPosition.y * texInfo.T.y) + (_decalPosition.z * texInfo.T.z) + texInfo.OffsetT - extents.MinT - (h * 0.5f);

			if ( s <= -w || t <= -h || s > extents.ExtentS + w || t > extents.ExtentT + h )
				continue;

			var (textureWidth, textureHeight) = WorldFile.GetTextureSize( face );

			DecalCreate( surface, 1f / scale, (extents.MinS + s) / textureWidth, (extents.MinT + t) / textureHeight );
		}
	}

	private void DecalCreate( int surface, float scale, float x, float y )
	{
		var face = WorldFile.Faces[surface];
		var (textureWidth, textureHeight) = WorldFile.GetTextureSize( face );
		var (_, width, height) = DecalTexture( _decalTexture );

		var count = 0;
		var lastDistance = 0xffff;
		Decal intersect = null;

		var decals = _surfaceDecals.GetValueOrDefault( surface );

		foreach ( var existing in decals ?? [] )
		{
			var (_, existingWidth, existingHeight) = DecalTexture( existing.Texture );

			if ( (existing.Flags & (DecalFlags.Permanent | DecalFlags.UseAxis)) != 0 || existingWidth > (float)(width * 1.5) )
				continue;

			var dx = Math.Abs( (int)((textureWidth * x) + (width >> 1) - ((textureWidth * existing.Dx) + (existingWidth >> 1))) );
			var dy = Math.Abs( (int)((height >> 1) + (textureHeight * y) - ((textureHeight * existing.Dy) + (existingHeight >> 1))) );

			var distance = dx < dy ? (int)((dx * 0.5) + dy) : (int)(dx + (dy * 0.5));

			if ( distance * existing.Scale < 8f )
			{
				count++;

				if ( intersect is null || distance <= lastDistance )
				{
					lastDistance = distance;
					intersect = existing;
				}
			}
		}

		var decal = count > 3 ? intersect : null;
		var limit = DecalLimit >= MaxDecals || (_decalFlags & DecalFlags.UseAxis) == DecalFlags.UseAxis ? MaxDecals : (int)DecalLimit;

		if ( limit == 0 )
			return;

		if ( decal is null )
		{
			var tries = 0;

			do
			{
				_decalAllocIndex++;

				if ( _decalAllocIndex >= limit )
					_decalAllocIndex = 0;

				tries++;
				decal = _decalPool[_decalAllocIndex] ??= new Decal();
			}
			while ( (decal.Flags & (DecalFlags.Permanent | DecalFlags.UseAxis)) != 0 && tries < limit );
		}

		DecalUnlink( decal );

		decal.Texture = _decalTexture;
		decal.Dx = x;
		decal.Dy = y;
		decal.Flags = _decalFlags;
		decal.Surface = surface;
		decal.Scale = scale;
		decal.EntityIndex = _decalEntity;

		decals ??= _surfaceDecals[surface] = [];

		var reference = (_decalFlags & DecalFlags.Custom) == DecalFlags.Custom ? decals.FindIndex( d => (d.Flags & DecalFlags.UseAxis) == DecalFlags.UseAxis ) : -1;

		if ( reference >= 0 )
			decals.Insert( reference, decal );
		else
			decals.Add( decal );

		_dirtyDecalEntities.Add( _decalEntity );
	}

	private void DecalUnlink( Decal decal )
	{
		if ( decal.Surface >= 0 && _surfaceDecals.TryGetValue( decal.Surface, out var decals ) )
		{
			decals.Remove( decal );
			_dirtyDecalEntities.Add( decal.EntityIndex );
		}

		_decalVertices.Remove( decal );

		decal.Surface = -1;
	}

	private void DecalRemoveAll( int texture )
	{
		foreach ( var decal in _decalPool )
		{
			if ( decal is null || decal.Texture != texture )
				continue;

			DecalUnlink( decal );

			decal.Dx = 0f;
			decal.Dy = 0f;
			decal.Scale = 0f;
			decal.Texture = 0;
			decal.Flags = default;
			decal.EntityIndex = 0;
		}

		_decalMaterials.Remove( texture );

		foreach ( var decalObject in _decalObjects.Values )
		{
			foreach ( var key in decalObject.Batches.Keys )
			{
				if ( key.Texture == texture )
					decalObject.Batches.Remove( key );
			}
		}
	}

	private int DecalGetOrigin( Decal decal, out Vector3 position )
	{
		var face = WorldFile.Faces[decal.Surface];
		var texInfo = WorldFile.TexInfos[face.TexInfo];
		var plane = WorldFile.Planes[face.PlaneNum];
		var extents = WorldFile.SurfaceExtents[decal.Surface];

		var (textureWidth, textureHeight) = WorldFile.GetTextureSize( face );
		var (_, decalWidth, decalHeight) = DecalTexture( decal.Texture );

		var length = MathLib.Length( texInfo.S );
		var half = (float)(length * 0.5);
		var scale = 1f / Math.Abs( length );

		scale *= scale;

		var s = scale * ((decalWidth * half) + ((textureWidth * decal.Dx) - extents.MinS) + extents.MinS - texInfo.OffsetS);
		var t = scale * ((decalHeight * half) + ((textureHeight * decal.Dy) - extents.MinT) + extents.MinT - texInfo.OffsetT);

		position = texInfo.S * s;
		position += texInfo.T * t;
		position += plane.Normal * plane.Dist;

		if ( decal.EntityIndex == 0 )
			return 0;

		if ( GetEntity( decal.EntityIndex ) is not { ModelIndex: not 0 } entity || GetBrushModel( entity.ModelIndex ) is not { } model )
			return 0;

		if ( entity.Angles.x != 0f || entity.Angles.y != 0f || entity.Angles.z != 0f )
		{
			MathLib.AngleVectorsTranspose( entity.Angles, out var forward, out var right, out var up );

			position = new Vector3( Vector3.Dot( position, forward ), Vector3.Dot( position, right ), Vector3.Dot( position, up ) );
		}

		if ( WorldFile.Models[model.SubModel].FirstFace != 0 )
			position += model.Hulls[0].ClipMins + entity.Origin;

		return decal.EntityIndex;
	}

	public List<DecalEntry> DecalListCreate()
	{
		var list = new List<DecalEntry>();

		foreach ( var decal in _decalPool )
		{
			if ( decal is null || decal.Surface < 0 || (decal.Flags & (DecalFlags.Custom | DecalFlags.UseAxis)) != 0 )
				continue;

			var depth = (byte)_surfaceDecals[decal.Surface].IndexOf( decal );
			var entityIndex = (short)DecalGetOrigin( decal, out var position );
			var name = DecalTexture( decal.Texture ).Name;

			if ( !list.Exists( x => x.Name == name && x.EntityIndex == entityIndex && MathLib.Length( position - x.Position ) < 2f ) )
				list.Add( new DecalEntry( name, entityIndex, depth, (byte)decal.Flags, position ) );
		}

		return [.. list.OrderByDescending( x => x.Depth )];
	}

	private void RestoreDecals( string fileName )
	{
		if ( Server.Get( Scene ).SavedDecals( fileName ) is not { } decals || DecalLimit == 0f )
			return;

		foreach ( var entry in decals )
		{
			var name = entry.Name.StartsWith( '}' ) ? $"{{{entry.Name[1..]}" : entry.Name;
			var texture = Math.Max( Array.FindIndex( DecalWad.Textures, x => x.Name == name ), 0 );

			DecalShoot( texture, entry.EntityIndex, 0, entry.Position, (DecalFlags)entry.Flags );
		}
	}

	private void ClearDecals()
	{
		foreach ( var decalObject in _decalObjects.Values )
			decalObject.Delete();

		Array.Clear( _decalPool );

		_surfaceDecals.Clear();
		_dirtyDecalEntities.Clear();
		_decalObjects.Clear();
		_decalStates.Clear();
		_decalMaterials.Clear();
		_decalVertices.Clear();
		_lightmaps = null;

		ClearDynamicLights();
		_decalAllocIndex = 0;
	}

	private void DrawDecals()
	{
		if ( _dirtyDecalEntities.Count == 0 )
			return;

		foreach ( var entityIndex in _dirtyDecalEntities )
			BuildDecalModel( entityIndex );

		_dirtyDecalEntities.Clear();
	}

	private void BuildDecalModel( int entityIndex )
	{
		var entity = GetEntity( entityIndex );

		if ( _lightmaps is null )
			BuildLightmaps();

		var batches = new List<(int Texture, List<DecalVertex> Vertices, List<int> Indices, List<DecalVertex[]> Parts)>();

		foreach ( var (surface, decals) in _surfaceDecals )
		{
			var first = 0;

			foreach ( var decal in decals )
			{
				if ( decal.EntityIndex != entityIndex )
					continue;

				var texture = decal.Texture;
				var index = batches.FindIndex( first, x => x.Texture == texture );

				if ( index < 0 )
				{
					index = batches.Count;
					batches.Add( (texture, [], [], []) );
				}

				first = index;

				DecalVertsClip( decal, surface, batches[index].Vertices, batches[index].Indices, batches[index].Parts );
			}
		}

		_decalObjects.TryGetValue( entityIndex, out var decalObject );

		if ( batches.Count == 0 )
		{
			if ( decalObject is not null )
				decalObject.Models = [];

			return;
		}

		var created = decalObject is null;

		decalObject ??= new DecalSceneObject( Scene.SceneWorld );

		var models = new List<Sandbox.Model>( batches.Count );
		var ordinals = new Dictionary<int, int>();
		var bounds = new BBox();
		var empty = true;

		foreach ( var (texture, vertices, indices, parts) in batches )
		{
			if ( indices.Count == 0 )
				continue;

			var ordinal = ordinals.GetValueOrDefault( texture );

			ordinals[texture] = ordinal + 1;

			if ( !decalObject.Batches.TryGetValue( (texture, ordinal), out var batch ) )
			{
				batch = new DecalBatch { Mesh = new Mesh( GetDecalMaterial( texture ) ), VertexCapacity = vertices.Count, IndexCapacity = indices.Count, Parts = parts };

				batch.Mesh.CreateVertexBuffer( vertices.Count, vertices );
				batch.Mesh.CreateIndexBuffer( indices.Count, indices );
				batch.Model = Sandbox.Model.Builder.AddMesh( batch.Mesh ).Create();

				decalObject.Batches[(texture, ordinal)] = batch;
			}
			else if ( SameDecalParts( batch.Parts, parts ) )
			{
				bounds = empty ? batch.Bounds : bounds.AddBBox( batch.Bounds );
				empty = false;

				models.Add( batch.Model );

				continue;
			}
			else
			{
				batch.Parts = parts;

				if ( vertices.Count > batch.VertexCapacity )
				{
					batch.VertexCapacity = Math.Max( vertices.Count, batch.VertexCapacity * 2 );
					batch.Mesh.SetVertexBufferSize( batch.VertexCapacity );
				}

				if ( indices.Count > batch.IndexCapacity )
				{
					batch.IndexCapacity = Math.Max( indices.Count, batch.IndexCapacity * 2 );
					batch.Mesh.SetIndexBufferSize( batch.IndexCapacity );
				}

				batch.Mesh.SetVertexBufferData( vertices );
				batch.Mesh.SetIndexBufferData( indices );
				batch.Mesh.SetVertexRange( 0, vertices.Count );
				batch.Mesh.SetIndexRange( 0, indices.Count );
			}

			var meshBounds = new BBox( vertices[0].Position, vertices[0].Position );

			foreach ( var vertex in vertices )
				meshBounds = meshBounds.AddPoint( vertex.Position );

			batch.Mesh.Bounds = batch.Bounds = meshBounds;

			bounds = empty ? meshBounds : bounds.AddBBox( meshBounds );
			empty = false;

			models.Add( batch.Model );
		}

		decalObject.Models = models;
		decalObject.LocalBounds = bounds;

		if ( !created )
			return;

		decalObject.Flags.IsTranslucent = true;
		decalObject.Flags.IsOpaque = false;
		decalObject.Flags.CastShadows = false;

		_decalObjects[entityIndex] = decalObject;

		if ( _decalStates.TryGetValue( entityIndex, out var state ) )
			UpdateDecalState( entityIndex, state.Visible, state.RenderMode, state.Blend, state.Transform );
		else
			UpdateDecalState( entityIndex, entity is not null, RenderMode.Normal, 1f, entity?.WorldTransform ?? global::Transform.Zero );
	}

	public void SetDecalState( int entityIndex, bool visible, RenderMode renderMode, float blend, Transform transform )
	{
		if ( UpdateDecalState( entityIndex, visible, renderMode, blend, transform ) is { } decalObject )
			AddDecalCommands( decalObject );
	}

	private DecalSceneObject UpdateDecalState( int entityIndex, bool visible, RenderMode renderMode, float blend, Transform transform )
	{
		_decalStates[entityIndex] = (visible, renderMode, blend, transform);

		if ( !_decalObjects.TryGetValue( entityIndex, out var decalObject ) )
			return null;

		decalObject.Visible = visible && renderMode != RenderMode.TransColor;
		decalObject.Opaque = renderMode == RenderMode.Normal;
		decalObject.RenderingEnabled = decalObject.Visible && !decalObject.Opaque;

		if ( !decalObject.Visible )
			return decalObject;

		decalObject.Transform = transform;
		decalObject.Attributes.SetCombo( "D_RENDER_MODE", (int)renderMode );
		decalObject.Attributes.Set( "Blend", blend );

		return decalObject;
	}

	private void AddDecalCommands( DecalSceneObject decalObject )
	{
		if ( !decalObject.Visible || !decalObject.Opaque )
			return;

		foreach ( var model in decalObject.Models )
			_decalCommands.DrawModel( model, decalObject.Transform, decalObject.Attributes );
	}

	private void BeginDecalCommands( CameraComponent camera )
	{
		if ( _decalCamera != camera )
		{
			if ( _decalCamera.IsValid() )
				_decalCamera.RemoveCommandList( _decalCommands );

			camera.AddCommandList( _decalCommands, Sandbox.Rendering.Stage.AfterOpaque, -1 );

			_decalCamera = camera;
		}

		if ( _decalNextCamera != _nextViewCamera && _nextViewCamera.IsValid() )
		{
			_nextViewCamera.AddCommandList( _decalCommands, Sandbox.Rendering.Stage.AfterOpaque, -1 );

			_decalNextCamera = _nextViewCamera;
		}

		_decalCommands.Reset();

		if ( _decalObjects.TryGetValue( 0, out var world ) )
			AddDecalCommands( world );
	}

	private Material GetDecalMaterial( int texture )
	{
		if ( _decalMaterials.TryGetValue( texture, out var material ) )
			return material;

		material = Material.Create( $"decal_{texture}", "goldsrc_lightmapped" );
		material.SetFeature( "F_DECAL", 1 );
		material.Set( "Color0", texture < 0 ? CustomDecalTexture( ~texture ) : Texture.Load( Server.GetMountPath( $"decals/{texture}.vtex" ) ) );

		for ( var i = 0; i < Face.MaxLightmaps; i++ )
			material.Set( $"Lightmap{i}", _lightmaps[i] );

		return _decalMaterials[texture] = material;
	}

	private static bool SameDecalParts( List<DecalVertex[]> a, List<DecalVertex[]> b )
	{
		if ( a.Count != b.Count )
			return false;

		for ( var i = 0; i < a.Count; i++ )
		{
			if ( !ReferenceEquals( a[i], b[i] ) )
				return false;
		}

		return true;
	}

	private void DecalVertsClip( Decal decal, int surface, List<DecalVertex> vertices, List<int> indices, List<DecalVertex[]> parts )
	{
		if ( !_decalVertices.TryGetValue( decal, out var clipped ) )
			_decalVertices[decal] = clipped = DecalVertsClip( decal, surface );

		if ( clipped.Length < 3 )
			return;

		var first = vertices.Count;

		parts.Add( clipped );
		vertices.AddRange( clipped );

		for ( var i = 1; i < clipped.Length - 1; i++ )
		{
			indices.Add( first );
			indices.Add( first + i + 1 );
			indices.Add( first + i );
		}
	}

	private DecalVertex[] DecalVertsClip( Decal decal, int surface )
	{
		var face = WorldFile.Faces[surface];
		var texInfo = WorldFile.TexInfos[face.TexInfo];
		var plane = WorldFile.Planes[face.PlaneNum];
		var normal = face.Side != 0 ? -plane.Normal : plane.Normal;

		var (textureWidth, textureHeight) = WorldFile.GetTextureSize( face );
		var (_, decalWidth, decalHeight) = DecalTexture( decal.Texture );

		var scaleS = textureWidth * decal.Scale / decalWidth;
		var scaleT = textureHeight * decal.Scale / decalHeight;

		var polygon = new List<(Vector3 Position, float S, float T)>( face.NumEdges );

		for ( var i = 0; i < face.NumEdges; i++ )
		{
			var position = WorldFile.GetVertex( face, i );

			var s = (Vector3.Dot( position, texInfo.S ) + texInfo.OffsetS) / textureWidth;
			var t = (Vector3.Dot( position, texInfo.T ) + texInfo.OffsetT) / textureHeight;

			s = (s - decal.Dx) * scaleS;
			t = (t - decal.Dy) * scaleT;

			if ( (decal.Flags & DecalFlags.HFlip) == DecalFlags.HFlip )
				s = 1f - s;

			if ( (decal.Flags & DecalFlags.VFlip) == DecalFlags.VFlip )
				t = 1f - t;

			polygon.Add( (position, s, t) );
		}

		polygon = ClipDecal( polygon, x => x.S, 0f, true );
		polygon = ClipDecal( polygon, x => x.S, 1f, false );
		polygon = ClipDecal( polygon, x => x.T, 0f, true );
		polygon = ClipDecal( polygon, x => x.T, 1f, false );

		if ( polygon.Count < 3 )
			return [];

		var vertices = new DecalVertex[polygon.Count];

		for ( var i = 0; i < polygon.Count; i++ )
		{
			var (position, s, t) = polygon[i];

			vertices[i] = new DecalVertex
			{
				Position = position,
				Normal = normal,
				TexCoord = new Vector2( s, t ),
				LightmapCoord = LightmapCoord( surface, position ),
				LightmapBlock = LightmapBlock( surface )
			};
		}

		return vertices;
	}

	private static List<(Vector3 Position, float S, float T)> ClipDecal( List<(Vector3 Position, float S, float T)> polygon, Func<(Vector3 Position, float S, float T), float> coordinate, float edge, bool keepGreater )
	{
		var result = new List<(Vector3 Position, float S, float T)>( polygon.Count + 1 );

		for ( var i = 0; i < polygon.Count; i++ )
		{
			var current = polygon[i];
			var next = polygon[(i + 1) % polygon.Count];

			var currentValue = coordinate( current );
			var nextValue = coordinate( next );

			var currentInside = keepGreater ? currentValue > edge : currentValue < edge;
			var nextInside = keepGreater ? nextValue > edge : nextValue < edge;

			if ( currentInside )
				result.Add( current );

			if ( currentInside == nextInside )
				continue;

			var fraction = (edge - currentValue) / (nextValue - currentValue);

			result.Add( (Vector3.Lerp( current.Position, next.Position, fraction ),
				current.S + ((next.S - current.S) * fraction),
				current.T + ((next.T - current.T) * fraction)) );
		}

		return result;
	}
}
