using System;

namespace GoldSrc.GameUI;

public enum Proportional
{
	None,
	Standard,
	Hd
}

public sealed class SchemeFont
{
	public HudFontFace Face { get; }

	public bool Additive { get; }

	public SchemeFont( HudFontFace face, bool additive )
	{
		Face = face;
		Additive = additive;
	}
}

public sealed class Scheme
{
	private const int DefaultProportionalBaseWide = 640;
	private const int DefaultProportionalBaseTall = 480;

	private readonly int _proportionalBaseWide;
	private readonly int _proportionalBaseTall;
	private readonly int _hdProportionalBaseWide;
	private readonly int _hdProportionalBaseTall;

	private int ProportionalBaseTall => Math.Min( _screenTall, _proportionalBaseTall );

	private int HdProportionalBaseTall => Math.Min( _screenTall, _hdProportionalBaseTall );

	public int GetProportionalBaseWide( Proportional mode ) => Math.Min( _screenWide, mode == Proportional.Hd ? _hdProportionalBaseWide : _proportionalBaseWide );
	private const int BoldWeight = 700;
	private const int SemiBoldWeight = 600;
	private const string BaseBorderName = "BaseBorder";

	private readonly KeyValues _colors;
	private readonly KeyValues _baseSettings;
	private readonly KeyValues _fonts;
	private readonly KeyValues _borders;
	private readonly Dictionary<string, Border> _borderCache = new( StringComparer.OrdinalIgnoreCase );
	private readonly Dictionary<(string Name, Proportional Mode), SchemeFont> _fontCache = [];

	private int _screenWide;
	private int _screenTall;

	public Scheme( string fileName )
	{
		var scheme = KeyValues.LoadFromFile( fileName );

		_colors = scheme?.FindKey( "Colors" );
		_baseSettings = scheme?.FindKey( "BaseSettings" );
		_fonts = scheme?.FindKey( "Fonts" );
		_borders = scheme?.FindKey( "Borders" );

		_proportionalBaseWide = _baseSettings?.GetInt( "ProportionalBaseWidth", DefaultProportionalBaseWide ) ?? DefaultProportionalBaseWide;
		_proportionalBaseTall = _baseSettings?.GetInt( "ProportionalBaseHeight", DefaultProportionalBaseTall ) ?? DefaultProportionalBaseTall;
		_hdProportionalBaseWide = _proportionalBaseWide;
		_hdProportionalBaseTall = _proportionalBaseTall;

		if ( _baseSettings?.FindKey( "ProportionalBaseWidthHD" ) is not null && _baseSettings.FindKey( "ProportionalBaseHeightHD" ) is not null )
		{
			_hdProportionalBaseWide = _baseSettings.GetInt( "ProportionalBaseWidthHD" );
			_hdProportionalBaseTall = _baseSettings.GetInt( "ProportionalBaseHeightHD" );
		}
	}

	public void SetScreenSize( int wide, int tall )
	{
		_screenWide = wide;

		if ( tall == _screenTall )
			return;

		_screenTall = tall;

		foreach ( var font in _fontCache.Values )
			font?.Face.Update( tall );
	}

	public int GetProportionalScaledValue( int value ) => (int)(_screenTall / (double)ProportionalBaseTall * value);

	public int GetHdProportionalScaledValue( int value ) => (int)((double)_screenTall / HdProportionalBaseTall * value);

	public float GetProportionalScale( Proportional mode )
	{
		return mode switch
		{
			Proportional.Hd => (float)_screenTall / HdProportionalBaseTall,
			Proportional.Standard => (float)_screenTall / ProportionalBaseTall,
			_ => 1f
		};
	}

	public string GetResourceString( string name ) => _baseSettings?.FindPath( name )?.Value ?? "";

	public Color32 GetColor( string name, Color32 defaultColor )
	{
		if ( _colors?.FindPath( name )?.Value is { } color )
		{
			ParseColor( color, out var parsed );

			return parsed;
		}

		if ( _baseSettings?.FindPath( name )?.Value is not { } setting )
			return ParseColor( name, out var literal ) >= 3 ? literal : defaultColor;

		return ParseColor( setting, out var value ) >= 3 ? value : GetColor( setting, defaultColor );
	}

	private static int ParseColor( string text, out Color32 color )
	{
		var parts = text.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		Span<int> values = stackalloc int[4];
		var count = 0;

		while ( count < 4 && count < parts.Length && int.TryParse( parts[count], out values[count] ) )
			count++;

		color = new Color32( (byte)values[0], (byte)values[1], (byte)values[2], (byte)values[3] );

		return count;
	}

	public Border GetBorder( string name )
	{
		if ( _borderCache.TryGetValue( name, out var border ) )
			return border;

		var key = _borders?.FindKey( name );

		if ( key is null )
			border = name.Equals( BaseBorderName, StringComparison.OrdinalIgnoreCase ) ? null : GetBorder( BaseBorderName );
		else if ( key.Value is { } alias )
			border = GetBorder( alias );
		else
			border = new Border( this, key );

		return _borderCache[name] = border;
	}

	public SchemeFont GetFont( string name, Proportional mode )
	{
		if ( _fontCache.TryGetValue( (name, mode), out var font ) )
			return font;

		return _fontCache[(name, mode)] = CreateFont( name, mode );
	}

	private SchemeFont CreateFont( string name, Proportional mode )
	{
		if ( CreateFace( name, mode ) is not { } face )
			return null;

		if ( _screenTall != 0 )
			face.Update( _screenTall );

		return new SchemeFont( face, _fonts.FindKey( name ).Keys[0].GetInt( "additive" ) != 0 );
	}

	public HudFontFace CreateFace( string name, Proportional mode = Proportional.None )
	{
		if ( _fonts?.FindKey( name ) is not { Keys.Count: > 0 } entries )
			return null;

		var first = entries.Keys[0];

		if ( FontFile( first.GetString( "name" ), first.GetInt( "weight" ) ) is not var (file, embolden) )
			return null;

		var sizes = new (int MinHeight, int MaxHeight, int Tall)[entries.Keys.Count];

		for ( var i = 0; i < sizes.Length; i++ )
		{
			var range = entries.Keys[i].GetString( "yres" ).Split( ' ', StringSplitOptions.RemoveEmptyEntries );

			sizes[i] = range.Length == 2
				? (Common.Atoi( range[0] ), Common.Atoi( range[1] ), entries.Keys[i].GetInt( "tall" ))
				: (0, 0, entries.Keys[i].GetInt( "tall" ));
		}

		var scaled = first.FindKey( "yres" ) is null;
		var proportionalBase = mode switch
		{
			Proportional.Hd when scaled => _hdProportionalBaseTall,
			Proportional.Standard when scaled => _proportionalBaseTall,
			_ => 0
		};

		return new HudFontFace( file, first.GetInt( "antialias" ) != 0, embolden, first.GetInt( "dropshadow" ) != 0 ? 1 : 0, sizes, first.GetInt( "blur" ), proportionalBase );
	}

	private static (string File, bool Embolden)? FontFile( string name, int weight )
	{
		if ( name.Equals( "Trebuchet MS", StringComparison.OrdinalIgnoreCase ) )
			return (weight >= BoldWeight ? "fonts/trebucbd.ttf" : "fonts/trebuc.ttf", false);

		if ( name.Equals( "Tahoma", StringComparison.OrdinalIgnoreCase ) && weight == 0 )
			return ("fonts/tahoma.ttf", false);

		if ( name.Equals( "Verdana", StringComparison.OrdinalIgnoreCase ) && weight == SemiBoldWeight )
			return ("fonts/verdana.ttf", true);

		return null;
	}
}
