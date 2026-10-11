using System;
using GoldSrc.Bsp;

namespace GoldSrc;

public sealed class DLight
{
	public Vector3 Origin;
	public float Radius;
	public byte R;
	public byte G;
	public byte B;
	public float Die;
	public float Decay;
	public float MinLight;
	public int Key;
	public bool Dark;

	internal void Clear( int key )
	{
		Origin = Vector3.Zero;
		Radius = 0f;
		R = 0;
		G = 0;
		B = 0;
		Die = 0f;
		Decay = 0f;
		MinLight = 0f;
		Key = key;
		Dark = false;
	}
}

partial class Client
{
	public const int MaxDLights = 32;
	public const int MaxELights = 64;

	private const int MaxLocalLights = 3;

	private readonly DLight[] _dlights = CreateLights( MaxDLights );
	private readonly DLight[] _elights = CreateLights( MaxELights );

	private const float FullBrightLight = 65280f;

	private readonly PointLight[] _sceneLights = new PointLight[MaxDLights];

	private readonly Vector4[] _localLights = new Vector4[MaxLocalLights];
	private readonly Vector3[] _localLinearLights = new Vector3[MaxLocalLights];
	private readonly float[] _localLightStrengths = new float[MaxLocalLights];
	private int _numLocalLights;

	private static DLight[] CreateLights( int count )
	{
		var lights = new DLight[count];

		for ( var i = 0; i < count; i++ )
			lights[i] = new DLight();

		return lights;
	}

	public void ClearLights()
	{
		FlashlightOff();

		foreach ( var light in _dlights )
			light.Clear( 0 );

		foreach ( var light in _elights )
			light.Clear( 0 );

		foreach ( var light in _sceneLights )
		{
			if ( light.IsValid() )
				light.Enabled = false;
		}
	}

	private void UpdateSceneLights()
	{
		for ( var i = 0; i < MaxDLights; i++ )
		{
			var light = _dlights[i];
			var active = light.Die >= ClientTime && light.Radius > 0f && !light.Dark;

			if ( !active )
			{
				if ( _sceneLights[i].IsValid() )
					_sceneLights[i].Enabled = false;

				continue;
			}

			if ( !_sceneLights[i].IsValid() )
			{
				_sceneLights[i] = new GameObject( true, "dlight" ).AddComponent<PointLight>();
				_sceneLights[i].GameObject.NetworkMode = NetworkMode.Never;
			}

			var peak = MathF.Min( light.Radius * 255f / FullBrightLight, 1f );
			var sceneLight = _sceneLights[i];

			sceneLight.Enabled = true;
			sceneLight.Shadows = true;
			sceneLight.WorldPosition = light.Origin;
			sceneLight.Radius = light.Radius;
			sceneLight.LightColor = new Color(
				ScreenToLinear( light.R / 255f * peak ),
				ScreenToLinear( light.G / 255f * peak ),
				ScreenToLinear( light.B / 255f * peak ) );
		}
	}

	public DLight AllocDlight( int key )
	{
		if ( key != 0 )
		{
			foreach ( var light in _dlights )
			{
				if ( light.Key != key )
					continue;

				light.Clear( key );
				return light;
			}
		}

		foreach ( var light in _dlights )
		{
			if ( light.Die >= ClientTime )
				continue;

			light.Clear( key );
			return light;
		}

		_dlights[0].Clear( key );
		return _dlights[0];
	}

	public DLight AllocElight( int key )
	{
		if ( key != 0 )
		{
			foreach ( var light in _elights )
			{
				if ( light.Key != key )
					continue;

				light.Clear( key );
				return light;
			}
		}

		foreach ( var light in _elights )
		{
			if ( light.Die >= ClientTime )
				continue;

			light.Clear( key );
			return light;
		}

		_elights[0].Clear( key );
		return _elights[0];
	}

