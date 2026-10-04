using System;
using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

public sealed class SchemeManager
{
	private const int SchemeNameLength = 32;
	private const int FontNameLength = 48;
	private const int TokenSize = 64;
	private const int MaxTempSchemes = 64;
	private const int DefaultFontSize = 17;
	private const string DefaultFontName = "Arial";

	[ConVar( "bitmapfonts" )]
	public static float BitmapFonts { get; set; } = 1f;

	private sealed class Scheme
	{
		public string SchemeName = "";
		public string FontName = "";
		public int FontSize;
		public int FontWeight;
		public Font Font;
		public byte[] FgColor = new byte[4];
		public byte[] BgColor = new byte[4];
		public byte[] ArmedFgColor = new byte[4];
		public byte[] ArmedBgColor = new byte[4];
		public byte[] MouseDownFgColor = new byte[4];
		public byte[] MouseDownBgColor = new byte[4];
		public byte[] BorderColor = new byte[4];
	}

	[SkipHotload]
	private static readonly int[] Resolutions = [320, 400, 512, 640, 800, 1024, 1152, 1280, 1600];

	private readonly Scheme[] _schemes;
	private readonly int _xRes;

	private static string LoadFileByResolution( string prefix, int xRes, string postfix )
	{
		var index = Resolutions.Length - 1;

		while ( Resolutions[index] > xRes )
		{
			index--;

			if ( index < 0 )
				return null;
		}

		while ( true )
		{
			if ( Server.LoadMountText( $"{prefix}{Resolutions[index]}{postfix}" ) is { } file )
				return file;

			if ( index == 0 )
				return null;

			index--;
		}
	}

	private static void ParseRgba( byte[] color, string text )
	{
		var parts = text.Split( ' ', StringSplitOptions.RemoveEmptyEntries );

		for ( var i = 0; i < 4; i++ )
			color[i] = (byte)(i < parts.Length ? Common.Atoi( parts[i] ) : 0);
	}

	private static string Truncate( string text, int size ) => text.Length > size - 1 ? text[..(size - 1)] : text;

	public SchemeManager( int xRes, int yRes )
	{
		_xRes = xRes;

		var file = LoadFileByResolution( "", xRes, "_textscheme.txt" );
		var schemes = new List<Scheme>();
		Scheme scheme = null;

		if ( file is null )
		{
			Log.Trace( "Unable to find *_textscheme.txt\n" );
		}
		else
		{
			var position = 0;
			var hasFgColor = false;
			var hasBgColor = false;
			var hasArmedFgColor = false;
			var hasArmedBgColor = false;
			var hasMouseDownFgColor = false;
			var hasMouseDownBgColor = false;

			var token = Common.Parse( file, ref position ) ?? "";

			while ( token.Length > 0 && schemes.Count - 1 < MaxTempSchemes )
			{
				var name = Truncate( token, TokenSize );

				token = Common.Parse( file, ref position ) ?? "";

				if ( !token.Equals( "=", StringComparison.OrdinalIgnoreCase ) )
				{
					if ( scheme is null )
						Log.Info( $"error parsing font scheme text file at file start - expected '=', found '{token}''\n" );
					else
						Log.Info( $"error parsing font scheme text file at scheme '{scheme.SchemeName}' - expected '=', found '{token}''\n" );

					break;
				}

				token = Common.Parse( file, ref position ) ?? "";

				var value = Truncate( token, TokenSize );

				if ( name.Equals( "SchemeName", StringComparison.OrdinalIgnoreCase ) )
				{
					if ( scheme is not null )
					{
						if ( !hasFgColor )
							Array.Fill( scheme.FgColor, (byte)255 );

						if ( !hasArmedFgColor )
							scheme.FgColor.CopyTo( scheme.ArmedFgColor, 0 );

						if ( !hasMouseDownFgColor )
							scheme.ArmedFgColor.CopyTo( scheme.MouseDownFgColor, 0 );

						if ( !hasBgColor )
							Array.Fill( scheme.BgColor, (byte)0 );

						if ( !hasArmedBgColor )
							scheme.BgColor.CopyTo( scheme.ArmedBgColor, 0 );

						if ( !hasMouseDownBgColor )
							scheme.ArmedBgColor.CopyTo( scheme.MouseDownBgColor, 0 );

						if ( scheme.FontSize == 0 )
							scheme.FontSize = DefaultFontSize;

						if ( scheme.FontName.Length == 0 )
							scheme.FontName = DefaultFontName;
					}

					scheme = new Scheme { SchemeName = Truncate( value, SchemeNameLength ) };
					schemes.Add( scheme );

					hasFgColor = hasBgColor = hasArmedFgColor = hasArmedBgColor = hasMouseDownFgColor = hasMouseDownBgColor = false;
				}

				if ( scheme is null )
				{
					Log.Info( "font scheme text file MUST start with a 'SchemeName'\n" );
					break;
				}

				if ( name.Equals( "FontName", StringComparison.OrdinalIgnoreCase ) )
				{
					scheme.FontName = Truncate( value, FontNameLength );
				}
				else if ( name.Equals( "FontSize", StringComparison.OrdinalIgnoreCase ) )
				{
					scheme.FontSize = Common.Atoi( value );
				}
				else if ( name.Equals( "FontWeight", StringComparison.OrdinalIgnoreCase ) )
				{
					scheme.FontWeight = Common.Atoi( value );
				}
				else if ( name.Equals( "FgColor", StringComparison.OrdinalIgnoreCase ) )
				{
					ParseRgba( scheme.FgColor, value );
					hasFgColor = true;
				}
				else if ( name.Equals( "BgColor", StringComparison.OrdinalIgnoreCase ) )
				{
					ParseRgba( scheme.BgColor, value );
					hasBgColor = true;
				}
				else if ( name.Equals( "FgColorArmed", StringComparison.OrdinalIgnoreCase ) )
				{
					ParseRgba( scheme.ArmedFgColor, value );
					hasArmedFgColor = true;
				}
				else if ( name.Equals( "BgColorArmed", StringComparison.OrdinalIgnoreCase ) )
				{
					ParseRgba( scheme.ArmedBgColor, value );
					hasArmedBgColor = true;
				}
				else if ( name.Equals( "FgColorMousedown", StringComparison.OrdinalIgnoreCase ) )
				{
					ParseRgba( scheme.MouseDownFgColor, value );
					hasMouseDownFgColor = true;
				}
				else if ( name.Equals( "BgColorMousedown", StringComparison.OrdinalIgnoreCase ) )
				{
					ParseRgba( scheme.MouseDownBgColor, value );
					hasMouseDownBgColor = true;
				}
				else if ( name.Equals( "BorderColor", StringComparison.OrdinalIgnoreCase ) )
				{
					ParseRgba( scheme.BorderColor, value );
					hasMouseDownBgColor = true;
				}

				token = Common.Parse( file, ref position ) ?? "";
			}
		}

		if ( schemes.Count == 0 )
		{
			scheme = new Scheme { SchemeName = "Default Scheme", FontName = DefaultFontName };

			Array.Fill( scheme.FgColor, (byte)255 );
			Array.Fill( scheme.ArmedFgColor, (byte)255 );
			Array.Fill( scheme.MouseDownFgColor, (byte)255 );

			schemes.Add( scheme );
		}

		_schemes = [.. schemes];

		for ( var i = 0; i < _schemes.Length; i++ )
		{
			var current = _schemes[i];

			for ( var j = 0; j < i; j++ )
			{
				var other = _schemes[j];

				if ( current.FontName.Equals( other.FontName, StringComparison.OrdinalIgnoreCase ) && current.FontSize == other.FontSize && current.FontWeight == other.FontWeight )
					current.Font = other.Font;
			}

			if ( current.Font is null )
			{
				byte[] fontData = null;

				if ( BitmapFonts != 0f )
				{
					var fontRes = _xRes >= 1600 ? 1600 : _xRes >= 1280 ? 1280 : _xRes >= 1152 ? 1152 : _xRes >= 1024 ? 1024 : _xRes >= 800 ? 800 : 640;
					var fontFileName = $"gfx\\vgui\\fonts\\{fontRes}_{current.SchemeName}.tga";

					fontData = Server.ReadFile( fontFileName.Replace( '\\', '/' ) );

					if ( fontData is null )
						Log.Info( $"Missing bitmap font: {fontFileName}\n" );
				}

				current.Font = new Font( current.FontName, fontData, current.FontSize, 0, 0f, current.FontWeight, false, false, false, false );
			}

			current.FgColor[3] = (byte)(255 - current.FgColor[3]);
			current.BgColor[3] = (byte)(255 - current.BgColor[3]);
			current.ArmedFgColor[3] = (byte)(255 - current.ArmedFgColor[3]);
			current.ArmedBgColor[3] = (byte)(255 - current.ArmedBgColor[3]);
			current.MouseDownFgColor[3] = (byte)(255 - current.MouseDownFgColor[3]);
			current.MouseDownBgColor[3] = (byte)(255 - current.MouseDownBgColor[3]);
		}
	}

