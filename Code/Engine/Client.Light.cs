using System;
using GoldSrc.Bsp;

namespace GoldSrc;

public struct StudioLight
{
	public int AmbientLight;
	public int ShadeLight;
	public Vector3 Color;
	public Vector3 LightVector;
	public Vector3Int? FloorColor;
}

partial class Client
{
	private readonly record struct LightSample( int Face, int Lightmap );

	private const int StudioFlagDirectLight = 0x100;
	private const int StudioFlagSkyLight = 0x400;

	[ConVar( "direct" )]
	public static float Direct { get; set; } = 0.9f;

	[ConVar( "gl_watersides" )]
	public static float WaterSides { get; set; }

	[ConVar( "r_fullbright" )]
	public static float FullBright { get; set; }

	private readonly int[] _lightStyleValues = new int[256];
	private readonly float[] _lightStyleData = new float[Server.MaxLightStyles];
	private Texture _lightStyleTexture;

	private void AnimateLight()
	{
		var frame = (int)(ClientTime * 10.0);

		for ( var i = 0; i < _lightStyleValues.Length; i++ )
		{
			var pattern = i < Server.MaxLightStyles ? _lightStyles[i] : null;

			_lightStyleValues[i] = string.IsNullOrEmpty( pattern ) ? 256 : (pattern[frame % pattern.Length] - 'a') * 22;
		}

		for ( var i = 0; i < Server.MaxLightStyles; i++ )
			_lightStyleData[i] = _lightStyleValues[i];

		_lightStyleTexture ??= Texture.Create( Server.MaxLightStyles, 1, ImageFormat.R32F ).WithDynamicUsage().Finish();
		_lightStyleTexture.Update<float>( _lightStyleData );

		Scene.RenderAttributes.Set( "LightStyleValues", _lightStyleTexture );
		Scene.RenderAttributes.Set( "HasLightStyleValues", 1f );

		UpdateLightVolume();
	}

	public StudioLight StudioDynamicLight( Effects effects, RenderFx renderFx, int lightMultiplier, Vector3 origin, int modelFlags )
	{
		if ( FullBright == 1f )
		{
			return new StudioLight
			{
				AmbientLight = 192,
				ShadeLight = 0,
				Color = Vector3.One,
				LightVector = new Vector3( 0f, 0f, -1f )
			};
		}

		var lightVector = new Vector3( 0f, 0f, (effects & Effects.InvLight) == Effects.InvLight ? 1f : -1f );
		var upOrigin = new Vector3( origin.x, origin.y, origin.z - (lightVector.z * 8f) );

		var red = 0;
		var green = 0;
		var blue = 0;

		if ( _moveVars.SkyColorR + _moveVars.SkyColorG + _moveVars.SkyColorB != 0f )
		{
			var skyVector = new Vector3( _moveVars.SkyVecX, _moveVars.SkyVecY, _moveVars.SkyVecZ );
			var end = origin - (skyVector * 8192f);
			var surface = WorldFile.Nodes.Length == 0 ? -1 : WorldFile.SurfaceAtPoint( 0, upOrigin, end );

			if ( (modelFlags & StudioFlagSkyLight) != 0 || (surface >= 0 && (WorldFile.SurfaceFlags[surface] & Bsp.File.SurfaceDrawSky) != 0) )
			{
				red = (int)_moveVars.SkyColorR;
				green = (int)_moveVars.SkyColorG;
				blue = (int)_moveVars.SkyColorB;

				lightVector = skyVector;
			}
		}

		if ( red + green + blue == 0 )
		{
			var end = upOrigin + (lightVector * 2048f);

			(red, green, blue) = LightVec( upOrigin, end );

			upOrigin.x -= 16f;
			upOrigin.y -= 16f;
			end.x -= 16f;
			end.y -= 16f;

			var grad0 = Gradient( upOrigin, end );

			upOrigin.x += 32f;
			end.x += 32f;

			var grad1 = Gradient( upOrigin, end );

			upOrigin.y += 32f;
			end.y += 32f;

			var grad2 = Gradient( upOrigin, end );

			upOrigin.x -= 32f;
			end.x -= 32f;

			var grad3 = Gradient( upOrigin, end );

			lightVector.x = grad0 - grad1 - grad2 + grad3;
			lightVector.y = grad1 + grad0 - grad2 - grad3;

			MathLib.VectorNormalize( ref lightVector );
		}

		if ( renderFx == RenderFx.LightMultiplier && lightMultiplier != 10 )
		{
			var multiplier = lightMultiplier / 10f;

			red = (int)(red * multiplier);
			green = (int)(green * multiplier);
			blue = (int)(blue * multiplier);
		}

		var floorColor = new Vector3Int( red, green, blue );
		var lightColor = new Vector3( red, green, blue );
		var total = (float)Math.Max( Math.Max( red, green ), blue );

		if ( total == 0f )
			total = 1f;

		lightVector *= total;

		foreach ( var dlight in _dlights )
		{
			if ( dlight.Die < ClientTime )
				continue;

			var distance = origin - dlight.Origin;
			var length = MathLib.Length( distance );
			var add = dlight.Radius - length;

			if ( add <= 0f )
				continue;

			total += add;
			lightVector += distance * (length > 1f ? add / length : add);

			lightColor.x += dlight.R * add * 0.00390625f;
			lightColor.y += dlight.G * add * 0.00390625f;
			lightColor.z += dlight.B * add * 0.00390625f;
		}

		var direct = (modelFlags & StudioFlagDirectLight) != 0 ? 0.6f : Math.Max( 0.75f, Math.Min( Direct, 1f ) );

		lightVector *= direct;

		var light = new StudioLight { ShadeLight = (int)MathLib.Length( lightVector ), FloorColor = floorColor };

		light.AmbientLight = (int)(total - light.ShadeLight);

		var max = Math.Max( Math.Max( lightColor.x, lightColor.y ), lightColor.z );

		light.Color = max == 0f ? Vector3.One : lightColor * (1f / max);

		if ( light.AmbientLight > 128 )
			light.AmbientLight = 128;

		if ( light.AmbientLight + light.ShadeLight > 255 )
			light.ShadeLight = 255 - light.AmbientLight;

		MathLib.VectorNormalize( ref lightVector );
		light.LightVector = lightVector;

		return light;
	}

