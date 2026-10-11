using System;
using GoldSrc.TrueType;

namespace GoldSrc;

public readonly record struct HudGlyph( int Offset, int Left, int Top, int Width );

public sealed class HudFontFace( string path, bool antialiased, bool embolden, int dropShadow, (int MinHeight, int MaxHeight, int Tall)[] sizes, int blur = 0, int proportionalBase = 0 )
{
	private const int CharacterCount = 256;
	private const int CachedWidths = 128;
	private const int AtlasColumns = 16;
	private const int MaxProportionalTall = 128;

	private readonly int[] _a = new int[CharacterCount];
	private readonly int[] _b = new int[CharacterCount];
	private readonly int[] _c = new int[CharacterCount];
	private readonly bool[] _measured = new bool[CharacterCount];
	private readonly bool[] _drawn = new bool[CharacterCount];

	private TrueTypeFont _font;
	private byte[] _pixels;
	private float[] _gaussian;
	private int _screenHeight;
	private int _ascent;
	private int _cellWidth;
	private int _blur;

	public Texture Atlas { get; private set; }

	public int Tall { get; private set; }

	public void Update( int screenHeight )
	{
		if ( Atlas is not null && _screenHeight == screenHeight )
			return;

		_font ??= new TrueTypeFont( FileSystem.Mounted.ReadAllBytes( path ).ToArray() ) { Grayscale = antialiased, Embolden = embolden };
		_screenHeight = screenHeight;

		var tall = sizes[0].Tall;

		_blur = blur;

		if ( proportionalBase != 0 )
		{
			var scale = (double)screenHeight / Math.Min( screenHeight, proportionalBase );

			tall = Math.Min( (int)(scale * tall), MaxProportionalTall );
			_blur = (int)(scale * blur);
		}
		else
		{
			foreach ( var size in sizes )
			{
				if ( screenHeight >= size.MinHeight && screenHeight <= size.MaxHeight )
					tall = size.Tall;
			}
		}

		_font.SelectHeight( tall, out var pixelsPerEm, out _ascent, out var height );
		_font.SetSize( pixelsPerEm );

		Tall = height + dropShadow;

		_gaussian = null;

		if ( _blur > 1 )
		{
			var sigma = _blur * 0.683;
			var scale = (float)(1.0 / Math.Sqrt( sigma * 6.28 * sigma ));

			_gaussian = new float[(_blur * 2) + 1];

			for ( var i = 0; i <= _blur * 2; i++ )
				_gaussian[i] = (float)(Math.Pow( 2.7, -((i - _blur) * (i - _blur)) / ((sigma + sigma) * sigma) ) * scale);
		}

		_cellWidth = (Tall * 2) + (_blur * 2);
		_pixels = new byte[_cellWidth * AtlasColumns * Tall * (CharacterCount / AtlasColumns) * 4];

		Array.Clear( _measured );
		Array.Clear( _drawn );

		for ( var i = 0; i < CachedWidths; i++ )
			Measure( i );

		Atlas = Texture.Create( _cellWidth * AtlasColumns, Tall * (CharacterCount / AtlasColumns) ).WithDynamicUsage().WithData( _pixels ).Finish();
	}

	private void Measure( int character )
	{
		var bitmap = _font.Render( (char)character );
		var width = Math.Max( bitmap.Width, 1 );
		var left = bitmap.Width == 0 ? 0 : bitmap.Left;

		_a[character] = left - _blur;
		_b[character] = width + (_blur * 2) + dropShadow;
		_c[character] = bitmap.Advance - left - width - _blur - dropShadow;
		_measured[character] = true;
	}

	public int Width( char character )
	{
		if ( character >= CharacterCount )
			return 0;

		if ( !_measured[character] )
			Measure( character );

		return _a[character] + _b[character] + _c[character];
	}

	public HudGlyph Glyph( char character )
	{
		if ( character >= CharacterCount )
			return default;

		Width( character );

		var left = character % AtlasColumns * _cellWidth;
		var top = character / AtlasColumns * Tall;
		var width = Math.Min( _b[character], _cellWidth );

		if ( !_drawn[character] )
		{
			_drawn[character] = true;

			var cell = new byte[width * Tall * 4];

			if ( antialiased )
				DrawGray( cell, _font.RenderAnsi( (byte)character ), width );
			else
				DrawMono( cell, _font.Render( character ), width );

			DrawShadow( cell, width );
			DrawBlur( cell, width );

			var stride = _cellWidth * AtlasColumns * 4;

			for ( var y = 0; y < Tall; y++ )
				Array.Copy( cell, y * width * 4, _pixels, ((top + y) * stride) + (left * 4), width * 4 );

			Atlas.Update( _pixels );
		}

		return new HudGlyph( _a[character], left, top, width );
	}

