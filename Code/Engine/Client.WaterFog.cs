using System;
using GoldSrc.Bsp;
using Plane = GoldSrc.Bsp.Plane;

namespace GoldSrc;

partial class Client
{
	private const float TransAlphaDistance = 1e9f;

	private readonly Plane[] _frustum = new Plane[4];
	private readonly List<(ClientEntity Entity, float Distance)> _transBrushes = [];

	private (byte R, byte G, byte B, byte Density)[] _waterPalettes;
	private (byte R, byte G, byte B, byte Density) _waterFog;
	private int[] _surfaceFrames;
	private int _surfaceFrameCount;
	private int _waterChain;

	public Vector4 WaterFog => new( _waterFog.R / 255f, _waterFog.G / 255f, _waterFog.B / 255f, 1536 - (4 * _waterFog.Density) );

	private void LoadWaterPalettes()
	{
		var world = WorldFile;

		_waterPalettes = new (byte, byte, byte, byte)[world.TextureNames.Length];
		_surfaceFrames = new int[world.Faces.Length];
		_surfaceFrameCount = 0;

		string[] wadFiles = null;

		for ( var i = 0; i < _waterPalettes.Length; i++ )
		{
			if ( world.TextureNames[i] is not { } name )
				continue;

			var palette = world.GetTexturePalette( i );

			if ( palette is null )
			{
				wadFiles ??= WadFiles();

				foreach ( var wadFile in wadFiles )
				{
					if ( Server.LoadMountFile( $"{wadFile}/{name.ToLowerInvariant()}" ) is not { } lump )
						continue;

					palette = Bsp.File.MipTexPalette( lump );
					break;
				}
			}

			if ( palette is { Length: >= 13 } )
				_waterPalettes[i] = (palette[9], palette[10], palette[11], palette[12]);
		}
	}

	private string[] WadFiles()
	{
		var text = WorldFile.Entities;
		var position = 0;

		if ( Common.Parse( text, ref position ) != "{" )
			return [];

		while ( Common.Parse( text, ref position ) is { } key && key != "}" )
		{
			if ( Common.Parse( text, ref position ) is not { } value )
				break;

			if ( key != "wad" )
				continue;

			var files = value.Split( ';', StringSplitOptions.RemoveEmptyEntries );

			for ( var i = 0; i < files.Length; i++ )
				files[i] = System.IO.Path.GetFileName( files[i].Replace( '\\', '/' ) );

			return files;
		}

		return [];
	}

	public void EmitWater( Vector3 viewOrigin, Vector3 forward, Vector3 right, Vector3 up, float fieldOfView, float width, float height )
	{
		MarkViewLeaves( viewOrigin );
		SetFrustum( viewOrigin, forward, right, up, fieldOfView, width, height );

		_surfaceFrameCount++;
		_waterChain = -1;

		RecursiveWorldNode( 0, viewOrigin );

		if ( _waterChain >= 0 )
			EmitWaterPolys( WorldFile.Faces[_waterChain] );

		_transBrushes.Clear();

		foreach ( var entity in VisibleEntities )
		{
			if ( !entity.Drawn || GetModelByIndex( entity.ModelIndex ) is not BrushModel )
				continue;

			if ( entity.RenderMode == RenderMode.Normal )
				DrawBrushModelWater( entity, viewOrigin );
			else
				AddTransBrush( entity, viewOrigin );
		}

		foreach ( var (entity, _) in _transBrushes )
			DrawBrushModelWater( entity, viewOrigin );
	}

	private void EmitWaterPolys( in Face face )
	{
		var mipTex = WorldFile.TexInfos[face.TexInfo].MipTex;

		if ( mipTex >= 0 && mipTex < _waterPalettes.Length )
			_waterFog = _waterPalettes[mipTex];
	}

	private void SetFrustum( Vector3 origin, Vector3 forward, Vector3 right, Vector3 up, float fieldOfView, float width, float height )
	{
		var aspect = width / height;

		if ( Client.WidescreenYFov != 0f && aspect > 1.5f )
		{
			var half = (float)(Math.Tan( fieldOfView * 0.008726646f ) * (aspect * 0.75f));
			var degrees = (float)(Math.Atan( half ) * 57.29577951308232);

			fieldOfView = degrees + degrees;
		}

		var fovX = fieldOfView < 1f || fieldOfView > 179f ? 90f : fieldOfView;
		var fovY = (float)Math.Atan( height / (float)(width / Math.Tan( fovX / 360f * Math.PI )) ) * 360f / 3.1415927f;

		var angleX = (90f - (fieldOfView * 0.5f)) * (MathF.PI / 180f);
		var angleY = (90f - (fovY * 0.5f)) * (MathF.PI / 180f);

		SetFrustumPlane( 0, origin, (forward * MathF.Cos( angleX )) + (right * MathF.Sin( angleX )) );
		SetFrustumPlane( 1, origin, (forward * MathF.Cos( angleX )) - (right * MathF.Sin( angleX )) );
		SetFrustumPlane( 2, origin, (forward * MathF.Cos( angleY )) + (up * MathF.Sin( angleY )) );
		SetFrustumPlane( 3, origin, (forward * MathF.Cos( angleY )) - (up * MathF.Sin( angleY )) );
	}