	public int GetSchemeHandle( string schemeName )
	{
		for ( var i = 0; i < _schemes.Length; i++ )
		{
			if ( schemeName.Equals( _schemes[i].SchemeName, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return 0;
	}

	private Scheme GetSafeScheme( int schemeHandle ) => schemeHandle < _schemes.Length ? _schemes[schemeHandle] : _schemes[0];

	public Font GetFont( int schemeHandle ) => GetSafeScheme( schemeHandle ).Font;

	private static void GetColor( byte[] color, out int r, out int g, out int b, out int a )
	{
		r = color[0];
		g = color[1];
		b = color[2];
		a = color[3];
	}

	public void GetFgColor( int schemeHandle, out int r, out int g, out int b, out int a ) => GetColor( GetSafeScheme( schemeHandle ).FgColor, out r, out g, out b, out a );

	public void GetBgColor( int schemeHandle, out int r, out int g, out int b, out int a ) => GetColor( GetSafeScheme( schemeHandle ).BgColor, out r, out g, out b, out a );

	public void GetFgArmedColor( int schemeHandle, out int r, out int g, out int b, out int a ) => GetColor( GetSafeScheme( schemeHandle ).ArmedFgColor, out r, out g, out b, out a );

	public void GetBgArmedColor( int schemeHandle, out int r, out int g, out int b, out int a ) => GetColor( GetSafeScheme( schemeHandle ).ArmedBgColor, out r, out g, out b, out a );

	public void GetFgMousedownColor( int schemeHandle, out int r, out int g, out int b, out int a ) => GetColor( GetSafeScheme( schemeHandle ).MouseDownFgColor, out r, out g, out b, out a );

	public void GetBgMousedownColor( int schemeHandle, out int r, out int g, out int b, out int a ) => GetColor( GetSafeScheme( schemeHandle ).MouseDownBgColor, out r, out g, out b, out a );

	public void GetBorderColor( int schemeHandle, out int r, out int g, out int b, out int a ) => GetColor( GetSafeScheme( schemeHandle ).BorderColor, out r, out g, out b, out a );
}