	private void DrawGray( byte[] cell, GlyphBitmap bitmap, int width )
	{
		var skip = width + 2 <= bitmap.Width ? (bitmap.Width - width) >> 1 : 0;

		for ( var row = 0; row < bitmap.Rows; row++ )
		{
			var y = _ascent - bitmap.Top + row;

			if ( y < 0 || y >= Tall )
				continue;

			for ( var column = skip; column < bitmap.Width; column++ )
			{
				var x = _blur - skip + column;
				var level = bitmap.Levels[(row * bitmap.Width) + column];

				if ( x >= width || level == 0 )
					continue;

				var offset = ((y * width) + x) * 4;

				cell[offset] = cell[offset + 1] = cell[offset + 2] = 255;
				cell[offset + 3] = (byte)(int)(MathF.Min( level * 0.015625f, 1f ) * 255f);
			}
		}
	}

	private void DrawMono( byte[] cell, GlyphBitmap bitmap, int width )
	{
		for ( var row = 0; row < bitmap.Rows; row++ )
		{
			var y = _ascent - bitmap.Top + row;

			if ( y < 0 || y >= Tall )
				continue;

			for ( var column = 0; column < bitmap.Width; column++ )
			{
				var x = _blur + column;

				if ( x >= width || !bitmap.Pixels[(row * bitmap.Width) + column] )
					continue;

				var offset = ((y * width) + x) * 4;

				cell[offset] = cell[offset + 1] = cell[offset + 2] = cell[offset + 3] = 255;
			}
		}
	}

	private void DrawShadow( byte[] cell, int width )
	{
		if ( dropShadow == 0 )
			return;

		for ( var y = Tall - 1; y >= dropShadow; y-- )
		{
			for ( var x = width - 1; x >= dropShadow; x-- )
			{
				var offset = ((y * width) + x) * 4;

				if ( cell[offset + 3] != 0 )
					continue;

				cell[offset] = cell[offset + 1] = cell[offset + 2] = 0;
				cell[offset + 3] = cell[((((y - dropShadow) * width) + x - dropShadow) * 4) + 3];
			}
		}
	}

	private void DrawBlur( byte[] cell, int width )
	{
		if ( _gaussian is null )
			return;

		byte[] source = [.. cell];

		for ( var y = 0; y < Tall; y++ )
		{
			for ( var x = 0; x < width; x++ )
			{
				var accumulator = 0f;

				for ( var x1 = Math.Max( x - _blur, 0 ); x1 <= Math.Min( x + _blur, width - 1 ); x1++ )
				{
					var weight = _gaussian[x1 - x + _blur];

					for ( var y1 = Math.Max( y - _blur, 0 ); y1 <= Math.Min( y + _blur, Tall - 1 ); y1++ )
						accumulator += weight * _gaussian[y1 - y + _blur] * source[((y1 * width) + x1) * 4];
				}

				var offset = ((y * width) + x) * 4;

				cell[offset] = cell[offset + 1] = cell[offset + 2] = 255;
				cell[offset + 3] = (byte)Math.Min( (int)accumulator, 255 );
			}
		}
	}
}

partial class Client
{
	private HudFontFace _creditsFace;

	private HudFontFace CreditsFace()
	{
		_creditsFace ??= EngineScheme.CreateFace( "CreditsFont" );
		_creditsFace.Update( ScreenHeight );

		return _creditsFace;
	}

	public int CharacterHeight => CreditsFace().Tall;

	public int CharacterWidth( char character ) => CreditsFace().Width( character );

	public int DrawCharacter( int x, int y, int number, int r, int g, int b )
	{
		return r == 0 && g == 0 && b == 0 ? 0 : DrawFontCharacter( x, y, (char)number, r, g, b );
	}

	private int DrawFontCharacter( int x, int y, char character, int r, int g, int b )
	{
		var face = CreditsFace();

		if ( !char.IsControl( character ) )
		{
			var glyph = face.Glyph( character );

			_hudDraws.Add( new HudDraw( HudDrawMode.Additive, face.Atlas, x + glyph.Offset, y, glyph.Width, face.Tall, glyph.Left, glyph.Top, new Color32( (byte)r, (byte)g, (byte)b, 255 ), null ) );
		}

		return face.Width( character );
	}

	public int DrawString( int x, int y, string text, int r, int g, int b )
	{
		var position = x;

		foreach ( var character in text )
			position += DrawFontCharacter( position, y, character, r, g, b );

		return position - x;
	}

	public int DrawStringReverse( int x, int y, string text, int r, int g, int b )
	{
		if ( text.Length == 0 )
			return x;

		var width = CharacterWidth( text[Math.Max( text.Length - 2, 0 )] );
		var position = x - width;

		for ( var i = text.Length - 1; i >= 0; i-- )
			position -= DrawFontCharacter( position, y, text[i], r, g, b );

		return width - position + x;
	}
}