	public void DecayLights( float frameTime )
	{
		foreach ( var light in _dlights )
		{
			if ( light.Radius == 0f )
				continue;

			if ( light.Die < ClientTime )
			{
				light.Radius = 0f;
				continue;
			}

			if ( light.Decay == 0f )
				continue;

			light.Radius -= light.Decay * frameTime;

			if ( light.Radius < 0f )
				light.Radius = 0f;
		}

		foreach ( var light in _elights )
		{
			if ( light.Radius == 0f )
				continue;

			if ( light.Die < ClientTime )
			{
				light.Radius = 0f;
				continue;
			}

			light.Radius -= frameTime * light.Decay;

			if ( light.Radius < 0f )
				light.Radius = 0f;
		}
	}

	public void StudioEntityLight( int entityIndex, Vector3 origin, Vector3[] attachments, RenderAttributes attributes )
	{
		var lightStrength = 1e+06f;

		_numLocalLights = 0;

		Array.Clear( _localLightStrengths );

		foreach ( var light in _elights )
		{
			if ( !(ClientTime < light.Die) || light.Radius <= 0f )
				continue;

			if ( (light.Key & 0xfff) == entityIndex )
			{
				var attachment = (light.Key >> 12) & 0xf;

				light.Origin = attachment == 0 ? origin : attachments[attachment - 1];
			}

			var radiusSquared = light.Radius * light.Radius;
			var distance = origin - light.Origin;
			var distanceSquared = (distance.x * distance.x) + (distance.y * distance.y) + (distance.z * distance.z);
			var strength = distanceSquared <= radiusSquared ? 1f : radiusSquared / distanceSquared;

			if ( strength <= 0.004f )
				continue;

			var index = _numLocalLights;

			if ( _numLocalLights >= MaxLocalLights )
			{
				index = -1;

				for ( var i = 0; i < _numLocalLights; i++ )
				{
					if ( _localLightStrengths[i] < lightStrength && _localLightStrengths[i] < strength )
					{
						index = i;
						lightStrength = _localLightStrengths[i];
					}
				}
			}

			if ( index == -1 )
				continue;

			_localLights[index] = new Vector4( light.Origin, radiusSquared );
			_localLightStrengths[index] = strength;
			_localLinearLights[index] = new Vector3( LinearGamma( light.R * 4 ), LinearGamma( light.G * 4 ), LinearGamma( light.B * 4 ) );

			if ( _numLocalLights <= index )
				_numLocalLights = index + 1;
		}

		attributes.Set( "LocalLightCount", (float)_numLocalLights );

		for ( var i = 0; i < _numLocalLights; i++ )
		{
			attributes.Set( $"LocalLight{i}", _localLights[i] );
			attributes.Set( $"LocalLightColor{i}", _localLinearLights[i] );
		}
	}

	private static int LinearGamma( int index )
	{
		return (int)(Math.Pow( index / 1023.0, Client.Gamma ) * 1023.0);
	}
}

partial class Client
{
	private const int DynamicLightColumns = LightmapColumns;

	private int[] _surfaceDlightBits;
	private int[] _surfaceDlightFrame;
	private Vector3[] _surfaceDlightOrigin;
	private int _dlightFrameCount;

	private readonly List<int> _dlightSurfaces = [];
	private readonly HashSet<int> _dirtyDlightPages = [];

	private float[][] _dynamicLight;
	private Texture _dynamicLightTexture;

	private void ClearDynamicLights()
	{
		_dynamicLightTexture?.Dispose();
		_dynamicLightTexture = null;
		_dynamicLight = null;

		_dlightSurfaces.Clear();
		_dirtyDlightPages.Clear();

		Scene.RenderAttributes.Set( "HasDynamicLight", 0f );
	}