	private float Gradient( Vector3 start, Vector3 end )
	{
		var (red, green, blue) = LightVec( start, end );

		return (float)((red + green + blue) / 768.0);
	}

	public (int Red, int Green, int Blue) LightVec( Vector3 start, Vector3 end )
	{
		if ( WorldFile.Lighting.Length == 0 )
			return (255, 255, 255);

		var (red, green, blue) = WorldFile.Nodes.Length == 0 ? (0, 0, 0) : RecursiveLightPoint( 0, start, end );

		return (Math.Min( red, 255 ), Math.Min( green, 255 ), Math.Min( blue, 255 ));
	}

	private (int Red, int Green, int Blue) RecursiveLightPoint( int nodeIndex, Vector3 start, Vector3 end )
	{
		if ( RecursiveLightSample( nodeIndex, start, end ) is not { } sample )
			return (0, 0, 0);

		var (red, green, blue) = BlendLightSample( sample, _lightStyleValues );

		red >>= 8;
		green >>= 8;
		blue >>= 8;

		if ( red == 0 )
			red = 1;

		return (red, green, blue);
	}

	private (int Red, int Green, int Blue) BlendLightSample( LightSample sample, int[] styleValues )
	{
		var face = WorldFile.Faces[sample.Face];
		var extents = WorldFile.SurfaceExtents[sample.Face];
		var size = ((extents.ExtentS >> 4) + 1) * ((extents.ExtentT >> 4) + 1) * 3;
		var lightmap = sample.Lightmap;

		var red = 0;
		var green = 0;
		var blue = 0;

		for ( var map = 0; map < Face.MaxLightmaps && face.Styles[map] != 255; map++ )
		{
			var scale = styleValues[face.Styles[map]];

			red += WorldFile.Lighting[lightmap] * scale;
			green += WorldFile.Lighting[lightmap + 1] * scale;
			blue += WorldFile.Lighting[lightmap + 2] * scale;

			lightmap += size;
		}

		return (red, green, blue);
	}

	private LightSample? RecursiveLightSample( int nodeIndex, Vector3 start, Vector3 end )
	{
		if ( nodeIndex < 0 )
			return null;

		var node = WorldFile.Nodes[nodeIndex];
		var plane = WorldFile.Planes[node.PlaneNum];

		var front = (start.x * plane.Normal.x) + (plane.Normal.y * start.y) + (plane.Normal.z * start.z) - plane.Dist;
		var back = (end.x * plane.Normal.x) + (end.y * plane.Normal.y) + (end.z * plane.Normal.z) - plane.Dist;
		var side = front < 0f ? 1 : 0;

		if ( (back < 0f) == (front < 0f) )
			return RecursiveLightSample( node.GetChild( side ), start, end );

		var frac = front / (front - back);
		var mid = start + ((end - start) * frac);

		if ( RecursiveLightSample( node.GetChild( side ), start, mid ) is { } sample )
			return sample;

		for ( var i = 0; i < node.NumFaces; i++ )
		{
			var index = node.FirstFace + i;

			if ( (WorldFile.SurfaceFlags[index] & Bsp.File.SurfaceDrawTiled) != 0 )
				continue;

			var face = WorldFile.Faces[index];
			var texInfo = WorldFile.TexInfos[face.TexInfo];
			var extents = WorldFile.SurfaceExtents[index];

			var s = (int)((mid.y * texInfo.S.y) + (mid.x * texInfo.S.x) + (mid.z * texInfo.S.z) + texInfo.OffsetS);
			if ( s < extents.MinS )
				continue;

			var t = (int)((mid.y * texInfo.T.y) + (mid.x * texInfo.T.x) + (mid.z * texInfo.T.z) + texInfo.OffsetT);
			if ( t < extents.MinT )
				continue;

			var ds = s - extents.MinS;
			var dt = t - extents.MinT;

			if ( ds > extents.ExtentS || dt > extents.ExtentT )
				continue;

			if ( face.LightOffset == -1 )
				return null;

			var width = (extents.ExtentS >> 4) + 1;

			return new LightSample( index, face.LightOffset + ((((dt >> 4) * width) + (ds >> 4)) * 3) );
		}

		return RecursiveLightSample( node.GetChild( 1 - side ), mid, end );
	}
}
