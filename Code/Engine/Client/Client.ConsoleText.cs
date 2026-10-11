using System;
using GoldSrc.GameUI;

namespace GoldSrc;

partial class Client
{
	private const int MaxCenterString = 1023;

	[ConVar( "scr_centertime" )]
	private static float ScrCenterTime { get; set; } = 2f;

	[ConVar( "scr_printspeed" )]
	private static float ScrPrintSpeed { get; set; } = 8f;

	[ConVar( "con_color", ConVarFlags.Saved )]
	private static string ConColor { get; set; } = "255 180 30";

	private Scheme _scheme;
	private HudFontFace _engineFace;

	private Color32 _textColor = new( 255, 180, 30, 255 );

	private string _centerString = "";
	private float _centerTimeOff;
	private float _centerTimeStart;
	private int _centerLines;

	private Scheme EngineScheme => _scheme ??= new Scheme( Vgui.SchemeFile );

	private HudFontFace EngineFace()
	{
		_engineFace ??= EngineScheme.CreateFace( "EngineFont" );
		_engineFace.Update( ScreenHeight );

		return _engineFace;
	}

	private int EngineFontTall => EngineFace().Tall;

	private int EngineCharacterWidth( char character ) => EngineFace().Width( character );

	private int EngineStringWidth( string text )
	{
		var width = 0;

		foreach ( var character in text )
			width += EngineCharacterWidth( character );

		return width;
	}

	private void SetTextColor( int r, int g, int b ) => _textColor = new Color32( (byte)r, (byte)g, (byte)b, 255 );

	private void ResetTextColor()
	{
		var parts = ConColor.Split( ' ', StringSplitOptions.RemoveEmptyEntries );

		if ( parts.Length >= 3 && int.TryParse( parts[0], out var r ) && int.TryParse( parts[1], out var g ) && int.TryParse( parts[2], out var b ) )
			SetTextColor( r, g, b );
	}

	public void DrawSetTextColor( float r, float g, float b ) => SetTextColor( (int)(r * 255f), (int)(g * 255f), (int)(b * 255f) );

	private int DrawEngineCharacter( int x, int y, char character )
	{
		var face = EngineFace();

		if ( !char.IsControl( character ) && character != ' ' )
		{
			var glyph = face.Glyph( character );

			_hudDraws.Add( new HudDraw( HudDrawMode.Normal, face.Atlas, x + glyph.Offset, y, glyph.Width, face.Tall, glyph.Left, glyph.Top, _textColor, null ) );
		}

		return face.Width( character );
	}

	public int DrawConsoleString( int x, int y, string text )
	{
		var position = x;

		foreach ( var character in text )
			position += DrawEngineCharacter( position, y, char.IsControl( character ) ? ' ' : character );

		ResetTextColor();

		return position;
	}

	public void DrawConsoleStringLen( string text, out int width, out int height )
	{
		width = EngineStringWidth( text );

		height = EngineFontTall;
	}

	private readonly System.Text.StringBuilder _consoleLine = new();

	public void ConsolePrint( string text )
	{
		_consoleLine.Append( text );

		var end = text.LastIndexOf( '\n' );

		if ( end < 0 )
			return;

		var pending = _consoleLine.Length - (text.Length - end);

		Log.Info( _consoleLine.ToString( 0, pending ) );

		_consoleLine.Remove( 0, pending + 1 );
	}

	private const int MaxDebugLines = 32;
	private const int MaxDebugLineLength = 79;
	private const double DebugLineTime = 4.0;

	private readonly string[] _debugText = new string[MaxDebugLines];
	private readonly float[] _debugExpire = new float[MaxDebugLines];
	private readonly Vector3[] _debugColor = new Vector3[MaxDebugLines];

	public void NPrintf( int index, string text )
	{
		if ( (uint)index >= MaxDebugLines )
			return;

		_debugText[index] = text.Length > MaxDebugLineLength ? text[..MaxDebugLineLength] : text;
		_debugExpire[index] = (float)(Host.RealTime + DebugLineTime);
		_debugColor[index] = new Vector3( 1f, 1f, 1f );
	}

	public void NXPrintf( int index, float timeToLive, Vector3 color, string text )
	{
		if ( index < 0 || index >= MaxDebugLines )
			return;

		_debugText[index] = text.Length > MaxDebugLineLength ? text[..MaxDebugLineLength] : text;
		_debugExpire[index] = (float)(timeToLive + Host.RealTime);
		_debugColor[index] = color;
	}

	private void DrawDebugArea()
	{
		for ( var i = 0; i < MaxDebugLines; i++ )
		{
			if ( !(Host.RealTime < _debugExpire[i]) )
				continue;

			var x = ScreenWidth - EngineStringWidth( _debugText[i] ) - 10;
			var y = (EngineFontTall * i) + 20;

			if ( y > ScreenHeight - 20 )
				continue;

			DrawSetTextColor( _debugColor[i].x, _debugColor[i].y, _debugColor[i].z );
			DrawConsoleString( x, y, _debugText[i] );
		}

		ResetTextColor();
	}

	public void CenterPrint( string text )
	{
		_centerString = text.Length > MaxCenterString ? text[..MaxCenterString] : text;
		_centerTimeOff = ScrCenterTime;
		_centerTimeStart = (float)ClientTime;

		_centerLines = 1;

		foreach ( var character in text )
		{
			if ( character == '\n' )
				_centerLines++;
		}
	}

	private void CheckDrawCenterString( bool intermission )
	{
		_centerTimeOff = (float)(_centerTimeOff - Time.Delta);

		if ( (_centerTimeOff <= 0f && !intermission) || MessageMode )
			return;

		var remaining = intermission ? (int)((ClientTime - _centerTimeStart) * ScrPrintSpeed) : 9999;

		var fontHeight = Math.Max( EngineFontTall, 12 );

		ResetTextColor();

		var y = _centerLines <= 4 ? (int)(ScreenHeight * 0.35) : fontHeight * 4;
		var start = 0;

		while ( true )
		{
			var length = 0;

			while ( length < 40 && start + length < _centerString.Length && _centerString[start + length] != '\n' )
				length++;

			var x = (ScreenWidth - EngineStringWidth( _centerString.Substring( start, length ) )) / 2;

			for ( var i = 0; i < length; i++ )
			{
				x += DrawEngineCharacter( x, y, _centerString[start + i] );

				if ( remaining-- == 0 )
					return;
			}

			y += fontHeight;

			while ( start < _centerString.Length && _centerString[start] != '\n' )
				start++;

			if ( start >= _centerString.Length )
				return;

			start++;
		}
	}
}
