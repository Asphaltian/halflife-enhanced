using System;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class Client
{
	private const int BlockWidth = 128;
	private const int BlockHeight = 128;
	private const byte FullbrightStyle = 254;
	private const byte NoStyle = 255;

	private const int LightmapColumns = 8;

	private (int Page, int X, int Y)[] _lightmapSurfaces;
	private Texture[] _lightmaps;
	private int _lightmapPageCount;

	private void BuildLightmaps()
	{
		_lightmapSurfaces = new (int, int, int)[WorldFile.Faces.Length];

		var allocated = new List<int[]>();
		var pages = new List<byte[][]>();

		for ( var i = 0; i < WorldFile.Faces.Length; i++ )
		{
			var face = WorldFile.Faces[i];
			var flags = WorldFile.SurfaceFlags[i];

			if ( (flags & (Bsp.File.SurfaceDrawSky | Bsp.File.SurfaceDrawTurb)) != 0 )
				continue;

			if ( (flags & Bsp.File.SurfaceDrawTiled) != 0 && (WorldFile.TexInfos[face.TexInfo].Flags & TexInfoFlags.Special) == TexInfoFlags.Special )
				continue;

			var width = (WorldFile.SurfaceExtents[i].ExtentS >> 4) + 1;
			var height = (WorldFile.SurfaceExtents[i].ExtentT >> 4) + 1;

			var page = AllocBlock( allocated, width, height, out var x, out var y );

			while ( pages.Count <= page )
				pages.Add( NewLightmapPage() );

			_lightmapSurfaces[i] = (page, x, y);

			CopyLightSamples( face, width, height, x, y, pages[page] );
		}

		if ( pages.Count == 0 )
			pages.Add( NewLightmapPage() );

		var atlasWidth = BlockWidth * LightmapColumns;
		var atlasHeight = BlockHeight * ((pages.Count + LightmapColumns - 1) / LightmapColumns);

		_lightmapPageCount = pages.Count;
		_lightmaps = new Texture[Face.MaxLightmaps];

		for ( var layer = 0; layer < Face.MaxLightmaps; layer++ )
		{
			var data = new byte[atlasWidth * atlasHeight * 4];

			for ( var pixel = 3; pixel < data.Length; pixel += 4 )
				data[pixel] = NoStyle;

			for ( var page = 0; page < pages.Count; page++ )
			{
				var left = page % LightmapColumns * BlockWidth;
				var top = page / LightmapColumns * BlockHeight;

				for ( var y = 0; y < BlockHeight; y++ )
					Array.Copy( pages[page][layer], y * BlockWidth * 4, data, (((top + y) * atlasWidth) + left) * 4, BlockWidth * 4 );
			}

			_lightmaps[layer] = Texture.Create( atlasWidth, atlasHeight, ImageFormat.RGBA8888 ).WithData( data ).Finish();
		}
	}

	private Vector2 LightmapCoord( int surface, Vector3 position )
	{
		var texInfo = WorldFile.TexInfos[WorldFile.Faces[surface].TexInfo];
		var extents = WorldFile.SurfaceExtents[surface];
		var (_, x, y) = _lightmapSurfaces[surface];

		var s = Vector3.Dot( position, texInfo.S ) + texInfo.OffsetS - extents.MinS + (x << 4) + 8f;
		var t = Vector3.Dot( position, texInfo.T ) + texInfo.OffsetT - extents.MinT + (y << 4) + 8f;

		return new Vector2( s, t ) * 0.00048828125f;
	}

	private Color32 LightmapBlock( int surface )
	{
		var page = _lightmapSurfaces[surface].Page;

		return new Color32( (byte)(page % LightmapColumns), (byte)(page / LightmapColumns), 0 );
	}

	private static int AllocBlock( List<int[]> allocated, int width, int height, out int x, out int y )
	{
		for ( var page = 0; ; page++ )
		{
			if ( page == allocated.Count )
				allocated.Add( new int[BlockWidth] );

			var best = BlockHeight;

			x = 0;
			y = 0;

			for ( var i = 0; i < BlockWidth - width; i++ )
			{
				var top = 0;
				int j;

				for ( j = 0; j < width; j++ )
				{
					if ( allocated[page][i + j] >= best )
						break;

					top = Math.Max( top, allocated[page][i + j] );
				}

				if ( j == width )
				{
					x = i;
					y = best = top;
				}
			}

			if ( best + height > BlockHeight )
				continue;

			for ( var i = 0; i < width; i++ )
				allocated[page][x + i] = best + height;

			return page;
		}
	}

	private void CopyLightSamples( in Face face, int width, int height, int x, int y, byte[][] page )
	{
		var fullbright = WorldFile.Lighting.Length == 0;
		var size = width * height * 3;

		for ( var layer = 0; layer < Face.MaxLightmaps; layer++ )
		{
			var style = face.Styles[layer];
			var source = face.LightOffset + (layer * size);
			var hasSamples = style != NoStyle && face.LightOffset >= 0 && source + size <= WorldFile.Lighting.Length;

			if ( layer == 0 && fullbright )
				style = FullbrightStyle;
			else if ( !hasSamples )
				break;

			for ( var t = 0; t < height; t++ )
			{
				for ( var s = 0; s < width; s++ )
				{
					var sample = source + (((t * width) + s) * 3);
					var pixel = (((y + t) * BlockWidth) + x + s) * 4;

					if ( hasSamples )
					{
						page[layer][pixel + 0] = WorldFile.Lighting[sample + 0];
						page[layer][pixel + 1] = WorldFile.Lighting[sample + 1];
						page[layer][pixel + 2] = WorldFile.Lighting[sample + 2];
					}

					page[layer][pixel + 3] = style;
				}
			}
		}
	}

	private static byte[][] NewLightmapPage()
	{
		var page = new byte[Face.MaxLightmaps][];

		for ( var i = 0; i < page.Length; i++ )
		{
			page[i] = new byte[BlockWidth * BlockHeight * 4];

			for ( var pixel = 3; pixel < page[i].Length; pixel += 4 )
				page[i][pixel] = NoStyle;
		}

		return page;
	}
}