	public void PushDlights()
	{
		if ( _lightmaps is null )
			BuildLightmaps();

		if ( _dynamicLight is null )
			CreateDynamicLight();

		foreach ( var surface in _dlightSurfaces )
			ClearDynamicLight( surface );

		_dlightSurfaces.Clear();
		_dlightFrameCount++;

		for ( var i = 0; i < MaxDLights; i++ )
		{
			var light = _dlights[i];

			if ( light.Die >= ClientTime && light.Radius != 0f )
				MarkLights( light.Origin, light, 1 << i, 0, Vector3.Zero );
		}

		foreach ( var entity in VisibleEntities )
		{
			if ( GetBrushModel( entity.ModelIndex ) is not { SubModel: > 0 } model )
				continue;

			var subModel = WorldFile.Models[model.SubModel];

			if ( subModel.FirstFace == 0 )
				continue;

			var origin = entity.WorldPosition;

			for ( var i = 0; i < MaxDLights; i++ )
			{
				var light = _dlights[i];

				if ( light.Die >= ClientTime && light.Radius != 0f )
					MarkLights( light.Origin - origin, light, 1 << i, subModel.HeadNode[0], origin );
			}
		}

		foreach ( var surface in _dlightSurfaces )
			AddDynamicLights( surface );

		UploadDynamicLight();

		Scene.RenderAttributes.Set( "DynamicLight", _dynamicLightTexture );
		Scene.RenderAttributes.Set( "HasDynamicLight", 1f );

		UpdateSceneLights();
	}

	private void CreateDynamicLight()
	{
		var rows = (_lightmapPageCount + DynamicLightColumns - 1) / DynamicLightColumns;

		_dynamicLight = new float[_lightmapPageCount][];

		for ( var i = 0; i < _dynamicLight.Length; i++ )
			_dynamicLight[i] = new float[BlockWidth * BlockHeight * 4];

		_dynamicLightTexture = Texture.Create( BlockWidth * DynamicLightColumns, BlockHeight * rows, ImageFormat.RGBA32323232F ).WithDynamicUsage().Finish();

		for ( var i = 0; i < _dynamicLight.Length; i++ )
			_dirtyDlightPages.Add( i );

		_surfaceDlightBits = new int[WorldFile.Faces.Length];
		_surfaceDlightFrame = new int[WorldFile.Faces.Length];
		_surfaceDlightOrigin = new Vector3[WorldFile.Faces.Length];

	}

	private void MarkLights( Vector3 origin, DLight light, int bit, int node, Vector3 entityOrigin )
	{
		while ( node >= 0 )
		{
			ref readonly var mnode = ref WorldFile.Nodes[node];
			var plane = WorldFile.Planes[mnode.PlaneNum];
			var distance = (plane.Normal.y * origin.y) + (plane.Normal.x * origin.x) + (plane.Normal.z * origin.z) - plane.Dist;

			if ( distance > light.Radius )
			{
				node = mnode.Child0;
				continue;
			}

			if ( distance < -light.Radius )
			{
				node = mnode.Child1;
				continue;
			}

			for ( var i = 0; i < mnode.NumFaces; i++ )
			{
				var surface = mnode.FirstFace + i;
				var reach = light.Radius - MathF.Abs( distance );

				if ( reach < light.MinLight )
					continue;

				var radius = reach - light.MinLight;
				var texInfo = WorldFile.TexInfos[WorldFile.Faces[surface].TexInfo];
				var extents = WorldFile.SurfaceExtents[surface];

				var s = (int)((origin.y * texInfo.S.y) + (origin.x * texInfo.S.x) + (origin.z * texInfo.S.z) + texInfo.OffsetS - extents.MinS);

				if ( -(int)radius >= s )
					continue;

				var t = (origin.x * texInfo.T.x) + (origin.y * texInfo.T.y) + (origin.z * texInfo.T.z) + texInfo.OffsetT - extents.MinT;

				if ( -(int)radius >= (int)t || s > extents.ExtentS + (int)radius || (int)t > extents.ExtentT + (int)radius )
					continue;

				if ( _surfaceDlightFrame[surface] != _dlightFrameCount )
				{
					_surfaceDlightFrame[surface] = _dlightFrameCount;
					_surfaceDlightBits[surface] = 0;
					_surfaceDlightOrigin[surface] = entityOrigin;
					_dlightSurfaces.Add( surface );
				}

				_surfaceDlightBits[surface] |= bit;
			}

			MarkLights( origin, light, bit, mnode.Child0, entityOrigin );

			node = mnode.Child1;
		}
	}