	private void SetFrustumPlane( int index, Vector3 origin, Vector3 normal )
	{
		_frustum[index] = new Plane( normal, Vector3.Dot( origin, normal ), PlaneType.AnyZ );
	}

	private bool CullBox( Vector3 mins, Vector3 maxs )
	{
		foreach ( var plane in _frustum )
		{
			if ( Bsp.File.BoxOnPlaneSide( mins, maxs, plane ) == 2 )
				return true;
		}

		return false;
	}

	private void RecursiveWorldNode( int index, Vector3 modelOrigin )
	{
		var world = WorldFile;

		while ( true )
		{
			if ( index < 0 )
			{
				var leafIndex = -1 - index;
				var leaf = world.Leafs[leafIndex];

				if ( leaf.Contents == Contents.Solid || _viewLeafFrames[leafIndex] != _viewFrameCount || CullBox( leaf.Mins, leaf.Maxs ) )
					return;

				for ( var i = 0; i < leaf.NumMarkSurfaces; i++ )
					_surfaceFrames[world.MarkSurfaces[leaf.FirstMarkSurface + i]] = _surfaceFrameCount;

				return;
			}

			var node = world.Nodes[index];

			if ( _viewNodeFrames[index] != _viewFrameCount || CullBox( node.Mins, node.Maxs ) )
				return;

			var plane = world.Planes[node.PlaneNum];

			var dot = plane.Type switch
			{
				PlaneType.X => modelOrigin.x,
				PlaneType.Y => modelOrigin.y,
				PlaneType.Z => modelOrigin.z,
				_ => (plane.Normal.y * modelOrigin.y) + (plane.Normal.x * modelOrigin.x) + (plane.Normal.z * modelOrigin.z)
			} - plane.Dist;

			var back = dot < 0f;

			RecursiveWorldNode( node.GetChild( back ? 1 : 0 ), modelOrigin );

			for ( var i = 0; i < node.NumFaces; i++ )
			{
				var surface = node.FirstFace + i;

				if ( _surfaceFrames[surface] != _surfaceFrameCount || (world.Faces[surface].Side != 0) != back )
					continue;

				if ( (WorldFile.SurfaceFlags[surface] & Bsp.File.SurfaceDrawTurb) != 0 && _waterChain < 0 )
					_waterChain = surface;
			}

			index = node.GetChild( back ? 0 : 1 );
		}
	}

	private void AddTransBrush( ClientEntity entity, Vector3 viewOrigin )
	{
		var model = GetModelByIndex( entity.ModelIndex );
		float distance;

		if ( entity.RenderMode == RenderMode.TransAlpha )
		{
			distance = TransAlphaDistance;
		}
		else
		{
			var offset = viewOrigin - (((model.Maxs + model.Mins) * 0.5f) + entity.WorldPosition);

			distance = (offset.y * offset.y) + (offset.x * offset.x) + (offset.z * offset.z);
		}

		var index = _transBrushes.Count;

		while ( index > 0 && distance > _transBrushes[index - 1].Distance )
			index--;

		_transBrushes.Insert( index, (entity, distance) );
	}

	private void DrawBrushModelWater( ClientEntity entity, Vector3 viewOrigin )
	{
		if ( entity.RenderMode == RenderMode.TransColor )
			return;

		var model = (BrushModel)GetModelByIndex( entity.ModelIndex );
		var origin = entity.WorldPosition;
		var angles = entity.RenderAngles;

		Vector3 mins;
		Vector3 maxs;

		if ( angles.x == 0f && angles.y == 0f && angles.z == 0f )
		{
			mins = origin + model.Mins;
			maxs = origin + model.Maxs;
		}
		else
		{
			var radius = model.Radius;

			mins = origin - radius;
			maxs = origin + radius;
		}

		if ( CullBox( mins, maxs ) )
			return;

		var world = WorldFile;
		var subModel = world.Models[model.SubModel];

		for ( var i = 0; i < subModel.NumFaces; i++ )
		{
			var surface = subModel.FirstFace + i;

			if ( (WorldFile.SurfaceFlags[surface] & Bsp.File.SurfaceDrawTurb) == 0 )
				continue;

			var face = world.Faces[surface];
			var plane = world.Planes[face.PlaneNum];

			if ( (plane.Type == PlaneType.Z || WaterSides != 0f) && mins.z + 1f < plane.Dist )
				EmitWaterPolys( face );
		}
	}
}

partial class Client
{
	private int[] _viewNodeFrames;
	private int[] _viewLeafFrames;
	private int _viewFrameCount;
	private int _viewLeaf;

	public void MarkViewLeaves( Vector3 viewOrigin )
	{
		var leaf = WorldFile.PointInLeaf( viewOrigin );

		if ( leaf == _viewLeaf )
			return;

		_viewLeaf = leaf;

		WorldFile.MarkLeaves( WorldFile.LeafPvs( leaf ), _viewNodeFrames, _viewLeafFrames, ++_viewFrameCount );
	}

	public bool BoxInViewPvs( Vector3 mins, Vector3 maxs )
	{
		return WorldFile.PvsNode( 0, mins, maxs, _viewNodeFrames, _viewLeafFrames, _viewFrameCount );
	}
}