	private bool HasLightmap( int surface )
	{
		var flags = WorldFile.SurfaceFlags[surface];

		if ( (flags & (Bsp.File.SurfaceDrawSky | Bsp.File.SurfaceDrawTurb)) != 0 )
			return false;

		return (flags & Bsp.File.SurfaceDrawTiled) == 0 || (WorldFile.TexInfos[WorldFile.Faces[surface].TexInfo].Flags & TexInfoFlags.Special) != TexInfoFlags.Special;
	}

	private void AddDynamicLights( int surface )
	{
		if ( !HasLightmap( surface ) )
			return;

		var face = WorldFile.Faces[surface];
		var texInfo = WorldFile.TexInfos[face.TexInfo];
		var plane = WorldFile.Planes[face.PlaneNum];
		var extents = WorldFile.SurfaceExtents[surface];
		var (page, x, y) = _lightmapSurfaces[surface];

		var smax = (extents.ExtentS >> 4) + 1;
		var tmax = (extents.ExtentT >> 4) + 1;
		var blocklights = _dynamicLight[page];

		for ( var i = 0; i < MaxDLights; i++ )
		{
			if ( (_surfaceDlightBits[surface] & (1 << i)) == 0 )
				continue;

			var light = _dlights[i];
			var impact = light.Origin - _surfaceDlightOrigin[surface];
			var radius = light.Radius - MathF.Abs( (impact.y * plane.Normal.y) + (impact.x * plane.Normal.x) + (impact.z * plane.Normal.z) - plane.Dist );
			var minLight = light.MinLight;

			if ( radius < minLight )
				continue;

			var localS = (impact.y * texInfo.S.y) + (impact.x * texInfo.S.x) + (impact.z * texInfo.S.z) + texInfo.OffsetS - extents.MinS;
			var localT = (impact.y * texInfo.T.y) + (impact.x * texInfo.T.x) + (impact.z * texInfo.T.z) + texInfo.OffsetT - extents.MinT;

			for ( var t = 0; t < tmax; t++ )
			{
				var td = Math.Abs( (int)(localT - (t * 16)) );

				for ( var s = 0; s < smax; s++ )
				{
					var sd = Math.Abs( (int)(localS - (s * 16)) );
					var distance = td < sd ? sd + (td >> 1) : (sd >> 1) + td;

					if ( distance >= radius - minLight )
						continue;

					var amount = (int)((radius - distance) * 256f);
					var texel = (((y + t) * BlockWidth) + x + s) * 4;

					blocklights[texel] += (light.R * amount) >> 8;
					blocklights[texel + 1] += (light.G * amount) >> 8;
					blocklights[texel + 2] += (light.B * amount) >> 8;
				}
			}
		}

		_dirtyDlightPages.Add( page );
	}

	private void ClearDynamicLight( int surface )
	{
		if ( !HasLightmap( surface ) )
			return;

		var extents = WorldFile.SurfaceExtents[surface];
		var (page, x, y) = _lightmapSurfaces[surface];

		var smax = (extents.ExtentS >> 4) + 1;
		var tmax = (extents.ExtentT >> 4) + 1;

		for ( var t = 0; t < tmax; t++ )
			Array.Clear( _dynamicLight[page], (((y + t) * BlockWidth) + x) * 4, smax * 4 );

		_dirtyDlightPages.Add( page );
	}

	private void UploadDynamicLight()
	{
		foreach ( var page in _dirtyDlightPages )
			_dynamicLightTexture.Update<float>( _dynamicLight[page], page % DynamicLightColumns * BlockWidth, page / DynamicLightColumns * BlockHeight, BlockWidth, BlockHeight );

		_dirtyDlightPages.Clear();
	}
}
